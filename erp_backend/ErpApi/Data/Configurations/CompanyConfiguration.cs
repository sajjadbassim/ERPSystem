using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        // الحد الأعلى 1 يمنع ابتلاع فروق تحويل حقيقية تحت اسم التقريب (R-GL-06)
        builder.ToTable("Companies", t =>
        {
            t.HasCheckConstraint("CK_Company_FxRoundingTolerance",
                "[FxRoundingToleranceBase] >= 0 AND [FxRoundingToleranceBase] <= 1");

            // إعلان إلزامي: EF Core يعطّل OUTPUT على الجداول ذات التريجرات، ونسيانه يفشل SaveChanges
            t.HasTrigger("TR_Company_ProtectBaseCurrencyLock");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.IsBaseCurrencyLocked).HasDefaultValue(false);

        // الافتراضي 1.0000 شبكة أمان بقيمة الدينار، لا بديل عن الحساب.
        // القيمة الدقيقة = 10^(−DecimalPlaces) لعملة الأساس، تُحسب في CompanyService.
        // ملاحظة: الافتراضي متساهل لعملة ذات خانتين (صوابها 0.0100)، فهو يحمي من الصفر لا من الإهمال
        builder.Property(e => e.FxRoundingToleranceBase)
            .HasPrecision(19, 4)
            .HasDefaultValue(1.0000m)
            .IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasDatabaseName("UQ_Company_Code")
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.BaseCurrency)
            .WithMany()
            .HasForeignKey(e => e.BaseCurrencyId)
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
