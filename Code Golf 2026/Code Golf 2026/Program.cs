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

    int channelWidth = GetPixelChannelWidth(file.Variant);
    int rowTokens = file.Width * channelWidth;
    for (int y = 0; y < file.Height; y++)
    {
        file.Rows.Add(ProcessPgmRow(tokens.GetRange(4 + y * rowTokens, rowTokens), channelWidth));
    }

    return file;
}

int GetPixelChannelWidth(PgmVariant variant)
{
    return variant switch
    {
        PgmVariant.P2 => 1,
        PgmVariant.P3 => 3,
        _ => 0
    };
}

List<PixelGrouping> ProcessPgmRow(List<string> row, int channelWidth)
{
    List<PixelGrouping> pixelGroupings = [];
    int? previousPixelValue = null;
    for (int i = 0; i < row.Count; i += channelWidth)
    {
        int pixel = 0;
        for (int c = 0; c < channelWidth; c++)
        {
            pixel = (pixel << 8) | int.Parse(row[i + c]);
        }

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

void WritePgmFile(string path, CompressedPgmFile file)
{
    int channelWidth = GetPixelChannelWidth(file.Variant);
    int mask = channelWidth == 1 ? int.MaxValue : 0xFF;
    using var writer = new StreamWriter(path);
    writer.WriteLine(file.Variant);
    writer.WriteLine($"{file.Width} {file.Height}");
    writer.WriteLine(file.MaxValue);
    foreach (var row in file.Rows)
    {
        var samples = row
            .SelectMany(g => Enumerable.Repeat(g.Value, g.Count))
            .SelectMany(pixel => Enumerable.Range(0, channelWidth)
                .Select(c => (pixel >> (8 * (channelWidth - 1 - c))) & mask));
        writer.WriteLine(string.Join(' ', samples));
    }
}

string command = args[0].ToLowerInvariant();
string inputPath = args[1];
string outputPath = args[2];

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file not found: {inputPath}");
    return 1;
}

switch (command)
{
    case "encode":
        HuffmanCodec.Write(outputPath, ReadPgmFile(inputPath));
        break;
    case "decode":
        WritePgmFile(outputPath, HuffmanCodec.Read(inputPath));
        break;
}

return 0;
