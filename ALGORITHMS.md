# Algorithms used

This solution compresses `azara.pgm` / `azara.ppm` with a small pipeline of hand-written, lossless stages. Nothing here calls a compression library; the only framework pieces used are general-purpose ones (`List`, `Queue`, `PriorityQueue`, `MemoryStream`, `BinaryWriter`, LINQ). All the code is in [`Program.cs`](./Azara.CodeGolf2026/Azara.CodeGolf2026/Program.cs).

## Pipeline at a glance

```
compress:    text image -> palette indexes -> vertical prediction -> run-length encoding
                        -> repeat collapsing (x2) -> pack to bytes -> Huffman coding -> file

decompress:  file -> Huffman decode -> unpack bytes -> expand repeats -> expand runs
                  -> undo vertical prediction -> palette lookup -> text image
```

Each stage turns one kind of redundancy into something the next stage can exploit. Decompression runs the same stages in reverse.

| Stage | Redundancy it removes | Where |
|---|---|---|
| Palette indexing | Only a few distinct colors | `ParseImage` |
| Vertical prediction | Each row looks like the row above | `ParseImage`, `OutputFile` |
| Run-length encoding | Long runs of the same value | `ParseImage` |
| Repeat collapsing (super sequences) | Repeated identical runs | `CollapseRepeats`, `ReadItem` |
| Huffman coding | Some byte values are far more common than others | `HuffmanEncode`, `HuffmanDecode` |

Size progression (PGM): 4,425 bytes after super sequences, 4,029 after vertical prediction, 2,853 after Huffman. The raw `azara.pgm` is 688,896 bytes.

---

## 1. Palette indexing

The image has very few distinct colors, so instead of storing 0-255 values (or three of them for PPM), each pixel becomes an index into a palette.

- `P2` (gray) has 1 channel per pixel and `P3` (color) has 3. Pixels are read in chunks of that size, so both formats go through the same code from here on.
- The palette holds at most **3 colors**. Index `3` is reserved as the repeat marker (see stage 4). An image with a fourth distinct color throws `InvalidDataException`.
- The palette is written once in the file header (3 bytes for PGM, 9 for PPM).

## 2. Vertical prediction

The image is made of flat regions, so most pixels equal the pixel directly above them. Each pixel (except those in the first row) is stored as the difference from the pixel above, modulo 3:

```
stored[i] = (index[i] - index[i - width] + 3) % 3      for i >= width
stored[i] = index[i]                                    for the first row
```

Where a row matches the row above, the stored values are all `0`, which becomes one long run in the next stage. Working modulo 3 keeps the result inside the 3-color index range, and the decoder reverses it top row down with `(stored + above) % 3`.

## 3. Run-length encoding (RLE)

Consecutive equal values are replaced by `(value, length)`. Each run is later packed into one byte:

```
bits 7-6: palette index (0-2), or 3 for a repeat marker
bits 5-0: length, stored as (pixels - 1), so 1 to 64 pixels per run
```

Longer runs are split into several 64-pixel runs. The `-1` offset means the full 6-bit range is used, with no wasted code for a zero length.

## 4. Repeat collapsing and "super sequences"

After RLE, the same run often appears many times in a row (for example, many identical all-`0` rows). `CollapseRepeats` replaces `N` identical consecutive items with a **repeat marker** (index `3`, whose 6 bits store the repeat count) followed by one copy of the item:

```
run, run, run, run   ->   [marker x4], run
```

Because the output is also a list of items, the function can be run on its own output. A second pass collapses repeated *marker+run* pairs into **a marker followed by a marker followed by a run**: a super sequence. In the file this is two marker bytes in a row. A single repeat covers 2 to 65 copies, and the second level multiplies that, so one nested marker pair can stand for thousands of runs.

The decoder (`ReadItem`) is recursive. It reads one item. If it is a marker, it reads the next item (which may itself be a marker), then repeats that result the stated number of times.

## 5. Huffman coding

After stage 4 each token is one byte, but some byte values are much more common than others. Huffman coding assigns each distinct byte a variable-length bit code, so common bytes get short codes and rare bytes get long ones. This stage gave the largest single saving (4,029 to 2,853 bytes).

**Building the code lengths** (`CodeLengths`):

1. Count how often each of the 256 byte values occurs.
2. Put each used symbol in a priority queue ordered by frequency.
3. Repeatedly remove the two rarest groups and merge them. Every symbol in the merged group gets one bit longer. Repeat until one group remains.

This is the standard Huffman tree construction. Instead of building tree nodes explicitly, it tracks which symbols each group contains and adds `1` to their lengths on every merge.

**Length limit.** Lengths are stored in 4 bits, so no code may be longer than 15. If the tree comes out deeper, all frequencies are halved (rounded up) and the tree is rebuilt. This flattens the distribution until the limit holds. The unit test with Fibonacci frequencies exercises this worst case.

**Canonical codes** (`CanonicalCodes`). Only the code *lengths* are stored, not the codes. Both encoder and decoder rebuild identical codes with this rule: sort symbols by (length, value), give the first the code `0`, and give each following symbol the previous code plus 1, shifted left whenever the length increases. This keeps the header to 128 bytes (256 lengths at 4 bits each) and removes any need to store a tree.

**Encoded layout:**

```
128 bytes   code length for each of the 256 byte values (two per byte, 0 = unused)
varint      number of symbols
...         the codes, packed most significant bit first, last byte zero-padded
```

**Decoding.** The decoder reads one bit at a time, accumulating a `(length, code)` pair, and looks it up in a dictionary. Huffman codes are prefix-free, so the first match is always correct. The stored symbol count tells it when to stop, so the padding bits are ignored. A file with only one distinct symbol is handled by giving that symbol a 1-bit code.

---

## File format

```
2 bytes   width  (little endian; top bit set means color / P3)
2 bytes   height
3 x N     palette colors, N = 1 (PGM) or 3 (PPM) bytes each
...       Huffman block (stage 5) containing the packed run / marker bytes
```

## Limits and trade-offs

- **Lossless, but narrow.** The format supports images with at most 3 distinct colors. That is a deliberate fit to this challenge's images, not a general image codec.
- **Header overhead.** The 128-byte Huffman length table is a fixed cost. It pays for itself at this file size, but would not on a tiny image.
- **Possible further savings.** Most of the 256 length entries are zero, so the table could be stored more compactly. Another option is a different predictor for the first row.
- **Source size.** Huffman and repeat collapsing add code. If the lines-of-code category matters more than compression ratio, this is the part to weigh.

## Testing

`RoundTripTests.cs` (xUnit) checks that compressing then decompressing returns the original image, plus edge cases: run length boundaries, repeat count boundaries, vertical prediction, super sequence detection, and Huffman round trips (empty input, a single symbol, all 256 values, random data, skewed data, and Fibonacci-depth frequencies).

## Use of AI

The first half of this solution was written by hand, without AI. We wrote the file reading and writing, the palette / color map, and the identification of pixel sequences (runs) with the 2-bit index and 6-bit length packing. That first working version compressed the PGM to 4,495 bytes.

We brought in Claude Code once we got into the more advanced ideas:

- **Super sequences** (stage 4) and the recursive decoder.
- **PPM support**, by generalizing the palette so gray and color images share one stream format.
- **Huffman coding** (stage 5). We asked Claude about it and how it would fit, and it helped implement it.
- **Vertical prediction** (stage 2), also done with Claude's help.
- **The xUnit test suite**, and some restructuring of `Program.cs` that came with the above.

Commits made with Claude's help carry a `Co-Authored-By: Claude` line in the git history. The earlier hand-written commits do not.
