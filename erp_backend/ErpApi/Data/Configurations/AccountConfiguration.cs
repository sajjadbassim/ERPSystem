using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts", t =>
        {
            t.HasCheckConstraint("CK_Account_Type", "[AccountType] >= 1 AND [AccountType] <= 5");
            t.HasCheckConstraint("CK_Account_NormalBalance", "[NormalBalance] IN (0, 1)");

            // الأصول والمصروفات طبيعتها مدينة، والخصوم وحقوق الملكية والإيرادات دائنة
            t.HasCheckConstraint("CK_Account_NormalBalanceMatchesType",
                "([AccountType] IN (1, 5) AND [NormalBalance] = 0) OR ([AccountType] IN (2, 3, 4) AND [NormalBalance] = 1)");

            t.HasCheckConstraint("CK_Account_SystemRole",
                "[SystemAccountRole] IS NULL OR ([SystemAccountRole] >= 1 AND [SystemAccountRole] <= 5)");

            // حساب دور نظامي غير قابل للترحيل يعطّل كل قيد يظهر فيه باقي تقريب
            t.HasCheckConstraint("CK_Account_SystemRolePostable",
                "[SystemAccountRole] IS NULL OR [IsPostable] = 1");

            t.HasTrigger("TR_Account_ValidateConfiguration");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.AccountType).IsRequired();
        builder.Property(e => e.NormalBalance).IsRequired();
        builder.Property(e => e.IsPostable).HasDefaultValue(true);

        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2");

        builder.HasIndex(e => new { e.CompanyId, e.Code })
            .IsUnique()
            .HasDatabaseName("UQ_Account_CompanyId_Code")
            .HasFilter("[IsDeleted] = 0");

        // حساب واحد لكل دور نظامي في كل شركة، ليجده المحرك بلا لبس
        builder.HasIndex(e => new { e.CompanyId, e.SystemAccountRole })
            .IsUnique()
            .HasDatabaseName("UQ_Account_CompanyId_SystemRole")
            .HasFilter("[SystemAccountRole] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(e => e.ParentAccountId)
            .HasDatabaseName("IX_Account_ParentAccountId");

        builder.HasIndex(e => new { e.CompanyId, e.AccountType })
            .HasDatabaseName("IX_Account_CompanyId_AccountType");

        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.ParentAccount)
            .WithMany()
            .HasForeignKey(e => e.ParentAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Currency)
            .WithMany()
            .HasForeignKey(e => e.CurrencyId)
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
