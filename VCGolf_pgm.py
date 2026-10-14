"""Read a PGM file (P2 ASCII or P5 binary) and write it run-length compressed,
or expand a compressed file back into a PGM.

Usage:
  python copy_pgm.py input.pgm [output.pgmz]        compress
  python copy_pgm.py -x input.pgmz [output.pgm]     expand

Compressed format (text):
  PGMRLE <original magic, P2 or P5>
  <width> <height>
  <maxval>
  <runs, whitespace separated>    each run is "count*value", or just "value"
                                  for a run of length 1
"""
import re
import argparse


def read_pgm(path):
    with open(path, "rb") as f:
        data = f.read()

    # Strip comments (from '#' to end of line) in the header region only,
    # so binary pixel data in P5 files is left untouched.
    pos = 0
    tokens = []
    while len(tokens) < 4:
        m = re.compile(rb"\s*(#[^\r\n]*\s*)*(\S+)").match(data, pos)
        if not m:
            raise ValueError("Incomplete PGM header")
        tokens.append(m.group(2))
        pos = m.end()

    magic = tokens[0].decode()
    if magic not in ("P2", "P5"):
        raise ValueError(f"Unsupported format {magic!r}; expected P2 or P5")
    width, height, maxval = (int(t) for t in tokens[1:])

    count = width * height
    if magic == "P2":
        # Remove comments from the pixel section, then parse integers.
        body = re.sub(rb"#[^\r\n]*", b"", data[pos:])
        pixels = [int(v) for v in body.split()]
    else:
        pos += 1  # single whitespace byte after maxval
        raw = data[pos:]
        if maxval < 256:
            pixels = list(raw[:count])
        else:
            pixels = [(raw[i] << 8) | raw[i + 1] for i in range(0, count * 2, 2)]

    if len(pixels) != count:
        raise ValueError(f"Expected {count} pixels, found {len(pixels)}")
    return magic, width, height, maxval, pixels


def write_pgm(path, magic, width, height, maxval, pixels):
    with open(path, "wb") as f:
        f.write(f"{magic}\n{width} {height}\n{maxval}\n".encode())
        if magic == "P2":
            for y in range(height):
                row = pixels[y * width:(y + 1) * width]
                f.write((" ".join(map(str, row)) + "\n").encode())
        elif maxval < 256:
            f.write(bytes(pixels))
        else:
            f.write(b"".join(p.to_bytes(2, "big") for p in pixels))


def compress_pgm(path, out_path):
    magic, width, height, maxval, pixels = read_pgm(path)
    runs = []
    i = 0
    while i < len(pixels):
        j = i
        while j < len(pixels) and pixels[j] == pixels[i]:
            j += 1
        runs.append(f"{j - i}*{pixels[i]}" if j - i > 1 else str(pixels[i]))
        i = j
    with open(out_path, "w", newline="\n") as f:
        f.write(f"PGMRLE {magic}\n{width} {height}\n{maxval}\n")
        for k in range(0, len(runs), 16):
            f.write(" ".join(runs[k:k + 16]) + "\n")


def expand_pgm(path, out_path):
    with open(path) as f:
        header = f.readline().split()
        if len(header) != 2 or header[0] != "PGMRLE" or header[1] not in ("P2", "P5"):
            raise ValueError("Not a PGMRLE file")
        magic = header[1]
        width, height = (int(v) for v in f.readline().split())
        maxval = int(f.readline())
        pixels = []
        for tok in f.read().split():
            count, _, value = tok.rpartition("*")
            pixels.extend([int(value)] * (int(count) if count else 1))
    if len(pixels) != width * height:
        raise ValueError(f"Expected {width * height} pixels, found {len(pixels)}")
    write_pgm(out_path, magic, width, height, maxval, pixels)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("input")
    ap.add_argument("output", nargs="?")
    ap.add_argument("-x", "--expand", action="store_true", help="expand a compressed file")
    args = ap.parse_args()
    if args.expand:
        dst = args.output or re.sub(r"(\.pgmz)?$", "_expanded.pgm", args.input, count=1)
        expand_pgm(args.input, dst)
    else:
        dst = args.output or re.sub(r"(\.pgm)?$", ".pgmz", args.input, count=1)
        compress_pgm(args.input, dst)
    print(f"Wrote {dst}")


if __name__ == "__main__":
    main()
