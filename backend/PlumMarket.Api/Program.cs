using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PlumMarket.Api.Data;
using PlumMarket.Api.Services;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=plum.db"));
builder.Services.AddScoped<OrderWorkflow>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<MarketingService>();
builder.Services.AddScoped<ShopAuth>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddSingleton<AiContentService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // No migrations in the prototype: a DB from an older model version is dropped and re-seeded.
    var outdated = db.Database.CanConnect() && db.Database.SqlQueryRaw<int>("SELECT user_version AS Value FROM pragma_user_version").AsEnumerable().First() != AppDbContext.SchemaVersion;
    if (args.Contains("--reset") || outdated) db.Database.EnsureDeleted();
    if (db.Database.EnsureCreated())
        db.Database.ExecuteSqlRaw($"PRAGMA user_version = {AppDbContext.SchemaVersion}");
    Seeder.Seed(db);
}

// The built Vue app (frontend `npm run build`) is served from wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

// Merchant uploads (product photos/videos, category images, chat attachments). Kept outside wwwroot
// because the frontend build empties that folder.
var uploads = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploads);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(uploads), RequestPath = "/uploads" });

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
