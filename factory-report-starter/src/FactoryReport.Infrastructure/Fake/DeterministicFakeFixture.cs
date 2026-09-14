using FactoryReport.Domain.Common;
using FactoryReport.Domain.Import;
using FactoryReport.Domain.MasterData;
using FactoryReport.Domain.Organizations;
using FactoryReport.Domain.Planning;
using FactoryReport.Domain.Production;
using FactoryReport.Domain.Reporting;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// 确定性 Fake 夹具快照（进程内只读）。
/// 『仅用于开发测试，不代表现场 MES 正式口径』；不含真实生产数据。
/// </summary>
public sealed class FakeFixtureSnapshot
{
    public IReadOnlyList<Factory> Factories { get; }
    public IReadOnlyList<Workshop> Workshops { get; }
    public IReadOnlyList<ProductionLine> ProductionLines { get; }
    public IReadOnlyList<Product> Products { get; }
    public IReadOnlyList<WorkOrder> WorkOrders { get; }
    public IReadOnlyList<ProductionRecord> ProductionRecords { get; }
    public IReadOnlyList<DailyProductionPlanLine> DailyPlanLines { get; }
    public IReadOnlyList<ImportBatch> ImportBatches { get; }
    public IReadOnlyList<DatasetVersionState> DatasetVersions { get; }

    public FakeFixtureSnapshot(
        IReadOnlyList<Factory> factories,
        IReadOnlyList<Workshop> workshops,
        IReadOnlyList<ProductionLine> productionLines,
        IReadOnlyList<Product> products,
        IReadOnlyList<WorkOrder> workOrders,
        IReadOnlyList<ProductionRecord> productionRecords,
        IReadOnlyList<DailyProductionPlanLine> dailyPlanLines,
        IReadOnlyList<ImportBatch> importBatches,
        IReadOnlyList<DatasetVersionState> datasetVersions)
    {
        Factories = factories;
        Workshops = workshops;
        ProductionLines = productionLines;
        Products = products;
        WorkOrders = workOrders;
        ProductionRecords = productionRecords;
        DailyPlanLines = dailyPlanLines;
        ImportBatches = importBatches;
        DatasetVersions = datasetVersions;
    }
}

/// <summary>
/// 确定性夹具常量与工厂方法。每次 <see cref="Create"/> 结果一致，不依赖 DateTime.Now/UtcNow。
/// 『仅用于开发测试，不代表现场 MES 正式口径』。
/// </summary>
public static class DeterministicFakeFixture
{
    // —— 固定 UTC 业务日期（不得用系统时钟）——
    public static readonly DateOnly Day1 = new(2026, 3, 10);
    public static readonly DateOnly Day2 = new(2026, 3, 11);
    public static readonly DateOnly Day3 = new(2026, 3, 12);
    public static readonly DateOnly MinDate = Day1;
    public static readonly DateOnly MaxDate = Day3;

