using ErpApi.Core.Constants;
using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class FiscalPeriodConfiguration : IEntityTypeConfiguration<FiscalPeriod>
{
    public void Configure(EntityTypeBuilder<FiscalPeriod> builder)
    {
        // المساواة مسموحة في مدى التاريخ لأن فترة التسويات النمطية مدتها يوم واحد
        builder.ToTable("FiscalPeriods", t =>
        {
            t.HasCheckConstraint("CK_FiscalPeriod_Number", "[PeriodNumber] >= 1 AND [PeriodNumber] <= 14");
            t.HasCheckConstraint("CK_FiscalPeriod_Type", "[PeriodType] IN (1, 2)");
            t.HasCheckConstraint("CK_FiscalPeriod_DateOrder", "[EndDate] >= [StartDate]");
            t.HasTrigger("TR_FiscalPeriod_WithinYearRange");
            t.HasTrigger("TR_FiscalPeriod_PreventOverlap");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PeriodNumber).IsRequired();
        builder.Property(e => e.PeriodType).IsRequired().HasDefaultValue(FiscalPeriodType.Regular);
        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.Property(e => e.StartDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.EndDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.IsClosed).HasDefaultValue(false);

        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(e => new { e.FiscalYearId, e.PeriodNumber })
            .IsUnique()
            .HasDatabaseName("UQ_FiscalPeriod_FiscalYearId_PeriodNumber");

        builder.HasIndex(e => new { e.StartDate, e.EndDate })
            .HasDatabaseName("IX_FiscalPeriod_StartDate_EndDate");

        builder.HasOne(e => e.FiscalYear)
            .WithMany()
            .HasForeignKey(e => e.FiscalYearId)
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
