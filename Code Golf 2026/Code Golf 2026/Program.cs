using Code_Golf_2026;
using System.IO.Compression;

CompressedPgmFile ReadPgmFile(string pgmFilePath)
{
    // P2 allows pixels to wrap across lines and comments anywhere, so read a token stream
    var tokens = File.ReadLines(pgmFilePath)
        .Select(l => l.Contains('#') ? l[..l.IndexOf('#')] : l)
        .SelectMany(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .ToList();

    var file = new CompressedPgmFile
    {
        Variant = Enum.Parse<PgmVariant>(tokens[0].ToUpper()),
        Width = int.Parse(tokens[1]),
        Height = int.Parse(tokens[2]),
        MaxValue = int.Parse(tokens[3]),
        Rows = []
    };

    for (int y = 0; y < file.Height; y++)
    {
        file.Rows.Add(ProcessPgmRow(tokens.GetRange(4 + y * file.Width, file.Width)));
    }

    return file;
}

List<PixelGrouping> ProcessPgmRow(List<string> row)
{
    List<PixelGrouping> pixelGroupings = [];
    int? previousPixelValue = null;
    foreach (var pixelText in row)
    {
        int pixel = int.Parse(pixelText);
        if (previousPixelValue == null || pixel != previousPixelValue)
        {
            pixelGroupings.Add(new PixelGrouping { Value = pixel, Count = 1 });
        }
        else
        {
            pixelGroupings[^1].Count++;
        }
        previousPixelValue = pixel;
    }

    return pixelGroupings;
}

void DefaultCompressionTest(string inputPath, string outputPath)
{
    using FileStream input = File.Open(inputPath, FileMode.Open);
    using FileStream output = File.Create(outputPath);
    var opt = new ZLibCompressionOptions { CompressionLevel = 9 };
    using var compressor = new DeflateStream(output, opt);
    input.CopyTo(compressor);
}

const string relativePathToBaseRepo = "../../../../../";
string inputPath = Path.Combine(relativePathToBaseRepo, "azara.pgm");
string textPath = Path.Combine(relativePathToBaseRepo, "azara_compressed.txt");
string binaryPath = Path.Combine(relativePathToBaseRepo, "azara_compressed.bin");
string defaultCompressionPath = Path.Combine(relativePathToBaseRepo, "azara_compressed_stdlib");
string huffmanPath = Path.Combine(relativePathToBaseRepo, "azara_compressed_huffman.bin");

var file = ReadPgmFile(inputPath);
DefaultCompressionTest(inputPath, defaultCompressionPath);

HuffmanCodec.Write(huffmanPath, file);
var decoded = HuffmanCodec.Read(huffmanPath);
var expected = file.Rows.Select(r => r.SelectMany(g => Enumerable.Repeat(g.Value, g.Count)).ToArray()).ToArray();
bool huffmanRoundTrips = decoded.Pixels.Length == expected.Length
    && decoded.Pixels.Zip(expected).All(p => p.First.SequenceEqual(p.Second));

Console.WriteLine($"Original PGM:   {new FileInfo(inputPath).Length,9:N0} bytes");
Console.WriteLine($"Huffman RLE:    {new FileInfo(huffmanPath).Length,9:N0} bytes (round trip OK: {huffmanRoundTrips})");
Console.WriteLine($"stdlib:         {new FileInfo(defaultCompressionPath).Length,9:N0} bytes");
//Console.WriteLine($"Round trip OK:  {roundTrips}");
