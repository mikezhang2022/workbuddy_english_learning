# 领域模型（阶段 3）

> **本阶段不包含 Oracle 表结构、EF Core Migration、DDL 或 Schema 脚本。**  
> 模型为数据库无关、可单元测试的第一版，供后续 Fake 数据、Oracle 映射与报表引擎使用。  
> 字段规划类型与 `docs/data-dictionary.md` 对应；Oracle 物理类型一律【待现场确认】。

---

## 1. 对象职责与关键字段

### 1.1 组织范围

| 对象 | 职责 | 关键字段 | 关联 |
|---|---|---|---|
| `Factory` | 工厂根组织 | `Id`, `Code`, `Name` | 被 Workshop / 业务表 `FactoryId` 引用 |
| `Workshop` | 车间 | `Id`, `FactoryId`, `Code`, `Name` | 归属 Factory |
| `ProductionLine` | 产线 | `Id`, `FactoryId`, `WorkshopId`, `Code`, `Name` | 归属 Factory + Workshop |

校验：组织标识 ID 必须为正；编码/名称不可空白。业务表一律保留 `FactoryId` 以支持多工厂。

### 1.2 主数据标识

| 对象 | 职责 | 关键字段 | 关联 |
|---|---|---|---|
| `Product` | 产品主数据标识 | `Id`, `FactoryId`, `ProductCode`, `Name` | 对应字典 `productCode` / `productName` |
| `WorkOrder` | 工单标识 | `Id`, `FactoryId`, `WorkOrderNo`, `ProductCode`, `PlanQuantity`, `CompletedQuantity`, 可选车间/产线与 UTC 完工时间 | 对应 `work_order_progress`；延期正式规则【待现场确认】 |

### 1.3 生产事实

| 对象 | 职责 | 关键字段 | 关联 |
|---|---|---|---|
| `ProductionQuantities` | 数量值对象（分列） | `ActualQuantity`, `GoodQuantity`, `DefectQuantity`, `ScrapQuantity`, `ReworkQuantity`, `InspectedQuantity` | 不同步/领域层混合计算口径 |
| `ProductionRecord` | 生产日事实 | `FactoryId`, `WorkshopId`, `ProductionLineId`, `ProductionDate`, `ProductCode`, `ShiftCode?`, `Quantities`, `DataUpdatedAtUtc` | 对应 `production_daily` / 质量数量列 |

### 1.4 生产计划

| 对象 | 职责 | 关键字段 | 关联 |
|---|---|---|---|
| `MonthlyProductionPlan` | 月度计划头 | `Id`, `FactoryId`, `PlanYearMonth`（YYYY-MM）, `Lines`, `CreatedAtUtc` | 对应 `monthly_production_plan` |
| `DailyProductionPlanLine` | 日计划行 | `FactoryId`, `WorkshopId`, `ProductionLineId?`, **`PlanDate`**, `ProductCode`, `PlanQuantity` | **必须保留 PlanDate** |

### 1.5 导入与发布状态

| 对象 / 枚举 | 职责 | 关键字段 |
|---|---|---|
| `ImportBatch` | 导入批次追踪 | `Id`, `FactoryId`, `DatasetCode`, `Status`, `TargetPublishStatus`, 行数统计, UTC 时间 |
| `ImportBatchStatus` | 批次状态 | Pending / Validating / Succeeded / Failed / Cancelled |
| `DatasetPublishStatus` | 数据版本发布 | Draft → Validating → Published → Disabled |
| `DatasetVersionState` | 版本领域状态 | `VersionNo`, `PublishStatus`, `IsActive`（仅 Published 可 Active）, UTC 时间 |

### 1.6 报表与达成率

| 对象 | 职责 | 关键字段 |
|---|---|---|
| `ReportCodes` | 稳定报表编码常量 | 见 §2 |
| `PlanAchievementKey` | 达成率关联键 | `FactoryId` + `WorkshopId` + `ProductionLineId` + `ProductionDate` + `ProductCode` |
| `PlanAchievementResult` | 达成率计算结果 | `PlanQuantity?`, `ActualQuantity`, `AchievementRate?`, `Status` |
| `PlanAchievementStatus` | 边界状态 | Calculated / PlanIsZero / PlanNotConfigured / MissingActual |
| `UtcInstant` | UTC 时间戳值对象 | `DateTimeOffset` 且 Offset=0 |

