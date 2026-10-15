using Code_Golf_2026;

CompressedPgmFile ReadPgmFile(string pgmFilePath)
{
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

const string relativePathToBaseRepo = "../../../../../";
string inputPath = Path.Combine(relativePathToBaseRepo, "azara.pgm");
string outputPath = Path.Combine(relativePathToBaseRepo, "azara.compressed.pgm");

var file = ReadPgmFile(inputPath);

HuffmanCodec.Write(outputPath, file);
var decoded = HuffmanCodec.Read(outputPath);
var expected = file.Rows.Select(r => r.SelectMany(g => Enumerable.Repeat(g.Value, g.Count)).ToArray()).ToArray();
bool huffmanRoundTrips = decoded.Pixels.Length == expected.Length
    && decoded.Pixels.Zip(expected).All(p => p.First.SequenceEqual(p.Second));

Console.WriteLine($"Original PGM:   {new FileInfo(inputPath).Length,9:N0} bytes");
Console.WriteLine($"Huffman RLE:    {new FileInfo(outputPath).Length,9:N0} bytes (round trip OK: {huffmanRoundTrips})");
