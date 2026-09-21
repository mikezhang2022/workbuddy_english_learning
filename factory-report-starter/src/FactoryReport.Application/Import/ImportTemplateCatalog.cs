using FactoryReport.Domain.Import;

namespace FactoryReport.Application.Import;

/// <summary>计划 / 实际 Excel 模板字段定义（与 data-dictionary / Fake 组织和产品编码对齐）。</summary>
public static class ImportTemplateCatalog
{
    public const string TemplateVersion = "1.0";
    public const string SheetName = "Data";

    public static ImportTemplateDefinition Plan { get; } = new()
    {
        DatasetCode = ImportDatasetCodes.PlanData,
        DisplayName = ImportDatasetCodes.DisplayName(ImportDatasetCodes.PlanData),
        TemplateVersion = TemplateVersion,
        SheetName = SheetName,
        FieldNotes =
            "计划数据模板。唯一键（Fake）：factoryCode+workshopCode+productionDate+productCode。" +
            "ReplaceScope（Fake）：factoryCode+planYearMonth。正式规则【待现场确认】。",
        Columns =
        [
            Col("factoryCode", "工厂编码", "string", true, "F-DEMO-01", "须存在于组织主数据"),
            Col("workshopCode", "车间编码", "string", true, "W-DEMO-A", "须属于该工厂"),
            Col("productionLineCode", "产线编码", "string", false, "L-A1", "若填写则须存在"),
            Col("planYearMonth", "计划年月", "string", true, "2026-03", "格式 YYYY-MM"),
            Col("productionDate", "生产日期", "date", true, "2026-03-10", "建议 ISO 日期 yyyy-MM-dd"),
            Col("productCode", "产品编码", "string", true, "PROD-NORMAL", "须存在于产品主数据"),
            Col("planQuantity", "计划数量", "decimal", true, "100", "须 ≥ 0"),
            Col("remark", "备注", "string", false, "demo", "可选说明")
        ],
        SampleRows =
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["factoryCode"] = "F-DEMO-01",
                ["workshopCode"] = "W-DEMO-A",
                ["productionLineCode"] = "L-A1",
                ["planYearMonth"] = "2026-03",
                ["productionDate"] = "2026-03-15",
                ["productCode"] = "PROD-NORMAL",
                ["planQuantity"] = 120,
                ["remark"] = "template-sample"
            }
        ]
    };

    public static ImportTemplateDefinition Actual { get; } = new()
    {
        DatasetCode = ImportDatasetCodes.ActualData,
        DisplayName = ImportDatasetCodes.DisplayName(ImportDatasetCodes.ActualData),
        TemplateVersion = TemplateVersion,
        SheetName = SheetName,
        FieldNotes =
            "实际数据模板（导入对比用）。不写入 Oracle/MES 业务表；仅存于产品导入存储（本阶段 Fake）。" +
            "唯一键（Fake）：factoryCode+workshopCode+productionDate+productCode。",
        Columns =
        [
            Col("factoryCode", "工厂编码", "string", true, "F-DEMO-01", "须存在于组织主数据"),
            Col("workshopCode", "车间编码", "string", true, "W-DEMO-A", "须属于该工厂"),
            Col("productionLineCode", "产线编码", "string", false, "L-A1", "若填写则须存在"),
            Col("productionDate", "生产日期", "date", true, "2026-03-10", "建议 ISO 日期 yyyy-MM-dd"),
            Col("productCode", "产品编码", "string", true, "PROD-NORMAL", "须存在于产品主数据"),
            Col("actualQuantity", "实际数量", "decimal", true, "95", "须 ≥ 0"),
            Col("goodQuantity", "良品数量", "decimal", false, "90", "若填须 ≥ 0"),
            Col("defectQuantity", "不良数量", "decimal", false, "5", "若填须 ≥ 0"),
            Col("shiftCode", "班次编码", "string", false, "DAY", "可选"),
            Col("remark", "备注", "string", false, "demo", "可选说明")
        ],
        SampleRows =
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["factoryCode"] = "F-DEMO-01",
                ["workshopCode"] = "W-DEMO-A",
                ["productionLineCode"] = "L-A1",
                ["productionDate"] = "2026-03-15",
                ["productCode"] = "PROD-NORMAL",
                ["actualQuantity"] = 95,
                ["goodQuantity"] = 90,
                ["defectQuantity"] = 5,
                ["shiftCode"] = "DAY",
                ["remark"] = "template-sample"
            }
        ]
    };

    public static ImportTemplateDefinition GetRequired(string datasetCode)
    {
        if (string.Equals(datasetCode, ImportDatasetCodes.PlanData, StringComparison.Ordinal))
        {
            return Plan;
        }

        if (string.Equals(datasetCode, ImportDatasetCodes.ActualData, StringComparison.Ordinal))
        {
            return Actual;
        }

        throw new ArgumentException($"Unknown import dataset code '{datasetCode}'.", nameof(datasetCode));
    }

    public static IReadOnlyList<ImportTemplateDefinition> All { get; } = [Plan, Actual];

    private static ImportTemplateColumn Col(
        string name,
        string displayName,
        string dataType,
        bool required,
        string example,
        string description)
        => new()
        {
            Name = name,
            DisplayName = displayName,
            DataType = dataType,
            Required = required,
            Example = example,
            Description = description
        };
}
