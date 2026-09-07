using System.Data;
using ErpApi.Core.Constants;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpApi.Data;

public class JournalPostingGateway : IJournalPostingGateway
{
    private const string LineTableType = "dbo.JournalLineInput";
    private const string GuidListType = "dbo.GuidList";

    private readonly AppDbContext _context;

    public JournalPostingGateway(AppDbContext context) => _context = context;

    public async Task<PostingResult> PostAsync(
        Guid branchId,
        DateOnly postingDate,
        DateOnly documentDate,
        string? description,
        JournalSourceModule sourceModule,
        Guid createdByUserId,
        IReadOnlyList<PostingLineInput> lines,
        CancellationToken ct = default)
    {
        var entryId = Guid.CreateVersion7();

        await using var command = CreateCommand("dbo.usp_JournalEntry_Post");

        command.Parameters.AddWithValue("@JournalEntryId", entryId);
        command.Parameters.AddWithValue("@TransactionId", Guid.CreateVersion7());
        command.Parameters.AddWithValue("@BranchId", branchId);
        command.Parameters.AddWithValue("@FiscalPeriodId", DBNull.Value);
        command.Parameters.Add("@PostingDate", SqlDbType.Date).Value = ToDateTime(postingDate);
        command.Parameters.Add("@DocumentDate", SqlDbType.Date).Value = ToDateTime(documentDate);
        command.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
        command.Parameters.Add("@SourceModule", SqlDbType.TinyInt).Value = (byte)sourceModule;
        command.Parameters.AddWithValue("@CreatedByUserId", createdByUserId);

        // مفتاح محجوز لسطر فرق التقريب إن لزم: الإجراء يولّده بنفسه فلا نعرف عدد
        // السطور مسبقاً، وتمريره يُبقي سياسة Guid v7 سارية بدل NEWID العشوائي
        command.Parameters.AddWithValue("@RoundingLineId", Guid.CreateVersion7());

        var lineTable = command.Parameters.AddWithValue("@Lines", BuildLineTable(lines));
        lineTable.SqlDbType = SqlDbType.Structured;
        lineTable.TypeName = LineTableType;

        var documentNumber = command.Parameters.Add("@DocumentNumber", SqlDbType.NVarChar, 30);
        documentNumber.Direction = ParameterDirection.Output;

        await ExecuteAsync(command, ct);

        return new PostingResult(entryId, (string)documentNumber.Value);
    }

    public async Task<PostingResult> ReverseAsync(
        Guid originalJournalEntryId,
        Guid branchId,
        DateOnly postingDate,
        DateOnly documentDate,
        string? description,
        Guid createdByUserId,
        IReadOnlyList<Guid> reversalLineIds,
        CancellationToken ct = default)
    {
        var entryId = Guid.CreateVersion7();

        await using var command = CreateCommand("dbo.usp_JournalEntry_Reverse");

        command.Parameters.AddWithValue("@JournalEntryId", entryId);
        command.Parameters.AddWithValue("@TransactionId", Guid.CreateVersion7());
        command.Parameters.AddWithValue("@OriginalJournalEntryId", originalJournalEntryId);
        command.Parameters.AddWithValue("@BranchId", branchId);
        command.Parameters.Add("@PostingDate", SqlDbType.Date).Value = ToDateTime(postingDate);
        command.Parameters.Add("@DocumentDate", SqlDbType.Date).Value = ToDateTime(documentDate);
        command.Parameters.AddWithValue("@FiscalPeriodId", DBNull.Value);
        command.Parameters.AddWithValue("@Description", (object?)description ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedByUserId", createdByUserId);

        var lineIds = command.Parameters.AddWithValue("@LineIds", BuildGuidTable(reversalLineIds));
        lineIds.SqlDbType = SqlDbType.Structured;
        lineIds.TypeName = GuidListType;

        var documentNumber = command.Parameters.Add("@DocumentNumber", SqlDbType.NVarChar, 30);
        documentNumber.Direction = ParameterDirection.Output;

        await ExecuteAsync(command, ct);

        return new PostingResult(entryId, (string)documentNumber.Value);
    }

    private SqlCommand CreateCommand(string procedureName) =>
        new(procedureName, (SqlConnection)_context.Database.GetDbConnection())
        {
            CommandType = CommandType.StoredProcedure
        };

    // الترجمة هنا لا في الخدمة: أرقام 50001..50031 و51001..51014 لا تعبر هذا الحدّ
    private static async Task ExecuteAsync(SqlCommand command, CancellationToken ct)
    {
        var connection = command.Connection!;
        var opened = connection.State != ConnectionState.Open;

        if (opened)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (SqlException exception)
            when (SqlErrorTranslator.TryTranslate(exception, out var translated) && translated is not null)
        {
            throw translated;
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DateTime ToDateTime(DateOnly value) => value.ToDateTime(TimeOnly.MinValue);

    // ترتيب الأعمدة يطابق تعريف النوع في القاعدة: SqlClient يربط أعمدة TVP بالموضع لا بالاسم
    private static DataTable BuildLineTable(IReadOnlyList<PostingLineInput> lines)
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
                ToDateTime(line.ExchangeRateDate),
                line.DebitFC,
                line.CreditFC,
                line.DebitBase,
                line.CreditBase);
        }

        return table;
    }

    private static DataTable BuildGuidTable(IReadOnlyList<Guid> ids)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));

        foreach (var id in ids)
        {
            table.Rows.Add(id);
        }

        return table;
    }
}