Application 层：`StableReportCodes`、`PlanAchievementEvaluator`、`IUtcClock`（委托 Domain，无基础设施依赖）。

---

## 2. 稳定报表编码

| 常量 | 稳定值 | 数据字典对应 |
|---|---|---|
| `ReportCodes.ProductionDaily` | `production_daily` | §1 生产日报 |
| `ReportCodes.WorkOrderProgress` | `work_order_progress` | §2 工单进度 |
| `ReportCodes.QualityStatistics` | `quality_statistics` | §3 质量统计 |
| `ReportCodes.ProductionPlanAchievement` | `production_plan_achievement` | §4 计划达成（字典组合数据集规划码曾写作 `plan_achievement`；**报表稳定编码以本常量为准**） |
| `ReportCodes.MonthlyProductionPlan` | `monthly_production_plan` | §5 Excel 月度计划 |

---

## 3. 与 data-dictionary.md 字段对应

| 领域字段 | 字典稳定英文编码 | 报表/节 |
|---|---|---|
| FactoryId / WorkshopId / ProductionLineId | factoryId / workshopId / productionLineId | 通用组织权限 |
| ProductionDate / PlanDate | businessDate / productionDate | 生产日报 / 计划 / 达成 |
| ProductCode | productCode | 各报表 |
| Actual/Good/Defect/Scrap/Rework/Inspected | actualQuantity / goodQuantity / defectQuantity / scrapQuantity / （返工分列） / inspectedQuantity | 生产日报、质量 |
| PlanQuantity | planQuantity | 计划、达成、工单 |
| WorkOrderNo | workOrderNo | 工单进度 |
| AchievementRate | achievementRate | 达成率 |
| DataUpdatedAtUtc | dataUpdatedAt | UTC |
| PlanYearMonth | planYearMonth | 月度计划 |

---

## 4. 规则分类

### 4.1 已确认（写入领域行为）

- 系统时间使用 **UTC** 存储语义（`UtcInstant` / `DateTimeOffset` Offset=0）。
- 业务表保留 **FactoryId**（多工厂）。
- 数量字段 **分列保存**，不在同步/领域层把返工/报废等混入实际产量公式。
- 达成率关联键：`FactoryId + WorkshopId + ProductionLineId + ProductionDate + ProductCode`。
- 计划为 **0** → `AchievementRate = null`（`PlanIsZero`）。
- 有实际无计划 → 状态 **未配置计划**（`PlanNotConfigured`），达成率为 null。
- 有计划无实际 → `ActualQuantity = 0`，`AchievementRate = 0%`（`MissingActual`）。
- 组织/计划关键标识不可为空或无效；适用对象数量不可为负。
- 五个稳定报表编码值固定（见 §2）。

### 4.2 Fake 临时口径（不得伪装成正式规则）

见 `business-decisions.md` ②，例如：

- Fake 夜班归属示例配置；
- Fake 良率/不良率分子分母演示公式；
- Fake 工单延期判断；
- Fake Excel ReplaceScope / 演示组织 Seed；
- Fake 达成率展示一位小数（正式舍入【待现场确认】）。

### 4.3 待现场确认（保持可配置 / 未实现为正式规则）

- 夜班归属与生产日/自然日关系；
- 正式良率、延期、返工/报废/冲销是否允许负数量等规则；
- MES 字段映射与只读视图；
- Oracle Schema、字符集、连接方式与物理类型映射；
- Excel 正式唯一键与 ReplaceScope；
- 达成率展示文案（「—」等）与舍入位数的正式口径。

---

## 5. 明确不在本阶段范围

- 不创建 `DbContext`、Oracle Migration、DDL、Schema 脚本；
- 不连接 Oracle / MES / ERP；
- 不实现登录、权限、业务 API、Excel 上传、报表页面；
- 不添加 SQL Server 相关依赖或代码。

阶段 4 已提供 Fake 内存仓储与确定性夹具（见 `docs/fake-data.md`）。下一步建议：基于 Fake 数据层实现报表查询 API / 生产日报（单独阶段），Oracle 映射与迁移仍为后续独立阶段。
