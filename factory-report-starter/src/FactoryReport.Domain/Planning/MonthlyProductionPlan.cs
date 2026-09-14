using System.Collections.ObjectModel;
using FactoryReport.Domain.Common;

namespace FactoryReport.Domain.Planning;

/// <summary>
/// 月度生产计划头。日计划行集合必须各自携带 PlanDate（不得由月总量均摊推算）。
/// <see cref="PlanVersionId"/> 关联数据集版本；正式发布/激活规则【待现场确认】。
/// </summary>
public sealed class MonthlyProductionPlan
{
    private readonly List<DailyProductionPlanLine> _lines;

    public long Id { get; }
    public long FactoryId { get; }
    public string PlanYearMonth { get; }

    /// <summary>
    /// 所属计划数据版本 Id。Fake 过滤 Published/Active；正式规则【待现场确认】。
    /// </summary>
    public Guid PlanVersionId { get; }

    public IReadOnlyList<DailyProductionPlanLine> Lines { get; }
    public UtcInstant CreatedAtUtc { get; }

    public MonthlyProductionPlan(
        long id,
        long factoryId,
        string planYearMonth,
        Guid planVersionId,
        IEnumerable<DailyProductionPlanLine> lines,
        UtcInstant createdAtUtc)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Plan Id must be a positive identifier.");
        }

        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        if (planVersionId == Guid.Empty)
        {
            throw new ArgumentException("PlanVersionId must be a non-empty GUID.", nameof(planVersionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(planYearMonth);
        ArgumentNullException.ThrowIfNull(lines);

        if (!IsValidYearMonth(planYearMonth.Trim()))
        {
            throw new ArgumentException("PlanYearMonth must be in YYYY-MM format.", nameof(planYearMonth));
        }

        var materialized = lines.ToList();
        foreach (var line in materialized)
        {
            if (line.FactoryId != factoryId)
            {
                throw new ArgumentException("Daily plan line FactoryId must match the monthly plan FactoryId.", nameof(lines));
            }

            if (line.PlanVersionId != planVersionId)
            {
                throw new ArgumentException("Daily plan line PlanVersionId must match the monthly plan PlanVersionId.", nameof(lines));
            }
        }

        Id = id;
        FactoryId = factoryId;
        PlanYearMonth = planYearMonth.Trim();
        PlanVersionId = planVersionId;
        _lines = materialized;
        Lines = new ReadOnlyCollection<DailyProductionPlanLine>(_lines);
        CreatedAtUtc = createdAtUtc;
    }

    private static bool IsValidYearMonth(string value)
    {
        if (value.Length != 7 || value[4] != '-')
        {
            return false;
        }

        return int.TryParse(value.AsSpan(0, 4), out var year)
            && int.TryParse(value.AsSpan(5, 2), out var month)
            && year is >= 1 and <= 9999
            && month is >= 1 and <= 12;
    }
}
