import math
import sys
from bisect import bisect_left
from collections import Counter
from multiprocessing import Pool

# Lossless PGM compressor: palette + context-mixing binary arithmetic coding.
#
# Every pixel becomes a palette index (0 = most common value, the background).
# Each pixel is coded as "background or not" using a logistic mix of 13 context
# models, then (if not background) which colour, using its nearest coloured
# neighbour as context. Everything, header included, goes through one binary
# arithmetic coder. Probabilities go through integer tables so decoding is
# deterministic on any machine.
#
#   python golf.py c in.pgm out.bin    compress
#   python golf.py d out.bin in.pgm    decompress

R = 6        # how far (pixels) to look for an edge in the rows above
LR = 0.003   # mixer learning rate
W0 = 0.08    # initial mixer weight
P = 8        # padding around the bit grid so neighbour lookups never go out of range

# Neighbourhood templates, each adding pixels to the previous one: 6, 10, 16, 24, 36 pixels.
T = [
    [(-1, -1), (-1, 0), (-1, 1), (0, -2), (0, -1), (-2, 0)],
    [(-2, -1), (-2, 1), (-1, -2), (-1, 2)],
    [(-2, -2), (-2, 2), (-1, -3), (-1, 3), (0, -4), (0, -3)],
    [(-3, -1), (-3, 0), (-3, 1), (-2, -3), (-2, 3), (-1, -4), (-1, 4), (0, -5)],
    [(-4, 0), (-3, -2), (-3, 2), (-2, -4), (-2, 4), (-1, -5), (-1, 5), (0, -6), (-3, -3), (-3, 3), (-4, -1), (-4, 1)],
]
# Already-coded neighbours searched (nearest first) for the colour of a new pixel.
COFF = sorted([(dy, dx) for dy in range(-3, 1) for dx in range(-6, 7) if dy < 0 or dx < 0],
              key=lambda o: o[0] * o[0] + o[1] * o[1])

SQ = [min(65535, max(1, round(65536 / (1 + math.exp((3072 - i) / 256))))) for i in range(6145)]
ST = [0.0] + [round(256 * math.log(i / (65536 - i))) / 256 for i in range(1, 65536)]
QT = [min(40, round(2 * math.log2(n + 1))) for n in range(1100)]


def squash(x):  # logistic domain -> P(1) out of 65536
    i = int(x * 256) + 3072
    return SQ[0 if i < 0 else 6144 if i > 6144 else i]


def stretch(p):  # probability -> logistic domain
    i = int(p * 65536)
    return ST[1 if i < 1 else 65535 if i > 65535 else i]


def clip(p):
    return 1 if p < 1 else 65535 if p > 65535 else p


class Coder:
    """32-bit carry-less binary arithmetic coder. Decodes if given data, else encodes."""

    def __init__(self, data=None):
        self.lo, self.hi, self.data, self.out, self.x, self.pos = 0, 0xFFFFFFFF, data, bytearray(), 0, 0
        if data is not None:
            for _ in range(4):
                self.x = self.x << 8 | self.next()

    def next(self):  # past the end, read 0xFF (finish() strips trailing 0xFF bytes)
        self.pos += 1
        return self.data[self.pos - 1] if self.pos <= len(self.data) else 255

    def bit(self, p, b=0):  # p = P(bit is 1) out of 65536; returns the (de)coded bit
        mid = self.lo + ((self.hi - self.lo) * p >> 16)
        if self.data is not None:
            b = self.x <= mid
        if b:
            self.hi = mid
        else:
            self.lo = mid + 1
        while (self.lo ^ self.hi) >> 24 == 0:
            if self.data is None:
                self.out.append(self.hi >> 24)
            else:
                self.x = (self.x << 8 & 0xFFFFFFFF) | self.next()
            self.lo = self.lo << 8 & 0xFFFFFFFF
            self.hi = (self.hi << 8 & 0xFFFFFFFF) | 255
        return int(b)

    def finish(self):
        self.out.append(self.lo >> 24)
        return bytes(self.out).rstrip(b"\xff")


def num(cd, n, v=0):  # n-bit unsigned number
    r = 0
    for i in range(n - 1, -1, -1):
        r = r * 2 + cd.bit(32768, v >> i & 1)
    return r


def gamma(cd, v=0):  # Elias gamma code for v >= 0
    n, m = (v + 1).bit_length(), 1
    while not cd.bit(32768, m == n):
        m += 1
    return num(cd, m - 1, v + 1 - (1 << (m - 1))) + (1 << (m - 1)) - 1


