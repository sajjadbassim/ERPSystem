using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        // الحد الأعلى 4 لا 6، مطابقةً لدقة FxRoundingToleranceBase وهي HasPrecision(19, 4).
        // الحد يُشتق من الخانات بالصيغة 10^(−DecimalPlaces) (R-AMT-07-a)، فعملة بخمس خانات
        // تُنتج 0.00001 ويخزّنه عمود (19,4) صفراً — أي تسامح صفري صامت لا القيمة المقصودة.
        // السقف هنا يجعل ما لا يمكن تمثيله مستحيل الإدخال بدل أن يمرّ ويُقطع بلا إعلان
        builder.ToTable("Currencies", t =>
            t.HasCheckConstraint("CK_Currency_DecimalPlaces",
                "[DecimalPlaces] >= 0 AND [DecimalPlaces] <= 4"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Symbol).HasMaxLength(10);
        builder.Property(e => e.DecimalPlaces).IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasDatabaseName("UQ_Currency_Code")
            .HasFilter("[IsDeleted] = 0");

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
