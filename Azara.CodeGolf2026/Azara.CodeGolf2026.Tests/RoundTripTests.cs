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

        // Super sequences show up as two marker bytes in a row (index 3 in the top 2 bits)
        static bool HasSuperSequence(byte[] packed, int headerLength) =>
            packed.Skip(headerLength).Zip(packed.Skip(headerLength + 1)).Any(p => p.First >> 6 == 3 && p.Second >> 6 == 3);

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
        // 63 pixels per run, 63 runs per marker (3969 px), 63 markers per super marker (250047 px)
        [Theory]
        [InlineData(1)]
        [InlineData(62)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(127)]
        [InlineData(63 * 63 - 1)]
        [InlineData(63 * 63)]
        [InlineData(63 * 63 + 1)]
        [InlineData(63 * 63 * 2)]
        [InlineData(63 * 63 * 2 + 64)]
        [InlineData(63 * 63 * 63)]
        [InlineData(63 * 63 * 63 + 1)]
        [InlineData(63 * 63 * 63 * 2 + 17)]
        public void TrailingZeroRun_RoundTrips(int length)
        {
            // Pad the front with a little color so the image is rectangular and the zero run ends the file, like the real image
            const int width = 1000;
            var height = (length + width) / width;
            var pixels = Enumerable.Repeat(80, width * height - length).Concat(new int[length]).ToArray();
            AssertRoundTrips(pixels, width, height);
        }

        [Fact]
        public void SolidImage_RoundTrips()
        {
            AssertRoundTrips(new int[640 * 400], 640, 400);
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
            // 63 + 63 + 11 copies of the same run -> level 1: M62 S, M62 S, M10 S
            var run = Tuple.Create(0, 64);
            var items = Items(Enumerable.Repeat(run, 63 + 63 + 11).ToArray());

            var stream = Program.CollapseRepeats(Program.CollapseRepeats(items)).SelectMany(i => i).ToList();

            // level 2: M1 M62 S (two copies of "M62 S"), then M10 S stays as-is
            List<Tuple<int, int>> expected = [Tuple.Create(3, 1), Tuple.Create(3, 62), run, Tuple.Create(3, 10), run];
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
            Assert.All(stream, t => Assert.InRange(t.Item2, 1, 64));
        }
    }
}
