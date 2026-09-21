namespace FactoryReport.Application.Import;

/// <summary>xlsx 解析与模板生成抽象（实现置于 Infrastructure，避免 Application 依赖 Excel 库）。</summary>
public interface IExcelWorkbookService
{
    byte[] CreateTemplate(ImportTemplateDefinition template);

    /// <summary>
    /// 解析工作表为字典行。失败抛 <see cref="ExcelParseException"/>（面向用户的可读信息）。
    /// </summary>
    IReadOnlyList<IReadOnlyDictionary<string, string?>> ParseDataRows(
        Stream content,
        string sheetName,
        IReadOnlyList<string> expectedColumns);
}

/// <summary>解析失败；不得向用户暴露内部堆栈。</summary>
public sealed class ExcelParseException : Exception
{
    public ExcelParseException(string message) : base(message)
    {
    }
}
