namespace ErpApi.Core.Models;

// لا يطبّق ICreationAuditable: لا CreatedByUserId هنا، فالرمز يُنشأ لحامله وUserId هو الفاعل.
// عمود منشئ ثانٍ كان سيكرر المعنى، ولو طبّقناه بمُلحِق صوري لكتب فيه Interceptor التدقيق
// (§4.2) قيمة تُهمَل بصمت.
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    // تجزئة لا الرمز الخام: نسخة القاعدة المسروقة لا تُمكّن من انتحال جلسة (بند 18)
    public byte[] TokenHash { get; set; } = [];

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    // يربط الرمز بخلفه عند التدوير، فتُبطل السلسلة كاملة عند اكتشاف إعادة استعمال
    public Guid? ReplacedByTokenId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public RefreshToken? ReplacedByToken { get; set; }
}
