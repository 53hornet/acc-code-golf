namespace Code_Golf_2026;

public class PgmFile
{
    public PgmVariant Variant { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int MaxValue { get; set; }

    public List<List<PixelGrouping>> Rows { get; set; } = [];
}
