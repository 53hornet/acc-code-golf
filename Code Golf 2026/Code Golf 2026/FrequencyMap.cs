using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Code_Golf_2026;

public class FrequencyMap
{
    public static IEnumerable<(byte,PixelGrouping)> GetHighFrequencyGroupings(List<PixelGrouping> pixelGroupings)
    {
        var grouped = pixelGroupings.GroupBy(x => (x.Value, x.Count));
        var orderedGroups = grouped.OrderBy(x => x.Count());
        var numDistinctCodes = GetNumDistinctValues(pixelGroupings);

        byte code = (byte)(255 - numDistinctCodes);
        //This is only possible if we don't have any overlap with the possible values
        //if image has a value for everything between 0-255, then we'll have to resort to going up a size to negative short values
        //and then zig-zag encode to prevent c# auto assuming gigantic int value
        Console.WriteLine($"{numDistinctCodes}");
        //Console.Write($"{code}");
        foreach (var group in orderedGroups)
        {
            //Console.WriteLine($"{group.Key.Value}, {group.Key.Count}, {group.Count()}");
            if(group.Count() > 3)
            {
                Console.WriteLine($"{group.Key.Value}");
                if(code > 0)
                {
                    yield return new(code, new PixelGrouping { Value = group.Key.Value, Count = group.Key.Count });
                }
                Console.WriteLine($"{code}");
                code--;
            }
        }
    }

    public static void TrySomething(List<PixelGrouping> pixelGroupings)
    {
        var grouped = pixelGroupings.GroupBy(x => (x.Value, x.Count));
        var orderedGroups = grouped.OrderBy(x => x.Count());
        //short code = -1;
        //foreach (var group in orderedGroups)
        //{
        //    Console.WriteLine($"{group.Key.Value}, {group.Key.Count}, {group.Count()}");
        //    if (group.Count() > 15)
        //    {
        //        //yield return new(code, new PixelGrouping { Value = group.Key.Value, Count = group.Key.Count });
        //        //code--;
        //    }
        //}

        Console.WriteLine($"num distinct values {pixelGroupings.Select(x => x.Value).Distinct().Count()}");
    }

    public static int GetNumDistinctValues(List<PixelGrouping> pixelGroupings)
    {
        return pixelGroupings.Select(x => x.Value).Distinct().Count();
    }
}
