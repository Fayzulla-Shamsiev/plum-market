using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PlumMarket.Api.Data;
using PlumMarket.Api.Domain;

namespace PlumMarket.Api.Services;

/// <summary>
/// What the panel shows under an answer when the assistant changed something (or needs the administrator to):
/// change — a setting or catalog edit, with a link to where it lives in the panel; link — a button to a panel
/// section; telegram — the form the bot token is typed into (never the chat); finish — setup is done.
/// </summary>
/// availability — the products just created, with a checkbox and a quantity box per branch.
/// categories — the categories just created, each with a photo slot.
public record AssistantCard(string Kind, string Title, List<string>? Lines = null, string? Link = null, string? LinkLabel = null,
    List<int>? ProductIds = null, List<int>? CategoryIds = null);

/// <summary>A tool's answer for the model, plus the card for the administrator.</summary>
public record ToolOutcome(string Result, AssistantCard? Card = null);

/// <summary>
/// The actions the setup assistant can take on the administrator's own store. Each one does what the matching
/// admin page does, with the same validation, so a store set up by chat is indistinguishable from one set up by
/// hand. Runs inside an admin request: the store filter already limits every query to this store.
/// </summary>
public partial class AssistantTools(AppDbContext db, StoreContext tenant)
{
    /// <summary>Panel sections the assistant can send the administrator to.</summary>
    public static readonly Dictionary<string, (string Path, string Label)> Sections = new()
    {
        ["dashboard"] = ("/dashboard", "Дашборд"),
        ["orders"] = ("/orders", "Заказы"),
        ["customers"] = ("/customers", "Клиенты"),
        ["chat"] = ("/chat", "Чат"),
        ["categories"] = ("/products/categories", "Каталог → Категории"),
        ["products"] = ("/products/items", "Каталог → Товары"),
        ["new_product"] = ("/products/items/new", "Новый товар"),
        ["discounts"] = ("/products/discounts", "Каталог → Скидки"),
        ["stock"] = ("/products/stock", "Каталог → Склад"),
        ["promocodes"] = ("/marketing/promocodes", "Маркетинг → Промокоды"),
        ["banners"] = ("/marketing/banners", "Маркетинг → Баннеры"),
        ["reviews"] = ("/marketing/reviews", "Маркетинг → Отзывы"),
        ["website"] = ("/platforms/website", "Платформы → Веб-сайт"),
        ["telegram"] = ("/platforms/telegram", "Платформы → Telegram-бот"),
        ["store"] = ("/store", "Магазин"),
    };

    /// <summary>Steps the administrator can accept as they are (see <see cref="Store.SetupAccepted"/>).</summary>
    public static readonly string[] AcceptableSteps = ["profile", "contacts", "delivery", "returns"];

    static readonly string[] Units = ["шт", "кг", "г", "л", "мл", "порция", "упак"];

    // ------------------------------------------------------------------ definitions (OpenAI function schemas)

