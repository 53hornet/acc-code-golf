using System.IO.Compression;
using System.Runtime.InteropServices;
using Code_Golf_2026;

(int Width, int Height, int MaxValue, List<List<PixelGrouping>> Rows) ReadPgmFile(string pgmFilePath)
{
    // P2 allows pixels to wrap across lines and comments anywhere, so read a token stream
    var tokens = File.ReadLines(pgmFilePath)
        .Select(l => l.Contains('#') ? l[..l.IndexOf('#')] : l)
        .SelectMany(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .ToList();

    // tokens[0] is the "P2" magic number
    int width = int.Parse(tokens[1]);
    int height = int.Parse(tokens[2]);
    int maxPixelValue = int.Parse(tokens[3]);

    var imageRows = new List<List<PixelGrouping>>();
    for (int y = 0; y < height; y++)
    {
        imageRows.Add(ProcessPgmRow(tokens.GetRange(4 + y * width, width)));
    }

    return (width, height, maxPixelValue, imageRows);
}

List<PixelGrouping> ProcessPgmRow(List<string> row)
{
    int? previousPixelValue = null;
    var pixelGroupings = new List<PixelGrouping>();
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

void WriteCompressedFile(string outputFilePath, List<List<PixelGrouping>> compressedData)
{
    using var writer = new StreamWriter(outputFilePath);
    foreach (var row in compressedData)
    {
        foreach (var pixelGrouping in row)
        {
            writer.Write($"{pixelGrouping.Value} {pixelGrouping.Count} ");
        }
        writer.WriteLine();
    }
}

void WriteBinaryCompressedFile(string outputFilePath, int width, int height, int maxValue,
    List<List<PixelGrouping>> compressedData)
{
    using var writer = new BinaryWriter(File.Create(outputFilePath));

    // Header, so the file can be decoded
    writer.Write7BitEncodedInt(width);
    writer.Write7BitEncodedInt(height);
    writer.Write7BitEncodedInt(maxValue);

    var pixelGroupings = compressedData.SelectMany(x => x).ToList();
    var highestGroupings = FrequencyMap.GetHighFrequencyGroupings(pixelGroupings).ToList();
    writer.Write7BitEncodedInt(short.MaxValue);
    FrequencyMap.TrySomething(pixelGroupings);
    foreach(var grouping in highestGroupings)
    {
        writer.Write7BitEncodedInt(grouping.Item1);
        writer.Write7BitEncodedInt(grouping.Item2.Value);
        writer.Write7BitEncodedInt(grouping.Item2.Count);
    }
    writer.Write7BitEncodedInt(short.MaxValue);
    //(n << 1) ^ (n >> 31)
    //https://codegolf.stackexchange.com/questions/276277/zigzag-encoding-and-decoding
    //.net apparently sees a negative number and freaks out thinking it's gigantic when doing Write7BitEncodedInt
    //by doing this zigzag it prevents that and can actually right a 2 byte int instead
    foreach (var row in compressedData)
    {
        foreach (var pixelGrouping in row)
        {
            int n1 = pixelGrouping.Value;
            int? n2 = pixelGrouping.Count;

            var highestGrouping = highestGroupings.FirstOrDefault(x => x.Item2.Value == pixelGrouping.Value && x.Item2.Count == pixelGrouping.Count);
            if(highestGrouping != default)
            {
                //Console.WriteLine($"found value {highestGrouping.Item2.Value}");
                n1 = highestGrouping.Item1;
                n2 = null;
            }

            //var n1Encoded = (n1 << 1) ^ (n1 >> 31);
            //writer.Write7BitEncodedInt(n1Encoded);
            writer.Write7BitEncodedInt(n1);
            if (n2 != null)
            {
                //var n2Encoded = (n2 << 1) ^ (n2 >> 31);
                //writer.Write7BitEncodedInt(n2Encoded.Value);
                writer.Write7BitEncodedInt(n2.Value);
            }
        }
    }
}

int[][] ReadBinaryCompressedFile(string path)
{
    using var reader = new BinaryReader(File.OpenRead(path));
    int width = reader.Read7BitEncodedInt();
    int height = reader.Read7BitEncodedInt();
    reader.Read7BitEncodedInt(); // maxValue

    var image = new int[height][];
    for (int y = 0; y < height; y++)
    {
        image[y] = new int[width];
        for (int x = 0; x < width;)
        {
            int value = reader.ReadByte();
            int count = reader.ReadByte();
            Array.Fill(image[y], value, x, count);
            x += count;
        }
    }
    return image;
}

void DefaultCompressionTest(string inputPath, string outputPath)
{
    using FileStream input = File.Open(inputPath, FileMode.Open);
    using FileStream output = File.Create(outputPath);
    var opt = new ZLibCompressionOptions { CompressionLevel = 9 };
    using var compressor = new DeflateStream(output, opt);
    input.CopyTo(compressor);
}

//C:\CodeGolf2026
const string inputPath = "C:\\CodeGolf2026\\azara.pgm";
const string textPath = "C:\\CodeGolf2026\\azara_compressed.txt";
const string binaryPath = "C:\\CodeGolf2026\\azara_compressed.bin";
const string defaultCompressionPath = "C:\\CodeGolf2026\\azara_compressed_stdlib";

var (width, height, maxValue, rows) = ReadPgmFile(inputPath);
WriteCompressedFile(textPath, rows);
WriteBinaryCompressedFile(binaryPath, width, height, maxValue, rows);
DefaultCompressionTest(inputPath, defaultCompressionPath);

FrequencyMap.GetHighFrequencyGroupings(rows.SelectMany(x => x).ToList());
// Verify the binary file round-trips to the original pixels
//var decoded = ReadBinaryCompressedFile(binaryPath);
//var original = File.ReadLines(inputPath)
//    .Select(l => l.Contains('#') ? l[..l.IndexOf('#')] : l)
//    .SelectMany(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
//    .Skip(4).Select(int.Parse).Chunk(width).ToArray();
//bool roundTrips = decoded.Length == original.Length
//    && decoded.Zip(original).All(p => p.First.SequenceEqual(p.Second));

Console.WriteLine($"Original PGM:   {new FileInfo(inputPath).Length,9:N0} bytes");
Console.WriteLine($"Text RLE:       {new FileInfo(textPath).Length,9:N0} bytes");
Console.WriteLine($"Binary RLE:     {new FileInfo(binaryPath).Length,9:N0} bytes");
Console.WriteLine($"stdlib:         {new FileInfo(defaultCompressionPath).Length,9:N0} bytes");
//Console.WriteLine($"Round trip OK:  {roundTrips}");
