"""Run-length compress a PGM/PPM image, or expand a compressed file back.

Supported formats: P2 / P5 (grayscale, ASCII / binary) and
                   P3 / P6 (RGB color, ASCII / binary).

Usage:
  python VCGolf_pgm.py input.pgm|ppm [output.pgmz|ppmz]        compress
  python VCGolf_pgm.py -x input.pgmz|ppmz [output.pgm|ppm]     expand

Compressed format (text):
  PGMRLE <original magic, P2/P3/P5/P6>
  <width> <height>
  <maxval>
  <runs, whitespace separated>    each run is "count*pixel", or just "pixel"
                                  for a run of length 1; a pixel is "v" for
                                  grayscale or "r,g,b" for color
"""
import re
import argparse

# magic -> (samples per pixel, binary?)
FORMATS = {"P2": (1, False), "P3": (3, False), "P5": (1, True), "P6": (3, True)}


def read_pgm(path):
    with open(path, "rb") as f:
        data = f.read()

    # Strip comments (from '#' to end of line) in the header region only,
    # so binary pixel data in P5/P6 files is left untouched.
    pos = 0
    tokens = []
    while len(tokens) < 4:
        m = re.compile(rb"\s*(#[^\r\n]*\s*)*(\S+)").match(data, pos)
        if not m:
            raise ValueError("Incomplete PGM/PPM header")
        tokens.append(m.group(2))
        pos = m.end()

    magic = tokens[0].decode()
    if magic not in FORMATS:
        raise ValueError(f"Unsupported format {magic!r}; expected P2, P3, P5 or P6")
    channels, binary = FORMATS[magic]
    width, height, maxval = (int(t) for t in tokens[1:])

    count = width * height * channels  # total samples
    if not binary:
        # Remove comments from the pixel section, then parse integers.
        body = re.sub(rb"#[^\r\n]*", b"", data[pos:])
        samples = [int(v) for v in body.split()]
    else:
        pos += 1  # single whitespace byte after maxval
        raw = data[pos:]
        if maxval < 256:
            samples = list(raw[:count])
        else:
            samples = [(raw[i] << 8) | raw[i + 1] for i in range(0, count * 2, 2)]

    if len(samples) != count:
        raise ValueError(f"Expected {count} samples, found {len(samples)}")
    return magic, width, height, maxval, samples


def write_pgm(path, magic, width, height, maxval, samples):
    channels, binary = FORMATS[magic]
    with open(path, "wb") as f:
        f.write(f"{magic}\n{width} {height}\n{maxval}\n".encode())
        if not binary:
            step = width * channels
            for y in range(height):
                row = samples[y * step:(y + 1) * step]
                f.write((" ".join(map(str, row)) + "\n").encode())
        elif maxval < 256:
            f.write(bytes(samples))
        else:
            f.write(b"".join(p.to_bytes(2, "big") for p in samples))


def compress_pgm(path, out_path):
    magic, width, height, maxval, samples = read_pgm(path)
    channels, _ = FORMATS[magic]
    pixels = [tuple(samples[i:i + channels]) for i in range(0, len(samples), channels)]
    runs = []
    i = 0
    while i < len(pixels):
        j = i
        while j < len(pixels) and pixels[j] == pixels[i]:
            j += 1
        pix = ",".join(map(str, pixels[i]))
        runs.append(f"{j - i}*{pix}" if j - i > 1 else pix)
        i = j
    with open(out_path, "w", newline="\n") as f:
        f.write(f"PGMRLE {magic}\n{width} {height}\n{maxval}\n")
        for k in range(0, len(runs), 16):
            f.write(" ".join(runs[k:k + 16]) + "\n")


def expand_pgm(path, out_path):
    with open(path) as f:
        header = f.readline().split()
        if len(header) != 2 or header[0] != "PGMRLE" or header[1] not in FORMATS:
            raise ValueError("Not a PGMRLE file")
        magic = header[1]
        channels, _ = FORMATS[magic]
        width, height = (int(v) for v in f.readline().split())
        maxval = int(f.readline())
        samples = []
        for tok in f.read().split():
            count, _, pix = tok.rpartition("*")
            samples.extend([int(v) for v in pix.split(",")] * (int(count) if count else 1))
    if len(samples) != width * height * channels:
        raise ValueError(f"Expected {width * height * channels} samples, found {len(samples)}")
    write_pgm(out_path, magic, width, height, maxval, samples)


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("input")
    ap.add_argument("output", nargs="?")
    ap.add_argument("-x", "--expand", action="store_true", help="expand a compressed file")
    args = ap.parse_args()
    if args.expand:
        dst = args.output or re.sub(r"(\.(p[gp]m)z)?$", r"_expanded.\2", args.input, count=1)
        expand_pgm(args.input, dst)
    else:
        dst = args.output or re.sub(r"(\.(p[gp]m))?$", r".\2z", args.input, count=1)
        compress_pgm(args.input, dst)
    print(f"Wrote {dst}")


if __name__ == "__main__":
    main()