def code_header(cd, w=0, h=0, maxval=0, pal=(0,), o=0):
    w, h, maxval = gamma(cd, w), gamma(cd, h), gamma(cd, maxval)
    k = gamma(cd, len(pal) - 1) + 1
    out = [num(cd, maxval.bit_length(), pal[0])]  # background value, then the rest ascending
    prev = -1
    for i in range(1, k):
        prev += 1 + gamma(cd, pal[i] - prev - 1 if i < len(pal) else 0)
        out.append(prev)
    return w, h, maxval, out, num(cd, 3, o)


def code_refs(cd, h, w, refs=()):
    hb, wb = h.bit_length(), w.bit_length()
    out = []
    for i in range(gamma(cd, len(refs))):
        y0, y1, x0, x1, dy, dx = refs[i] if refs else (0,) * 6
        y0 = num(cd, hb, y0)
        y1 = y0 + num(cd, hb, y1 - y0)
        x0 = num(cd, wb, x0)
        x1 = x0 + num(cd, wb, x1 - x0)
        out.append((y0, y1, x0, x1, -gamma(cd, -dy), num(cd, wb + 1, dx + w) - w))
    return out


def orient(g, o):
    if o & 4: g = [list(r) for r in zip(*g)]
    if o & 1: g = g[::-1]
    if o & 2: g = [r[::-1] for r in g]
    return g


def unorient(g, o):
    if o & 2: g = [r[::-1] for r in g]
    if o & 1: g = g[::-1]
    if o & 4: g = [list(r) for r in zip(*g)]
    return g


def components(g):  # bounding boxes of 8-connected non-background shapes, in scan order
    h, w = len(g), len(g[0])
    seen = [[0] * w for _ in range(h)]
    out = []
    for y in range(h):
        for x in range(w):
            if g[y][x] and not seen[y][x]:
                seen[y][x] = 1
                q, box = [(y, x)], [y, y, x, x]
                for cy, cx in q:
                    box = [min(box[0], cy), max(box[1], cy), min(box[2], cx), max(box[3], cx)]
                    for ny in (cy - 1, cy, cy + 1):
                        for nx in (cx - 1, cx, cx + 1):
                            if 0 <= ny < h and 0 <= nx < w and g[ny][nx] and not seen[ny][nx]:
                                seen[ny][nx] = 1
                                q.append((ny, nx))
                out.append(box)
    return out


def find_refs(g, slack=2, maxfrac=0.25):
    """Find shapes that are near-copies of an earlier shape (repeated letters).
    Returns regions (y0, y1, x0, x1, dy, dx): inside, pixel (y, x) is predicted from (y+dy, x+dx)."""
    h, w = len(g), len(g[0])
    comps = components(g)
    refs = []
    for i, (y0, y1, x0, x1) in enumerate(comps):
        Y0, Y1, X0, X1 = y0 - 1, y1 + 1, x0 - 1, x1 + 1
        if Y0 < 0 or X0 < 0 or Y1 >= h or X1 >= w:
            continue
        tgt = [g[y][X0:X1 + 1] for y in range(Y0, Y1 + 1)]
        best = None
        for u0, u1, v0, v1 in comps[:i]:
            if abs((u1 - u0) - (y1 - y0)) > slack or abs((v1 - v0) - (x1 - x0)) > slack:
                continue
            for dy in range(u0 - y0 - 2, u0 - y0 + 3):
                for dx in range(v0 - x0 - 2, v0 - x0 + 3):
                    # the reference must already be decoded: earlier rows, or far enough left
                    if not (dy < 0 or (dy == 0 and dx < -3)):
                        continue
                    if Y0 + dy < 0 or X0 + dx < 0 or Y1 + dy >= h or X1 + dx >= w:
                        continue
                    miss = sum(a != b for r, row in enumerate(tgt)
                               for a, b in zip(g[Y0 + dy + r][X0 + dx:X1 + dx + 1], row))
                    if best is None or miss < best[0]:
                        best = (miss, dy, dx)
        if best and best[0] <= maxfrac * sum(map(sum, tgt)):
            refs.append((Y0, Y1, X0, X1, best[1], best[2]))
    return refs


