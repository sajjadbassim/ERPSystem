using ErpApi.Core.Constants;
using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t =>
        {
            // NULL في CompanyId يعني «خارج كل شركة»، وهو نطاق لا يجوز لمستخدم عادي.
            // القيد يجعل الغياب مقصوراً على الجذر بدل أن يكون حالة بيانات محتملة
            t.HasCheckConstraint("CK_User_SystemUserHasNoCompany",
                $"([Id] = '{SystemUser.IdLiteral}' AND [CompanyId] IS NULL) OR "
                + $"([Id] <> '{SystemUser.IdLiteral}' AND [CompanyId] IS NOT NULL)");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(e => e.SecurityStamp).IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2");

        // التفرّد على مستوى الشركة لا النظام: نفس الاسم مسموح في شركة أخرى
        builder.HasIndex(e => new { e.CompanyId, e.UserName })
            .IsUnique()
            .HasDatabaseName("UQ_User_CompanyId_UserName");

        // مرشَّح لأن البريد اختياري، وبلا الترشيح يقبل SQL Server صفاً واحداً بـ NULL
        builder.HasIndex(e => new { e.CompanyId, e.Email })
            .IsUnique()
            .HasDatabaseName("UQ_User_CompanyId_Email")
            .HasFilter("[Email] IS NOT NULL");

        builder.HasOne(e => e.Company)
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.NoAction);

        // المستخدم الجذر منشئ نفسه، فالصف الأول يُرضي المفتاح بذاته
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // بلا HasQueryFilter عمداً (R-LIFE-06)
    }
}
