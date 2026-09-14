namespace FactoryReport.Client.Services.Display;

/// <summary>
/// Fake 演示组织目录（与 DeterministicFakeFixture Id 对齐）。
/// 仅用于全局/工厂级授权时的下拉选项标签；正式组织主数据【待现场确认】。
/// </summary>
internal static class FakeDemoOrgCatalog
{
    public static IReadOnlyList<OrgOption> Factories { get; } =
    [
        new(1, "工厂 1（F-DEMO-01）"),
        new(2, "工厂 2（F-DEMO-02）")
    ];

    public static IReadOnlyList<(long FactoryId, OrgOption Option)> Workshops { get; } =
    [
        (1, new OrgOption(10, "车间 10（W-DEMO-A）")),
        (1, new OrgOption(20, "车间 20（W-DEMO-B）")),
        (2, new OrgOption(30, "车间 30（W-DEMO-X）"))
    ];

    public static IReadOnlyList<(long FactoryId, long WorkshopId, OrgOption Option)> Lines { get; } =
    [
        (1, 10, new OrgOption(101, "产线 101（L-A1）")),
        (1, 10, new OrgOption(102, "产线 102（L-A2）")),
        (1, 20, new OrgOption(201, "产线 201（L-B1）")),
        (2, 30, new OrgOption(301, "产线 301（L-X1）"))
    ];
}

public readonly record struct OrgOption(long Id, string Label);
