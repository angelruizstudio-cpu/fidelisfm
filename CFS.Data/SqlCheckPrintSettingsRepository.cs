using System.Data;
using CFS.Core.Models;
using CFS.Core.Services;
using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlCheckPrintSettingsRepository(SqlConnectionFactory connectionFactory) : ICheckPrintSettingsRepository
{
    public async Task<CheckPrintSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, cancellationToken))
        {
            return CheckPrintSettings.Defaults();
        }

        const string sql = """
            SELECT TOP 1
                   SheetOffsetX,
                   SheetOffsetY,
                   DateLeft,
                   DateTop,
                   PayeeLeft,
                   PayeeTop,
                   AmountLeft,
                   AmountTop,
                   WordsLeft,
                   WordsTop,
                   AddressLeft,
                   AddressTop,
                   MemoLeft,
                   MemoTop,
                   StubTitleLeft,
                   StubTitleTop,
                   StubPayeeLeft,
                   StubPayeeTop,
                   StubDateLeft,
                   StubDateTop,
                   StubAccountLeft,
                   StubAccountTop,
                   StubMemoLeft,
                   StubMemoTop,
                   StubAmountLeft,
                   StubAmountTop,
                   UpdatedAt,
                   UpdatedBy
            FROM dbo.CFS_CheckPrintSettings
            WHERE Id = 1;
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadSettings(reader)
            : CheckPrintSettings.Defaults();
    }

    public async Task SaveAsync(
        CheckPrintSettingsEntry entry,
        string userName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        if (!await TableExistsAsync(connection, cancellationToken))
        {
            throw new InvalidOperationException("Falta crear la tabla dbo.CFS_CheckPrintSettings. Usa el script CFS.Data/Sql/CFS_CheckPrintSettings.sql.");
        }

        const string sql = """
            MERGE dbo.CFS_CheckPrintSettings AS Target
            USING (SELECT 1 AS Id) AS Source
               ON Target.Id = Source.Id
            WHEN MATCHED THEN
                UPDATE SET
                    SheetOffsetX = @sheetOffsetX,
                    SheetOffsetY = @sheetOffsetY,
                    DateLeft = @dateLeft,
                    DateTop = @dateTop,
                    PayeeLeft = @payeeLeft,
                    PayeeTop = @payeeTop,
                    AmountLeft = @amountLeft,
                    AmountTop = @amountTop,
                    WordsLeft = @wordsLeft,
                    WordsTop = @wordsTop,
                    AddressLeft = @addressLeft,
                    AddressTop = @addressTop,
                    MemoLeft = @memoLeft,
                    MemoTop = @memoTop,
                    StubTitleLeft = @stubTitleLeft,
                    StubTitleTop = @stubTitleTop,
                    StubPayeeLeft = @stubPayeeLeft,
                    StubPayeeTop = @stubPayeeTop,
                    StubDateLeft = @stubDateLeft,
                    StubDateTop = @stubDateTop,
                    StubAccountLeft = @stubAccountLeft,
                    StubAccountTop = @stubAccountTop,
                    StubMemoLeft = @stubMemoLeft,
                    StubMemoTop = @stubMemoTop,
                    StubAmountLeft = @stubAmountLeft,
                    StubAmountTop = @stubAmountTop,
                    UpdatedAt = SYSUTCDATETIME(),
                    UpdatedBy = @user
            WHEN NOT MATCHED THEN
                INSERT
                    (Id, SheetOffsetX, SheetOffsetY, DateLeft, DateTop, PayeeLeft, PayeeTop,
                     AmountLeft, AmountTop, WordsLeft, WordsTop, AddressLeft, AddressTop,
                     MemoLeft, MemoTop, StubTitleLeft, StubTitleTop, StubPayeeLeft, StubPayeeTop,
                     StubDateLeft, StubDateTop, StubAccountLeft, StubAccountTop, StubMemoLeft,
                     StubMemoTop, StubAmountLeft, StubAmountTop, UpdatedAt, UpdatedBy)
                VALUES
                    (1, @sheetOffsetX, @sheetOffsetY, @dateLeft, @dateTop, @payeeLeft, @payeeTop,
                     @amountLeft, @amountTop, @wordsLeft, @wordsTop, @addressLeft, @addressTop,
                     @memoLeft, @memoTop, @stubTitleLeft, @stubTitleTop, @stubPayeeLeft, @stubPayeeTop,
                     @stubDateLeft, @stubDateTop, @stubAccountLeft, @stubAccountTop, @stubMemoLeft,
                     @stubMemoTop, @stubAmountLeft, @stubAmountTop, SYSUTCDATETIME(), @user);
            """;

        await using var command = new SqlCommand(sql, connection);
        AddParameters(command, entry);
        command.Parameters.Add("@user", SqlDbType.NVarChar, 100).Value = userName;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID('dbo.CFS_CheckPrintSettings');",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static CheckPrintSettings ReadSettings(SqlDataReader reader) => new()
    {
        SheetOffsetX = GetDecimal(reader, "SheetOffsetX"),
        SheetOffsetY = GetDecimal(reader, "SheetOffsetY"),
        DateLeft = GetDecimal(reader, "DateLeft"),
        DateTop = GetDecimal(reader, "DateTop"),
        PayeeLeft = GetDecimal(reader, "PayeeLeft"),
        PayeeTop = GetDecimal(reader, "PayeeTop"),
        AmountLeft = GetDecimal(reader, "AmountLeft"),
        AmountTop = GetDecimal(reader, "AmountTop"),
        WordsLeft = GetDecimal(reader, "WordsLeft"),
        WordsTop = GetDecimal(reader, "WordsTop"),
        AddressLeft = GetDecimal(reader, "AddressLeft"),
        AddressTop = GetDecimal(reader, "AddressTop"),
        MemoLeft = GetDecimal(reader, "MemoLeft"),
        MemoTop = GetDecimal(reader, "MemoTop"),
        StubTitleLeft = GetDecimal(reader, "StubTitleLeft"),
        StubTitleTop = GetDecimal(reader, "StubTitleTop"),
        StubPayeeLeft = GetDecimal(reader, "StubPayeeLeft"),
        StubPayeeTop = GetDecimal(reader, "StubPayeeTop"),
        StubDateLeft = GetDecimal(reader, "StubDateLeft"),
        StubDateTop = GetDecimal(reader, "StubDateTop"),
        StubAccountLeft = GetDecimal(reader, "StubAccountLeft"),
        StubAccountTop = GetDecimal(reader, "StubAccountTop"),
        StubMemoLeft = GetDecimal(reader, "StubMemoLeft"),
        StubMemoTop = GetDecimal(reader, "StubMemoTop"),
        StubAmountLeft = GetDecimal(reader, "StubAmountLeft"),
        StubAmountTop = GetDecimal(reader, "StubAmountTop"),
        UpdatedAt = reader["UpdatedAt"] is DBNull ? null : reader.GetDateTime(reader.GetOrdinal("UpdatedAt")),
        UpdatedBy = reader["UpdatedBy"] is DBNull ? null : reader.GetString(reader.GetOrdinal("UpdatedBy"))
    };

    private static decimal GetDecimal(SqlDataReader reader, string name) =>
        reader.GetDecimal(reader.GetOrdinal(name));

    private static void AddParameters(SqlCommand command, CheckPrintSettingsEntry entry)
    {
        AddDecimal(command, "@sheetOffsetX", entry.SheetOffsetX);
        AddDecimal(command, "@sheetOffsetY", entry.SheetOffsetY);
        AddDecimal(command, "@dateLeft", entry.DateLeft);
        AddDecimal(command, "@dateTop", entry.DateTop);
        AddDecimal(command, "@payeeLeft", entry.PayeeLeft);
        AddDecimal(command, "@payeeTop", entry.PayeeTop);
        AddDecimal(command, "@amountLeft", entry.AmountLeft);
        AddDecimal(command, "@amountTop", entry.AmountTop);
        AddDecimal(command, "@wordsLeft", entry.WordsLeft);
        AddDecimal(command, "@wordsTop", entry.WordsTop);
        AddDecimal(command, "@addressLeft", entry.AddressLeft);
        AddDecimal(command, "@addressTop", entry.AddressTop);
        AddDecimal(command, "@memoLeft", entry.MemoLeft);
        AddDecimal(command, "@memoTop", entry.MemoTop);
        AddDecimal(command, "@stubTitleLeft", entry.StubTitleLeft);
        AddDecimal(command, "@stubTitleTop", entry.StubTitleTop);
        AddDecimal(command, "@stubPayeeLeft", entry.StubPayeeLeft);
        AddDecimal(command, "@stubPayeeTop", entry.StubPayeeTop);
        AddDecimal(command, "@stubDateLeft", entry.StubDateLeft);
        AddDecimal(command, "@stubDateTop", entry.StubDateTop);
        AddDecimal(command, "@stubAccountLeft", entry.StubAccountLeft);
        AddDecimal(command, "@stubAccountTop", entry.StubAccountTop);
        AddDecimal(command, "@stubMemoLeft", entry.StubMemoLeft);
        AddDecimal(command, "@stubMemoTop", entry.StubMemoTop);
        AddDecimal(command, "@stubAmountLeft", entry.StubAmountLeft);
        AddDecimal(command, "@stubAmountTop", entry.StubAmountTop);
    }

    private static void AddDecimal(SqlCommand command, string name, decimal value) =>
        command.Parameters.Add(name, SqlDbType.Decimal).Value = value;
}
