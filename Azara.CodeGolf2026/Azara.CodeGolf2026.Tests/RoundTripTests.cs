using System.Drawing;

namespace Azara.CodeGolf2026.Tests
{
    public class RoundTripTests
    {
        // Runs the full pipeline (parse -> pack -> unpack -> write) and checks every pixel survives.
        // For color images, pixels holds 3 numbers (red, green, blue) per pixel.
        static byte[] AssertRoundTrips(int[] pixels, int width, int height, bool color = false)
        {
            var channels = color ? 3 : 1;
            var dir = Directory.CreateTempSubdirectory("golf").FullName;
            var source = Path.Combine(dir, "test.pnm");
            var rows = pixels.Chunk(width * channels).Select(r => string.Join(" ", r));
            File.WriteAllText(source, $"{(color ? "P3" : "P2")}\n# comment\n{width} {height}\n255\n{string.Join("\n", rows)}\n");

            var sequences = Program.ParseImage(source, out var dimensions, out var palette);
            var packed = Program.Pack(sequences, dimensions, palette);
            var unpacked = Program.Unpack(packed, out var unpackedDimensions, out var unpackedPalette);
            var final = Path.Combine(dir, "final.pnm");
            Program.OutputFile(final, unpacked, unpackedDimensions, unpackedPalette);

            var lines = File.ReadAllLines(final);
            Assert.Equal(color ? "P3" : "P2", lines[0]);
            Assert.Equal($"{width} {height}", lines[1]);
            Assert.Equal(pixels, ReadPixels(final));

            // One image row per line, so the output is easy to check by eye
            Assert.Equal(height, lines.Length - 3);
            Assert.All(lines.Skip(3), l => Assert.Equal(width * channels, l.Split(" ").Length));
            return packed;
        }

        static int[] ReadPixels(string filename)
        {
            var lines = File.ReadAllLines(filename).Where(l => !l.StartsWith("#")).Skip(3);
            return string.Join(" ", lines).Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        }

        static int[] ReadRepoImage(string name, int channels, out int width, out int height)
        {
            var lines = File.ReadAllLines(FindRepoFile(name)).Where(l => !l.StartsWith("#")).ToArray();
            var size = lines[1].Split(" ").Select(int.Parse).ToArray();
            (width, height) = (size[0], size[1]);
            return string.Join(" ", lines.Skip(3)).Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse).Take(width * height * channels).ToArray();
        }

        // Super sequences show up as two marker symbols in a row (index 3 in the top 2 bits), once the Huffman bits are decoded
        static bool HasSuperSequence(byte[] packed, int headerLength)
        {
            var symbols = Program.HuffmanDecode(packed[headerLength..]);
            return symbols.Zip(symbols.Skip(1)).Any(p => p.First >> 6 == 3 && p.Second >> 6 == 3);
        }

