using CryptoTgShop.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTgShop.Data;

public sealed class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

	public DbSet<DataRecord> DataRecords => Set<DataRecord>();

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

		base.OnModelCreating(modelBuilder);
	}
}


