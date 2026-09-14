using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Security.DataScope;
using FactoryReport.Domain.Production;

namespace FactoryReport.Application.Reporting.ProductionDaily;

/// <summary>
/// 生产日报聚合查询：按「生产日期 + 工厂/车间/产线 + 产品」汇总数量并计算 Fake 良率。
/// 良率公式（Fake）：YieldRate = GoodQuantity / InspectionQuantity；分母为 0 时返回 null。
/// 『Fake 测试口径，现场 MES 接入前须确认』。
/// </summary>
public sealed class ProductionDailyReportService : IProductionDailyReportService
{
    private readonly IReportDataQueryService _queryService;
    private readonly IDataAccessModeProvider _dataAccessModeProvider;
    private readonly IUtcClock _clock;
    private readonly IReportQueryScopeService _queryScopeService;

    public ProductionDailyReportService(
        IReportDataQueryService queryService,
        IDataAccessModeProvider dataAccessModeProvider,
        IUtcClock clock,
        IReportQueryScopeService queryScopeService)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _dataAccessModeProvider = dataAccessModeProvider ?? throw new ArgumentNullException(nameof(dataAccessModeProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _queryScopeService = queryScopeService ?? throw new ArgumentNullException(nameof(queryScopeService));
    }

    public async Task<ProductionDailyQueryResponse> QueryAsync(
        ProductionDailyQueryRequest request,
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

        var records = await _queryService
            .GetProductionRecordsAsync(scope, dateRange, validated.ProductCode, cancellationToken)
            .ConfigureAwait(false);

        // 强制工厂隔离：仓储已按 FactoryId 过滤；此处再断言，防止未来实现误串工厂。
        records = records.Where(r => r.FactoryId == validated.FactoryId).ToList();

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

        var rows = Aggregate(records, factoryCodeById, workshopCodeById, lineCodeById);
        return BuildResponse(validated, rows);
    }

    private ProductionDailyQueryResponse BuildResponse(
        ValidatedQuery validated,
        IReadOnlyList<ProductionDailyReportRow> rows)
    {
        var mode = _dataAccessModeProvider.Mode;
        return new ProductionDailyQueryResponse
        {
            Meta = new ProductionDailyReportMeta
            {
                ReportCode = StableReportCodes.ProductionDaily,
                DataAccessMode = mode.ToString(),
                IsFake = _dataAccessModeProvider.IsFake,
                Filters = new ProductionDailyFilterEcho
                {
                    FactoryId = validated.FactoryId,
                    StartDate = validated.StartDate,
                    EndDate = validated.EndDate,
                    WorkshopId = validated.WorkshopId,
                    ProductionLineId = validated.ProductionLineId,
                    ProductCode = validated.ProductCode
                },
                YieldRateFormula =
                    "GoodQuantity / InspectionQuantity (null when InspectionQuantity = 0)",
                YieldRateDisclaimer = "Fake 测试口径，现场 MES 接入前须确认",
                GeneratedAtUtc = _clock.UtcNow.Value
            },
            Rows = rows
        };
    }

    internal static IReadOnlyList<ProductionDailyReportRow> Aggregate(
        IReadOnlyList<ProductionRecord> records,
        IReadOnlyDictionary<long, string> factoryCodeById,
        IReadOnlyDictionary<long, string> workshopCodeById,
        IReadOnlyDictionary<long, string> lineCodeById)
    {
        return records
            .GroupBy(r => new
            {
                r.ProductionDate,
                r.FactoryId,
                r.WorkshopId,
                r.ProductionLineId,
                r.ProductCode
            })
            .OrderBy(g => g.Key.ProductionDate)
            .ThenBy(g => g.Key.FactoryId)
            .ThenBy(g => g.Key.WorkshopId)
            .ThenBy(g => g.Key.ProductionLineId)
            .ThenBy(g => g.Key.ProductCode, StringComparer.Ordinal)
            .Select(g =>
            {
                var actual = g.Sum(x => x.Quantities.ActualQuantity);
                var good = g.Sum(x => x.Quantities.GoodQuantity);
                var defect = g.Sum(x => x.Quantities.DefectQuantity);
                var scrap = g.Sum(x => x.Quantities.ScrapQuantity);
                var rework = g.Sum(x => x.Quantities.ReworkQuantity);
                var inspection = g.Sum(x => x.Quantities.InspectedQuantity);

                return new ProductionDailyReportRow
                {
                    ProductionDate = g.Key.ProductionDate,
                    FactoryId = g.Key.FactoryId,
                    FactoryCode = factoryCodeById.GetValueOrDefault(g.Key.FactoryId, string.Empty),
                    WorkshopId = g.Key.WorkshopId,
                    WorkshopCode = workshopCodeById.GetValueOrDefault(g.Key.WorkshopId, string.Empty),
                    ProductionLineId = g.Key.ProductionLineId,
                    ProductionLineCode = lineCodeById.GetValueOrDefault(g.Key.ProductionLineId, string.Empty),
                    ProductCode = g.Key.ProductCode,
                    ActualQuantity = actual,
                    GoodQuantity = good,
                    DefectQuantity = defect,
                    ScrapQuantity = scrap,
                    ReworkQuantity = rework,
                    InspectionQuantity = inspection,
                    // Fake 临时口径：GoodQuantity / InspectionQuantity；分母 0 → null。
                    // 『Fake 测试口径，现场 MES 接入前须确认』
                    YieldRate = ComputeYieldRate(good, inspection)
                };
            })
            .ToList();
    }

    /// <summary>
    /// Fake 良率：GoodQuantity / InspectionQuantity；InspectionQuantity 为 0 时返回 null。
    /// 『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public static decimal? ComputeYieldRate(decimal goodQuantity, decimal inspectionQuantity)
    {
        if (inspectionQuantity == 0m)
        {
            return null;
        }

        return goodQuantity / inspectionQuantity;
    }

    private static ValidatedQuery Validate(ProductionDailyQueryRequest request)
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
}
