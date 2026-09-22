using System.Text;

namespace PlumMarket.Api.Services;

/// <summary>
/// Uzbek Latin ⇄ Cyrillic script conversion. Both scripts are the same language, so this is a
/// deterministic mapping rather than a translation — it works offline and is exact for regular text.
/// </summary>
public static class UzTransliterator
{
    // Multi-letter Latin sequences first so "sh" wins over "s" + "h".
    static readonly (string Lat, string Cyr)[] LatToCyr =
    [
        ("o'", "ў"), ("o‘", "ў"), ("oʻ", "ў"), ("g'", "ғ"), ("g‘", "ғ"), ("gʻ", "ғ"),
        ("sh", "ш"), ("ch", "ч"), ("yo", "ё"), ("yu", "ю"), ("ya", "я"), ("ye", "е"), ("ts", "ц"),
        ("a", "а"), ("b", "б"), ("d", "д"), ("e", "е"), ("f", "ф"), ("g", "г"), ("h", "ҳ"), ("i", "и"),
        ("j", "ж"), ("k", "к"), ("l", "л"), ("m", "м"), ("n", "н"), ("o", "о"), ("p", "п"), ("q", "қ"),
        ("r", "р"), ("s", "с"), ("t", "т"), ("u", "у"), ("v", "в"), ("x", "х"), ("y", "й"), ("z", "з"),
        ("'", "ъ"), ("’", "ъ"), ("ʼ", "ъ"), ("c", "к"), ("w", "в"),
    ];

    static readonly Dictionary<char, string> CyrToLat = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo", ['ж'] = "j",
        ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o",
        ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "x", ['ц'] = "ts",
        ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sh", ['ъ'] = "'", ['ы'] = "i", ['ь'] = "", ['э'] = "e", ['ю'] = "yu",
        ['я'] = "ya", ['ў'] = "o'", ['қ'] = "q", ['ғ'] = "g'", ['ҳ'] = "h",
    };

    public static string ToCyrillic(string latin)
    {
        var sb = new StringBuilder(latin.Length);
        var i = 0;
        while (i < latin.Length)
        {
            var matched = false;
            foreach (var (lat, cyr) in LatToCyr)
            {
                if (string.Compare(latin, i, lat, 0, lat.Length, StringComparison.OrdinalIgnoreCase) != 0) continue;
                var upper = char.IsUpper(latin[i]);
                // "E" at the start of a word is "Э" in Cyrillic.
                var value = lat == "e" && (i == 0 || !char.IsLetter(latin[i - 1])) ? "э" : cyr;
                sb.Append(upper ? Capitalize(value, latin, i, lat.Length) : value);
                i += lat.Length;
                matched = true;
                break;
            }
            if (!matched) sb.Append(latin[i++]);
        }
        return sb.ToString();
    }

    public static string ToLatin(string cyrillic)
    {
        var sb = new StringBuilder(cyrillic.Length);
        for (var i = 0; i < cyrillic.Length; i++)
        {
            var ch = cyrillic[i];
            var lower = char.ToLowerInvariant(ch);
            if (!CyrToLat.TryGetValue(lower, out var lat)) { sb.Append(ch); continue; }
            // "е" at the start of a word or after a vowel reads "ye".
            if (lower == 'е' && (i == 0 || !char.IsLetter(cyrillic[i - 1]) || "аеёиоуэюяў".Contains(char.ToLowerInvariant(cyrillic[i - 1]))))
                lat = "ye";
            if (char.IsUpper(ch) && lat.Length > 0)
                lat = char.ToUpperInvariant(lat[0]) + lat[1..];
            sb.Append(lat);
        }
        return sb.ToString();
    }

    /// <summary>ALL-CAPS source keeps the whole digraph upper-case; Title-case only the first letter.</summary>
    static string Capitalize(string value, string source, int index, int length)
    {
        var allCaps = length > 1 && index + 1 < source.Length && char.IsUpper(source[index + 1]);
        return allCaps ? value.ToUpperInvariant() : char.ToUpperInvariant(value[0]) + value[1..];
    }
}
