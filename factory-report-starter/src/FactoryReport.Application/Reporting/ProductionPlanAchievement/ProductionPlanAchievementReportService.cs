using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Security.DataScope;
using FactoryReport.Domain.Planning;
using FactoryReport.Domain.Production;
using FactoryReport.Domain.Reporting;

namespace FactoryReport.Application.Reporting.ProductionPlanAchievement;

/// <summary>
/// 生产计划达成组合查询：按已确认关联键匹配日计划与实际产量，并计算达成率。
/// <list type="bullet">
/// <item>有计划且计划 &gt; 0：AchievementRate = Actual / Plan（Calculated）</item>
/// <item>计划为 0：AchievementRate = null（PlanIsZero）</item>
/// <item>有实际无计划：AchievementRate = null（PlanNotConfigured；不得展示为 0%）</item>
/// <item>有计划无实际：Actual = 0，AchievementRate = 0（MissingActual）</item>
/// <item>不得用月计划平均推算日计划</item>
/// </list>
/// 边界规则为已确认产品规则；当前数量来源为 Fake 夹具。
/// </summary>
public sealed class ProductionPlanAchievementReportService : IProductionPlanAchievementReportService
{
    private readonly IReportDataQueryService _queryService;
    private readonly IDataAccessModeProvider _dataAccessModeProvider;
    private readonly IUtcClock _clock;
    private readonly PlanAchievementEvaluator _evaluator;
    private readonly IReportQueryScopeService _queryScopeService;

