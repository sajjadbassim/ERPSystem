using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries", t =>
        {
            t.HasCheckConstraint("CK_JournalEntry_SourceModule", "[SourceModule] IN (1, 2, 3, 4)");
            t.HasCheckConstraint("CK_JournalEntry_NotSelfReversal",
                "[ReversalOfJournalEntryId] IS NULL OR [ReversalOfJournalEntryId] <> [Id]");

            t.HasCheckConstraint("CK_JournalEntry_DocumentDateNotAfterPostingDate",
                "[DocumentDate] <= [PostingDate]");
        });

        builder.HasKey(e => e.Id);

        // الإجراء المخزَّن يمرّر المفتاح صراحة في متغير قبل الإدراج، ليربط به السطور في نفس المعاملة.
        // الافتراضي هنا احتياط لا آلية معتمدة: يعمل فقط لو أُدرج صف بلا مفتاح
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(e => e.TransactionId).IsRequired();
        builder.Property(e => e.DocumentNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.PostingDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.DocumentDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.SourceModule).IsRequired();

        builder.HasIndex(e => new { e.BranchId, e.DocumentNumber })
            .IsUnique()
            .HasDatabaseName("UQ_JournalEntry_BranchId_DocumentNumber");

        // قيد واحد لا يُعكَس مرتين
        builder.HasIndex(e => e.ReversalOfJournalEntryId)
            .IsUnique()
            .HasDatabaseName("UQ_JournalEntry_ReversalOf")
            .HasFilter("[ReversalOfJournalEntryId] IS NOT NULL");

        builder.HasIndex(e => e.TransactionId).HasDatabaseName("IX_JournalEntry_TransactionId");
        builder.HasIndex(e => e.PostingDate).HasDatabaseName("IX_JournalEntry_PostingDate");
        builder.HasIndex(e => e.FiscalPeriodId).HasDatabaseName("IX_JournalEntry_FiscalPeriodId");

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.FiscalPeriod)
            .WithMany()
            .HasForeignKey(e => e.FiscalPeriodId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.ReversalOfJournalEntry)
            .WithMany()
            .HasForeignKey(e => e.ReversalOfJournalEntryId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(e => e.Lines)
            .WithOne(l => l.JournalEntry)
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.NoAction);

        // المفتاح يمنع محو فاعل تدقيقي له أثر مالي. لا UpdatedByUserId هنا: المرحَّل لا يُعدَّل
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