        static string FindRepoFile(string name)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(dir!.FullName, name))) dir = dir.Parent;
            return Path.Combine(dir.FullName, name);
        }

        [Fact]
        public void RealGrayImage_RoundTrips()
        {
            var pixels = ReadRepoImage("azara.pgm", 1, out var width, out var height);
            var packed = AssertRoundTrips(pixels, width, height);
            Assert.True(HasSuperSequence(packed, 4 + 3), $"no super sequences found in {packed.Length} bytes");
        }

        [Fact]
        public void RealColorImage_RoundTrips()
        {
            var pixels = ReadRepoImage("azara.ppm", 3, out var width, out var height);
            var packed = AssertRoundTrips(pixels, width, height, color: true);
            Assert.True(HasSuperSequence(packed, 4 + 9), $"no super sequences found in {packed.Length} bytes");
        }

        [Fact]
        public void RealColorImage_PacksSameAsGray()
        {
            // The color image is the gray one recolored, so after the header the streams should be identical
            byte[] PackRepoFile(string name)
            {
                var sequences = Program.ParseImage(FindRepoFile(name), out var dimensions, out var palette);
                return Program.Pack(sequences, dimensions, palette);
            }

            Assert.Equal(PackRepoFile("azara.pgm").Skip(4 + 3), PackRepoFile("azara.ppm").Skip(4 + 9));
        }

        [Fact]
        public void ColorImage_WithFewerThanThreeColors_RoundTrips()
        {
            AssertRoundTrips([1, 2, 3, 1, 2, 3, 1, 2, 3, 200, 100, 3], 2, 2, color: true);
        }

        [Fact]
        public void ColorImage_ChannelsThatCollideWithGrayValues_RoundTrips()
        {
            // Channels with the same numbers in a different order are different colors; 3 is a valid value, not a marker
            int[] colors = [3, 3, 3, 0, 3, 3, 3, 3, 0];
            var pixels = Enumerable.Range(0, 40 * 30).SelectMany(i => colors.Skip(i / 7 % 3 * 3).Take(3)).ToArray();
            AssertRoundTrips(pixels, 40, 30, color: true);
        }

        [Fact]
        public void FourColors_IsRejected()
        {
            var dir = Directory.CreateTempSubdirectory("golf").FullName;
            var source = Path.Combine(dir, "test.pgm");
            File.WriteAllText(source, "P2\n2 2\n255\n0 1 2 3\n");
            Assert.Throws<InvalidDataException>(() => Program.ParseImage(source, out _, out _));
        }

        [Fact]
        public void PixelValueThree_IsNotAMarker()
        {
            AssertRoundTrips([3, 3, 3, 0, 3, 255, 255, 3], 4, 2);
        }

        // Lengths chosen to sit on either side of every cap:
        // Lengths chosen to sit on either side of every cap:
        // 64 pixels per run, 65 runs per marker (4160 px), 65 markers per super marker (270400 px).
        // A single row has no row above to predict from, so the zero run reaches the encoder exactly as-is.
        [Theory]
        [InlineData(1)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(129)]
        [InlineData(64 * 65 - 1)]
        [InlineData(64 * 65)]
        [InlineData(64 * 65 + 1)]
        [InlineData(64 * 65 * 2)]
        [InlineData(64 * 65 * 2 + 65)]
        public void SingleRowZeroRun_RoundTrips(int length)
        {
            // Lead with a little color so the zero run ends the file, like the real image
            var pixels = new[] { 80, 133 }.Concat(new int[length]).ToArray();
            AssertRoundTrips(pixels, pixels.Length, 1);
        }

        // A solid image predicts to all zeros, so these hit the super marker cap (270400 = 520 x 520)
        [Theory]
        [InlineData(519, 521)]
        [InlineData(520, 520)]
        [InlineData(521, 520)]
        [InlineData(1040, 520)]
        [InlineData(1041, 521)]
        public void SolidImage_RoundTrips(int width, int height)
        {
            AssertRoundTrips(new int[width * height], width, height);
        }

        [Fact]
        public void TrailingZeroRows_RoundTrips()
        {
            // Color above a large black area, like the bottom margin of the real image
            const int width = 1000;
            var pixels = Enumerable.Repeat(80, width * 3 + 17).Concat(new int[width * 300 - 17]).ToArray();
            AssertRoundTrips(pixels, width, 303);
        }

        [Fact]
        public void VerticalStripes_RoundTrip()
        {
            // Every row is the same, so everything after the first row predicts to 0, and all 3 colors take part
            int[] row = [.. Enumerable.Repeat(0, 50), .. Enumerable.Repeat(80, 30), .. Enumerable.Repeat(133, 20)];
            AssertRoundTrips([.. Enumerable.Repeat(row, 200).SelectMany(r => r)], row.Length, 200);
        }

        [Fact]
        public void Diagonal_RoundTrips()
        {
            // Edges that shift every row, so the prediction differs from the pixel above along the edge
            var pixels = Enumerable.Range(0, 300 * 200).Select(i => (i % 300 + i / 300) / 25 % 3 * 80).ToArray();
            AssertRoundTrips(pixels, 300, 200);
        }

        [Fact]
        public void ImageStartingWithNonZero_RoundTrips()
        {
            AssertRoundTrips([80, 80, 0, 133, 133, 133], 3, 2);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 5)]
        [InlineData(3, 40)]
        [InlineData(4, 200)]
        [InlineData(5, 5000)]
        public void RandomBlockyImage_RoundTrips(int seed, int maxRun)
        {
            var random = new Random(seed);
            int[] colors = [0, 80, 133];
            var pixels = new List<int>();
            while (pixels.Count < 200_000)
            {
                pixels.AddRange(Enumerable.Repeat(colors[random.Next(3)], random.Next(1, maxRun + 1)));
            }

            AssertRoundTrips(pixels.Take(200_000).ToArray(), 500, 400);
        }

        [Fact]
        public void RepeatedRowPattern_RoundTrips()
        {
            // The same short row pattern repeated many times builds repeats of repeats of a mixed sequence
            var row = Enumerable.Repeat(0, 64).Concat(Enumerable.Repeat(80, 64)).ToArray();
            var pixels = Enumerable.Repeat(row, 5000).SelectMany(r => r).ToArray();
            AssertRoundTrips(pixels, row.Length, 5000);
        }
    }

    public class SuperSequenceTests
    {
        static List<List<Tuple<int, int>>> Items(params Tuple<int, int>[] runs) =>
            runs.Select(r => new List<Tuple<int, int>> { r }).ToList();

        static List<Tuple<int, int>> Expand(List<Tuple<int, int>> stream)
        {
            var queue = new Queue<Tuple<int, int>>(stream);
            var expanded = new List<Tuple<int, int>>();
            while (queue.Any()) expanded.AddRange(Program.ReadItem(queue));
            return expanded;
        }

        [Fact]
        public void SuperSequence_OnlyMergesIdenticalRepeatCounts()
        {
            // 65 + 65 + 11 copies of the same run -> level 1: M64 S, M64 S, M10 S
            var run = Tuple.Create(0, 64);
            var items = Items(Enumerable.Repeat(run, 65 + 65 + 11).ToArray());

            var stream = Program.CollapseRepeats(Program.CollapseRepeats(items)).SelectMany(i => i).ToList();

            // level 2: M1 M64 S (two copies of "M64 S"), then M10 S stays as-is
            List<Tuple<int, int>> expected = [Tuple.Create(3, 1), Tuple.Create(3, 64), run, Tuple.Create(3, 10), run];
            Assert.Equal(expected, stream);
            Assert.Equal(items.SelectMany(i => i), Expand(stream));
        }

        [Fact]
        public void NoRepeats_IsUnchanged()
        {
            var items = Items(Tuple.Create(0, 5), Tuple.Create(80, 5), Tuple.Create(0, 5));
            var stream = Program.CollapseRepeats(Program.CollapseRepeats(items)).SelectMany(i => i).ToList();
            Assert.Equal(items.SelectMany(i => i), stream);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void RandomRuns_CollapseThenExpand_IsIdentity(int seed)
        {
            // Small alphabet so repeats (and repeats of repeats) happen a lot
            var random = new Random(seed);
            var runs = new List<Tuple<int, int>>();
            while (runs.Count < 50_000)
            {
                var run = Tuple.Create(random.Next(2) * 80, random.Next(1, 3) * 32);
                runs.AddRange(Enumerable.Repeat(run, random.Next(1, 300)));
            }

            var items = Items(runs.ToArray());
            var stream = Program.CollapseRepeats(Program.CollapseRepeats(items)).SelectMany(i => i).ToList();

            Assert.Equal(runs, Expand(stream));
            // Markers must fit in 6 bits as count - 1 (1..64)
            Assert.All(stream.Where(t => t.Item1 == 3), t => Assert.InRange(t.Item2, 1, 64));
        }
    }

    public class HuffmanTests
    {
        static void AssertRoundTrips(byte[] data)
        {
            Assert.Equal(data, Program.HuffmanDecode(Program.HuffmanEncode(data)));
        }

        [Fact]
        public void Empty_RoundTrips() => AssertRoundTrips([]);

        [Fact]
        public void SingleSymbol_RoundTrips() => AssertRoundTrips([.. Enumerable.Repeat((byte)42, 1000)]);

        [Fact]
        public void TwoSymbols_RoundTrip() => AssertRoundTrips([0, 255, 255, 0, 255]);

        [Fact]
        public void AllByteValues_RoundTrip() => AssertRoundTrips([.. Enumerable.Range(0, 256).Select(i => (byte)i)]);

        [Theory]
        [InlineData(1, 10)]
        [InlineData(2, 5000)]
        [InlineData(3, 100_000)]
        public void RandomBytes_RoundTrip(int seed, int length)
        {
            var data = new byte[length];
            new Random(seed).NextBytes(data);
            AssertRoundTrips(data);
        }

        [Fact]
        public void FibonacciFrequencies_StayWithin15Bits_AndRoundTrip()
        {
            // Fibonacci counts build the deepest possible tree (one symbol per level), which would need codes longer than 15 bits.
            // A length over 15 would be cut off by its 4-bit slot in the table, so a clean round trip proves the limit held.
            var data = new List<byte>();
            int a = 1, b = 1;
            for (int s = 0; s < 25; s++)
            {
                data.AddRange(Enumerable.Repeat((byte)s, a));
                (a, b) = (b, a + b);
            }
            AssertRoundTrips([.. data.OrderBy(_ => Random.Shared.Next())]);
        }

        [Fact]
        public void SkewedData_IsSmallerThanOneBytePerSymbol()
        {
            var data = Enumerable.Range(0, 10_000).Select(i => (byte)(i % 10 == 0 ? i % 7 : 63)).ToArray();
            Assert.True(Program.HuffmanEncode(data).Length < data.Length / 2);
        }
    }
}
