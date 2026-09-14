using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Domain.Import;
using FactoryReport.Domain.Planning;
using FactoryReport.Domain.Reporting;

namespace FactoryReport.Application.Reporting.MonthlyProductionPlan;

/// <summary>
/// 月度生产计划只读查询：按工厂 + 计划月份返回月度文件内的日计划行。
/// <list type="bullet">
/// <item>返回行必须保留 PlanDate；不得将月计划平均或推算到每天</item>
/// <item>Fake 版本行为：仅返回 PublishStatus=Published 且 IsActive=true 的版本行</item>
/// <item>真实 Excel 发布 / 激活 / 回退【待现场确认】</item>
/// </list>
/// </summary>
public sealed class MonthlyProductionPlanReportService : IMonthlyProductionPlanReportService
{
    private readonly IReportDataQueryService _queryService;
    private readonly IDataAccessModeProvider _dataAccessModeProvider;
    private readonly IUtcClock _clock;

    public MonthlyProductionPlanReportService(
        IReportDataQueryService queryService,
        IDataAccessModeProvider dataAccessModeProvider,
        IUtcClock clock)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _dataAccessModeProvider = dataAccessModeProvider ?? throw new ArgumentNullException(nameof(dataAccessModeProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<MonthlyProductionPlanQueryResponse> QueryAsync(
        MonthlyProductionPlanQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validated = Validate(request);

        var scope = new OrganizationScopeFilter(
            validated.FactoryId,
            validated.WorkshopId,
            validated.ProductionLineId);
        var dateRange = new DateRangeFilter(validated.MonthStart, validated.MonthEnd);

        var versionsTask = _queryService.GetDatasetVersionsAsync(
            validated.FactoryId,
            ReportCodes.MonthlyProductionPlan,
            cancellationToken);
        var plansTask = _queryService.GetDailyPlanLinesAsync(
            scope, dateRange, validated.ProductCode, cancellationToken);

        await Task.WhenAll(versionsTask, plansTask).ConfigureAwait(false);

        var versions = (await versionsTask.ConfigureAwait(false))
            .Where(v => v.FactoryId == validated.FactoryId)
            .Where(v => string.Equals(v.DatasetCode, ReportCodes.MonthlyProductionPlan, StringComparison.Ordinal))
            .ToList();

        // Fake 版本行为：仅 Published + Active。正式发布/激活/回退【待现场确认】。
        var activePublished = versions
            .Where(v => v.PublishStatus == DatasetPublishStatus.Published && v.IsActive)
            .ToList();
        var activeVersionIds = activePublished.Select(v => v.Id).ToHashSet();
        var versionById = versions.ToDictionary(v => v.Id);

        var activeVersion = activePublished
            .OrderByDescending(v => v.PublishedAtUtc?.Value ?? v.CreatedAtUtc.Value)
            .FirstOrDefault();

        // 强制工厂隔离；仅保留 Active Published 版本下的日计划原始行（不做月均摊）。
        var plans = (await plansTask.ConfigureAwait(false))
            .Where(p => p.FactoryId == validated.FactoryId)
            .Where(p => activeVersionIds.Contains(p.PlanVersionId))
            .Where(p => p.PlanDate.Year == validated.Year && p.PlanDate.Month == validated.Month)
            .ToList();

        var factories = await _queryService.GetFactoriesAsync(cancellationToken).ConfigureAwait(false);
        var workshops = await _queryService.GetWorkshopsAsync(validated.FactoryId, cancellationToken).ConfigureAwait(false);
        var lines = await _queryService
            .GetProductionLinesAsync(validated.FactoryId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var factoryCodeById = factories.ToDictionary(f => f.Id, f => f.Code);
        var workshopCodeById = workshops.ToDictionary(w => w.Id, w => w.Code);
        var lineCodeById = lines.ToDictionary(l => l.Id, l => l.Code);

        if (!factoryCodeById.ContainsKey(validated.FactoryId))
        {
            return BuildResponse(validated, [], activeVersion);
        }

        var rows = MapRows(plans, versionById, factoryCodeById, workshopCodeById, lineCodeById);
        return BuildResponse(validated, rows, activeVersion);
    }

    private MonthlyProductionPlanQueryResponse BuildResponse(
        ValidatedQuery validated,
        IReadOnlyList<MonthlyProductionPlanReportRow> rows,
        DatasetVersionState? activeVersion)
    {
        var mode = _dataAccessModeProvider.Mode;
        return new MonthlyProductionPlanQueryResponse
        {
            Meta = new MonthlyProductionPlanReportMeta
            {
                ReportCode = StableReportCodes.MonthlyProductionPlan,
                DataAccessMode = mode.ToString(),
                IsFake = _dataAccessModeProvider.IsFake,
                Filters = new MonthlyProductionPlanFilterEcho
                {
                    FactoryId = validated.FactoryId,
                    PlanMonth = validated.PlanMonth,
                    WorkshopId = validated.WorkshopId,
                    ProductionLineId = validated.ProductionLineId,
                    ProductCode = validated.ProductCode
                },
                ActivePlanVersionId = activeVersion?.Id,
                ActivePlanVersionNo = activeVersion?.VersionNo,
                DataSourceIdentifier = activeVersion is null
                    ? "FakeFixture:DeterministicFakeFixture:none-active"
                    : $"FakeFixture:DeterministicFakeFixture:{activeVersion.VersionNo}",
                GeneratedAtUtc = _clock.UtcNow.Value
            },
            Rows = rows
        };
    }

    /// <summary>
    /// 将夹具/仓储日计划行映射为响应行；保持 PlanDate 与 PlanQuantity 与原始行一致（无均摊逻辑）。
    /// </summary>
    internal static IReadOnlyList<MonthlyProductionPlanReportRow> MapRows(
        IReadOnlyList<DailyProductionPlanLine> plans,
        IReadOnlyDictionary<Guid, DatasetVersionState> versionById,
        IReadOnlyDictionary<long, string> factoryCodeById,
        IReadOnlyDictionary<long, string> workshopCodeById,
        IReadOnlyDictionary<long, string> lineCodeById)
    {
        return plans
            .OrderBy(p => p.PlanDate)
            .ThenBy(p => p.WorkshopId)
            .ThenBy(p => p.ProductionLineId ?? 0)
            .ThenBy(p => p.ProductCode, StringComparer.Ordinal)
            .Select(p =>
            {
                versionById.TryGetValue(p.PlanVersionId, out var version);
                string? lineCode = null;
                if (p.ProductionLineId is { } lineId)
                {
                    lineCode = lineCodeById.GetValueOrDefault(lineId);
                }

                return new MonthlyProductionPlanReportRow
                {
                    PlanDate = p.PlanDate,
                    FactoryId = p.FactoryId,
                    FactoryCode = factoryCodeById.GetValueOrDefault(p.FactoryId, string.Empty),
                    WorkshopId = p.WorkshopId,
                    WorkshopCode = workshopCodeById.GetValueOrDefault(p.WorkshopId, string.Empty),
                    ProductionLineId = p.ProductionLineId,
                    ProductionLineCode = lineCode,
                    ProductCode = p.ProductCode,
                    PlanQuantity = p.PlanQuantity,
                    PlanVersionId = p.PlanVersionId,
                    PlanVersionNo = version?.VersionNo ?? string.Empty,
                    PublishStatus = version?.PublishStatus.ToString() ?? string.Empty,
                    IsActive = version?.IsActive ?? false,
                    Remark = p.Remark
                };
            })
            .ToList();
    }

    private static ValidatedQuery Validate(MonthlyProductionPlanQueryRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.FactoryId is null)
        {
            errors["FactoryId"] = ["FactoryId is required."];
        }
        else if (request.FactoryId.Value <= 0)
        {
            errors["FactoryId"] = ["FactoryId must be a positive identifier."];
        }

        string? planMonth = null;
        int year = 0;
        int month = 0;
        if (string.IsNullOrWhiteSpace(request.PlanMonth))
        {
            errors["PlanMonth"] = ["PlanMonth is required (yyyy-MM)."];
        }
        else
        {
            planMonth = request.PlanMonth.Trim();
            if (!TryParseYearMonth(planMonth, out year, out month))
            {
                errors["PlanMonth"] = ["PlanMonth must be in yyyy-MM format."];
            }
        }

        if (request.WorkshopId is <= 0)
        {
            errors["WorkshopId"] = ["WorkshopId must be positive when provided."];
        }

        if (request.ProductionLineId is <= 0)
        {
            errors["ProductionLineId"] = ["ProductionLineId must be positive when provided."];
        }

        if (errors.Count > 0)
        {
            var detail = string.Join(" ", errors.SelectMany(e => e.Value));
            throw new ReportQueryValidationException(detail, errors);
        }

        var productCode = string.IsNullOrWhiteSpace(request.ProductCode)
            ? null
            : request.ProductCode.Trim();

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));

        return new ValidatedQuery(
            FactoryId: request.FactoryId!.Value,
            PlanMonth: planMonth!,
            Year: year,
            Month: month,
            MonthStart: monthStart,
            MonthEnd: monthEnd,
            WorkshopId: request.WorkshopId,
            ProductionLineId: request.ProductionLineId,
            ProductCode: productCode);
    }

    /// <summary>严格校验 yyyy-MM（四位年 + '-' + 两位月）。</summary>
    public static bool TryParseYearMonth(string value, out int year, out int month)
    {
        year = 0;
        month = 0;
        if (value.Length != 7 || value[4] != '-')
        {
            return false;
        }

        if (!int.TryParse(value.AsSpan(0, 4), out year)
            || !int.TryParse(value.AsSpan(5, 2), out month))
        {
            return false;
        }

        // 拒绝 "2026-3" 已由长度拦截；再拒绝 2026-00 / 2026-13
        return year is >= 1 and <= 9999 && month is >= 1 and <= 12;
    }

    private sealed record ValidatedQuery(
        long FactoryId,
        string PlanMonth,
        int Year,
        int Month,
        DateOnly MonthStart,
        DateOnly MonthEnd,
        long? WorkshopId,
        long? ProductionLineId,
        string? ProductCode);
}