    public ProductionPlanAchievementReportService(
        IReportDataQueryService queryService,
        IDataAccessModeProvider dataAccessModeProvider,
        IUtcClock clock,
        IReportQueryScopeService queryScopeService,
        PlanAchievementEvaluator? evaluator = null)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _dataAccessModeProvider = dataAccessModeProvider ?? throw new ArgumentNullException(nameof(dataAccessModeProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _queryScopeService = queryScopeService ?? throw new ArgumentNullException(nameof(queryScopeService));
        _evaluator = evaluator ?? new PlanAchievementEvaluator();
    }

    public async Task<ProductionPlanAchievementQueryResponse> QueryAsync(
        ProductionPlanAchievementQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validated = Validate(request);

        var scope = await _queryScopeService
            .ResolveEffectiveOrganizationScopeAsync(
                validated.FactoryId,
                validated.WorkshopId,
                validated.ProductionLineId,
                cancellationToken)
            .ConfigureAwait(false);
        var dateRange = new DateRangeFilter(validated.StartDate, validated.EndDate);

        var recordsTask = _queryService.GetProductionRecordsAsync(
            scope, dateRange, validated.ProductCode, cancellationToken);
        var plansTask = _queryService.GetDailyPlanLinesAsync(
            scope, dateRange, validated.ProductCode, cancellationToken);

        await Task.WhenAll(recordsTask, plansTask).ConfigureAwait(false);

        // 强制工厂隔离：仓储已按 FactoryId 过滤；此处再断言，防止未来实现误串工厂。
        var records = (await recordsTask.ConfigureAwait(false))
            .Where(r => r.FactoryId == validated.FactoryId)
            .ToList();
        var plans = (await plansTask.ConfigureAwait(false))
            .Where(p => p.FactoryId == validated.FactoryId)
            .ToList();

        var factories = await _queryService.GetFactoriesAsync(cancellationToken).ConfigureAwait(false);
        var workshops = await _queryService.GetWorkshopsAsync(validated.FactoryId, cancellationToken).ConfigureAwait(false);
        var lines = await _queryService
            .GetProductionLinesAsync(validated.FactoryId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var factoryCodeById = factories.ToDictionary(f => f.Id, f => f.Code);
        var workshopCodeById = workshops.ToDictionary(w => w.Id, w => w.Code);
        var lineCodeById = lines.ToDictionary(l => l.Id, l => l.Code);

        // 未知 FactoryId：不串数据，返回空结果（非错误）。
        if (!factoryCodeById.ContainsKey(validated.FactoryId))
        {
            return BuildResponse(validated, []);
        }

        var rows = Combine(records, plans, factoryCodeById, workshopCodeById, lineCodeById);
        return BuildResponse(validated, rows);
    }

    private ProductionPlanAchievementQueryResponse BuildResponse(
        ValidatedQuery validated,
        IReadOnlyList<ProductionPlanAchievementReportRow> rows)
    {
        var mode = _dataAccessModeProvider.Mode;
        return new ProductionPlanAchievementQueryResponse
        {
            Meta = new ProductionPlanAchievementReportMeta
            {
                ReportCode = StableReportCodes.ProductionPlanAchievement,
                DataAccessMode = mode.ToString(),
                IsFake = _dataAccessModeProvider.IsFake,
                Filters = new ProductionPlanAchievementFilterEcho
                {
                    FactoryId = validated.FactoryId,
                    StartDate = validated.StartDate,
                    EndDate = validated.EndDate,
                    WorkshopId = validated.WorkshopId,
                    ProductionLineId = validated.ProductionLineId,
                    ProductCode = validated.ProductCode
                },
                GeneratedAtUtc = _clock.UtcNow.Value
            },
            Rows = rows
        };
    }

    internal IReadOnlyList<ProductionPlanAchievementReportRow> Combine(
        IReadOnlyList<ProductionRecord> records,
        IReadOnlyList<DailyProductionPlanLine> plans,
        IReadOnlyDictionary<long, string> factoryCodeById,
        IReadOnlyDictionary<long, string> workshopCodeById,
        IReadOnlyDictionary<long, string> lineCodeById)
    {
        // 实际：按关联键汇总 ActualQuantity（同键多班次求和）。
        var actualByKey = records
            .GroupBy(r => new AchievementJoinKey(
                r.FactoryId,
                r.WorkshopId,
                r.ProductionLineId,
                r.ProductionDate,
                r.ProductCode))
            .ToDictionary(
                g => g.Key,
                g => (decimal?)g.Sum(x => x.Quantities.ActualQuantity));

        // 日计划：仅保留含产线的行（关联键要求 ProductionLineId）；同键求和。
        // 不得用月计划平均推算日计划——本服务只读 DailyProductionPlanLine。
        var planByKey = plans
            .Where(p => p.ProductionLineId is > 0)
            .GroupBy(p => new AchievementJoinKey(
                p.FactoryId,
                p.WorkshopId,
                p.ProductionLineId!.Value,
                p.PlanDate,
                p.ProductCode))
            .ToDictionary(
                g => g.Key,
                g => (decimal?)g.Sum(x => x.PlanQuantity));

        var allKeys = actualByKey.Keys
            .Union(planByKey.Keys)
            .OrderBy(k => k.ProductionDate)
            .ThenBy(k => k.FactoryId)
            .ThenBy(k => k.WorkshopId)
            .ThenBy(k => k.ProductionLineId)
            .ThenBy(k => k.ProductCode, StringComparer.Ordinal);

        var rows = new List<ProductionPlanAchievementReportRow>();
        foreach (var joinKey in allKeys)
        {
            actualByKey.TryGetValue(joinKey, out var actualQuantity);
            planByKey.TryGetValue(joinKey, out var planQuantity);

            var domainKey = new PlanAchievementKey(
                joinKey.FactoryId,
                joinKey.WorkshopId,
                joinKey.ProductionLineId,
                joinKey.ProductionDate,
                joinKey.ProductCode);

            var result = _evaluator.Evaluate(domainKey, planQuantity, actualQuantity);

            rows.Add(new ProductionPlanAchievementReportRow
            {
                ProductionDate = joinKey.ProductionDate,
                FactoryId = joinKey.FactoryId,
                FactoryCode = factoryCodeById.GetValueOrDefault(joinKey.FactoryId, string.Empty),
                WorkshopId = joinKey.WorkshopId,
                WorkshopCode = workshopCodeById.GetValueOrDefault(joinKey.WorkshopId, string.Empty),
                ProductionLineId = joinKey.ProductionLineId,
                ProductionLineCode = lineCodeById.GetValueOrDefault(joinKey.ProductionLineId, string.Empty),
                ProductCode = joinKey.ProductCode,
                PlanQuantity = result.PlanQuantity,
                ActualQuantity = result.ActualQuantity,
                AchievementRate = result.AchievementRate,
                PlanStatus = result.Status.ToString()
            });
        }

        return rows;
    }

    private static ValidatedQuery Validate(ProductionPlanAchievementQueryRequest request)
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

        if (request.StartDate is null || request.StartDate.Value == default)
        {
            errors["StartDate"] = ["StartDate is required (business production date, DateOnly)."];
        }

        if (request.EndDate is null || request.EndDate.Value == default)
        {
            errors["EndDate"] = ["EndDate is required (business production date, DateOnly)."];
        }

        if (request.WorkshopId is <= 0)
        {
            errors["WorkshopId"] = ["WorkshopId must be positive when provided."];
        }

        if (request.ProductionLineId is <= 0)
        {
            errors["ProductionLineId"] = ["ProductionLineId must be positive when provided."];
        }

        if (request.StartDate is { } start
            && request.EndDate is { } end
            && start != default
            && end != default
            && start > end)
        {
            errors["DateRange"] = ["StartDate must not be later than EndDate."];
        }

        if (errors.Count > 0)
        {
            var detail = string.Join(" ", errors.SelectMany(e => e.Value));
            throw new ReportQueryValidationException(detail, errors);
        }

        var productCode = string.IsNullOrWhiteSpace(request.ProductCode)
            ? null
            : request.ProductCode.Trim();

        return new ValidatedQuery(
            FactoryId: request.FactoryId!.Value,
            StartDate: request.StartDate!.Value,
            EndDate: request.EndDate!.Value,
            WorkshopId: request.WorkshopId,
            ProductionLineId: request.ProductionLineId,
            ProductCode: productCode);
    }

    private sealed record ValidatedQuery(
        long FactoryId,
        DateOnly StartDate,
        DateOnly EndDate,
        long? WorkshopId,
        long? ProductionLineId,
        string? ProductCode);

    private readonly record struct AchievementJoinKey(
        long FactoryId,
        long WorkshopId,
        long ProductionLineId,
        DateOnly ProductionDate,
        string ProductCode);
}
