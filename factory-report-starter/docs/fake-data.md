# Fake 内存数据层（阶段 4）

> **声明**：本夹具为虚构开发测试数据，**不含真实生产数据**。  
> 所有 Fake 数值与口径 **『仅用于开发测试，不代表现场 MES 正式口径』**。  
> Fake **永不**连接 Oracle、MES、文件网络机密或任何外部服务。

---

## 1. 夹具内容概览

| 类别 | 数量级 | 说明 |
|---|---|---|
| 工厂 | 2 | `F-DEMO-01`（Id=1）、`F-DEMO-02`（Id=2） |
| 车间 | 3 | 工厂 1：`W-DEMO-A` / `W-DEMO-B`；工厂 2：`W-DEMO-X` |
| 产线 | 4 | A1、A2、B1、X1 |
| 产品 | 6 | 覆盖边界场景与日期筛选 |
| 工单 | 7 | 工厂 1：正常进行中 / 计划 0 / 车间 B / 已关闭 / 已完成 / 未完成延期候选；工厂 2：隔离工单。含计划开始/完成 UTC 与 Fake 临时状态 |

| 生产事实 | 6 | 含完整数量分列与跨车间/跨厂 |
| 日计划行 | 6 | 故意缺省「仅有实际」产品的计划 |
| 导入批次 / 版本 | 各 2 | `monthly_production_plan`，按工厂隔离 |

### 固定 UTC 日期（不依赖系统时钟）

| 常量 | 值 |
|---|---|
| Day1 | `2026-03-10` |
| Day2 | `2026-03-11` |
| Day3 | `2026-03-12`（范围内无事实，供空结果断言） |
| DataUpdatedAtUtc | `2026-03-10T08:00:00Z` |
| PlannedStartUtc | `2026-03-10T08:00:00Z` |
| PlannedFinishUtc（常规） | `2026-03-12T16:00:00Z` |
| EarlyPlannedFinishUtc（延期场景） | `2026-03-11T12:00:00Z` |
| FakeComparedAtUtc（测试比较时间） | `2026-03-12T10:00:00Z` |

实现：`Infrastructure/Fake/DeterministicFakeFixture.cs`。

### Fake 工单状态（临时，非 MES 正式枚举）【待现场确认】

| StatusCode | 用途 |
|---|---|
| `Open` | 未完成 |
| `Completed` | 已完成（不延期） |
| `Closed` | 已关闭（不延期） |

---

## 2. 覆盖场景清单

| 场景 | 产品编码 | 计划 | 实际 | 期望达成状态 |
|---|---|---|---|---|
| a. 正常有计划有实际 | `PROD-NORMAL` | 120 | 100 | `Calculated`（100/120） |
| b. 计划为 0 | `PROD-PLAN-ZERO` | 0 | 40 | `PlanIsZero`（达成率 null） |
| c. 有实际无计划 | `PROD-ACTUAL-ONLY` | （无行） | 55 | `PlanNotConfigured` |
| d. 有计划无实际 | `PROD-PLAN-ONLY` | 80 | （无行） | `MissingActual`（实际 0、达成率 0%） |
| e. 完整数量分列 | 同 a | — | 良品 90 / 不良 5 / 报废 3 / 返工 2 / 检验 100 | 分列可读 |
| f. 组织隔离 | `PROD-F2`（工厂 2） | 180 | 200 | 按 `FactoryId` 筛选互不串扰 |

额外：Day2 `PROD-DAY2` 用于日期范围筛选；车间 B 行用于组织范围筛选。

### 工单进度场景（阶段 6）

| 工单号 | 状态（Fake） | 计划完成 | 期望（在 FakeComparedAtUtc） |
|---|---|---|---|
| `WO-DEMO-1001` | Open | Day3 16:00 | 进行中，不延期；完成率 100/120 |
| `WO-DEMO-1002` | Open | Day3 16:00 | 计划 0 → CompletionRate null |
| `WO-DEMO-B001` | Open | Day3 16:00 | 车间 B 筛选 |
| `WO-DEMO-CLOSED` | Closed | Day2 12:00 | 已关闭，不延期 |
| `WO-DEMO-DONE` | Completed | Day2 12:00 | 已完成，不延期 |
| `WO-DEMO-OVERDUE` | Open | Day2 12:00 | 未完成且超期 → IsOverdue |
| `WO-DEMO-2001` | Open | Day3 16:00 | 工厂 2 隔离；超报剩余 0 |

---

## 3. 数据集限制

- **数量级**：个位数～十余条实体，非性能压测集。
- **日期范围**：仅 `2026-03-10`～`2026-03-12`；不含跨年、跨月大量历史。
- **不覆盖**：真实 MES 冲销负数量流水、夜班正式归属、Excel 校验失败样例、权限用户矩阵、小时趋势桶、不良类型排名明细。
- **不连接**：Oracle、文件导入路径、网络、环境机密。
- Domain 层保持纯净：Fake 逻辑仅在 `Infrastructure/Fake`。

---

## 4. 接口与查询入口

Application（`FactoryReport.Application/DataAccess/`）：

| 抽象 | 用途 |
|---|---|
| `IOrganizationReadRepository` | 工厂 / 车间 / 产线 |
| `IProductReadRepository` | 产品 |
| `IWorkOrderReadRepository` | 工单 |
| `IProductionRecordReadRepository` | 生产事实（强制 FactoryId + 日期范围） |
| `IDailyProductionPlanReadRepository` | 日计划（强制 FactoryId + 日期范围） |
| `IImportBatchReadRepository` | 导入批次 / 数据版本 |
| `IReportDataQueryService` / `ReportDataQueryService` | 报表引擎聚合查询入口 |
| `IProductionDailyReportService` | 生产日报（`production_daily`）只读聚合；见 `docs/api-production-daily.md` |
| `IWorkOrderProgressReportService` | 工单进度（`work_order_progress`）只读查询；见 `docs/api-work-order-progress.md` |

筛选类型：`OrganizationScopeFilter`、`DateRangeFilter`。

---

## 5. Fake → Oracle 替换点【待现场确认】

| 位置 | 说明 |
|---|---|
| `Infrastructure/DependencyInjection.cs` | 当前强制注册 Fake 仓储与 `ReportDataQueryService`；未来按 `DataMode=Oracle` 切换 |
| `Infrastructure/Fake/*` | 现行 Fake 实现与 `DeterministicFakeFixture` |
| `Infrastructure/Persistence/Oracle/OraclePersistencePlaceholder.cs` | Oracle 接入目录占位；列出待实现仓储接口名 |
| 配置 | `FactoryReport:DataMode`（默认 `Fake`）；真实连接串不得入库 |

替换步骤（后续阶段，本阶段不做）：

1. 在 `Persistence/Oracle/` 实现上述 `I*ReadRepository`（DbContext / ODP.NET）。
2. 在 DI 中按 `DataMode` 注册 Oracle 实现。
3. Schema / 字符集 / 连接方式 / 只读视图 —— 全部【待现场确认】。

---

## 6. 与业务决策的关系

边界计算规则（计划 0 / 未配置计划 / 缺实际）以 `docs/business-decisions.md` ① 与领域 `PlanAchievementResult` 为准。  
Fake 夜班编码、良率演示公式等仍属 ②「Fake 测试临时口径」，不得当作现场正式规则。
