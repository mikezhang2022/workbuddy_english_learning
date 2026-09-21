using System.Globalization;
using FactoryReport.Domain.Import;
using FactoryReport.Domain.MasterData;
using FactoryReport.Domain.Organizations;

namespace FactoryReport.Application.Import;

/// <summary>逐行校验引擎：必填、类型、日期、组织/产品、负数、重复、日期范围。</summary>
public static class ImportRowValidator
{
    public static readonly DateOnly MinReasonableDate = new(2000, 1, 1);
    public static readonly DateOnly MaxReasonableDate = new(2100, 12, 31);

    public const string CodeRequired = "required";
    public const string CodeType = "type";
    public const string CodeDateFormat = "date_format";
    public const string CodeDateRange = "date_range";
    public const string CodeOrgNotFound = "org_not_found";
    public const string CodeProductNotFound = "product_not_found";
    public const string CodeNegative = "negative";
    public const string CodeDuplicate = "duplicate";

    public static IReadOnlyList<ImportRowError> Validate(
        string datasetCode,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        IReadOnlyList<Factory> factories,
        IReadOnlyList<Workshop> workshops,
        IReadOnlyList<ProductionLine> lines,
        IReadOnlyList<Product> products)
    {
        var template = ImportTemplateCatalog.GetRequired(datasetCode);
        var errors = new List<ImportRowError>();
        var seenKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var rowNumber = i + 2; // 1 = header
            var row = rows[i];
            ValidateRequiredAndTypes(template, row, rowNumber, errors);

            // 若基础类型已失败，仍继续组织/重复检查以便完整报告
            TryResolveOrgAndProduct(
                row,
                rowNumber,
                factories,
                workshops,
                lines,
                products,
                errors,
                out var factory,
                out var workshop);

            if (string.Equals(datasetCode, ImportDatasetCodes.PlanData, StringComparison.Ordinal))
            {
                ValidatePlanExtras(row, rowNumber, errors);
            }

            var key = BuildBusinessKey(row);
            if (key is not null)
            {
                if (seenKeys.TryGetValue(key, out var firstRow))
                {
                    errors.Add(new ImportRowError(
                        rowNumber,
                        "businessKey",
                        $"与第 {firstRow} 行重复（同文件内唯一键冲突）。",
                        CodeDuplicate,
                        key));
                }
                else
                {
                    seenKeys[key] = rowNumber;
                }
            }

            _ = factory;
            _ = workshop;
        }

