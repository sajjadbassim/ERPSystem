using Microsoft.Data.SqlClient;

namespace ErpApi.Tests.Infrastructure;

public sealed record AuditRow(
    Guid Id,
    Guid UserId,
    string Action,
    string EntityName,
    string EntityKey,
    string? BeforeJson,
    string? AfterJson,
    DateTime CreatedAt);

// عقد سجل التدقيق الذي تفرضه الاختبارات على التنفيذ.
// القراءة بـ SQL خام لا عبر EF: الجدول لا يُقرأ من التطبيق أصلاً، والاختبار يفحص
// ما استقر في القاعدة لا ما تظنه طبقة التتبّع
public static class AuditClient
{
    public const string TableName = "AuditLogEntries";

    // أسماء الأفعال. الصيغة النقطية نفسها التي يفرضها G05 بـ 'Query.AllBranches'
    public const string UserCreated = "User.Created";
    public const string UserUpdated = "User.Updated";
    public const string RoleUpdated = "Role.Updated";
    public const string UserRoleCreated = "UserRole.Created";
    public const string UserRoleDeleted = "UserRole.Deleted";
    public const string UserBranchDeleted = "UserBranch.Deleted";
    public const string RolePermissionCreated = "RolePermission.Created";
    public const string QueryAllBranches = "Query.AllBranches";

    // الأعمدة المسموح تدقيقها لكل كيان. قائمة سماح لا حظر: العمود الجديد يغيب
    // عن السجل حتى يُضاف صراحةً، فإضافة عمود سرّي لا تُسرّبه بالسهو
    public static readonly IReadOnlyList<string> UserAuditedColumns =
        ["UserName", "FullName", "Email", "CompanyId", "IsActive"];

    // ما يجب ألّا يظهر في السجل أبداً. ليست قائمة حظر تُطبَّق في الكود، بل
    // ما تفحص الاختبارات غيابه — وغيابه ناتج عن كونه خارج قائمة السماح أصلاً
    public static readonly IReadOnlyList<string> ForbiddenColumnNames =
        ["PasswordHash", "SecurityStamp", "TokenHash"];

    public static async Task<List<AuditRow>> ReadAsync(
        TestDatabase database, string? entityName = null, string? entityKey = null, Guid? userId = null)
    {
        var rows = new List<AuditRow>();

        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            $"""
             SELECT Id, UserId, Action, EntityName, EntityKey, BeforeJson, AfterJson, CreatedAt
             FROM {TableName}
             WHERE (@entityName IS NULL OR EntityName = @entityName)
               AND (@entityKey  IS NULL OR EntityKey  = @entityKey)
               AND (@userId     IS NULL OR UserId     = @userId)
             ORDER BY CreatedAt, Id
             """, connection);

        command.Parameters.AddWithValue("@entityName", (object?)entityName ?? DBNull.Value);
        command.Parameters.AddWithValue("@entityKey", (object?)entityKey ?? DBNull.Value);
        command.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(new AuditRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetDateTime(7)));
        }

        return rows;
    }

    // كل ما كُتب في السجل نصّاً واحداً. الفحص على المستودع كله لا على صف بعينه:
    // تسريب السرّ من أي مسار — متوقَّع أو غير متوقَّع — يجب أن يُلتقط
    public static Task<string?> AllStoredTextAsync(TestDatabase database) =>
        PostingClient.ScalarAsync<string>(database,
            $"""
             SELECT STRING_AGG(CAST(
                 Action + '|' + EntityName + '|' + EntityKey + '|'
                 + ISNULL(BeforeJson, '') + '|' + ISNULL(AfterJson, '') AS NVARCHAR(MAX)), ' ')
             FROM {TableName}
             """);
}
