using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Options binding
builder.Services.Configure<CryptoTgShop.Options.TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<CryptoTgShop.Options.BotTextOptions>(builder.Configuration.GetSection("BotText"));
builder.Services.Configure<CryptoTgShop.Options.AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.Configure<CryptoTgShop.Options.CloudinaryOptions>(builder.Configuration.GetSection("Cloudinary"));
builder.Services.Configure<CryptoTgShop.Options.NowPaymentsOptions>(builder.Configuration.GetSection("NowPayments"));

// Telegram services
builder.Services.AddHttpClient<CryptoTgShop.Services.Interfaces.ITelegramApiClient, CryptoTgShop.Services.TelegramApiClient>();
builder.Services.AddScoped<CryptoTgShop.Services.Interfaces.IUserMessageHandler, CryptoTgShop.Services.UserMessageHandler>();
builder.Services.AddSingleton<CryptoTgShop.Services.IAdminWizardStore, CryptoTgShop.Services.AdminWizardStore>();
builder.Services.AddScoped<CryptoTgShop.Services.Interfaces.IImageStorage, CryptoTgShop.Services.CloudinaryImageStorage>();

// NowPayments services
builder.Services.AddScoped<CryptoTgShop.Services.Interfaces.INowPaymentsApiClient, CryptoTgShop.Services.NowPaymentsApiClient>();

// EF Core
builder.Services.AddDbContext<CryptoTgShop.Data.AppDbContext>(options =>
{
    var baseConnectionString = builder.Configuration.GetConnectionString("Postgres") ?? string.Empty;
    
    // Convert URI-style connection string to traditional format and add SSL parameters
    string fullConnectionString = baseConnectionString;
    if (!string.IsNullOrWhiteSpace(baseConnectionString))
    {
        // Handle URI format (postgresql://user:pass@host:port/dbname)
        if (baseConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
            baseConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(baseConnectionString);
                var connStringBuilder = new System.Text.StringBuilder();
                connStringBuilder.Append($"Host={uri.Host}");
                
                if (uri.Port != -1)
                {
                    connStringBuilder.Append($";Port={uri.Port}");
                }
                
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var userInfo = uri.UserInfo.Split(':');
                    if (userInfo.Length >= 1)
                    {
                        connStringBuilder.Append($";Username={Uri.UnescapeDataString(userInfo[0])}");
                    }
                    if (userInfo.Length >= 2)
                    {
                        connStringBuilder.Append($";Password={Uri.UnescapeDataString(userInfo[1])}");
                    }
                }
                
                var dbName = uri.AbsolutePath.TrimStart('/');
                if (!string.IsNullOrEmpty(dbName))
                {
                    connStringBuilder.Append($";Database={dbName}");
                }
                
                // Parse existing query parameters manually
                var query = uri.Query.TrimStart('?');
                if (!string.IsNullOrEmpty(query))
                {
                    var queryPairs = query.Split('&');
                    foreach (var pair in queryPairs)
                    {
                        var parts = pair.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].ToLower();
                            var value = Uri.UnescapeDataString(parts[1]);
                            
                            // Skip SSL-related parameters as we'll set them explicitly
                            if (key != "sslmode" && key != "ssl_mode" && key != "channel_binding")
                            {
                                connStringBuilder.Append($";{parts[0]}={value}");
                            }
                        }
                    }
                }
                
                // Add SSL parameters
                connStringBuilder.Append(";SSL Mode=Require");
                connStringBuilder.Append(";Channel Binding=require");
                
                fullConnectionString = connStringBuilder.ToString();
            }
            catch (UriFormatException)
            {
                // If URI parsing fails, treat as traditional connection string
                // Check if it already has query parameters
                var separator = baseConnectionString.Contains('?') ? "&" : "?";
                fullConnectionString = $"{baseConnectionString}{separator}sslmode=require&channel_binding=require";
            }
        }
        else
        {
            // Traditional connection string format - append SSL parameters
            var hasParams = baseConnectionString.Contains(';') || baseConnectionString.Contains('=');
            var separator = hasParams ? ";" : "";
            fullConnectionString = $"{baseConnectionString}{separator}SSL Mode=Require;Channel Binding=require";
        }
    }
    
    options.UseNpgsql(fullConnectionString);
});

var app = builder.Build();

// Configure the HTTP request pipeline.

// Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CryptoTgShop.Data.AppDbContext>();
    dbContext.Database.Migrate();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