    /// <summary>夹具内所有 DataUpdatedAt / CreatedAt 使用的固定 UTC 时刻。</summary>
    public static readonly UtcInstant FixedUpdatedAtUtc =
        UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));

    public static readonly UtcInstant FixedPublishedAtUtc =
        UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc));

    /// <summary>工单计划开始（固定 UTC）。</summary>
    public static readonly UtcInstant FixedPlannedStartUtc =
        UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));

    /// <summary>工单计划完成（固定 UTC；Day3 16:00）。</summary>
    public static readonly UtcInstant FixedPlannedFinishUtc =
        UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 12, 16, 0, 0, DateTimeKind.Utc));

    /// <summary>较早计划完成（Day2 12:00），供延期场景。</summary>
    public static readonly UtcInstant EarlyPlannedFinishUtc =
        UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 11, 12, 0, 0, DateTimeKind.Utc));

    /// <summary>Fake 时钟断言常用「当前时间」：晚于 Early、早于 Fixed 计划完成。</summary>
    public static readonly UtcInstant FakeComparedAtUtc =
        UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 12, 10, 0, 0, DateTimeKind.Utc));

    // —— Fake 工单状态（临时，非 MES 正式枚举）【待现场确认】——
    public const string StatusOpen = "Open";
    public const string StatusCompleted = "Completed";
    public const string StatusClosed = "Closed";

    public const string WorkOrderNormal = "WO-DEMO-1001";
    public const string WorkOrderPlanZero = "WO-DEMO-1002";
    public const string WorkOrderWorkshopB = "WO-DEMO-B001";
    public const string WorkOrderFactory2 = "WO-DEMO-2001";
    public const string WorkOrderClosed = "WO-DEMO-CLOSED";
    public const string WorkOrderCompleted = "WO-DEMO-DONE";
    public const string WorkOrderOverdueOpen = "WO-DEMO-OVERDUE";

    // —— 组织 ——
    public const long FactoryDemo1Id = 1;
    public const long FactoryDemo2Id = 2;
    public const string FactoryDemo1Code = "F-DEMO-01";
    public const string FactoryDemo2Code = "F-DEMO-02";

    public const long WorkshopAId = 10;
    public const long WorkshopBId = 20;
    public const long WorkshopXId = 30;

    public const long LineA1Id = 101;
    public const long LineA2Id = 102;
    public const long LineB1Id = 201;
    public const long LineX1Id = 301;

    // —— 产品编码（场景标签）——
    /// <summary>场景 a+e：正常有计划有实际，且数量分列完整。</summary>
    public const string ProductNormal = "PROD-NORMAL";

    /// <summary>场景 b：计划为 0（达成率 null）。</summary>
    public const string ProductPlanZero = "PROD-PLAN-ZERO";

    /// <summary>场景 c：有实际无计划（未配置计划）。</summary>
    public const string ProductActualOnly = "PROD-ACTUAL-ONLY";

    /// <summary>场景 d：有计划无实际。</summary>
    public const string ProductPlanOnly = "PROD-PLAN-ONLY";

    /// <summary>场景 f：工厂 2 隔离数据。</summary>
    public const string ProductFactory2 = "PROD-F2";

    /// <summary>Day2 用于日期范围筛选。</summary>
    public const string ProductDay2 = "PROD-DAY2";

    /// <summary>质量统计：检验数为 0 → YieldRate/DefectRate 均为 null。</summary>
    public const string ProductZeroInspection = "PROD-ZERO-INSP";

    public static readonly Guid ImportBatchFactory1Id = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid ImportBatchFactory2Id = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid DatasetVersionFactory1Id = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    public static readonly Guid DatasetVersionFactory2Id = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    /// <summary>
    /// 创建全新只读快照（确定性）。『仅用于开发测试，不代表现场 MES 正式口径』。
    /// </summary>
    public static FakeFixtureSnapshot Create()
    {
        var factories = new[]
        {
            new Factory(FactoryDemo1Id, FactoryDemo1Code, "Demo Factory One"),
            new Factory(FactoryDemo2Id, FactoryDemo2Code, "Demo Factory Two")
        };

        var workshops = new[]
        {
            new Workshop(WorkshopAId, FactoryDemo1Id, "W-DEMO-A", "Demo Workshop A"),
            new Workshop(WorkshopBId, FactoryDemo1Id, "W-DEMO-B", "Demo Workshop B"),
            new Workshop(WorkshopXId, FactoryDemo2Id, "W-DEMO-X", "Demo Workshop X")
        };

        var lines = new[]
        {
            new ProductionLine(LineA1Id, FactoryDemo1Id, WorkshopAId, "L-A1", "Demo Line A1"),
            new ProductionLine(LineA2Id, FactoryDemo1Id, WorkshopAId, "L-A2", "Demo Line A2"),
            new ProductionLine(LineB1Id, FactoryDemo1Id, WorkshopBId, "L-B1", "Demo Line B1"),
            new ProductionLine(LineX1Id, FactoryDemo2Id, WorkshopXId, "L-X1", "Demo Line X1")
        };

        var products = new[]
        {
            new Product(1001, FactoryDemo1Id, ProductNormal, "Normal Plan+Actual Product"),
            new Product(1002, FactoryDemo1Id, ProductPlanZero, "Plan Zero Product"),
            new Product(1003, FactoryDemo1Id, ProductActualOnly, "Actual Without Plan Product"),
            new Product(1004, FactoryDemo1Id, ProductPlanOnly, "Plan Without Actual Product"),
            new Product(1005, FactoryDemo1Id, ProductDay2, "Day2 Range Filter Product"),
            new Product(1006, FactoryDemo1Id, ProductZeroInspection, "Zero Inspection Quantity Product"),
            new Product(2001, FactoryDemo2Id, ProductFactory2, "Factory Two Isolated Product")
        };

        // 工单夹具：『仅用于开发测试，不代表现场 MES 正式口径』。
        // StatusCode 为 Fake 临时值（Open/Completed/Closed），正式枚举【待现场确认】。
        var workOrders = new[]
        {
            // 进行中：计划完成 FixedPlannedFinishUtc；在 FakeComparedAtUtc 时尚不延期
            new WorkOrder(
                id: 5001,
                factoryId: FactoryDemo1Id,
                workOrderNo: WorkOrderNormal,
                productCode: ProductNormal,
                planQuantity: 120m,
                completedQuantity: 100m,
                workshopId: WorkshopAId,
                productionLineId: LineA1Id,
                plannedStartAtUtc: FixedPlannedStartUtc,
                plannedFinishAtUtc: FixedPlannedFinishUtc,
                statusCode: StatusOpen),
            // 计划数量 0 → CompletionRate null
            new WorkOrder(
                id: 5002,
                factoryId: FactoryDemo1Id,
                workOrderNo: WorkOrderPlanZero,
                productCode: ProductPlanZero,
                planQuantity: 0m,
                completedQuantity: 0m,
                workshopId: WorkshopAId,
                productionLineId: LineA1Id,
                plannedStartAtUtc: FixedPlannedStartUtc,
                plannedFinishAtUtc: FixedPlannedFinishUtc,
                statusCode: StatusOpen),
            // 车间 B / 产线 B1 组织筛选
            new WorkOrder(
                id: 5003,
                factoryId: FactoryDemo1Id,
                workOrderNo: WorkOrderWorkshopB,
                productCode: ProductNormal,
                planQuantity: 50m,
                completedQuantity: 10m,
                workshopId: WorkshopBId,
                productionLineId: LineB1Id,
                plannedStartAtUtc: FixedPlannedStartUtc,
                plannedFinishAtUtc: FixedPlannedFinishUtc,
                statusCode: StatusOpen),
            // 已关闭且计划完成已过：即使比较时间晚于计划完成也不延期
            new WorkOrder(
                id: 5004,
                factoryId: FactoryDemo1Id,
                workOrderNo: WorkOrderClosed,
                productCode: ProductNormal,
                planQuantity: 80m,
                completedQuantity: 80m,
                workshopId: WorkshopAId,
                productionLineId: LineA1Id,
                plannedStartAtUtc: FixedPlannedStartUtc,
                plannedFinishAtUtc: EarlyPlannedFinishUtc,
                actualFinishAtUtc: EarlyPlannedFinishUtc,
                statusCode: StatusClosed),
            // 已完成且计划完成已过：不延期
            new WorkOrder(
                id: 5005,
                factoryId: FactoryDemo1Id,
                workOrderNo: WorkOrderCompleted,
                productCode: ProductDay2,
                planQuantity: 40m,
                completedQuantity: 40m,
                workshopId: WorkshopAId,
                productionLineId: LineA2Id,
                plannedStartAtUtc: FixedPlannedStartUtc,
                plannedFinishAtUtc: EarlyPlannedFinishUtc,
                actualFinishAtUtc: EarlyPlannedFinishUtc,
                statusCode: StatusCompleted),
            // 未完成且计划完成已过（Early）：在 FakeComparedAtUtc 下应标记延期
            new WorkOrder(
                id: 5006,
                factoryId: FactoryDemo1Id,
                workOrderNo: WorkOrderOverdueOpen,
                productCode: ProductActualOnly,
                planQuantity: 60m,
                completedQuantity: 20m,
                workshopId: WorkshopAId,
                productionLineId: LineA1Id,
                plannedStartAtUtc: FixedPlannedStartUtc,
                plannedFinishAtUtc: EarlyPlannedFinishUtc,
                statusCode: StatusOpen),
            // 工厂 2 隔离
            new WorkOrder(
                id: 6001,
                factoryId: FactoryDemo2Id,
                workOrderNo: WorkOrderFactory2,
                productCode: ProductFactory2,
                planQuantity: 180m,
                completedQuantity: 200m,
                workshopId: WorkshopXId,
                productionLineId: LineX1Id,
                plannedStartAtUtc: FixedPlannedStartUtc,
                plannedFinishAtUtc: FixedPlannedFinishUtc,
                statusCode: StatusOpen)
        };

        // 场景 a+e：正常有计划有实际 + 完整数量分列（良品/不良/报废/返工/检验）
        // 『仅用于开发测试，不代表现场 MES 正式口径』
        var recordNormal = new ProductionRecord(
            factoryId: FactoryDemo1Id,
            workshopId: WorkshopAId,
            productionLineId: LineA1Id,
            productionDate: Day1,
            productCode: ProductNormal,
            quantities: new ProductionQuantities(
                actualQuantity: 100m,
                goodQuantity: 90m,
                defectQuantity: 5m,
                scrapQuantity: 3m,
                reworkQuantity: 2m,
                inspectedQuantity: 100m),
            dataUpdatedAtUtc: FixedUpdatedAtUtc,
            shiftCode: "DAY");

        // 场景 b：计划为 0，仍有实际
        var recordPlanZero = new ProductionRecord(
            factoryId: FactoryDemo1Id,
            workshopId: WorkshopAId,
            productionLineId: LineA1Id,
            productionDate: Day1,
            productCode: ProductPlanZero,
            quantities: new ProductionQuantities(40m, 38m, 2m, 0m, 0m, 40m),
            dataUpdatedAtUtc: FixedUpdatedAtUtc,
            shiftCode: "DAY");

        // 场景 c：有实际无计划
        var recordActualOnly = new ProductionRecord(
            factoryId: FactoryDemo1Id,
            workshopId: WorkshopAId,
            productionLineId: LineA1Id,
            productionDate: Day1,
            productCode: ProductActualOnly,
            quantities: new ProductionQuantities(55m, 50m, 5m, 0m, 0m, 55m),
            dataUpdatedAtUtc: FixedUpdatedAtUtc,
            shiftCode: "DAY");

        // Day2：日期范围筛选（工厂 1）
        var recordDay2 = new ProductionRecord(
            factoryId: FactoryDemo1Id,
            workshopId: WorkshopAId,
            productionLineId: LineA2Id,
            productionDate: Day2,
            productCode: ProductDay2,
            quantities: new ProductionQuantities(30m, 28m, 2m, 0m, 0m, 30m),
            dataUpdatedAtUtc: FixedUpdatedAtUtc,
            shiftCode: "DAY");

        // 场景 f：工厂 2 隔离
        var recordFactory2 = new ProductionRecord(
            factoryId: FactoryDemo2Id,
            workshopId: WorkshopXId,
            productionLineId: LineX1Id,
            productionDate: Day1,
            productCode: ProductFactory2,
            quantities: new ProductionQuantities(200m, 190m, 8m, 2m, 1m, 200m),
            dataUpdatedAtUtc: FixedUpdatedAtUtc,
            shiftCode: "DAY");

        // 车间 B：组织范围筛选用
        var recordWorkshopB = new ProductionRecord(
            factoryId: FactoryDemo1Id,
            workshopId: WorkshopBId,
            productionLineId: LineB1Id,
            productionDate: Day1,
            productCode: ProductNormal,
            quantities: new ProductionQuantities(10m, 10m, 0m, 0m, 0m, 10m),
            dataUpdatedAtUtc: FixedUpdatedAtUtc,
            shiftCode: "NIGHT");

        // 质量统计：检验数为 0 → YieldRate / DefectRate 均为 null
        // 『仅用于开发测试，不代表现场 MES 正式口径』
        var recordZeroInspection = new ProductionRecord(
            factoryId: FactoryDemo1Id,
            workshopId: WorkshopAId,
            productionLineId: LineA1Id,
            productionDate: Day1,
            productCode: ProductZeroInspection,
            quantities: new ProductionQuantities(
                actualQuantity: 5m,
                goodQuantity: 0m,
                defectQuantity: 0m,
                scrapQuantity: 1m,
                reworkQuantity: 1m,
                inspectedQuantity: 0m),
            dataUpdatedAtUtc: FixedUpdatedAtUtc,
            shiftCode: "DAY");

        var productionRecords = new[]
        {
            recordNormal,
            recordPlanZero,
            recordActualOnly,
            recordDay2,
            recordFactory2,
            recordWorkshopB,
            recordZeroInspection
        };

        var dailyPlans = new[]
        {
            // 场景 a：计划 120，对应实际 100
            new DailyProductionPlanLine(
                factoryId: FactoryDemo1Id,
                workshopId: WorkshopAId,
                planDate: Day1,
                productCode: ProductNormal,
                planQuantity: 120m,
                productionLineId: LineA1Id,
                remark: "scenario-a-normal"),

            // 场景 b：计划 0
            new DailyProductionPlanLine(
                factoryId: FactoryDemo1Id,
                workshopId: WorkshopAId,
                planDate: Day1,
                productCode: ProductPlanZero,
                planQuantity: 0m,
                productionLineId: LineA1Id,
                remark: "scenario-b-plan-zero"),

            // 场景 d：有计划无实际
            new DailyProductionPlanLine(
                factoryId: FactoryDemo1Id,
                workshopId: WorkshopAId,
                planDate: Day1,
                productCode: ProductPlanOnly,
                planQuantity: 80m,
                productionLineId: LineA1Id,
                remark: "scenario-d-plan-only"),

            // Day2 计划
            new DailyProductionPlanLine(
                factoryId: FactoryDemo1Id,
                workshopId: WorkshopAId,
                planDate: Day2,
                productCode: ProductDay2,
                planQuantity: 35m,
                productionLineId: LineA2Id,
                remark: "day2-range"),

            // 工厂 2
            new DailyProductionPlanLine(
                factoryId: FactoryDemo2Id,
                workshopId: WorkshopXId,
                planDate: Day1,
                productCode: ProductFactory2,
                planQuantity: 180m,
                productionLineId: LineX1Id,
                remark: "scenario-f-isolation"),

            // 车间 B
            new DailyProductionPlanLine(
                factoryId: FactoryDemo1Id,
                workshopId: WorkshopBId,
                planDate: Day1,
                productCode: ProductNormal,
                planQuantity: 20m,
                productionLineId: LineB1Id,
                remark: "workshop-b")
            // 场景 c：故意不为 ProductActualOnly 配置计划
        };

        var importBatches = new[]
        {
            new ImportBatch(
                id: ImportBatchFactory1Id,
                factoryId: FactoryDemo1Id,
                datasetCode: ReportCodes.MonthlyProductionPlan,
                status: ImportBatchStatus.Succeeded,
                createdAtUtc: FixedUpdatedAtUtc,
                targetPublishStatus: DatasetPublishStatus.Published,
                sourceFileName: "fake-monthly-plan-f1.xlsx",
                totalRows: 4,
                errorRows: 0,
                completedAtUtc: FixedUpdatedAtUtc),
            new ImportBatch(
                id: ImportBatchFactory2Id,
                factoryId: FactoryDemo2Id,
                datasetCode: ReportCodes.MonthlyProductionPlan,
                status: ImportBatchStatus.Succeeded,
                createdAtUtc: FixedUpdatedAtUtc,
                targetPublishStatus: DatasetPublishStatus.Published,
                sourceFileName: "fake-monthly-plan-f2.xlsx",
                totalRows: 1,
                errorRows: 0,
                completedAtUtc: FixedUpdatedAtUtc)
        };

        var datasetVersions = new[]
        {
            new DatasetVersionState(
                id: DatasetVersionFactory1Id,
                factoryId: FactoryDemo1Id,
                datasetCode: ReportCodes.MonthlyProductionPlan,
                versionNo: "v2026.03.10-fake-f1",
                publishStatus: DatasetPublishStatus.Published,
                isActive: true,
                createdAtUtc: FixedPublishedAtUtc,
                publishedAtUtc: FixedPublishedAtUtc),
            new DatasetVersionState(
                id: DatasetVersionFactory2Id,
                factoryId: FactoryDemo2Id,
                datasetCode: ReportCodes.MonthlyProductionPlan,
                versionNo: "v2026.03.10-fake-f2",
                publishStatus: DatasetPublishStatus.Published,
                isActive: true,
                createdAtUtc: FixedPublishedAtUtc,
                publishedAtUtc: FixedPublishedAtUtc)
        };

        return new FakeFixtureSnapshot(
            factories,
            workshops,
            lines,
            products,
            workOrders,
            productionRecords,
            dailyPlans,
            importBatches,
            datasetVersions);
    }
}
