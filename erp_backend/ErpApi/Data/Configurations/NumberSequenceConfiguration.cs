using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("NumberSequences", t =>
        {
            t.HasCheckConstraint("CK_NumberSequence_CurrentValue", "[CurrentValue] >= 0");
            t.HasCheckConstraint("CK_NumberSequence_Padding", "[PaddingLength] >= 1 AND [PaddingLength] <= 18");

            // ترقيم القيد على مستوى الفرع، ليبقى UQ_JournalEntry_BranchId_DocumentNumber فحصاً دقيقاً
            t.HasCheckConstraint("CK_NumberSequence_JournalScope",
                "[DocumentType] <> 1 OR [BranchId] IS NOT NULL");

            t.HasTrigger("TR_NumberSequence_PreventDelete");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DocumentType).IsRequired();
        builder.Property(e => e.Prefix).HasMaxLength(10).IsRequired();
        builder.Property(e => e.CurrentValue).HasDefaultValue(0L);
        builder.Property(e => e.PaddingLength).HasDefaultValue((byte)6);

        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2");

        // إلغاء المرشِّح الذي يضيفه EF تلقائياً للأعمدة الـ nullable: مطلوب أن يعامل SQL Server
        // NULL = NULL هنا، وإلا صار ممكناً وجود عدّادين لنفس النطاق ومنه رقمان متطابقان
        builder.HasIndex(e => new { e.CompanyId, e.BranchId, e.DocumentType, e.FiscalYearId })
            .IsUnique()
            .HasDatabaseName("UQ_NumberSequence_Scope")
            .HasFilter(null);

        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.NoAction);

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
    }
}
