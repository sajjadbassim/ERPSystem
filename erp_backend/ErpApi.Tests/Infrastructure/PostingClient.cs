using System.Data;
using Microsoft.Data.SqlClient;

namespace ErpApi.Tests.Infrastructure;

// يستدعي الإجراءات المخزَّنة مباشرة. الخدمات لم تُبنَ بعد، والعقد المفحوص هنا هو عقد القاعدة.
public static class PostingClient
{
    public const string LineTableType = "dbo.JournalLineInput";
    public const string GuidListType = "dbo.GuidList";

    public static async Task<string> PostAsync(TestDatabase database, PostRequest request)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("dbo.usp_JournalEntry_Post", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@JournalEntryId", request.JournalEntryId);
        command.Parameters.AddWithValue("@TransactionId", request.TransactionId);
        command.Parameters.AddWithValue("@BranchId", request.BranchId);
        command.Parameters.AddWithValue("@FiscalPeriodId", (object?)request.FiscalPeriodId ?? DBNull.Value);
        command.Parameters.Add("@PostingDate", SqlDbType.Date).Value = request.PostingDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@DocumentDate", SqlDbType.Date).Value = request.DocumentDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
        command.Parameters.Add("@SourceModule", SqlDbType.TinyInt).Value = request.SourceModule;
        command.Parameters.AddWithValue("@CreatedByUserId", request.CreatedByUserId);
        command.Parameters.AddWithValue("@RoundingLineId", request.RoundingLineId);

        var lines = command.Parameters.AddWithValue("@Lines", BuildLineTable(request.Lines));
        lines.SqlDbType = SqlDbType.Structured;
        lines.TypeName = LineTableType;

        var documentNumber = command.Parameters.Add("@DocumentNumber", SqlDbType.NVarChar, 30);
        documentNumber.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync();

        return (string)documentNumber.Value;
    }

    public static async Task<string> ReverseAsync(TestDatabase database, ReverseRequest request)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("dbo.usp_JournalEntry_Reverse", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@JournalEntryId", request.JournalEntryId);
        command.Parameters.AddWithValue("@TransactionId", request.TransactionId);
        command.Parameters.AddWithValue("@OriginalJournalEntryId", request.OriginalJournalEntryId);
        command.Parameters.AddWithValue("@BranchId", request.BranchId);
        command.Parameters.Add("@PostingDate", SqlDbType.Date).Value = request.PostingDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@DocumentDate", SqlDbType.Date).Value = request.DocumentDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@FiscalPeriodId", (object?)request.FiscalPeriodId ?? DBNull.Value);
        command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedByUserId", request.CreatedByUserId);

        var lineIds = command.Parameters.AddWithValue("@LineIds", BuildGuidTable(request.LineIds));
        lineIds.SqlDbType = SqlDbType.Structured;
        lineIds.TypeName = GuidListType;

        var documentNumber = command.Parameters.Add("@DocumentNumber", SqlDbType.NVarChar, 30);
        documentNumber.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync();

        return (string)documentNumber.Value;
    }

    // ترتيب الأعمدة يجب أن يطابق تعريف النوع في القاعدة: SqlClient يربط أعمدة TVP بالموضع لا بالاسم
    private static DataTable BuildLineTable(IEnumerable<LineDraft> lines)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("LineNumber", typeof(short));
        table.Columns.Add("AccountId", typeof(Guid));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("CurrencyId", typeof(Guid));
        table.Columns.Add("ExchangeRate", typeof(decimal));
        table.Columns.Add("ExchangeRateDate", typeof(DateTime));
        table.Columns.Add("DebitFC", typeof(decimal));
        table.Columns.Add("CreditFC", typeof(decimal));
        table.Columns.Add("DebitBase", typeof(decimal));
        table.Columns.Add("CreditBase", typeof(decimal));

        foreach (var line in lines)
        {
            table.Rows.Add(
                line.Id,
                line.LineNumber,
                line.AccountId,
                (object?)line.Description ?? DBNull.Value,
                line.CurrencyId,
                line.ExchangeRate,
                line.ExchangeRateDate.ToDateTime(TimeOnly.MinValue),
                line.DebitFC,
                line.CreditFC,
                line.DebitBase,
                line.CreditBase);
        }

        return table;
    }

    private static DataTable BuildGuidTable(IEnumerable<Guid> ids)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));

        foreach (var id in ids)
        {
            table.Rows.Add(id);
        }

        return table;
    }

    public static async Task<T?> ScalarAsync<T>(TestDatabase database, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)result;
    }

    public static async Task ExecuteAsync(TestDatabase database, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
