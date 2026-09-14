namespace FactoryReport.Domain.Production;

/// <summary>
/// 生产数量值对象：实际/良品/不良/报废/返工/检验分列保存，不同步层混合计算。
/// </summary>
public sealed class ProductionQuantities : IEquatable<ProductionQuantities>
{
    public decimal ActualQuantity { get; }
    public decimal GoodQuantity { get; }
    public decimal DefectQuantity { get; }
    public decimal ScrapQuantity { get; }
    public decimal ReworkQuantity { get; }
    public decimal InspectedQuantity { get; }

    public ProductionQuantities(
        decimal actualQuantity,
        decimal goodQuantity,
        decimal defectQuantity,
        decimal scrapQuantity,
        decimal reworkQuantity,
        decimal inspectedQuantity)
    {
        QuantityGuard.EnsureNonNegative(actualQuantity, nameof(actualQuantity));
        QuantityGuard.EnsureNonNegative(goodQuantity, nameof(goodQuantity));
        QuantityGuard.EnsureNonNegative(defectQuantity, nameof(defectQuantity));
        QuantityGuard.EnsureNonNegative(scrapQuantity, nameof(scrapQuantity));
        QuantityGuard.EnsureNonNegative(reworkQuantity, nameof(reworkQuantity));
        QuantityGuard.EnsureNonNegative(inspectedQuantity, nameof(inspectedQuantity));

        ActualQuantity = actualQuantity;
        GoodQuantity = goodQuantity;
        DefectQuantity = defectQuantity;
        ScrapQuantity = scrapQuantity;
        ReworkQuantity = reworkQuantity;
        InspectedQuantity = inspectedQuantity;
    }

    public static ProductionQuantities Zero { get; } = new(0m, 0m, 0m, 0m, 0m, 0m);

    public bool Equals(ProductionQuantities? other)
    {
        if (other is null)
        {
            return false;
        }

        return ActualQuantity == other.ActualQuantity
            && GoodQuantity == other.GoodQuantity
            && DefectQuantity == other.DefectQuantity
            && ScrapQuantity == other.ScrapQuantity
            && ReworkQuantity == other.ReworkQuantity
            && InspectedQuantity == other.InspectedQuantity;
    }

    public override bool Equals(object? obj) => Equals(obj as ProductionQuantities);

    public override int GetHashCode() =>
        HashCode.Combine(ActualQuantity, GoodQuantity, DefectQuantity, ScrapQuantity, ReworkQuantity, InspectedQuantity);
}
