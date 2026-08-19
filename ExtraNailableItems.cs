using System;
using System.Collections.Generic;

namespace UnlimitedHammer;

internal static class ExtraNailableItems
{
    private static readonly HashSet<string> Names =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "137 model ship junk (big)",
            "138 model ship junk (small)",
            "190 flower pot",
            "191 flower pot 1",
            "192 flower pot 2",
            "193 flower pot 3",
            "194 flower pot 4 (small)",
            "195 flower pot 5 (small)"
        };

    public static bool Contains(ShipItem item)
    {
        if (item == null)
            return false;

        return Names.Contains(NormalizeName(item.transform.name));
    }

    private static string NormalizeName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
            return string.Empty;

        itemName = itemName.Trim();

        while (itemName.EndsWith("(Clone)", StringComparison.Ordinal))
        {
            itemName = itemName
                .Substring(0, itemName.Length - "(Clone)".Length)
                .Trim();
        }

        return itemName;
    }
}
