using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Options binding
builder.Services.Configure<CryptoTgShop.Options.TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<CryptoTgShop.Options.BotTextOptions>(builder.Configuration.GetSection("BotText"));
builder.Services.Configure<CryptoTgShop.Options.AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.Configure<CryptoTgShop.Options.CloudinaryOptions>(builder.Configuration.GetSection("Cloudinary"));

// Telegram services
builder.Services.AddHttpClient<CryptoTgShop.Services.Interfaces.ITelegramApiClient, CryptoTgShop.Services.TelegramApiClient>();
builder.Services.AddScoped<CryptoTgShop.Services.Interfaces.IUserMessageHandler, CryptoTgShop.Services.UserMessageHandler>();
builder.Services.AddSingleton<CryptoTgShop.Services.IAdminWizardStore, CryptoTgShop.Services.AdminWizardStore>();
builder.Services.AddScoped<CryptoTgShop.Services.Interfaces.IImageStorage, CryptoTgShop.Services.CloudinaryImageStorage>();

// EF Core
builder.Services.AddDbContext<CryptoTgShop.Data.AppDbContext>(options =>
{
    var baseConnectionString = builder.Configuration.GetConnectionString("Postgres") ?? string.Empty;
    
    // Append SSL parameters if base connection string is provided
    string fullConnectionString = baseConnectionString;
    if (!string.IsNullOrWhiteSpace(baseConnectionString))
    {
        fullConnectionString = $"{baseConnectionString}?sslmode=require&channel_binding=require";
    }
    
    options.UseNpgsql(fullConnectionString);
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