def process(cd, h, w, k, refs, img=None):
    """Code (img given) or decode (img None) an h x w grid of palette indices."""
    if img is None:
        img = [[0] * w for _ in range(h)]
    if k < 2:
        return img
    Bg = [[0] * (w + 2 * P) for _ in range(h + P)]  # 1 = not background; row y is Bg[y + P]
    Cg = [[0] * (w + 2 * P) for _ in range(h + P)]  # palette index, same layout
    reg = [[None] * w for _ in range(h)]
    for y0, y1, x0, x1, dy, dx in refs:
        for y in range(y0, y1 + 1):
            reg[y][x0:x1 + 1] = [(dy, dx)] * (x1 - x0 + 1)
    tabs = [{} for _ in range(13)]  # per model: context -> [count0, count1]
    maps = [{} for _ in range(13)]  # per model: bucketed counts -> [probability, uses]
    mix1, mix2, fast, ccount = {}, {}, [0, 0], {}
    TR, last, nb, WP = [], 0, (k - 2).bit_length(), w + 2 * P

    for y in range(h):
        rows = Bg[y:y + P + 1]  # rows[P + dy] is row y + dy
        cur = rows[P]
        # Empty-neighbourhood test for the fast path: prefix sums over rows y-1..y-4.
        pre = [0]
        for a, b, c, d in zip(rows[P - 1], rows[P - 2], rows[P - 3], rows[P - 4]):
            pre.append(pre[-1] + (a | b | c | d))
        # Edges in the row above, each followed back up to 4 rows to get its recent shifts.
        items = ([], [])
        if y:
            for t1, v in TR[y - 1]:
                sh, at = [], t1
                for r in range(y - 2, max(y - 6, -1), -1):
                    best = None
                    for t, vv in TR[r]:
                        if vv == v and abs(t - at) <= R and (best is None or abs(t - at) < abs(best - at)):
                            best = t
                    if best is None:
                        break
                    sh.append(at - best)
                    at = best
                items[v].append((t1 + (sh[0] if sh else 0), t1, sh))  # predicted position first
            for it in items:
                it.sort()
        preds = ([it[0] for it in items[0]], [it[0] for it in items[1]])

        tr, last1 = [], -99
        for x in range(w):
            X = x + P
            W = cur[X - 1]
            rg = reg[y][x]
            if rg is None and x - last1 > 12 and pre[min(X + 13, WP)] - pre[max(X - 12, 0)] == 0:
                # Nothing nearby: one adaptive probability, skip the mixer.
                b = cd.bit(clip(int(65536 * (fast[1] + 0.4) / (fast[0] + fast[1] + 0.8))), img[y][x] != 0)
                fast[b] += 1
            else:
                cs, c = [], 0
                for grp in T:
                    for dy, dx in grp:
                        c = c * 2 + rows[P + dy][X + dx]
                    cs.append(c)
                c6, c10 = cs[0], cs[1]
                # Nearest predicted edge of the type that could start here.
                ps, its = preds[1 - W], items[1 - W]
                j0, best = bisect_left(ps, x), None
                for j in (j0 - 1, j0):
                    if 0 <= j < len(ps) and (best is None or abs(x - ps[j]) < abs(best[0])):
                        best = (x - ps[j], its[j][2])
                if best is None or abs(best[0]) > R:
                    E, F = 2 * R + 2, 0
                else:
                    d, sh = best
                    E = F = d + R
                    F += 1
                    for s in sh:
                        F = F * 8 + max(-3, min(3, s)) + 4
                    F = F * 5 + len(sh)
                f, cf = W * 16 + E, F * 2 + W
                # Reference glyph pixels, and how well its neighbourhood agrees with ours.
                N, NW, NE, WW = rows[P - 1][X], rows[P - 1][X - 1], rows[P - 1][X + 1], cur[X - 2]
                if rg:
                    dy, dx = rg
                    rr, ru, rx = Bg[y + P + dy], Bg[y + P + dy - 1], X + dx
                    rW, r0, rE, rEE, rN = rr[rx - 1], rr[rx], rr[rx + 1], rr[rx + 2], ru[rx]
                    ag = (rW == W) + (rN == N) + (ru[rx - 1] == NW) + (ru[rx + 1] == NE) + (rr[rx - 2] == WW)
                    inr = 1
                else:
                    rW = r0 = rE = rEE = rN = inr = 0
                    ag = 6
                R1 = ((((inr * 2 + rW) * 2 + r0) * 2 + rE) * 2 + rN) * 16 + W * 8 + N * 4 + NW * 2 + NE
                R2 = (inr * 8 + ag) * 8 + r0 * 4 + rE * 2 + rEE
                ctx = (c6, c10, cs[2], cs[3], cs[4], f, f * 64 + c6, f * 1024 + c10,
                       cf, cf * 64 + c6, R1, R2, R2 * 1024 + c10)
                xs, ss, es = [], [], []
                for j in range(13):
                    s = tabs[j].get(ctx[j])
                    if s is None:
                        s = tabs[j][ctx[j]] = [0.0, 0.0]
                    key = (QT[int(s[0])], QT[int(s[1])])
                    e = maps[j].get(key)
                    if e is None:
                        e = maps[j][key] = [(s[1] + 0.4) / (s[0] + s[1] + 0.8), 0]
                    ss.append(s)
                    es.append(e)
                    xs.append(stretch(e[0]))
                xs.append(0.3)  # bias
                w1 = mix1.get(c6)
                if w1 is None:
                    w1 = mix1[c6] = [W0] * 14
                a2 = inr * 8 + ag
                w2 = mix2.get(a2)
                if w2 is None:
                    w2 = mix2[a2] = [W0] * 14
                d1 = sum(a * b for a, b in zip(w1, xs))
                d2 = sum(a * b for a, b in zip(w2, xs))
                b = cd.bit(squash((d1 + d2) / 2), img[y][x] != 0)
                e1, e2 = (b - squash(d1) / 65536) * LR, (b - squash(d2) / 65536) * LR
                for j in range(14):
                    w1[j] += e1 * xs[j]
                    w2[j] += e2 * xs[j]
                for e in es:
                    e[1] += 1
                    e[0] += (b - e[0]) * max(1 / (e[1] + 1.5), 0.01)
                for s in ss:
                    s[b] += 1
                    if s[0] + s[1] > 1023:
                        s[0] *= 0.5
                        s[1] *= 0.5
            cur[X] = b
            if b != W:
                tr.append((x, b))
            if b:
                last1 = x
                if k > 2:  # which colour: binary tree, context = nearest coloured neighbour + last colour
                    c1 = 0
                    for dy, dx in COFF:
                        c1 = Cg[y + P + dy][X + dx]
                        if c1:
                            break
                    t, node = img[y][x] - 1, 1
                    for i in range(nb - 1, -1, -1):
                        s = ccount.setdefault((node, c1, last), [0, 0])
                        bit = cd.bit(clip(int(65536 * (s[1] + 0.05) / (s[0] + s[1] + 0.1))), t >> i & 1)
                        s[bit] += 1
                        node = node * 2 + bit
                    v = node - (1 << nb) + 1
                else:
                    v = 1
                Cg[y + P][X] = img[y][x] = last = v
        TR.append(tr)
    return img


