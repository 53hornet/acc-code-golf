
using System.Text.RegularExpressions;

string decodedFile = @"C:\azara\acc-code-golf\decoded.pgm";
    var (width, height, maxShade, pixels) = ReadPGM(@"C:\azara\acc-code-golf\azara.pgm");
    File.WriteAllBytes(@"C:\azara\acc-code-golf\output.rle", Encode(pixels));
    WritePGM(decodedFile, width, height, maxShade, Decode(File.ReadAllBytes(@"C:\azara\acc-code-golf\output.rle")));
    
    static(int width, int height, int maxShade, int[] pixels) ReadPGM(string path)
    {
        var numbers = Regex.Replace(File.ReadAllText(path), @"#[^\r\n]*", "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(int.Parse).ToArray();
        return (numbers[0], numbers[1], numbers[2], numbers[3..]);        
    }
    static void WritePGM(string path, int width, int height, int maxShade, int[] pixels)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("P2");
        writer.WriteLine($"{width} {height}");
        writer.WriteLine(maxShade);
        for (int i = 0; i < pixels.Length; i++)
        {
            if (i > 0 && i % width == 0)
                writer.WriteLine();
            writer.Write(pixels[i]);
            writer.Write(' ');
        }   
    }
    static byte[] Encode(int[] pixels)
    {
        var palette = pixels.Distinct().ToArray();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write7BitEncodedInt(palette.Length);
        foreach (int shade in palette)
            writer.Write7BitEncodedInt(shade);
        for (int i = 0; i < pixels.Length;) //for each pixel
        {
            int end = i + 1; //declare end as the next pixel
            while (end < pixels.Length && pixels[end] == pixels[i]) //while the next pixel is the same as the current pixel
                end++; //increment end
            writer.Write7BitEncodedInt((end - i - 1) * palette.Length + Array.IndexOf(palette, pixels[i])); //get difference between end and i, this is the length of the run, multiply by palette length and add the index of the current pixel in the palette
            i = end; //set i to end, so we can continue to the next pixel
        }
        return stream.ToArray();
    }
    static int[] Decode(byte[] encoded)
    {
        using var stream = new MemoryStream(encoded);
        using var reader = new BinaryReader(stream);
        int n = reader.Read7BitEncodedInt();
        var palette = new int[n];
        for (int i = 0; i < n; i++)
            palette[i] = reader.Read7BitEncodedInt();
        var pixels = new List<int>();
        while (stream.Position < stream.Length)
        {
            int packed = reader.Read7BitEncodedInt();
            pixels.AddRange(
                Enumerable.Repeat(palette[packed % n], packed / n + 1));
        }
        return [.. pixels];
    }

