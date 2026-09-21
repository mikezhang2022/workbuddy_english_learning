using FactoryReport.Application.Import;
using MiniExcelLibs;

namespace FactoryReport.Infrastructure.Excel;

/// <summary>
/// 基于 MiniExcel（Apache-2.0）的模板生成与解析。不执行宏、不计算公式。
/// </summary>
public sealed class MiniExcelWorkbookService : IExcelWorkbookService
{
    public byte[] CreateTemplate(ImportTemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);

        using var stream = new MemoryStream();

        var dataRows = new List<Dictionary<string, object?>>();
        foreach (var sample in template.SampleRows)
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var col in template.Columns)
            {
                sample.TryGetValue(col.Name, out var value);
                row[col.Name] = value;
            }

            dataRows.Add(row);
        }

        if (dataRows.Count == 0)
        {
            var empty = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var col in template.Columns)
            {
                empty[col.Name] = null;
            }

            dataRows.Add(empty);
        }

        var notes = template.Columns.Select(c => new Dictionary<string, object?>
        {
            ["column"] = c.Name,
            ["displayName"] = c.DisplayName,
            ["dataType"] = c.DataType,
            ["required"] = c.Required ? "Y" : "N",
            ["example"] = c.Example,
            ["description"] = c.Description
        }).ToList();

        notes.Insert(0, new Dictionary<string, object?>
        {
            ["column"] = "_meta",
            ["displayName"] = template.DisplayName,
            ["dataType"] = template.TemplateVersion,
            ["required"] = template.DatasetCode,
            ["example"] = template.SheetName,
            ["description"] = template.FieldNotes
        });

        var sheets = new Dictionary<string, object>
        {
            [template.SheetName] = dataRows,
            ["FieldNotes"] = notes
        };

        stream.SaveAs(sheets);
        return stream.ToArray();
    }

    public IReadOnlyList<IReadOnlyDictionary<string, string?>> ParseDataRows(
        Stream content,
        string sheetName,
        IReadOnlyList<string> expectedColumns)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);
        ArgumentNullException.ThrowIfNull(expectedColumns);

        if (!content.CanSeek)
        {
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            copy.Position = 0;
            return ParseSeekable(copy, sheetName, expectedColumns);
        }

        return ParseSeekable(content, sheetName, expectedColumns);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string?>> ParseSeekable(
        Stream content,
        string sheetName,
        IReadOnlyList<string> expectedColumns)
    {
        try
        {
            // 校验 OOXML zip 签名（PK）
            if (content.CanSeek && content.Length >= 4)
            {
                var header = new byte[4];
                var pos = content.Position;
                var read = content.Read(header, 0, 4);
                content.Position = pos;
                if (read < 2 || header[0] != 0x50 || header[1] != 0x4B)
                {
                    throw new ExcelParseException("文件不是有效的 .xlsx（缺少 OOXML 签名）。");
                }
            }

            var rows = content.Query(useHeaderRow: true, sheetName: sheetName).Cast<IDictionary<string, object?>>().ToList();
            if (rows.Count == 0)
            {
                // 可能工作表名不对：再试默认第一张
                content.Position = 0;
                rows = content.Query(useHeaderRow: true).Cast<IDictionary<string, object?>>().ToList();
            }

            if (rows.Count == 0)
            {
                return [];
            }

            var headers = rows[0].Keys.Select(k => k?.Trim() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = expectedColumns.Where(c => !headers.Contains(c)).ToArray();
            if (missing.Length == expectedColumns.Count)
            {
                throw new ExcelParseException(
                    "表头与模板不匹配：未找到任何预期列。请下载官方模板后重新填写。");
            }

            var result = new List<IReadOnlyDictionary<string, string?>>(rows.Count);
            foreach (var raw in rows)
            {
                var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var col in expectedColumns)
                {
                    string? value = null;
                    foreach (var kv in raw)
                    {
                        if (string.Equals(kv.Key?.Trim(), col, StringComparison.OrdinalIgnoreCase))
                        {
                            value = FormatCell(kv.Value);
                            break;
                        }
                    }

                    dict[col] = value;
                }

                // 跳过全空行
                if (dict.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                result.Add(dict);
            }

            return result;
        }
        catch (ExcelParseException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ExcelParseException("无法解析 Excel 文件。请确认文件为有效的 .xlsx 且未加密、未含宏。");
        }
    }

    private static string? FormatCell(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            DateTimeOffset dto => dto.UtcDateTime.ToString("yyyy-MM-dd"),
            double d when Math.Abs(d - Math.Round(d)) < 0.0000001 => ((long)Math.Round(d)).ToString(),
            float f when Math.Abs(f - Math.Round(f)) < 0.0000001 => ((long)Math.Round(f)).ToString(),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Trim()
        };
    }
}
