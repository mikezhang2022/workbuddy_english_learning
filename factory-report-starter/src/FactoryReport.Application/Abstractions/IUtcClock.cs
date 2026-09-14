using FactoryReport.Domain.Common;

namespace FactoryReport.Application.Abstractions;

/// <summary>
/// 应用层时钟抽象：一律返回 UTC。
/// </summary>
public interface IUtcClock
{
    UtcInstant UtcNow { get; }
}
