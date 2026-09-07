using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class FxRateConfiguration : IEntityTypeConfiguration<FxRate>
{
    public void Configure(EntityTypeBuilder<FxRate> builder)
    {
        builder.ToTable("FxRates", t =>
        {
            t.HasCheckConstraint("CK_FxRate_DifferentCurrencies", "[FromCurrencyId] <> [ToCurrencyId]");
            t.HasCheckConstraint("CK_FxRate_RatePositive", "[Rate] > 0");
            t.HasCheckConstraint("CK_FxRate_RateSource", "[RateSource] IN (1, 2, 3)");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Rate).HasPrecision(28, 12).IsRequired();
        builder.Property(e => e.RateDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.RateSource).IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId, e.RateDate, e.RateSource })
            .IsUnique()
            .HasDatabaseName("UQ_FxRate_From_To_Date_Source")
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId, e.RateDate })
            .HasDatabaseName("IX_FxRate_From_To_Date")
            .IsDescending(false, false, true);

        builder.HasOne(e => e.FromCurrency)
            .WithMany()
            .HasForeignKey(e => e.FromCurrencyId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.ToCurrency)
            .WithMany()
            .HasForeignKey(e => e.ToCurrencyId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
