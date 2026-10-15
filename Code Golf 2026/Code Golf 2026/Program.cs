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

void WritePgmFile(string path, CompressedPgmFile file)
{
    using var writer = new StreamWriter(path);
    writer.WriteLine(file.Variant);
    writer.WriteLine($"{file.Width} {file.Height}");
    writer.WriteLine(file.MaxValue);
    foreach (var row in file.Rows)
    {
        writer.WriteLine(string.Join(' ', row.SelectMany(g => Enumerable.Repeat(g.Value, g.Count))));
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
