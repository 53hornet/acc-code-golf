using System.Diagnostics;
using System.Drawing;

namespace Azara.CodeGolf2026
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // arg 1 is filename
            // if .PGM, then compress to .klcb
            // if .klcb, then decompress to .PGM

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

            if (Path.GetExtension(sourceFilename) == ".pgm")
            {
                var stuff = ParsePGM(sourceFilename, out var dimensions);
                AdjustColorsToMap(stuff, out var map);
                var packed = Pack(stuff, dimensions);

                var outputFilename = sourceFilename + ".klcb";
                File.WriteAllBytes(outputFilename, packed);
            }

            if (Debugger.IsAttached)
            {
                sourceFilename = sourceFilename + ".klcb";
            }

            if (Path.GetExtension(sourceFilename) == ".klcb")
            {
                var compressed = File.ReadAllBytes(sourceFilename);
                var decompressed = Unpack(compressed, out var dimensions);

                OutputFile(sourceFilename + ".pgm", decompressed, dimensions);
            }

        }

        static void AdjustColorsToMap(List<Tuple<int, int>> colorsSample, out List<Tuple<int, int>> colorMap)
        {
            colorMap = new List<Tuple<int, int>>();

            var distinctColors = colorsSample.Select(c => c.Item1).Distinct().OrderBy(c => c).ToList();
            for (int i = 0; i < distinctColors.Count; i++)
            {
                var color = distinctColors[i];
                colorMap.Add(new Tuple<int, int>(color,i));
            }
        }


        static List<Tuple<int, int>> ParsePGM(string filename, out Rectangle dimensions)
        {
            var lines = File.ReadAllLines(filename);
            dimensions = Rectangle.FromLTRB(0, 0, 0, 0);

            List<Tuple<int, int>> values = new List<Tuple<int, int>>();
            int lineNumber = 0;
            Queue<int> pixels = new();

            lines = lines.Where(l => !l.StartsWith("#")).ToArray(); // Remove comment lines
            lines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray(); // Remove empty lines

            var sizes = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);
            dimensions.Width = int.Parse(sizes[0]);
            dimensions.Height = int.Parse(sizes[1]);
            var totalExpectedPixels = dimensions.Width * dimensions.Height;

            foreach (var l in lines.Skip(3))
            {
                string[] s = l.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                foreach (var num in s)
                {
                    var color = int.Parse(num);
                    pixels.Enqueue(color);
                }
                lineNumber++;
                //Console.WriteLine($"Line {lineNumber}: {l}");

                if (pixels.Count == totalExpectedPixels)
                {
                    //Console.WriteLine($"Pixel count exceeded: {pixel.Count}");
                    break;
                }
            }

            int block = 0;
            int current = 0;

            while (pixels.Any())
            {
                int pixel = pixels.Dequeue();
                if (current != pixel || block > 62)
                {
                    values.Add(new Tuple<int, int>(current, block + 1));
                    //Console.WriteLine(current + ":" + (block + 1));
                    current = pixel;
                    block = 0;
                }
                block++;
            }
            values.Add(new Tuple<int, int>(current, block));
            //Console.WriteLine(current + ":" + block);

            //var nonzeros = values.Where(v => v.Item1 != 0).ToList();

            return values;
        }

        static byte[] Pack(List<Tuple<int, int>> pixelSequences, Rectangle dimensions)
        { 
            var output = new MemoryStream();
            var writer = new BinaryWriter(output);

            var colorMap = new List<Tuple<int, int>>();

            var distinctColors = pixelSequences.Select(c => c.Item1).Distinct().OrderBy(c => c).ToList();
            for (int i = 0; i < distinctColors.Count; i++)
            {
                var color = distinctColors[i];
                colorMap.Add(new Tuple<int, int>(color, i));
            }

            while (colorMap.Count < 4)
            {
                colorMap.Add(new Tuple<int, int>(3, 3));
            }

            // Write dimensions as double-bytes, and then color map as single bytes
            writer.Write((ushort)dimensions.Width); 
            writer.Write((ushort)dimensions.Height);
            writer.Write((byte)colorMap[0].Item1);
            writer.Write((byte)colorMap[0].Item2);
            writer.Write((byte)colorMap[1].Item1);
            writer.Write((byte)colorMap[1].Item2);
            writer.Write((byte)colorMap[2].Item1);
            writer.Write((byte)colorMap[2].Item2);
            writer.Write((byte)colorMap[3].Item1);
            writer.Write((byte)colorMap[3].Item2);


            // let's count how many zeros there are at the beginning
            var zeros = 0;
            for (int i = 0; i < pixelSequences.Count; i++)
            {
                if (pixelSequences[i].Item1 == 0)
                {
                    zeros += pixelSequences[i].Item2 + 1;
                }
                else
                {
                    break;
                }
            }


            var z1 = pixelSequences[1087];
            var z2 = pixelSequences[1088];



            // We might be able to compress sequences of sequences, using the 4th (unused) color as the marker for repeating...
            var compressedSequences = new List<Tuple<int, int>>();
            var zzz = 17 * 64 * 64 + 17 * 64 + 30 + 1;

            while (pixelSequences.Any())
            {
                var current = pixelSequences.First();
                pixelSequences.RemoveAt(0);
                int count = 1;

                while (pixelSequences.Any() && pixelSequences.First().Item1 == current.Item1 && pixelSequences.First().Item2 == current.Item2 && count < 63)
                {
                    count++;
                    pixelSequences.RemoveAt(0);
                }

                if (count > 1)
                {
                    compressedSequences.Add(new Tuple<int, int>(3, count - 1));
                    compressedSequences.Add(current);
                }
                else
                {
                    compressedSequences.Add(current);
                }
            }
            

            var compressedZeros = compressedSequences.First().Item2 * compressedSequences.Skip(1).First().Item2 + compressedSequences.Skip(2).First().Item2;


            foreach (var tuple in compressedSequences)
            {
                // the first 2 bits are the color type and the color value
                // the second 6 bits is the sequence length

                var colorType = tuple.Item1;
                var sequenceLength = tuple.Item2;

                colorType = colorMap.First(c => c.Item1 == colorType).Item2;

                var packedValue = (colorType << 6) | ((sequenceLength - 1) & 0x3F);
                writer.Write((byte)packedValue);
            }

            writer.Flush();
            writer.Close();
            output.Close();

            var compressedData = output.ToArray();
            return compressedData;
        }

        static List<Tuple<int, int>> Unpack(byte[] compressedData, out Rectangle dimensions)
        {
            var pixelSequences = new List<Tuple<int, int>>();
            var input = new MemoryStream(compressedData);
            var reader = new BinaryReader(input);

            var header = reader.ReadBytes(4); // Read the first 4 bytes for dimensions and color map
            dimensions = new Rectangle(0, 0, BitConverter.ToUInt16(header, 0), BitConverter.ToUInt16(header, 2));

            var colorMap = new List<Tuple<int,int>>();

            for (int i = 0; i < 4; i++)
            {
                var colorType = reader.ReadByte();
                var colorValue = reader.ReadByte();
                colorMap.Add(new Tuple<int, int>(colorType, colorValue));
            }

            while (input.Position < input.Length)
            {
                var packedValue = reader.ReadByte();
                var colorType = (packedValue >> 6) & 0x03; // Extract the first 2 bits
                var sequenceLength = (packedValue & 0x3F) + 1; // Extract the last 6 bits

                colorType = colorMap.First(c => c.Item2 == colorType).Item1;

                pixelSequences.Add(Tuple.Create(colorType, sequenceLength));
            }

            // Look through list for sequence repeat markers
            var expandedSequences = new List<Tuple<int, int>>();
            while (pixelSequences.Any())
            { 
                var current = pixelSequences.First();
                pixelSequences.RemoveAt(0);

                if (current.Item1 == 3) // Sequence repeat marker
                {
                    var next = pixelSequences.First();
                    pixelSequences.RemoveAt(0);

                    for (int i = 0; i < current.Item2 + 1; i++)
                    {
                        expandedSequences.Add(next);
                    }
                }
                else
                {
                    expandedSequences.Add(current);
                }
            }


            // let's count how many zeros there are at the beginning
            var zeros = 0;
            for (int i = 0; i < expandedSequences.Count; i++)
            {
                if (expandedSequences[i].Item1 == 0)
                {
                    zeros += expandedSequences[i].Item2 + 1;
                }
                else
                {
                    break;
                }
            }

            var z1 = expandedSequences[1087];
            var z2 = expandedSequences[1088];

            reader.Close();
            input.Close();
            return expandedSequences;
        }   

        static void OutputFile(string filename, List<Tuple<int, int>> colorsSample, Rectangle dimensions)
        {
            using (var writer = new StreamWriter(filename))
            {
                writer.WriteLine($"P2");
                writer.WriteLine($"{dimensions.Width} {dimensions.Height}");
                writer.WriteLine($"255");

                var builder = new System.Text.StringBuilder();

                foreach (var tuple in colorsSample)
                {
                    var list = Enumerable.Repeat(tuple.Item1, tuple.Item2 - 1);
                    var text = string.Join(" ", list);

                    builder.Append($"{text} ");
                }

                writer.Write(builder.ToString());
            }
        }
    }
}