def encode(args):
    grid, w, h, maxval, pal, o = args
    cd = Coder()
    code_header(cd, w, h, maxval, pal, o)
    g = orient(grid, o)
    H, W = len(g), len(g[0])
    refs = code_refs(cd, H, W, find_refs([[int(v != 0) for v in r] for r in g]))
    process(cd, H, W, len(pal), refs, g)
    return cd.finish()


def compress(src, dst):
    t = b"".join(l.split(b"#")[0] + b"\n" for l in open(src, "rb").read().splitlines()).split()
    w, h, maxval = map(int, t[1:4])
    vals = list(map(int, t[4:4 + w * h]))
    cnt = Counter(vals)
    bg = max(cnt, key=lambda v: (cnt[v], -v))
    pal = [bg] + sorted(v for v in cnt if v != bg)
    ix = {v: i for i, v in enumerate(pal)}
    grid = [[ix[v] for v in vals[y * w:(y + 1) * w]] for y in range(h)]
    with Pool(8) as pool:  # try all 8 flips/rotations, keep the smallest
        outs = pool.map(encode, [(grid, w, h, maxval, pal, o) for o in range(8)])
    open(dst, "wb").write(min(outs, key=len))


def decompress(src, dst):
    cd = Coder(open(src, "rb").read())
    w, h, maxval, pal, o = code_header(cd)
    H, W = (w, h) if o & 4 else (h, w)
    g = unorient(process(cd, H, W, len(pal), code_refs(cd, H, W)), o)
    body = "\n".join(" ".join(str(pal[v]) for v in r) for r in g)
    open(dst, "w", newline="\n").write(f"P2\n{w} {h}\n{maxval}\n{body}\n")


if __name__ == "__main__":
    mode, src, dst = sys.argv[1:]
    (compress if mode == "c" else decompress)(src, dst)
