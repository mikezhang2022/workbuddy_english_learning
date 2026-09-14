using FactoryReport.Domain.Common;

namespace FactoryReport.Infrastructure.Time;

/// <summary>
/// 系统 UTC 时钟实现。
/// </summary>
public sealed class SystemUtcClock : Application.Abstractions.IUtcClock
{
    public UtcInstant UtcNow => UtcInstant.Now();
}