    /// <summary>
    /// Strict schemas: every property is required, optional ones are nullable — the model then always sends
    /// well-formed arguments, and null means "leave as it is".
    /// </summary>
    public static readonly object[] Definitions =
    [
        Fn("update_store_profile",
            "Change the shop's name, its «О нас» text and/or its return & exchange terms (Платформы → Веб-сайт). " +
            "Texts are plain text; a line starting with '## ' is a heading, paragraphs are separated by blank lines. " +
            "Pass null for what should stay unchanged.",
            new()
            {
                ["name"] = NStr("New shop name, 2–80 characters"),
                ["about"] = NStr("«О нас» — who the shop is, what it sells, why buy here (Russian)"),
                ["return_terms"] = NStr("Return & exchange terms shown to customers (Russian)"),
            }),
        Fn("update_contacts",
            "Set the shop's public phone number and/or working hours (Магазин). Null keeps the current value.",
            new()
            {
                ["phone"] = NStr("Phone, e.g. '+998 90 123 45 67'"),
                ["working_hours"] = NStr("Free text, e.g. 'Ежедневно 09:00–21:00' or 'Пн–Сб 10:00–20:00'"),
            }),
        Fn("update_delivery",
            "Set courier delivery price, the order amount from which delivery is free, and/or the delivery terms text " +
            "(Магазин → Доставка). Amounts are whole сум. Null keeps the current value.",
            new()
            {
                ["delivery_fee"] = NInt("Delivery price in сум, 0 = always free"),
                ["free_delivery_from"] = NInt("Order total from which delivery is free, in сум; 0 = never free"),
                ["delivery_terms"] = NStr("Delivery & pickup terms shown to customers ('## ' headings allowed)"),
            }),
        Fn("save_branch",
            "Update an existing branch (pass its id) or create a new one (branch_id null). A branch is a pickup point " +
            "and stock location. For a new branch name and address are required. Coordinates: pass your best estimate " +
            "for the address (Uzbekistan) — the administrator will be asked to check the pin on the map.",
            new()
            {
                ["branch_id"] = NInt("Existing branch id, or null to create a new branch"),
                ["name"] = NStr("Branch name, e.g. 'Чиланзар'"),
                ["address"] = NStr("Street address"),
                ["phone"] = NStr("Branch phone"),
                ["working_hours"] = NStr("Branch working hours"),
                ["lat"] = NNum("Latitude"),
                ["lng"] = NNum("Longitude"),
            }),
        Fn("create_categories",
            "Create catalog categories. Existing names are skipped. Always give the Uzbek (Latin) name too.",
            new()
            {
                ["categories"] = Arr(Obj(new()
                {
                    ["name_ru"] = Str("Name in Russian"),
                    ["name_uz"] = Str("Name in Uzbek, Latin script"),
                    ["description_ru"] = NStr("Short description in Russian"),
                    ["description_uz"] = NStr("Short description in Uzbek Latin"),
                    ["parent"] = NStr("Russian name of an existing parent category, for a sub-category"),
                    ["image_url"] = NStr("An /uploads/... image the administrator attached"),
                })),
            }),
        Fn("create_products",
            "Create products (up to 40 per call). A category given by its Russian name is created if missing. " +
            "branch_ids says where each product is sold (null = every branch); quantity is the stock in each of " +
            "those branches (null = unlimited). A count of pieces is quantity, not more products: \"3 розы\" is one " +
            "product «Роза» with quantity 3. Always give Uzbek (Latin) names and short descriptions in both " +
            "languages. Never invent prices: use the administrator's. The panel then shows a table where the " +
            "administrator can tick branches and type quantities per product.",
            new()
            {
                ["products"] = Arr(Obj(new()
                {
                    ["name_ru"] = Str("Name in Russian"),
                    ["name_uz"] = Str("Name in Uzbek, Latin script"),
                    ["description_ru"] = NStr("1–2 sentences in Russian"),
                    ["description_uz"] = NStr("1–2 sentences in Uzbek Latin"),
                    ["price"] = Int("Price in сум, > 0"),
                    ["old_price"] = NInt("Crossed-out price in сум (must be higher than price)"),
                    ["cost_price"] = NInt("Cost price in сум, for profit reports"),
                    ["unit"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = Units, ["description"] = "Unit of sale" },
                    ["category"] = NStr("Russian category name"),
                    ["weight_grams"] = NInt("Weight in grams"),
                    ["image_url"] = NStr("An /uploads/... image the administrator attached"),
                    ["branch_ids"] = new Dictionary<string, object> { ["type"] = new[] { "array", "null" }, ["items"] = new Dictionary<string, object> { ["type"] = "integer" }, ["description"] = "Branch ids where it is sold; null = all branches" },
                    ["quantity"] = NInt("Pieces in stock per branch; null = unlimited (made to order, always available)"),
                })),
            }),
        Fn("update_product",
            "Change one product. Null keeps a field as it is; old_price 0 removes the crossed-out price.",
            new()
            {
                ["product_id"] = Int("Product id"),
                ["name_ru"] = NStr("Name in Russian"),
                ["name_uz"] = NStr("Name in Uzbek Latin"),
                ["description_ru"] = NStr("Description in Russian"),
                ["description_uz"] = NStr("Description in Uzbek Latin"),
                ["price"] = NInt("Price in сум"),
                ["old_price"] = NInt("Crossed-out price in сум, 0 to remove"),
                ["category"] = NStr("Russian category name (created if missing)"),
                ["image_url"] = NStr("An /uploads/... image to use as the main photo"),
                ["is_active"] = new Dictionary<string, object> { ["type"] = new[] { "boolean", "null" }, ["description"] = "Shown in the shop" },
                ["branch_ids"] = new Dictionary<string, object> { ["type"] = new[] { "array", "null" }, ["items"] = new Dictionary<string, object> { ["type"] = "integer" }, ["description"] = "Branch ids where it is sold (replaces the current list); null keeps it" },
                ["quantity"] = NInt("Stock per listed branch; null keeps it (or unlimited for newly listed branches)"),
            }),
        Fn("delete_products",
            "Delete products. Only after the administrator explicitly confirmed which ones.",
            new() { ["product_ids"] = Arr(Int("Product id")) }),
        Fn("delete_category",
            "Delete an empty category (no products, no sub-categories). Only after explicit confirmation.",
            new() { ["category_id"] = Int("Category id") }),
        Fn("set_bonus_program",
            "Turn the loyalty points program on or off. 1 point = 1 сум off a later order.",
            new()
            {
                ["enabled"] = Bool("On or off"),
                ["spend_per_point"] = NInt("How many сум a customer spends to earn 1 point, e.g. 100 (= 1% back)"),
            }),
        Fn("create_promo_code",
            "Create a promo code customers type at checkout.",
            new()
            {
                ["code"] = Str("3–20 Latin letters/digits, e.g. 'WELCOME10'"),
                ["type"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "percent", "fixed" } },
                ["value"] = Int("Percent (1–100) or amount in сум"),
                ["min_order_amount"] = NInt("Minimum order total in сум"),
                ["days_valid"] = Int("How many days from today it works"),
                ["first_order_only"] = Bool("Only for a customer's first order"),
                ["usage_limit"] = NInt("Total number of uses, null = unlimited"),
            }),
        Fn("request_telegram_bot",
            "Show the administrator the secure form to connect their Telegram bot token from @BotFather. The token " +
            "must never be typed into the chat — call this instead of asking for it.",
            new()),
        Fn("accept_step",
            "Record that the administrator is happy with a setup step as it is (e.g. keeps the default delivery price).",
            new()
            {
                ["step"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = AcceptableSteps },
            }),
        Fn("open_section",
            "Show a button that opens a section of the admin panel — for things done by hand there (uploading many " +
            "photos, moving a branch pin on the map, stock levels, banners).",
            new()
            {
                ["section"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = Sections.Keys.ToArray() },
                ["label"] = Str("Button text in the administrator's language, e.g. 'Открыть карту филиала'"),
            }),
        Fn("finish_setup",
            "Mark the first-run setup as finished, once the shop can take orders and the administrator agrees. " +
            "Shows buttons to the admin panel and the shop.",
            new()),
    ];

    static object Fn(string name, string description, Dictionary<string, object> properties) => new
    {
        type = "function",
        function = new
        {
            name, description, strict = true,
            parameters = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = properties.Keys.ToArray(),
                ["additionalProperties"] = false,
            },
        },
    };

    static Dictionary<string, object> Obj(Dictionary<string, object> properties) => new()
    {
        ["type"] = "object",
        ["properties"] = properties,
        ["required"] = properties.Keys.ToArray(),
        ["additionalProperties"] = false,
    };

    static Dictionary<string, object> Arr(object items) => new() { ["type"] = "array", ["items"] = items };
    static Dictionary<string, object> Str(string d) => new() { ["type"] = "string", ["description"] = d };
    static Dictionary<string, object> NStr(string d) => new() { ["type"] = new[] { "string", "null" }, ["description"] = d };
    static Dictionary<string, object> Int(string d) => new() { ["type"] = "integer", ["description"] = d };
    static Dictionary<string, object> NInt(string d) => new() { ["type"] = new[] { "integer", "null" }, ["description"] = d };
    static Dictionary<string, object> NNum(string d) => new() { ["type"] = new[] { "number", "null" }, ["description"] = d };
    static Dictionary<string, object> Bool(string d) => new() { ["type"] = "boolean", ["description"] = d };

    // ------------------------------------------------------------------ execution

    /// <summary>Runs one tool call. A failure is reported to the model as text, so it can explain or retry.</summary>
    public async Task<ToolOutcome> RunAsync(string name, string arguments)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments);
            var a = doc.RootElement;
            return name switch
            {
                "update_store_profile" => await UpdateProfile(a),
                "update_contacts" => await UpdateContacts(a),
                "update_delivery" => await UpdateDelivery(a),
                "save_branch" => await SaveBranch(a),
                "create_categories" => await CreateCategories(a),
                "create_products" => await CreateProducts(a),
                "update_product" => await UpdateProduct(a),
                "delete_products" => await DeleteProducts(a),
                "delete_category" => await DeleteCategory(a),
                "set_bonus_program" => await SetBonus(a),
                "create_promo_code" => await CreatePromo(a),
                "request_telegram_bot" => RequestBot(),
                "accept_step" => await AcceptStep(a),
                "open_section" => OpenSection(a),
                "finish_setup" => await Finish(),
                _ => new ToolOutcome($"Ошибка: неизвестный инструмент {name}"),
            };
        }
        catch (ToolError e)
        {
            return new ToolOutcome("Ошибка: " + e.Message);
        }
        catch (JsonException)
        {
            return new ToolOutcome("Ошибка: аргументы не разобраны, повторите вызов.");
        }
        catch (Exception e) when (e is DbUpdateException or InvalidOperationException or KeyNotFoundException)
        {
            // Whatever this call left half-added must not ride along with the next save.
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified && x.Entity is not AssistantMessage).ToList())
                entry.State = entry.State == EntityState.Added ? EntityState.Detached : EntityState.Unchanged;
            return new ToolOutcome("Ошибка: не удалось сохранить — " + e.Message);
        }
    }

    sealed class ToolError(string message) : Exception(message);

    async Task<ToolOutcome> UpdateProfile(JsonElement a)
    {
        var store = tenant.Store!;
        var settings = await db.Settings.FirstAsync();
        var lines = new List<string>();
        if (S(a, "name") is { } name)
        {
            if (name.Length is < 2 or > 80) throw new ToolError("название магазина — от 2 до 80 символов");
            store.Name = name;
            settings.StoreName = name;
            lines.Add($"Название: {name}");
        }
        if (S(a, "about") is { } about)
        {
            settings.AboutText = Cut(about, 4000);
            lines.Add("«О нас»: " + Preview(about));
        }
        if (S(a, "return_terms") is { } returns)
        {
            settings.ReturnTerms = Cut(returns, 8000);
            lines.Add("Условия возврата: " + Preview(returns));
        }
        if (lines.Count == 0) return new ToolOutcome("Ничего не изменено: все поля пустые.");
        await db.SaveChangesAsync();
        return new ToolOutcome("Сохранено. " + string.Join("; ", lines),
            new AssistantCard("change", "Профиль магазина обновлён", lines, "/platforms/website", "Платформы → Веб-сайт"));
    }

    async Task<ToolOutcome> UpdateContacts(JsonElement a)
    {
        var settings = await db.Settings.FirstAsync();
        var branch = await db.Branches.OrderBy(b => b.Id).FirstOrDefaultAsync();
        var lines = new List<string>();
        if (S(a, "phone") is { } phone)
        {
            // The main branch shows the same number until it is given its own.
            if (branch is not null && (branch.Phone is null || branch.Phone == settings.Phone)) branch.Phone = Cut(phone, 40);
            settings.Phone = Cut(phone, 40);
            lines.Add($"Телефон: {phone}");
        }
        if (S(a, "working_hours") is { } hours)
        {
            if (branch is not null && (branch.WorkingHours is null || branch.WorkingHours == settings.WorkingHours)) branch.WorkingHours = Cut(hours, 120);
            settings.WorkingHours = Cut(hours, 120);
            lines.Add($"Время работы: {hours}");
        }
        if (lines.Count == 0) return new ToolOutcome("Ничего не изменено: все поля пустые.");
        Accept("contacts");
        await db.SaveChangesAsync();
        return new ToolOutcome("Сохранено. " + string.Join("; ", lines),
            new AssistantCard("change", "Контакты обновлены", lines, "/store", "Магазин"));
    }

    async Task<ToolOutcome> UpdateDelivery(JsonElement a)
    {
        var settings = await db.Settings.FirstAsync();
        var lines = new List<string>();
        if (L(a, "delivery_fee") is { } fee)
        {
            if (fee < 0) throw new ToolError("стоимость доставки не может быть отрицательной");
            settings.DeliveryFee = fee;
            lines.Add("Доставка: " + (fee == 0 ? "бесплатно" : Money(fee)));
        }
        if (L(a, "free_delivery_from") is { } free)
        {
            if (free < 0) throw new ToolError("сумма бесплатной доставки не может быть отрицательной");
            settings.FreeDeliveryFrom = free > 0 ? free : null;
            lines.Add(free > 0 ? $"Бесплатно от {Money(free)}" : "Без бесплатной доставки");
        }
        if (S(a, "delivery_terms") is { } terms)
        {
            settings.DeliveryTerms = Cut(terms, 8000);
            lines.Add("Условия: " + Preview(terms));
        }
        if (lines.Count == 0) return new ToolOutcome("Ничего не изменено: все поля пустые.");
        Accept("delivery");
        await db.SaveChangesAsync();
        // The template's text says prices are "set in the admin panel" — wrong once they are, so it gets rewritten.
        var note = settings.DeliveryTerms == StoreProvisioner.DefaultDeliveryTerms
            ? " Текст условий доставки всё ещё шаблонный: сразу вызови update_delivery с delivery_terms — самовывоз, " +
              "доставка (цена, порог бесплатной доставки, срок), оплата наличными — на основе того, что известно."
            : "";
        return new ToolOutcome("Сохранено. " + string.Join("; ", lines) + note,
            new AssistantCard("change", "Доставка настроена", lines, "/store", "Магазин → Доставка"));
    }

    async Task<ToolOutcome> SaveBranch(JsonElement a)
    {
        var id = L(a, "branch_id");
        Branch branch;
        var created = false;
        if (id is { } existing)
        {
            branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == existing) ?? throw new ToolError($"филиал {existing} не найден");
        }
        else
        {
            if (S(a, "name") is null || S(a, "address") is null) throw new ToolError("для нового филиала нужны название и адрес");
            // Until told otherwise a new point answers the shop's phone and keeps the shop's hours.
            var settings = await db.Settings.AsNoTracking().FirstAsync();
            branch = new Branch { Lat = 41.3111, Lng = 69.2797, Phone = settings.Phone, WorkingHours = settings.WorkingHours };
            db.Branches.Add(branch);
            created = true;
        }

        if (S(a, "name") is { } name) branch.Name = Cut(name, 80)!;
        if (S(a, "address") is { } address) branch.Address = Cut(address, 200)!;
        if (S(a, "phone") is { } phone) branch.Phone = Cut(phone, 40);
        if (S(a, "working_hours") is { } hours) branch.WorkingHours = Cut(hours, 120);
        if (D(a, "lat") is { } lat && D(a, "lng") is { } lng && lat is > 37 and < 46 && lng is > 55 and < 74)
        {
            branch.Lat = Math.Round(lat, 6);
            branch.Lng = Math.Round(lng, 6);
        }
        await db.SaveChangesAsync();

        if (created)
        {
            // Like «Добавить филиал» in Магазин: the new point sells what the first one sells.
            var first = await db.Branches.Where(b => b.Id != branch.Id).OrderBy(b => b.Id).Select(b => b.Id).FirstOrDefaultAsync();
            var rows = await db.Stock.AsNoTracking().Where(s => s.BranchId == first).ToListAsync();
            db.Stock.AddRange(rows.Select(r => new StockItem { ProductId = r.ProductId, BranchId = branch.Id, Status = r.Status, Quantity = r.Quantity, UpdatedAt = DateTime.Now }));
            await db.SaveChangesAsync();
        }

        // A shop with one branch has one phone and one timetable: until the shop's own contacts are set, the main
        // branch's become them.
        var contactsToo = false;
        if (!created && await db.Branches.OrderBy(b => b.Id).Select(b => b.Id).FirstAsync() == branch.Id
            && !tenant.Store!.SetupAccepted.Contains("contacts"))
        {
            var settings = await db.Settings.FirstAsync();
            if (S(a, "phone") is not null) { settings.Phone = branch.Phone; contactsToo = true; }
            if (S(a, "working_hours") is not null) { settings.WorkingHours = branch.WorkingHours; contactsToo = true; }
            if (contactsToo)
            {
                Accept("contacts");
                await db.SaveChangesAsync();
            }
        }

        var lines = new List<string> { branch.Name };
        if (branch.Address.Length > 0) lines.Add(branch.Address);
        if (branch.Phone is { } p) lines.Add(p);
        if (branch.WorkingHours is { } h) lines.Add(h);
        if (contactsToo) lines.Add("Телефон и часы стали и контактами магазина");
        lines.Add("Проверьте точку на карте — она поставлена по адресу примерно");
        return new ToolOutcome($"{(created ? "Создан" : "Обновлён")} филиал id={branch.Id}: {string.Join(", ", lines.Take(4))}. " +
            (contactsToo ? "Телефон и часы работы сохранены и как контакты магазина (шаг contacts выполнен). " : "") +
            $"Координаты {branch.Lat}, {branch.Lng} — попросите проверить точку на карте в разделе «Магазин».",
            new AssistantCard("change", created ? "Филиал добавлен" : "Филиал обновлён", lines, "/store", "Проверить на карте"));
    }

    async Task<ToolOutcome> CreateCategories(JsonElement a)
    {
        var all = await db.Categories.ToListAsync();
        var created = new List<string>();
        var createdIds = new List<Category>();
        var skipped = new List<string>();
        foreach (var c in Items(a, "categories"))
        {
            var ru = S(c, "name_ru");
            if (ru is null) continue;
            if (all.Any(x => Same(x.Name.Get(), ru))) { skipped.Add(ru); continue; }
            int? parentId = null;
            if (S(c, "parent") is { } parentName)
                parentId = all.FirstOrDefault(x => Same(x.Name.Get(), parentName))?.Id
                    ?? throw new ToolError($"родительская категория «{parentName}» не найдена — создайте её сначала");
            var category = new Category
            {
                ParentId = parentId,
                Name = Names(ru, S(c, "name_uz")),
                Description = Names(S(c, "description_ru") ?? "", S(c, "description_uz")),
                ImageUrl = Upload(S(c, "image_url")),
                CreatedAt = DateTime.Now,
                SortOrder = all.Count,
            };
            db.Categories.Add(category);
            all.Add(category);
            created.Add(ru);
            createdIds.Add(category);
            // Children in the same call may name this one as their parent.
            await db.SaveChangesAsync();
        }
        var result = created.Count > 0 ? $"Созданы категории: {string.Join(", ", created)}." : "Новых категорий нет.";
        if (skipped.Count > 0) result += $" Уже были: {string.Join(", ", skipped)}.";
        if (created.Count > 0) result += " В карточке под ответом у каждой категории есть кнопка для фото.";
        return new ToolOutcome(result, created.Count == 0 ? null
            : new AssistantCard("categories", $"Создано категорий: {created.Count}", created, "/products/categories", "Каталог → Категории",
                CategoryIds: createdIds.Select(c => c.Id).ToList()));
    }

    async Task<ToolOutcome> CreateProducts(JsonElement a)
    {
        var categories = await db.Categories.ToListAsync();
        var existing = await db.Products.AsNoTracking().Select(p => p.Name).ToListAsync();
        var branches = await db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => new { b.Id, b.Name }).ToListAsync();
        var allBranches = branches.Select(b => b.Id).ToList();
        var sort = await db.Products.CountAsync();
        var created = new List<string>();
        var products = new List<Product>();
        var problems = new List<string>();

        var merged = new List<string>();
        foreach (var (p, ru, uz, quantity) in MergeNumbered(Items(a, "products").Take(40).ToList(), merged))
        {
            if (existing.Any(n => Same(n.Get(), ru))) { problems.Add($"«{ru}» уже есть"); continue; }
            var price = L(p, "price") ?? 0;
            if (price <= 0) { problems.Add($"«{ru}»: нет цены"); continue; }
            var old = L(p, "old_price");

            var product = new Product
            {
                Name = Names(ru, uz),
                Description = Names(S(p, "description_ru") ?? "", S(p, "description_uz")),
                Price = price,
                OldPrice = old is { } o && o > price ? o : null,
                CostPrice = Math.Max(0, L(p, "cost_price") ?? 0),
                Unit = S(p, "unit") is { } u && Units.Contains(u) ? u : "шт",
                WeightGrams = (int?)L(p, "weight_grams") is > 0 and var g ? g : null,
                Category = await CategoryByName(categories, S(p, "category")),
                IsActive = true,
                SortOrder = sort++,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
            if (Upload(S(p, "image_url")) is { } image) product.Media.Add(new ProductMedia { Url = image, Type = "image" });
            var where = BranchList(p, allBranches) ?? allBranches;
            if (where.Count == 0) { problems.Add($"«{ru}»: филиалы не найдены"); continue; }
            SetStock(product, where, quantity);
            db.Products.Add(product);
            products.Add(product);
            existing.Add(product.Name);
            var place = where.Count == allBranches.Count ? "" : " — " + string.Join(", ", branches.Where(b => where.Contains(b.Id)).Select(b => b.Name));
            created.Add($"{ru} — {Money(price)}{place}{(quantity is { } q ? $", {q} шт" : "")}");
        }
        await db.SaveChangesAsync();

        var result = created.Count > 0 ? $"Созданы товары ({created.Count}): {string.Join("; ", created)}." : "Товары не созданы.";
        if (problems.Count > 0) result += " Пропущены: " + string.Join("; ", problems) + ".";
        if (merged.Count > 0) result += " Одинаковые позиции с номерами объединены в один товар с остатком: " + string.Join("; ", merged) + ".";
        if (created.Count > 0)
            result += " Под ответом показана таблица: там администратор отмечает, в каких филиалах есть каждый товар, и " +
                      "вписывает остаток (пусто = без ограничений). Фото можно прикрепить в чате или добавить в карточке товара.";
        return new ToolOutcome(result, created.Count == 0 ? null
            : new AssistantCard("availability", $"Добавлено товаров: {created.Count}", created, "/products/stock", "Каталог → Склад",
                products.Select(x => x.Id).ToList()));
    }

    async Task<ToolOutcome> UpdateProduct(JsonElement a)
    {
        var id = L(a, "product_id") ?? 0;
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id) ?? throw new ToolError($"товар {id} не найден");
        var lines = new List<string>();
        if (S(a, "name_ru") is { } ru) { p.Name["ru"] = ru; lines.Add($"Название: {ru}"); }
        if (S(a, "name_uz") is { } uz) { p.Name["uz"] = uz; p.Name["oz"] = UzTransliterator.ToCyrillic(uz); }
        if (S(a, "description_ru") is { } dru) { p.Description["ru"] = dru; lines.Add("Описание обновлено"); }
        if (S(a, "description_uz") is { } duz) { p.Description["uz"] = duz; p.Description["oz"] = UzTransliterator.ToCyrillic(duz); }
        if (L(a, "price") is { } price)
        {
            if (price <= 0) throw new ToolError("цена должна быть больше нуля");
            p.Price = price;
            if (p.OldPrice is { } o && o <= price) p.OldPrice = null;
            lines.Add($"Цена: {Money(price)}");
        }
        if (L(a, "old_price") is { } old)
        {
            if (old > 0 && old <= p.Price) throw new ToolError("старая цена должна быть выше текущей");
            p.OldPrice = old > 0 ? old : null;
            lines.Add(old > 0 ? $"Старая цена: {Money(old)}" : "Старая цена убрана");
        }
        if (S(a, "category") is { } cat)
        {
            p.Category = await CategoryByName(await db.Categories.ToListAsync(), cat);
            lines.Add($"Категория: {cat}");
        }
        if (Upload(S(a, "image_url")) is { } image)
        {
            p.Media.RemoveAll(m => m.Url == image);
            p.Media.Insert(0, new ProductMedia { Url = image, Type = "image" });
            lines.Add("Фото добавлено");
        }
        if (a.TryGetProperty("is_active", out var active) && active.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            p.IsActive = active.GetBoolean();
            lines.Add(p.IsActive ? "Показан в магазине" : "Скрыт из магазина");
        }
        var allBranches = await db.Branches.Select(b => b.Id).ToListAsync();
        if (BranchList(a, allBranches) is { Count: > 0 } where)
        {
            await db.Entry(p).Collection(x => x.Stock).LoadAsync();
            SetStock(p, where, (int?)L(a, "quantity"), keepQuantity: L(a, "quantity") is null);
            lines.Add($"Филиалы: {string.Join(", ", await db.Branches.Where(b => where.Contains(b.Id)).Select(b => b.Name).ToListAsync())}");
        }
        else if (L(a, "quantity") is { } qty)
        {
            await db.Entry(p).Collection(x => x.Stock).LoadAsync();
            SetStock(p, p.Stock.Select(s => s.BranchId).ToList(), (int)qty);
            lines.Add($"Остаток: {qty} в каждом филиале");
        }
        if (lines.Count == 0) return new ToolOutcome("Ничего не изменено.");
        p.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return new ToolOutcome($"Товар {p.Name.Get()} (id={p.Id}) обновлён: {string.Join("; ", lines)}.",
            new AssistantCard("change", $"Товар «{p.Name.Get()}» обновлён", lines, $"/products/items/{p.Id}", "Открыть товар"));
    }

    async Task<ToolOutcome> DeleteProducts(JsonElement a)
    {
        var ids = Items(a, "product_ids").Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32()).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
        if (products.Count == 0) return new ToolOutcome("Товары с такими id не найдены.");
        db.Products.RemoveRange(products);
        foreach (var d in await db.Discounts.ToListAsync()) d.ProductIds.RemoveAll(ids.Contains);
        await db.SaveChangesAsync();
        var names = products.Select(p => p.Name.Get()).ToList();
        return new ToolOutcome($"Удалены товары: {string.Join(", ", names)}.",
            new AssistantCard("change", $"Удалено товаров: {names.Count}", names, "/products/items", "Каталог → Товары"));
    }

    async Task<ToolOutcome> DeleteCategory(JsonElement a)
    {
        var id = L(a, "category_id") ?? 0;
        var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id) ?? throw new ToolError($"категория {id} не найдена");
        if (await db.Categories.AnyAsync(x => x.ParentId == id)) throw new ToolError("у категории есть подкатегории — сначала удалите или перенесите их");
        if (await db.Products.CountAsync(p => p.CategoryId == id) is var n and > 0) throw new ToolError($"в категории {n} товаров — перенесите или удалите их");
        db.Categories.Remove(c);
        await db.SaveChangesAsync();
        return new ToolOutcome($"Категория «{c.Name.Get()}» удалена.",
            new AssistantCard("change", $"Категория «{c.Name.Get()}» удалена", null, "/products/categories", "Каталог → Категории"));
    }

    async Task<ToolOutcome> SetBonus(JsonElement a)
    {
        var settings = await db.Settings.FirstAsync();
        settings.BonusEnabled = a.GetProperty("enabled").GetBoolean();
        if (L(a, "spend_per_point") is { } spend)
        {
            if (spend < 1) throw new ToolError("сумма за 1 балл должна быть больше нуля");
            settings.SpendPerPoint = spend;
        }
        await db.SaveChangesAsync();
        var line = settings.BonusEnabled
            ? $"1 балл за каждые {Money(settings.SpendPerPoint)} покупки, 1 балл = 1 сум"
            : "Бонусная программа выключена";
        return new ToolOutcome(line, new AssistantCard("change", settings.BonusEnabled ? "Бонусы включены" : "Бонусы выключены",
            [line], "/customers", "Клиенты → Настройки баллов"));
    }

    async Task<ToolOutcome> CreatePromo(JsonElement a)
    {
        var code = (S(a, "code") ?? "").ToUpperInvariant();
        if (!PromoFormat().IsMatch(code)) throw new ToolError("код: 3–20 латинских букв, цифр, «-» или «_»");
        if (await db.PromoCodes.AnyAsync(p => p.Code == code)) throw new ToolError($"промокод {code} уже существует");
        var percent = S(a, "type") != "fixed";
        var value = L(a, "value") ?? 0;
        if (value <= 0 || (percent && value > 100)) throw new ToolError(percent ? "скидка — от 1 до 100%" : "сумма скидки должна быть больше нуля");
        var min = L(a, "min_order_amount") is > 0 and var m ? m : (long?)null;
        if (!percent && min is { } mm && value >= mm) throw new ToolError("фиксированная скидка должна быть меньше минимальной суммы заказа");
        var days = Math.Clamp((int)(L(a, "days_valid") ?? 30), 1, 3650);
        int? limit = (int?)L(a, "usage_limit") is > 0 and var l ? l : null;

        db.PromoCodes.Add(new PromoCode
        {
            Code = code, Type = percent ? DiscountType.Percent : DiscountType.Fixed, Value = value,
            MinOrderAmount = min, UsageLimit = limit, FirstOrderOnly = a.GetProperty("first_order_only").GetBoolean(),
            StartsAt = DateTime.Now, EndsAt = DateTime.Today.AddDays(days + 1).AddSeconds(-1), IsActive = true, CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();
        var lines = new List<string>
        {
            percent ? $"Скидка {value}%" : $"Скидка {Money(value)}",
            $"Действует до {DateTime.Today.AddDays(days):dd.MM.yyyy}",
        };
        if (min is { } minOrder) lines.Add($"От {Money(minOrder)}");
        if (a.GetProperty("first_order_only").GetBoolean()) lines.Add("Только на первый заказ");
        return new ToolOutcome($"Промокод {code} создан: {string.Join(", ", lines)}.",
            new AssistantCard("change", $"Промокод {code}", lines, "/marketing/promocodes", "Маркетинг → Промокоды"));
    }

    ToolOutcome RequestBot()
    {
        var store = tenant.Store!;
        if (store.BotUsername is { Length: > 0 } username)
            return new ToolOutcome($"Бот уже подключён: @{username}.",
                new AssistantCard("link", $"Бот @{username} подключён", null, "/platforms/telegram", "Платформы → Telegram-бот"));
        return new ToolOutcome("Администратору показана защищённая форма для токена. Он вставит токен туда (не в чат) " +
                               "и нажмёт «Подключить»; после этого в чат придёт сообщение о подключении.",
            new AssistantCard("telegram", "Подключение Telegram-бота"));
    }

    async Task<ToolOutcome> AcceptStep(JsonElement a)
    {
        var step = S(a, "step") ?? "";
        if (!AcceptableSteps.Contains(step)) throw new ToolError("неизвестный шаг");
        Accept(step);
        await db.SaveChangesAsync();
        return new ToolOutcome($"Шаг «{step}» отмечен как выполненный.");
    }

    static ToolOutcome OpenSection(JsonElement a)
    {
        var key = S(a, "section") ?? "";
        if (!Sections.TryGetValue(key, out var section)) return new ToolOutcome("Ошибка: неизвестный раздел");
        return new ToolOutcome($"Показана кнопка «{S(a, "label")}» → {section.Label}.",
            new AssistantCard("link", S(a, "label") ?? section.Label, null, section.Path, section.Label));
    }

    async Task<ToolOutcome> Finish()
    {
        var store = tenant.Store!;
        store.OnboardedAt ??= DateTime.Now;
        await db.SaveChangesAsync();
        return new ToolOutcome("Настройка отмечена как завершённая. Администратору показаны кнопки «В админ-панель» и «Открыть магазин».",
            new AssistantCard("finish", "Магазин готов к работе", null, "/dashboard", "Перейти в админ-панель"));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// «Роза 1», «Роза 2», «Роза 3» at one price are three pieces of one product, not three products: they become «Роза»
    /// with the pieces as its stock. Everything else passes through untouched.
    /// </summary>
    static IEnumerable<(JsonElement P, string Ru, string? Uz, int? Quantity)> MergeNumbered(List<JsonElement> items, List<string> merged)
    {
        var named = items.Where(p => S(p, "name_ru") is not null).ToList();
        foreach (var group in named.GroupBy(p => (Base: Unnumbered(S(p, "name_ru")!).ToLowerInvariant(), Price: L(p, "price"))))
        {
            var list = group.ToList();
            // Only a plain 1, 2, 3… count — «Капучино 300» and «Капучино 400» are different sizes, not pieces.
            var numbers = list.Select(p => Numbered().Match(S(p, "name_ru")!)).Select(m => m.Success && int.TryParse(m.Value.Trim(' ', '№', '#', '-'), out var n) ? n : 0).Order().ToList();
            if (list.Count > 1 && numbers.SequenceEqual(Enumerable.Range(1, list.Count)))
            {
                var total = list.Sum(p => (int?)L(p, "quantity") ?? 1);
                var ru = Unnumbered(S(list[0], "name_ru")!);
                merged.Add($"{ru} ×{total}");
                yield return (list[0], ru, S(list[0], "name_uz") is { } uz ? Unnumbered(uz) : null, total);
            }
            else
                foreach (var p in list) yield return (p, S(p, "name_ru")!, S(p, "name_uz"), (int?)L(p, "quantity"));
        }
    }

    static string Unnumbered(string name) => Numbered().Replace(name, "").Trim();

    [GeneratedRegex(@"\s*(?:№|#|-)?\s*\d+\s*$")]
    private static partial Regex Numbered();

    /// <summary>The branch ids given in <paramref name="name"/> that really are this store's; null when none were given.</summary>
    static List<int>? BranchList(JsonElement a, List<int> storeBranches, string name = "branch_ids")
    {
        if (!a.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) return null;
        return v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => x.GetInt32())
            .Where(storeBranches.Contains).Distinct().ToList();
    }

    /// <summary>
    /// One stock row per branch the product is sold in (no row = not sold there), like the product form does.
    /// A quantity makes the stock limited; none means unlimited, or — with <paramref name="keepQuantity"/> — as it was.
    /// </summary>
    public static void SetStock(Product product, List<int> branchIds, int? quantity, bool keepQuantity = false)
    {
        product.Stock.RemoveAll(s => !branchIds.Contains(s.BranchId));
        foreach (var b in branchIds)
        {
            var row = product.Stock.FirstOrDefault(s => s.BranchId == b);
            if (row is null) product.Stock.Add(row = new StockItem { BranchId = b, Status = StockStatus.Unlimited });
            else if (keepQuantity) continue;
            if (quantity is { } q)
            {
                row.Status = q > 0 ? StockStatus.Limited : StockStatus.OutOfStock;
                row.Quantity = Math.Max(0, q);
            }
            else if (!keepQuantity)
            {
                row.Status = StockStatus.Unlimited;
                row.Quantity = 0;
            }
            row.UpdatedAt = DateTime.Now;
        }
    }

    void Accept(string step)
    {
        var store = tenant.Store!;
        var set = store.SetupAccepted.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (set.Add(step)) store.SetupAccepted = string.Join(',', set);
    }

    async Task<Category?> CategoryByName(List<Category> categories, string? name)
    {
        if (name is null) return null;
        var found = categories.FirstOrDefault(c => Same(c.Name.Get(), name));
        if (found is not null) return found;
        found = new Category { Name = Localized.Of(name), CreatedAt = DateTime.Now, SortOrder = categories.Count };
        db.Categories.Add(found);
        categories.Add(found);
        await db.SaveChangesAsync();
        return found;
    }

    /// <summary>Russian + Uzbek Latin from the model; Uzbek Cyrillic is an exact script conversion.</summary>
    static Localized Names(string ru, string? uz)
    {
        var l = new Localized();
        if (!string.IsNullOrWhiteSpace(ru)) l["ru"] = ru.Trim();
        if (!string.IsNullOrWhiteSpace(uz))
        {
            l["uz"] = uz.Trim();
            l["oz"] = UzTransliterator.ToCyrillic(uz.Trim());
        }
        return l;
    }

    /// <summary>Only files the administrator uploaded to this app may become product or category images.</summary>
    static string? Upload(string? url) =>
        url is not null && url.StartsWith("/uploads/", StringComparison.Ordinal) && !url.Contains("..") ? url : null;

    static IEnumerable<JsonElement> Items(JsonElement a, string name) =>
        a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToList() : [];

    static string? S(JsonElement a, string name) =>
        a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { } s && s.Trim().Length > 0 ? s.Trim() : null;

    static long? L(JsonElement a, string name) =>
        a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? (long)Math.Round(v.GetDouble()) : null;

    static double? D(JsonElement a, string name) =>
        a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    static string? Cut(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim()[..Math.Min(v.Trim().Length, max)];

    static string Preview(string text)
    {
        var flat = Regex.Replace(text.Replace("## ", ""), @"\s+", " ").Trim();
        return flat.Length <= 90 ? flat : flat[..90].TrimEnd() + "…";
    }

    public static string Money(long v) => v.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ") + " сум";

    [GeneratedRegex("^[A-Z0-9_-]{3,20}$")]
    private static partial Regex PromoFormat();
}
