namespace FactoryReport.Application.Import;

/// <summary>模板列定义（名称、类型、是否必填、示例与说明）。</summary>
public sealed class ImportTemplateColumn
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string DataType { get; init; }
    public required bool Required { get; init; }
    public required string Example { get; init; }
    public required string Description { get; init; }
}

/// <summary>可下载模板元数据。</summary>
public sealed class ImportTemplateDefinition
{
    public required string DatasetCode { get; init; }
    public required string DisplayName { get; init; }
    public required string TemplateVersion { get; init; }
    public required string SheetName { get; init; }
    public required IReadOnlyList<ImportTemplateColumn> Columns { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> SampleRows { get; init; }
    public required string FieldNotes { get; init; }
}
