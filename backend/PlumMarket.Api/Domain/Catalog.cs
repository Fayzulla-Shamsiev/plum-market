using System.Text.Json.Serialization;

namespace PlumMarket.Api.Domain;

/// <summary>
/// Catalog content languages from the spec: Russian, Uzbek (Latin) and Uzbek (Cyrillic).
/// Stored as a JSON object per field, e.g. {"ru":"Круассан","uz":"Kruassan","oz":"Круассан"}.
/// </summary>
public class Localized : Dictionary<string, string>
{
    public static readonly string[] Languages = ["ru", "uz", "oz"];

    public Localized() { }
    public Localized(IDictionary<string, string> source) : base(source) { }

    public static Localized Of(string ru, string? uz = null, string? oz = null)
    {
        var l = new Localized { ["ru"] = ru };
        if (uz is not null) l["uz"] = uz;
        if (oz is not null) l["oz"] = oz;
        return l;
    }

    /// <summary>Value in the requested language, falling back to Russian, then to any non-empty value.</summary>
    public string Get(string lang = "ru") =>
        TryGetValue(lang, out var v) && !string.IsNullOrWhiteSpace(v) ? v
        : TryGetValue("ru", out var ru) && !string.IsNullOrWhiteSpace(ru) ? ru
        : Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "";
}

public class Category : IStoreOwned
{
    [JsonIgnore] public int StoreId { get; set; }
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public Localized Name { get; set; } = new();
    public Localized Description { get; set; } = new();
    public string? ImageUrl { get; set; }
    public string? BannerUrl { get; set; }
    /// <summary>How products are laid out in the storefront: grid2 | grid3 | list.</summary>
    public string Layout { get; set; } = "grid2";
    /// <summary>Default product order in the storefront: manual | popular | new | price_asc | price_desc.</summary>
    public string ProductSort { get; set; } = "manual";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public enum StockStatus { Unlimited, Limited, OutOfStock }

public class ProductAttribute
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

public class ProductVariant
{
    public string Name { get; set; } = "";
    public long? Price { get; set; }
    public string? Sku { get; set; }
}

public class ProductMedia
{
    public string Url { get; set; } = "";
    /// <summary>image | video</summary>
    public string Type { get; set; } = "image";
}

public class Product : IStoreOwned
{
    [JsonIgnore] public int StoreId { get; set; }
    public int Id { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public Localized Name { get; set; } = new();
    public Localized Description { get; set; } = new();

    public long Price { get; set; }
    public long? OldPrice { get; set; }
    public long CostPrice { get; set; }
    /// <summary>шт | кг | г | л | мл | порция | упак</summary>
    public string Unit { get; set; } = "шт";

    public int? WeightGrams { get; set; }
    public int? LengthCm { get; set; }
    public int? WidthCm { get; set; }
    public int? HeightCm { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<ProductAttribute> Attributes { get; set; } = new();
    public List<ProductVariant> Variants { get; set; } = new();
    public List<ProductMedia> Media { get; set; } = new();

    /// <summary>ИКПУ (MXIK) — 17-digit national product classifier code used on fiscal receipts.</summary>
    public string? Ikpu { get; set; }
    public string? PackageCode { get; set; }
    public string? UnitCode { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>One row per branch where the product is sold; no row = not available there.</summary>
    public List<StockItem> Stock { get; set; } = new();
}

/// <summary>Per-branch availability and stock level of a product.</summary>
public class StockItem : IStoreOwned
{
    [JsonIgnore] public int StoreId { get; set; }
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int BranchId { get; set; }
    public StockStatus Status { get; set; } = StockStatus.Unlimited;
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum DiscountType { Percent, Fixed }

public class Discount : IStoreOwned
{
    [JsonIgnore] public int StoreId { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DiscountType Type { get; set; }
    /// <summary>Percent (1–100) or a fixed amount in сум, depending on <see cref="Type"/>.</summary>
    public long Value { get; set; }
    public List<int> ProductIds { get; set; } = new();
    /// <summary>Empty = all branches.</summary>
    public List<int> BranchIds { get; set; } = new();
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public long? MinOrderAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public enum ReviewStatus { New, Answered }

public class Review : IStoreOwned
{
    [JsonIgnore] public int StoreId { get; set; }
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public ReviewStatus Status { get; set; }
    public string? Reply { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RepliedAt { get; set; }
}
