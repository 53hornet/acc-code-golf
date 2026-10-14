namespace Code_Golf_2026;

using Symbol = (int Value, int Count);

/// <summary>
/// Canonical Huffman coding over (value, count) runs.
/// File layout: width, height, maxValue, symbolCount, then per symbol (value, count, codeLength)
/// in canonical order, then the packed bit stream (rows are decoded until `width` pixels are filled).
/// </summary>
public static class HuffmanCodec
{
    private record Node(Symbol? Symbol, Node? Left = null, Node? Right = null);

    public static void Write(string path, CompressedPgmFile file)
    {
        var runs = file.Rows.SelectMany(r => r).Select(g => (g.Value, g.Count)).ToList();
        var table = AssignCanonicalCodes(BuildCodeLengths(runs));
        var codes = table.ToDictionary(t => t.Symbol, t => (t.Code, t.Length));

        using var writer = new BinaryWriter(File.Create(path));
        writer.Write7BitEncodedInt(file.Width);
        writer.Write7BitEncodedInt(file.Height);
        writer.Write7BitEncodedInt(file.MaxValue);

        writer.Write7BitEncodedInt(table.Count);
        foreach (var (symbol, length, _) in table)
        {
            writer.Write7BitEncodedInt(symbol.Value);
            writer.Write7BitEncodedInt(symbol.Count);
            writer.Write((byte)length);
        }

        int buffer = 0, bitsInBuffer = 0;
        foreach (var run in runs)
        {
            var (code, length) = codes[run];
            for (int i = length - 1; i >= 0; i--)
            {
                buffer = (buffer << 1) | (int)((code >> i) & 1);
                if (++bitsInBuffer == 8)
                {
                    writer.Write((byte)buffer);
                    buffer = 0;
                    bitsInBuffer = 0;
                }
            }
        }
        if (bitsInBuffer > 0)
        {
            writer.Write((byte)(buffer << (8 - bitsInBuffer)));
        }
    }

    public static (int Width, int Height, int MaxValue, int[][] Pixels) Read(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        int width = reader.Read7BitEncodedInt();
        int height = reader.Read7BitEncodedInt();
        int maxValue = reader.Read7BitEncodedInt();

        int symbolCount = reader.Read7BitEncodedInt();
        var lengths = new List<(Symbol Symbol, int Length)>();
        for (int i = 0; i < symbolCount; i++)
        {
            lengths.Add(((reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt()), reader.ReadByte()));
        }
        // Written in canonical order, so the same assignment reproduces the encoder's codes
        var lookup = AssignCanonicalCodes(lengths)
            .ToDictionary(t => (t.Length, t.Code), t => t.Symbol);

        int buffer = 0, bitsLeft = 0;
        int ReadBit()
        {
            if (bitsLeft == 0)
            {
                buffer = reader.ReadByte();
                bitsLeft = 8;
            }
            return (buffer >> --bitsLeft) & 1;
        }

        var image = new int[height][];
        for (int y = 0; y < height; y++)
        {
            image[y] = new int[width];
            for (int x = 0; x < width;)
            {
                uint code = 0;
                int length = 0;
                Symbol symbol;
                do
                {
                    code = (code << 1) | (uint)ReadBit();
                    length++;
                } while (!lookup.TryGetValue((length, code), out symbol));

                Array.Fill(image[y], symbol.Value, x, symbol.Count);
                x += symbol.Count;
            }
        }
        return (width, height, maxValue, image);
    }

    private static List<(Symbol Symbol, int Length)> BuildCodeLengths(List<Symbol> runs)
    {
        var frequencies = runs.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());

        // A lone symbol still needs 1 bit, otherwise its code would be empty
        if (frequencies.Count == 1)
        {
            return [(frequencies.Keys.First(), 1)];
        }

        var queue = new PriorityQueue<Node, int>();
        foreach (var (symbol, frequency) in frequencies)
        {
            queue.Enqueue(new Node(symbol), frequency);
        }
        while (queue.Count > 1)
        {
            queue.TryDequeue(out var left, out var leftFrequency);
            queue.TryDequeue(out var right, out var rightFrequency);
            queue.Enqueue(new Node(null, left, right), leftFrequency + rightFrequency);
        }

        var lengths = new List<(Symbol, int)>();
        void Walk(Node node, int depth)
        {
            if (node.Symbol is { } symbol)
            {
                lengths.Add((symbol, depth));
                return;
            }
            Walk(node.Left!, depth + 1);
            Walk(node.Right!, depth + 1);
        }
        Walk(queue.Dequeue(), 0);
        return lengths;
    }

    // Canonical order: shorter codes first, ties broken by (value, count), so only the lengths need storing
    private static List<(Symbol Symbol, int Length, uint Code)> AssignCanonicalCodes(
        List<(Symbol Symbol, int Length)> lengths)
    {
        var ordered = lengths
            .OrderBy(l => l.Length).ThenBy(l => l.Symbol.Value).ThenBy(l => l.Symbol.Count)
            .ToList();

        var table = new List<(Symbol, int, uint)>();
        uint code = 0;
        int previousLength = ordered[0].Length;
        foreach (var (symbol, length) in ordered)
        {
            code <<= length - previousLength;
            previousLength = length;
            table.Add((symbol, length, code));
            code++;
        }
        return table;
    }
}
