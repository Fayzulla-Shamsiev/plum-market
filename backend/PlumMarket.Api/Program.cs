using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PlumMarket.Api.Data;
using PlumMarket.Api.Middleware;
using PlumMarket.Api.Services;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=plum.db";
// The database may live on a mounted disk (ConnectionStrings__Default=Data Source=/var/data/plum.db) so that a
// deploy doesn't take the data with it; SQLite needs that folder to exist.
if (new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource is { } path
    && Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } folder)
    Directory.CreateDirectory(folder);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(nameof(TelegramBotApi), c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<TelegramBotApi>();
builder.Services.AddScoped<StoreLinks>();
builder.Services.AddScoped<StoreContext>();
builder.Services.AddScoped<AdminAuth>();
builder.Services.AddScoped<OrderWorkflow>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<MarketingService>();
builder.Services.AddScoped<ShopAuth>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddSingleton<AiContentService>();

var app = builder.Build();

// Behind Render's proxy the app is reached over https; without this the links we hand to Telegram would say
// http, and Telegram only opens a Mini App over https.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    KnownNetworks = { }, KnownProxies = { },
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // No migrations in the prototype: a DB from an older model version is dropped and created empty.
    var outdated = db.Database.CanConnect() && db.Database.SqlQueryRaw<int>("SELECT user_version AS Value FROM pragma_user_version").AsEnumerable().First() != AppDbContext.SchemaVersion;
    if (args.Contains("--reset") || outdated) db.Database.EnsureDeleted();
    if (db.Database.EnsureCreated())
        db.Database.ExecuteSqlRaw($"PRAGMA user_version = {AppDbContext.SchemaVersion}");

    // The demo store ("Plum Bakery" + its administrator and customer) is created when it isn't in the database
    // yet, so a fresh deploy always has something to show. An existing one is left exactly as it is.
    if (builder.Configuration.GetValue("Demo:Enabled", true) && !args.Contains("--no-demo"))
    {
        var tenant = scope.ServiceProvider.GetRequiredService<StoreContext>();
        if (DemoData.Ensure(db, tenant))
            app.Logger.LogInformation("Demo store seeded: /shop/{Slug}, admin {Phone} / {Password}",
                DemoData.StoreSlug, DemoData.AdminPhone, DemoData.AdminPassword);
    }
}

// The built Vue app (frontend `npm run build`) is served from wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

// Merchant uploads (product photos/videos, category images, chat attachments). Kept outside wwwroot
// because the frontend build empties that folder.
var uploads = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploads);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(uploads), RequestPath = "/uploads" });

// Every API request is tied to one store before it reaches a controller.
app.UseMiddleware<StoreMiddleware>();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