        return errors;
    }

    private static void ValidateRequiredAndTypes(
        ImportTemplateDefinition template,
        IReadOnlyDictionary<string, string?> row,
        int rowNumber,
        List<ImportRowError> errors)
    {
        foreach (var column in template.Columns)
        {
            row.TryGetValue(column.Name, out var raw);
            var value = Normalize(raw);

            if (column.Required && value is null)
            {
                errors.Add(new ImportRowError(
                    rowNumber,
                    column.Name,
                    $"必填字段「{column.DisplayName}」缺失。",
                    CodeRequired,
                    raw));
                continue;
            }

            if (value is null)
            {
                continue;
            }

            switch (column.DataType)
            {
                case "date":
                    if (!TryParseDate(value, out var date))
                    {
                        errors.Add(new ImportRowError(
                            rowNumber,
                            column.Name,
                            $"日期格式非法，期望 yyyy-MM-dd，实际「{value}」。",
                            CodeDateFormat,
                            value));
                    }
                    else if (date < MinReasonableDate || date > MaxReasonableDate)
                    {
                        errors.Add(new ImportRowError(
                            rowNumber,
                            column.Name,
                            $"日期超出合理范围（{MinReasonableDate:yyyy-MM-dd}～{MaxReasonableDate:yyyy-MM-dd}）。",
                            CodeDateRange,
                            value));
                    }

                    break;

                case "decimal":
                    if (!TryParseDecimal(value, out var number))
                    {
                        errors.Add(new ImportRowError(
                            rowNumber,
                            column.Name,
                            $"数值类型错误，无法解析「{value}」。",
                            CodeType,
                            value));
                    }
                    else if (number < 0)
                    {
                        errors.Add(new ImportRowError(
                            rowNumber,
                            column.Name,
                            $"数值不能为负（{value}）。",
                            CodeNegative,
                            value));
                    }

                    break;

                case "string":
                    if (string.Equals(column.Name, "planYearMonth", StringComparison.Ordinal)
                        && !IsYearMonth(value))
                    {
                        errors.Add(new ImportRowError(
                            rowNumber,
                            column.Name,
                            $"计划年月格式非法，期望 YYYY-MM，实际「{value}」。",
                            CodeType,
                            value));
                    }

                    break;
            }
        }
    }

    private static void ValidatePlanExtras(
        IReadOnlyDictionary<string, string?> row,
        int rowNumber,
        List<ImportRowError> errors)
    {
        var yearMonth = Normalize(Get(row, "planYearMonth"));
        var dateRaw = Normalize(Get(row, "productionDate"));
        if (yearMonth is null || dateRaw is null || !TryParseDate(dateRaw, out var date))
        {
            return;
        }

        var expected = $"{date.Year:D4}-{date.Month:D2}";
        if (!string.Equals(yearMonth, expected, StringComparison.Ordinal))
        {
            errors.Add(new ImportRowError(
                rowNumber,
                "productionDate",
                $"生产日期须落在计划年月 {yearMonth} 内（当前推得 {expected}）。",
                CodeDateRange,
                dateRaw));
        }
    }

    private static void TryResolveOrgAndProduct(
        IReadOnlyDictionary<string, string?> row,
        int rowNumber,
        IReadOnlyList<Factory> factories,
        IReadOnlyList<Workshop> workshops,
        IReadOnlyList<ProductionLine> lines,
        IReadOnlyList<Product> products,
        List<ImportRowError> errors,
        out Factory? factory,
        out Workshop? workshop)
    {
        factory = null;
        workshop = null;

        var factoryCode = Normalize(Get(row, "factoryCode"));
        if (factoryCode is not null)
        {
            factory = factories.FirstOrDefault(f =>
                string.Equals(f.Code, factoryCode, StringComparison.OrdinalIgnoreCase));
            if (factory is null)
            {
                errors.Add(new ImportRowError(
                    rowNumber,
                    "factoryCode",
                    $"组织编码不存在：工厂「{factoryCode}」。",
                    CodeOrgNotFound,
                    factoryCode));
            }
        }

        var workshopCode = Normalize(Get(row, "workshopCode"));
        if (workshopCode is not null && factory is not null)
        {
            var factoryId = factory.Id;
            var factoryCodeLabel = factory.Code;
            workshop = workshops.FirstOrDefault(w =>
                w.FactoryId == factoryId
                && string.Equals(w.Code, workshopCode, StringComparison.OrdinalIgnoreCase));
            if (workshop is null)
            {
                errors.Add(new ImportRowError(
                    rowNumber,
                    "workshopCode",
                    $"组织编码不存在：车间「{workshopCode}」（工厂 {factoryCodeLabel}）。",
                    CodeOrgNotFound,
                    workshopCode));
            }
        }

        var lineCode = Normalize(Get(row, "productionLineCode"));
        if (lineCode is not null && factory is not null)
        {
            var factoryId = factory.Id;
            var line = lines.FirstOrDefault(l =>
                l.FactoryId == factoryId
                && string.Equals(l.Code, lineCode, StringComparison.OrdinalIgnoreCase));
            if (line is null)
            {
                errors.Add(new ImportRowError(
                    rowNumber,
                    "productionLineCode",
                    $"组织编码不存在：产线「{lineCode}」。",
                    CodeOrgNotFound,
                    lineCode));
            }
            else if (workshop is not null && line.WorkshopId != workshop.Id)
            {
                errors.Add(new ImportRowError(
                    rowNumber,
                    "productionLineCode",
                    $"产线「{lineCode}」不属于车间「{workshop.Code}」。",
                    CodeOrgNotFound,
                    lineCode));
            }
        }

        var productCode = Normalize(Get(row, "productCode"));
        if (productCode is not null && factory is not null)
        {
            var factoryId = factory.Id;
            var product = products.FirstOrDefault(p =>
                p.FactoryId == factoryId
                && string.Equals(p.ProductCode, productCode, StringComparison.OrdinalIgnoreCase));
            if (product is null)
            {
                errors.Add(new ImportRowError(
                    rowNumber,
                    "productCode",
                    $"产品编码不存在：「{productCode}」。",
                    CodeProductNotFound,
                    productCode));
            }
        }
    }

    private static string? BuildBusinessKey(IReadOnlyDictionary<string, string?> row)
    {
        var factory = Normalize(Get(row, "factoryCode"));
        var workshop = Normalize(Get(row, "workshopCode"));
        var date = Normalize(Get(row, "productionDate"));
        var product = Normalize(Get(row, "productCode"));
        if (factory is null || workshop is null || date is null || product is null)
        {
            return null;
        }

        return $"{factory}|{workshop}|{date}|{product}";
    }

    public static bool TryParseDate(string value, out DateOnly date)
    {
        if (DateOnly.TryParseExact(
                value,
                ["yyyy-MM-dd", "yyyy/M/d", "yyyy/MM/dd"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        date = default;
        return false;
    }

    public static bool TryParseDecimal(string value, out decimal number)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number);

    private static bool IsYearMonth(string value)
        => value.Length == 7
           && value[4] == '-'
           && int.TryParse(value.AsSpan(0, 4), out _)
           && int.TryParse(value.AsSpan(5, 2), out var m)
           && m is >= 1 and <= 12;

    private static string? Get(IReadOnlyDictionary<string, string?> row, string key)
        => row.TryGetValue(key, out var v) ? v : null;

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
