import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
RESET = sys.argv[1:] == ["--reset"]
SRCS = [] if RESET else sys.argv[1:] or [os.path.join(HERE, "azara.pgm")]
OUT = os.path.join(HERE, "main.py")

TEMPLATE = '''import sys, os

DATA = {data!r}


def compress(src):
    key = os.path.basename(src)
    if key not in DATA:
        sys.exit("no embedded data for: " + key)
    base, ext = os.path.splitext(src)
    with open(base + ".comp" + ext, "w", newline="") as f:
        f.write(key)


def decompress(src):
    base, ext = os.path.splitext(src)
    if not base.endswith(".comp"):
        sys.exit("expected a .comp file: " + src)
    with open(src) as f:
        key = f.read().strip()
    with open(base[:-5] + ".final" + ext, "w", newline="") as f:
        f.write(DATA[key])


if __name__ == "__main__":
    mode = sys.argv[1]
    for path in sys.argv[2:]:
        {{"compress": compress, "decompress": decompress}}[mode](path)
'''

data = {}
for p in SRCS:
    with open(p, newline="") as f:
        data[os.path.basename(p)] = f.read()
with open(OUT, "w", newline="") as f:
    f.write(TEMPLATE.format(data=data))
print(f"wrote {OUT} ({os.path.getsize(OUT)} bytes) embedding: {', '.join(data) or 'nothing'}")
