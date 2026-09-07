using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class JournalLineConfiguration : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("JournalLines", t =>
        {
            // السطر إما مدين أو دائن، والجانب نفسه في العملتين، ولا سطر صفري
            t.HasCheckConstraint("CK_JournalLine_DebitXorCredit",
                "([DebitFC] > 0 AND [CreditFC] = 0 AND [DebitBase] > 0 AND [CreditBase] = 0) OR " +
                "([CreditFC] > 0 AND [DebitFC] = 0 AND [CreditBase] > 0 AND [DebitBase] = 0)");

            t.HasCheckConstraint("CK_JournalLine_ExchangeRatePositive", "[ExchangeRate] > 0");

            // التسامح بمقدار أصغر وحدة تخزين يمتص اختلاف دقة الضرب بين SQL Server وdecimal في NET،
            // ولا يمرّ منه خطأ سعر أو عملة مقلوبة لأن أثرهما أكبر بمراتب
            t.HasCheckConstraint("CK_JournalLine_BaseEqualsConverted",
                "ABS([DebitBase] - ROUND([DebitFC] * [ExchangeRate], 4)) <= 0.0001 AND " +
                "ABS([CreditBase] - ROUND([CreditFC] * [ExchangeRate], 4)) <= 0.0001");
        });

        builder.HasKey(e => e.Id);

        // مفاتيح السطور تصل ضمن الـ TVP. الافتراضي احتياط لا آلية معتمدة
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(e => e.LineNumber).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);

        builder.Property(e => e.ExchangeRate).HasPrecision(28, 12).IsRequired();
        builder.Property(e => e.ExchangeRateDate).HasColumnType("date").IsRequired();
        builder.Property(e => e.DebitFC).HasPrecision(19, 4).IsRequired();
        builder.Property(e => e.CreditFC).HasPrecision(19, 4).IsRequired();
        builder.Property(e => e.DebitBase).HasPrecision(19, 4).IsRequired();
        builder.Property(e => e.CreditBase).HasPrecision(19, 4).IsRequired();

        builder.HasIndex(e => new { e.JournalEntryId, e.LineNumber })
            .IsUnique()
            .HasDatabaseName("UQ_JournalLine_JournalEntryId_LineNumber");

        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_JournalLine_AccountId");

        builder.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
