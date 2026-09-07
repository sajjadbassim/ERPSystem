using ErpApi.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpApi.Data.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries", t =>
            t.HasTrigger("TR_AuditLogEntry_PreventModification"));

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityKey).HasMaxLength(200).IsRequired();

        // الاستثناء الوحيد المبرَّر من منع nvarchar(max) (بند 16): الحمولة لقطة JSON
        // لكيان كامل، وطولها دالة على عدد أعمدة الكيان المدقَّق ومحتواها لا على قاعدة عمل.
        // أي حدّ نختاره هنا رقم مخترَع، وتجاوزه يعني بتر السجل بصمت — وسجل مبتور
        // أسوأ من سجل كبير، لأنه يبدو كاملاً وهو ناقص
        builder.Property(e => e.BeforeJson).HasColumnType("nvarchar(max)");
        builder.Property(e => e.AfterJson).HasColumnType("nvarchar(max)");

        builder.Property(e => e.CreatedAt).HasColumnType("datetime2").IsRequired();

        // «ماذا جرى لهذا الصف بالذات ومتى» — سؤال ROADMAP عن منح الصلاحية وسحبها.
        // بلا هذا الفهرس يصير جوابه مسحاً كاملاً للجدول الذي ينمو ولا يُقلَّم (الحارس J11)
        builder.HasIndex(e => new { e.EntityName, e.EntityKey, e.CreatedAt })
            .HasDatabaseName("IX_AuditLogEntry_Entity_Key_CreatedAt");

        // «ماذا فعل هذا المستخدم ومتى»
        builder.HasIndex(e => new { e.UserId, e.CreatedAt })
            .HasDatabaseName("IX_AuditLogEntry_UserId_CreatedAt");

        // المفتاح الأجنبي الوحيد على الجدول. الفاعل يبقى مرجعاً حقيقياً لأن المستخدم
        // لا يُحذف أصلاً (R-LIFE-06)، بينما EntityKey نصّ لأن مرجعه قد يُمحى
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // بلا HasQueryFilter عمداً: لا IsDeleted أصلاً، ولا شيء يُخفى من سجل تدقيق
    }
}
