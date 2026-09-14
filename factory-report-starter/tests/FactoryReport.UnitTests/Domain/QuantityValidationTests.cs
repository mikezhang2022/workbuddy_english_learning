using FactoryReport.Domain.Common;
using FactoryReport.Domain.Production;

namespace FactoryReport.UnitTests.Domain;

public class QuantityValidationTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    public void Production_quantities_reject_negatives(decimal negative)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProductionQuantities(negative, 0, 0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProductionQuantities(0, negative, 0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProductionQuantities(0, 0, negative, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProductionQuantities(0, 0, 0, negative, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProductionQuantities(0, 0, 0, 0, negative, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProductionQuantities(0, 0, 0, 0, 0, negative));
    }

    [Fact]
    public void Production_record_keeps_quantity_columns_separate()
    {
        var quantities = new ProductionQuantities(
            actualQuantity: 100m,
            goodQuantity: 90m,
            defectQuantity: 5m,
            scrapQuantity: 3m,
            reworkQuantity: 2m,
            inspectedQuantity: 95m);

        var record = new ProductionRecord(
            factoryId: 1,
            workshopId: 2,
            productionLineId: 3,
            productionDate: new DateOnly(2026, 9, 1),
            productCode: "P-100",
            quantities: quantities,
            dataUpdatedAtUtc: UtcInstant.Now());

        Assert.Equal(100m, record.Quantities.ActualQuantity);
        Assert.Equal(90m, record.Quantities.GoodQuantity);
        Assert.Equal(5m, record.Quantities.DefectQuantity);
        Assert.Equal(3m, record.Quantities.ScrapQuantity);
        Assert.Equal(2m, record.Quantities.ReworkQuantity);
        Assert.Equal(95m, record.Quantities.InspectedQuantity);
    }

    [Fact]
    public void Utc_instant_rejects_non_zero_offset()
    {
        var offset = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.FromHours(8));
        Assert.Throws<ArgumentException>(() => new UtcInstant(offset));
    }
}
