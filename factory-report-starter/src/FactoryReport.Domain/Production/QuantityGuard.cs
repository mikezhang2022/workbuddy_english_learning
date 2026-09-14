namespace FactoryReport.Domain.Production;

/// <summary>
/// 数量非负校验。冲销是否允许负数量【待现场确认】，当前领域对象拒绝负数。
/// </summary>
public static class QuantityGuard
{
    public static void EnsureNonNegative(decimal value, string paramName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Quantity must not be negative.");
        }
    }
}
