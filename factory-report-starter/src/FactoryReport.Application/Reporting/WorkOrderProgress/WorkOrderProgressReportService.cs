using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Security.DataScope;
using FactoryReport.Domain.Common;
using FactoryReport.Domain.MasterData;

namespace FactoryReport.Application.Reporting.WorkOrderProgress;

/// <summary>
/// 工单进度查询：按工厂隔离与筛选条件返回工单行，并计算 Fake 完成率 / 延期。
/// <para>
/// Fake 延期规则：未完成且未关闭（Status 非 Completed/Closed），且比较时间晚于 PlannedFinishUtc。
/// 『Fake 测试规则：现场须确认工单状态枚举、时区、延期口径与计划时间来源』。
/// </para>
/// <para>
/// Fake 完成率：ActualQuantity / PlannedQuantity；计划为 0 → null（同达成率零分母口径）。
/// </para>
/// </summary>
public sealed class WorkOrderProgressReportService : IWorkOrderProgressReportService
{
    /// <summary>
    /// Fake 临时「已完成/已关闭」状态集合。正式 MES 枚举【待现场确认】。
    /// 『仅用于开发测试，不代表现场 MES 正式枚举』。
    /// </summary>
    public static readonly HashSet<string> FakeCompletedOrClosedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed",
        "Closed"
    };

    private readonly IReportDataQueryService _queryService;
    private readonly IDataAccessModeProvider _dataAccessModeProvider;
    private readonly IUtcClock _clock;
    private readonly IReportQueryScopeService _queryScopeService;

    public WorkOrderProgressReportService(
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

    public async Task<WorkOrderProgressQueryResponse> QueryAsync(
        WorkOrderProgressQueryRequest request,
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

        var workOrders = await _queryService
            .GetWorkOrdersAsync(scope, validated.ProductCode, cancellationToken)
            .ConfigureAwait(false);

        // 强制工厂隔离：仓储已按 FactoryId 过滤；此处再断言，防止未来实现误串工厂。
        workOrders = workOrders.Where(w => w.FactoryId == validated.FactoryId).ToList();

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
            return BuildResponse(validated, []);
        }

        var comparedAt = _clock.UtcNow;
        var filtered = ApplyFilters(workOrders, validated);
        var rows = MapRows(filtered, factoryCodeById, workshopCodeById, lineCodeById, comparedAt);
        return BuildResponse(validated, rows, comparedAt);
    }

    private WorkOrderProgressQueryResponse BuildResponse(
        ValidatedQuery validated,
        IReadOnlyList<WorkOrderProgressReportRow> rows,
        UtcInstant? comparedAt = null)
    {
        var mode = _dataAccessModeProvider.Mode;
        var at = comparedAt ?? _clock.UtcNow;
        return new WorkOrderProgressQueryResponse
        {
            Meta = new WorkOrderProgressReportMeta
            {
                ReportCode = StableReportCodes.WorkOrderProgress,
                DataAccessMode = mode.ToString(),
                IsFake = _dataAccessModeProvider.IsFake,
                Filters = new WorkOrderProgressFilterEcho
                {
                    FactoryId = validated.FactoryId,
                    WorkshopId = validated.WorkshopId,
                    ProductionLineId = validated.ProductionLineId,
                    ProductCode = validated.ProductCode,
                    WorkOrderCode = validated.WorkOrderCode,
                    Status = validated.Status,
                    PlannedFinishFrom = validated.PlannedFinishFrom,
                    PlannedFinishTo = validated.PlannedFinishTo
                },
                ComparedAtUtc = at.Value,
                GeneratedAtUtc = at.Value
            },
            Rows = rows
        };
    }

    internal static IReadOnlyList<WorkOrder> ApplyFilters(
        IReadOnlyList<WorkOrder> workOrders,
        ValidatedQuery validated)
    {
        IEnumerable<WorkOrder> query = workOrders;

        if (validated.WorkOrderCode is not null)
        {
            query = query.Where(w =>
                string.Equals(w.WorkOrderNo, validated.WorkOrderCode, StringComparison.Ordinal));
        }

        if (validated.Status is not null)
        {
            query = query.Where(w =>
                w.StatusCode is not null
                && string.Equals(w.StatusCode, validated.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (validated.PlannedFinishFrom is { } from)
        {
            query = query.Where(w =>
                w.PlannedFinishAtUtc is not null
                && DateOnly.FromDateTime(w.PlannedFinishAtUtc.Value.Value.UtcDateTime) >= from);
        }

        if (validated.PlannedFinishTo is { } to)
        {
            query = query.Where(w =>
                w.PlannedFinishAtUtc is not null
                && DateOnly.FromDateTime(w.PlannedFinishAtUtc.Value.Value.UtcDateTime) <= to);
        }

        return query
            .OrderBy(w => w.WorkOrderNo, StringComparer.Ordinal)
            .ToList();
    }

    internal static IReadOnlyList<WorkOrderProgressReportRow> MapRows(
        IReadOnlyList<WorkOrder> workOrders,
        IReadOnlyDictionary<long, string> factoryCodeById,
        IReadOnlyDictionary<long, string> workshopCodeById,
        IReadOnlyDictionary<long, string> lineCodeById,
        UtcInstant comparedAt)
    {
        return workOrders
            .Select(w =>
            {
                var isCompleted = IsCompletedStatus(w.StatusCode);
                var plannedFinish = w.PlannedFinishAtUtc;
                var isOverdue = EvaluateOverdue(isCompleted, plannedFinish, comparedAt);

                return new WorkOrderProgressReportRow
                {
                    FactoryId = w.FactoryId,
                    FactoryCode = factoryCodeById.GetValueOrDefault(w.FactoryId, string.Empty),
                    WorkshopId = w.WorkshopId,
                    WorkshopCode = w.WorkshopId is { } wid
                        ? workshopCodeById.GetValueOrDefault(wid)
                        : null,
                    ProductionLineId = w.ProductionLineId,
                    ProductionLineCode = w.ProductionLineId is { } lid
                        ? lineCodeById.GetValueOrDefault(lid)
                        : null,
                    WorkOrderCode = w.WorkOrderNo,
                    ProductCode = w.ProductCode,
                    Status = w.StatusCode,
                    PlannedQuantity = w.PlanQuantity,
                    ActualQuantity = w.CompletedQuantity,
                    RemainingQuantity = ComputeRemaining(w.PlanQuantity, w.CompletedQuantity),
                    CompletionRate = ComputeCompletionRate(w.PlanQuantity, w.CompletedQuantity),
                    PlannedStartUtc = w.PlannedStartAtUtc?.Value,
                    PlannedFinishUtc = plannedFinish?.Value,
                    IsCompleted = isCompleted,
                    IsOverdue = isOverdue
                };
            })
            .ToList();
    }

    /// <summary>
    /// Fake：Status 为 Completed 或 Closed（忽略大小写）视为已完成/已关闭。
    /// 『仅用于开发测试，不代表现场 MES 正式枚举』。
    /// </summary>
    public static bool IsCompletedStatus(string? statusCode)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
        {
            return false;
        }

        return FakeCompletedOrClosedStatuses.Contains(statusCode.Trim());
    }

    /// <summary>
    /// Fake 延期：未完成/未关闭，且 PlannedFinishUtc 有值，且 comparedAt &gt; PlannedFinishUtc。
    /// 『Fake 测试规则：现场须确认工单状态枚举、时区、延期口径与计划时间来源』。
    /// </summary>
    public static bool EvaluateOverdue(
        bool isCompleted,
        UtcInstant? plannedFinishAtUtc,
        UtcInstant comparedAt)
    {
        if (isCompleted)
        {
            return false;
        }

        if (plannedFinishAtUtc is null)
        {
            return false;
        }

        return comparedAt.CompareTo(plannedFinishAtUtc.Value) > 0;
    }

    /// <summary>
    /// Fake 完成率：Actual / Planned；计划为 0 → null（同达成率 PlanIsZero）。
    /// </summary>
    public static decimal? ComputeCompletionRate(decimal plannedQuantity, decimal actualQuantity)
    {
        if (plannedQuantity == 0m)
        {
            return null;
        }

        return actualQuantity / plannedQuantity;
    }

    /// <summary>
    /// Fake 剩余：超报时显示 0。『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public static decimal ComputeRemaining(decimal plannedQuantity, decimal actualQuantity)
    {
        var remaining = plannedQuantity - actualQuantity;
        return remaining < 0m ? 0m : remaining;
    }

    private static ValidatedQuery Validate(WorkOrderProgressQueryRequest request)
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

        if (request.WorkshopId is <= 0)
        {
            errors["WorkshopId"] = ["WorkshopId must be positive when provided."];
        }

        if (request.ProductionLineId is <= 0)
        {
            errors["ProductionLineId"] = ["ProductionLineId must be positive when provided."];
        }

        if (request.PlannedFinishFrom is { } from
            && request.PlannedFinishTo is { } to
            && from > to)
        {
            errors["PlannedFinishDateRange"] =
                ["PlannedFinishFrom must not be later than PlannedFinishTo."];
        }

        // 单端提供另一端缺失时允许；仅校验成对时的顺序。
        if (errors.Count > 0)
        {
            var detail = string.Join(" ", errors.SelectMany(e => e.Value));
            throw new ReportQueryValidationException(detail, errors);
        }

        return new ValidatedQuery(
            FactoryId: request.FactoryId!.Value,
            WorkshopId: request.WorkshopId,
            ProductionLineId: request.ProductionLineId,
            ProductCode: TrimOrNull(request.ProductCode),
            WorkOrderCode: TrimOrNull(request.WorkOrderCode),
            Status: TrimOrNull(request.Status),
            PlannedFinishFrom: request.PlannedFinishFrom,
            PlannedFinishTo: request.PlannedFinishTo);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal sealed record ValidatedQuery(
        long FactoryId,
        long? WorkshopId,
        long? ProductionLineId,
        string? ProductCode,
        string? WorkOrderCode,
        string? Status,
        DateOnly? PlannedFinishFrom,
        DateOnly? PlannedFinishTo);
}
