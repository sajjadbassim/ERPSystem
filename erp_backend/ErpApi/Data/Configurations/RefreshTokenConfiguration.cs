using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens", t =>
            t.HasCheckConstraint("CK_RefreshToken_ExpiresAfterCreation", "[ExpiresAt] > [CreatedAt]"));

        builder.HasKey(e => e.Id);

        // SHA-256 بطول ثابت: varbinary(32) لا nvarchar، فلا مقارنة نصية ولا ترميز وسيط
        builder.Property(e => e.TokenHash).HasColumnType("varbinary(32)").IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnType("datetime2").IsRequired();
        builder.Property(e => e.RevokedAt).HasColumnType("datetime2");

        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("UQ_RefreshToken_TokenHash");

        // البحث عند التدوير وإبطال السلسلة يقع على المستخدم
        builder.HasIndex(e => e.UserId).HasDatabaseName("IX_RefreshToken_UserId");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.ReplacedByToken)
            .WithMany()
            .HasForeignKey(e => e.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
