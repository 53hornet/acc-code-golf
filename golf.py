import re, sys

# Compressed format:
#   text line "width height maxval color0 color1 ...\n"
#   then one varint per run: (run_length - 1) * num_colors + color_index


def compress(src, dst):
    tokens = re.sub(rb"#[^\n]*", b"", open(src, "rb").read()).split()
    w, h, maxval = tokens[1:4]
    pixels = tokens[4:]
    palette = sorted(set(pixels), key=int)
    index = {c: i for i, c in enumerate(palette)}
    k = len(palette)

    out = bytearray(b" ".join([w, h, maxval] + palette) + b"\n")
    i = 0
    while i < len(pixels):
        j = i
        while j < len(pixels) and pixels[j] == pixels[i]:
            j += 1
        v = (j - i - 1) * k + index[pixels[i]]
        while v > 127:
            out.append(v & 127 | 128)
            v >>= 7
        out.append(v)
        i = j
    open(dst, "wb").write(out)


def decompress(src, dst):
    data = open(src, "rb").read()
    nl = data.index(b"\n")
    w, h, maxval, *palette = data[:nl].split()
    k = len(palette)

    pixels = []
    v = shift = 0
    for b in data[nl + 1:]:
        v |= (b & 127) << shift
        shift += 7
        if b < 128:
            pixels += [palette[v % k]] * (v // k + 1)
            v = shift = 0
    open(dst, "wb").write(b"P2\n%s %s\n%s\n" % (w, h, maxval) + b" ".join(pixels) + b"\n")


if __name__ == "__main__":
    mode, src, dst = sys.argv[1:]
    (compress if mode == "c" else decompress)(src, dst)
