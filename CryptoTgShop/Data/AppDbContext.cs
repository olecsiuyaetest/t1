using CryptoTgShop.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTgShop.Data;

public sealed class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

	public DbSet<DataRecord> DataRecords => Set<DataRecord>();
	public DbSet<Payment> Payments => Set<Payment>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<DataRecord>(e =>
		{
			e.ToTable("data_records");
			e.HasKey(x => x.Id);
			e.Property(x => x.Id).ValueGeneratedOnAdd();
			e.Property(x => x.Message).IsRequired();
			e.Property(x => x.Type).IsRequired();
			e.Property(x => x.ImageUrl).IsRequired();
			e.Property(x => x.IsUsed).HasDefaultValue(false);
			e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
		});

		modelBuilder.Entity<Payment>(e =>
		{
			e.ToTable("payments");
			e.HasKey(x => x.Id);
			e.Property(x => x.Id).ValueGeneratedOnAdd();
			e.Property(x => x.TelegramChatId).IsRequired();
			e.Property(x => x.Category).IsRequired();
			e.Property(x => x.PaymentId).IsRequired();
			e.Property(x => x.PaymentUrl).IsRequired();
			e.Property(x => x.PriceAmount).IsRequired().HasPrecision(18, 8);
			e.Property(x => x.PriceCurrency).IsRequired();
			e.Property(x => x.PayCurrency).IsRequired();
			e.Property(x => x.OrderId).IsRequired();
			e.Property(x => x.Status).IsRequired();
			e.Property(x => x.CreatedAtUtc).HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
			e.HasIndex(x => x.PaymentId).IsUnique();
			e.HasIndex(x => x.OrderId).IsUnique();
		});

		base.OnModelCreating(modelBuilder);
	}
}


