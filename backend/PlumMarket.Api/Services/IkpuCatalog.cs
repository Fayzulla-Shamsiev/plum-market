namespace PlumMarket.Api.Services;

public record IkpuEntry(string Code, string Name, string PackageCode, string PackageName, string UnitCode, string UnitName, string[] Keywords);

/// <summary>
/// "Сгенерировать ИКПУ": picks a classifier entry for a product by keyword match.
///
/// DEMO REFERENCE: the codes below are placeholders shaped like real 17-digit MXIK codes, not values
/// taken from the official classifier. Production must query the tax committee's registry
/// (tasnif.soliq.uz API) with the product name and let the merchant confirm the match.
/// </summary>
public static class IkpuCatalog
{
    public static readonly IkpuEntry[] Entries =
    [
        new("10710001001000000", "Хлеб и хлебобулочные изделия", "1499", "штука", "796", "шт", ["хлеб", "багет", "лепёшка", "лепешка", "non", "батон"]),
        new("10710002001000000", "Изделия из слоёного теста (круассаны)", "1499", "штука", "796", "шт", ["круассан", "слоён", "kruassan"]),
        new("10710003001000000", "Пирожки, самса, выпечка с начинкой", "1499", "штука", "796", "шт", ["самса", "пирож", "somsa", "маффин"]),
        new("10720001001000000", "Торты", "1508", "упаковка", "796", "шт", ["торт", "медовик", "наполеон", "бархат", "tort"]),
        new("10720002001000000", "Пирожные и десерты", "1499", "штука", "796", "шт", ["пирожн", "эклер", "чизкейк", "тирамису", "десерт"]),
        new("56100001001000000", "Пицца (продукция общепита)", "1499", "штука", "796", "шт", ["пицца", "pitsa"]),
        new("56100002001000000", "Бургеры, шаурма, хот-доги (продукция общепита)", "1499", "штука", "796", "шт", ["бургер", "шаурма", "лаваш", "хот-дог", "сэндвич", "чизбургер"]),
        new("56100003001000000", "Салаты и супы (продукция общепита)", "1510", "порция", "796", "шт", ["салат", "суп"]),
        new("56300001001000000", "Кофе и кофейные напитки (общепит)", "1510", "порция", "796", "шт", ["кофе", "капучино", "латте", "американо", "раф", "эспрессо"]),
        new("56300002001000000", "Чай и безалкогольные напитки (общепит)", "1510", "порция", "796", "шт", ["чай", "лимонад", "сок", "фреш", "напит"]),
    ];

    public static IkpuEntry? Suggest(string productName, string? categoryName)
    {
        var text = $"{productName} {categoryName}".ToLowerInvariant();
        return Entries
            .Select(e => (Entry: e, Score: e.Keywords.Count(k => text.Contains(k.ToLowerInvariant()))))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Entry)
            .FirstOrDefault();
    }

    public static IkpuEntry? Find(string? code) => Entries.FirstOrDefault(e => e.Code == code);
}
