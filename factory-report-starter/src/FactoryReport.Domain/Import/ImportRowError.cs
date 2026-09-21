namespace FactoryReport.Domain.Import;

/// <summary>
/// 导入行级校验错误。错误行不得静默丢弃；明细须含行号、列名与原因。
/// </summary>
public sealed class ImportRowError
{
    public int RowNumber { get; }
    public string ColumnName { get; }
    public string? RawValue { get; }
    public string Reason { get; }
    public string Code { get; }

    public ImportRowError(
        int rowNumber,
        string columnName,
        string reason,
        string code,
        string? rawValue = null)
    {
        if (rowNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rowNumber), "RowNumber must be 1-based and positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        RowNumber = rowNumber;
        ColumnName = columnName.Trim();
        Reason = reason.Trim();
        Code = code.Trim();
        RawValue = rawValue;
    }
}
