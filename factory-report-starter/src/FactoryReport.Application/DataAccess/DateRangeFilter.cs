namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 业务日期闭区间筛选（含起止日）。用于生产事实与日计划查询，避免一次加载全部数据。
/// </summary>
public sealed class DateRangeFilter
{
    public DateOnly FromInclusive { get; }
    public DateOnly ToInclusive { get; }

    public DateRangeFilter(DateOnly fromInclusive, DateOnly toInclusive)
    {
        if (fromInclusive == default)
        {
            throw new ArgumentException("FromInclusive is required.", nameof(fromInclusive));
        }

        if (toInclusive == default)
        {
            throw new ArgumentException("ToInclusive is required.", nameof(toInclusive));
        }

        if (toInclusive < fromInclusive)
        {
            throw new ArgumentException("ToInclusive must be greater than or equal to FromInclusive.", nameof(toInclusive));
        }

        FromInclusive = fromInclusive;
        ToInclusive = toInclusive;
    }

    public bool Contains(DateOnly date) => date >= FromInclusive && date <= ToInclusive;
}
