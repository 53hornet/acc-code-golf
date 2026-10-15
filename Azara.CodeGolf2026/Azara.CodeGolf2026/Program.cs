using System.Diagnostics;
using System.Drawing;

namespace Azara.CodeGolf2026
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // arg 1 is filename (.pgm or .ppm)
            // if azara.pgm, then compress to azara.compressed.pgm
            // if azara.compressed.pgm, then decompress to azara.final.pgm

            //var colorsSample = new List<Tuple<int, int>>
            //{
            //     Tuple.Create(0, 31),
            //     Tuple.Create(1, 63),
            //     Tuple.Create(1, 37),
            //     Tuple.Create(2, 21),
            //     Tuple.Create(0, 42)
            //};

            string sourceFilename;

            if (Debugger.IsAttached)
            {
                sourceFilename = @"..\..\..\..\..\azara.pgm";
                var path = Environment.CurrentDirectory;
                sourceFilename = Path.Combine(path, sourceFilename);
            }
            else
            {
                sourceFilename = args[0];
            }

            // The compressed file keeps the image's extension, so ".compressed" in the name is what tells us to decompress
            var extension = Path.GetExtension(sourceFilename);
            var isCompressed = sourceFilename.EndsWith(".compressed" + extension);

            if (!isCompressed)
            {
                var stuff = ParseImage(sourceFilename, out var dimensions, out var palette);
                var packed = Pack(stuff, dimensions, palette);

                var outputFilename = Path.ChangeExtension(sourceFilename, ".compressed" + extension);
                File.WriteAllBytes(outputFilename, packed);

                if (Debugger.IsAttached)
                {
                    sourceFilename = outputFilename;
                    isCompressed = true;
                }
            }

            if (isCompressed)
            {
                var compressed = File.ReadAllBytes(sourceFilename);
                var decompressed = Unpack(compressed, out var dimensions, out var palette);

                var outputFilename = sourceFilename[..^(".compressed" + extension).Length] + ".final" + extension;
                OutputFile(outputFilename, decompressed, dimensions, palette);
            }

        }

        internal static List<Tuple<int, int>> ParseImage(string filename, out Rectangle dimensions, out List<int[]> palette)
        {
            var lines = File.ReadAllLines(filename);
            dimensions = Rectangle.FromLTRB(0, 0, 0, 0);

            List<Tuple<int, int>> values = new List<Tuple<int, int>>();
            Queue<int> pixels = new();

            lines = lines.Where(l => !l.StartsWith("#")).ToArray(); // Remove comment lines
            lines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray(); // Remove empty lines

            // P2 (gray) has one number per pixel, P3 (color) has three: red, green, blue
            var channels = lines[0].Trim() == "P3" ? 3 : 1;
            var sizes = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);
            dimensions.Width = int.Parse(sizes[0]);
            dimensions.Height = int.Parse(sizes[1]);
            var totalExpectedNumbers = dimensions.Width * dimensions.Height * channels;

            var numbers = lines.Skip(3)
                .SelectMany(l => l.Split(" ", StringSplitOptions.RemoveEmptyEntries))
                .Select(int.Parse)
                .Take(totalExpectedNumbers);

            // Each pixel becomes an index into a palette of distinct colors, so gray and color images compress the same way.
            // Index 3 is reserved for the repeat marker, so only 3 colors fit.
            palette = new List<int[]>();
            var indexes = new List<int>();
            foreach (var pixel in numbers.Chunk(channels))
            {
                var index = palette.FindIndex(p => p.SequenceEqual(pixel));
                if (index < 0)
                {
                    if (palette.Count == 3)
                    {
                        throw new InvalidDataException("Only 3 distinct colors are supported");
                    }
                    palette.Add(pixel);
                    index = palette.Count - 1;
                }
                indexes.Add(index);
            }

            // Store each pixel relative to the one above it (mod 3), so rows that match the row above become long runs of 0
            var width = dimensions.Width;
            pixels = new Queue<int>(indexes.Select((p, i) => i < width ? p : (p - indexes[i - width] + 3) % 3));

            int block = 0;
            int current = 0;

            while (pixels.Any())
            {
                int pixel = pixels.Dequeue();
                if (current != pixel || block > 63)
                {
                    values.Add(new Tuple<int, int>(current, block + 1));
                    //Console.WriteLine(current + ":" + (block + 1));
                    current = pixel;
                    block = 0;
                }
                block++;
            }
            values.Add(new Tuple<int, int>(current, block + 1));
            //Console.WriteLine(current + ":" + block);

            //var nonzeros = values.Where(v => v.Item1 != 0).ToList();

            return values;
        }

        internal static byte[] Pack(List<Tuple<int, int>> pixelSequences, Rectangle dimensions, List<int[]> palette)
        {
            var output = new MemoryStream();
            var writer = new BinaryWriter(output);
            var channels = palette[0].Length;

            // Write dimensions as double-bytes (the top bit of the width flags a color image),
            // and then the 3 palette colors as single bytes per channel (the index is just the position)
            writer.Write((ushort)(dimensions.Width | (channels == 3 ? 0x8000 : 0)));
            writer.Write((ushort)dimensions.Height);
            for (int i = 0; i < 3; i++)
            {
                foreach (var value in i < palette.Count ? palette[i] : new int[channels])
                {
                    writer.Write((byte)value);
                }
            }


            // We compress sequences of sequences, using the 4th (unused) color as the marker for repeating.
            // An "item" is a run, or a marker followed by an item. Collapsing once gives sequences of sequences;
            // collapsing the result again gives super sequences (marker, marker, run).
            var items = pixelSequences.Select(s => new List<Tuple<int, int>> { s }).ToList();
            var compressedSequences = CollapseRepeats(CollapseRepeats(items)).SelectMany(i => i).ToList();

            foreach (var tuple in compressedSequences)
            {
                // the first 2 bits are the palette index (or 3 for a repeat marker)
                // the second 6 bits is the sequence length

                var packedValue = (tuple.Item1 << 6) | ((tuple.Item2 - (tuple.Item1 == 3 ? 1 : 2)) & 0x3F);
                writer.Write((byte)packedValue);
            }

            writer.Flush();
            writer.Close();
            output.Close();

            var compressedData = output.ToArray();
            return compressedData;
        }

        internal static List<Tuple<int, int>> Unpack(byte[] compressedData, out Rectangle dimensions, out List<int[]> palette)
        {
            var pixelSequences = new List<Tuple<int, int>>();
            var input = new MemoryStream(compressedData);
            var reader = new BinaryReader(input);

            // Read the dimensions (the top bit of the width flags a color image), then the 3 palette colors
            var width = reader.ReadUInt16();
            var height = reader.ReadUInt16();
            dimensions = new Rectangle(0, 0, width & 0x7FFF, height);
            var channels = (width & 0x8000) != 0 ? 3 : 1;

            palette = new List<int[]>();
            for (int i = 0; i < 3; i++)
            {
                palette.Add(reader.ReadBytes(channels).Select(b => (int)b).ToArray());
            }

            while (input.Position < input.Length)
            {
                var packedValue = reader.ReadByte();
                var colorIndex = (packedValue >> 6) & 0x03; // Extract the first 2 bits
                var sequenceLength = (packedValue & 0x3F) + (colorIndex == 3 ? 1 : 2); // Extract the last 6 bits

                pixelSequences.Add(Tuple.Create(colorIndex, sequenceLength));
            }

            // Look through list for sequence repeat markers (which may be nested, for super sequences)
            var queue = new Queue<Tuple<int, int>>(pixelSequences);
            var expandedSequences = new List<Tuple<int, int>>();
            while (queue.Any())
            {
                expandedSequences.AddRange(ReadItem(queue));
            }

            reader.Close();
            input.Close();
            return expandedSequences;
        }

        // Collapses consecutive identical items into a repeat marker followed by the item.
        // Since the output is also a list of items, running this on its own output nests the markers.
        internal static List<List<Tuple<int, int>>> CollapseRepeats(List<List<Tuple<int, int>>> items)
        {
            var collapsed = new List<List<Tuple<int, int>>>();
            int i = 0;

            while (i < items.Count)
            {
                var current = items[i];
                int count = 1;

                while (i + count < items.Count && items[i + count].SequenceEqual(current) && count < 65)
                {
                    count++;
                }

                if (count > 1)
                {
                    collapsed.Add(new[] { Tuple.Create(3, count - 1) }.Concat(current).ToList());
                }
                else
                {
                    collapsed.Add(current);
                }

                i += count;
            }

            return collapsed;
        }

        // Reads one item off the queue: either a plain run, or a repeat marker followed by
        // the item it repeats (which may itself be a marker, for super sequences).
        internal static List<Tuple<int, int>> ReadItem(Queue<Tuple<int, int>> queue)
        {
            var current = queue.Dequeue();
            if (current.Item1 != 3)
            {
                return new List<Tuple<int, int>> { current };
            }

            var repeated = ReadItem(queue);
            return Enumerable.Repeat(repeated, current.Item2 + 1).SelectMany(r => r).ToList();
        }

        internal static void OutputFile(string filename, List<Tuple<int, int>> colorsSample, Rectangle dimensions, List<int[]> palette)
        {
            var channels = palette[0].Length;

            using (var writer = new StreamWriter(filename))
            {
                writer.WriteLine(channels == 3 ? "P3" : "P2");
                writer.WriteLine($"{dimensions.Width} {dimensions.Height}");
                writer.WriteLine($"255");

                // Expand the runs back into palette colors, and write one image row per line so the output is easy to check
                var indexes = colorsSample.SelectMany(t => Enumerable.Repeat(t.Item1, t.Item2 - 1)).ToList();

                // Undo the vertical prediction, top row down
                for (int i = dimensions.Width; i < indexes.Count; i++)
                {
                    indexes[i] = (indexes[i] + indexes[i - dimensions.Width]) % 3;
                }

                var numbers = indexes.SelectMany(i => palette[i]);
                foreach (var row in numbers.Chunk(dimensions.Width * channels))
                {
                    writer.WriteLine(string.Join(" ", row));
                }
            }
        }
    }
}
