# 数据字典（规划口径）

> **范围**：工厂移动报表平台第一版核心报表与 Excel 计划模板。  
> **数据库**：Oracle。【Oracle 版本、Schema、字符集、连接方式、现场只读视图均为待现场确认】  
> **类型说明**：下文「规划类型」为产品/应用层口径；「Oracle 类型」列统一标记【待现场确认】，不得臆造现场物理类型。  
> **来源说明**：未知 MES/ERP 表或视图时标记【待现场确认】，不得虚构真实表名。  
> **模拟数据**：Fake 仅用内存/文件，不得连接数据库。

---

## 0. 通用约定

| 约定项 | 规划口径 | 备注 |
|---|---|---|
| 时区 | 业务展示用工厂时区；库内系统时间保存 UTC | 工厂时区【待现场确认】 |
| 业务生产日期 | `BusinessDate`，与系统创建时间分离 | 与自然日关系见 `business-decisions.md` |
| 组织权限字段 | `FactoryId` / `WorkshopId` / `ProductionLineId` | API 强制追加数据范围 |
| 空值 | 指标卡与表格需区分「无数据」与「零」 | Fake 临时规则见业务决策文档 |
| 编码稳定性 | 英文 `code` 一经发布尽量不变 | 中文名称可调整 |

### 0.1 规划类型 → Oracle 映射（一律待确认）

| 规划类型 | 含义 | Oracle 类型 |
|---|---|---|
| string | 文本 | 【待现场确认】（如 VARCHAR2 / NVARCHAR2） |
| int / long | 整数 | 【待现场确认】（如 NUMBER） |
| decimal | 小数/金额/比率计算中间值 | 【待现场确认】（如 NUMBER(p,s)） |
| date | 业务日期 | 【待现场确认】（如 DATE） |
| datetimeoffset | UTC 时间戳 | 【待现场确认】（如 TIMESTAMP WITH TIME ZONE） |
| bool | 布尔 | 【待现场确认】（如 NUMBER(1)） |
| guid | 内部主键 | 【待现场确认】（如 RAW(16) / VARCHAR2） |
| json/clob | 大文本 JSON | 【待现场确认】（如 CLOB） |
| percent | 比率展示（通常由 decimal 计算后格式化） | 同 decimal |

---

## 1. 生产日报（`production_daily`）

**用途**：按生产日/班次查看计划、实际、良品、不良、报废与达成/良率及小时趋势。  
**更新频率**：首页与实际产量约 1 分钟同步（规格建议）；真实频率【待现场确认】。  
**组织权限字段**：FactoryId, WorkshopId, ProductionLineId。  
**可用筛选**：生产日期（或范围）、工厂、车间、产线、班次、产品编码。

### 1.1 字段与指标

| 中文名称 | 稳定英文编码 | 规划类型 | 单位 | 业务定义 | 计算公式 | 来源系统 | 来源表/视图.字段 | 更新频率 | 可作为筛选 | 权限字段 |
|---|---|---|---|---|---|---|---|---|---|---|
| 工厂 ID | factoryId | long | — | 工厂组织主键 | — | 本地维表 / MES | 【待现场确认】 | 基础资料 15 分钟 | 是 | 是 |
| 车间 ID | workshopId | long | — | 车间组织主键 | — | 本地维表 / MES | 【待现场确认】 | 同上 | 是 | 是 |
| 产线 ID | productionLineId | long | — | 产线组织主键 | — | 本地维表 / MES | 【待现场确认】 | 同上 | 是 | 是 |
| 生产日期 | businessDate | date | — | 业务归属生产日 | 班次跨天规则见决策文档 | MES | 【待现场确认】 | 随产量 | 是 | 否 |
| 班次编码 | shiftCode | string | — | 班次标识 | — | MES / 配置 | 【待现场确认】 | 配置 | 是 | 否 |
| 产品编码 | productCode | string | — | 产品业务编码 | — | MES | 【待现场确认】 | 基础资料 | 是 | 否 |
| 产品名称 | productName | string | — | 产品显示名 | — | MES | 【待现场确认】 | 基础资料 | 否 | 否 |
| 计划数量 | planQuantity | decimal | 件 | 当日/当班计划产量 | 来自 Excel 计划或 MES 计划【待现场确认】 | Excel / MES | 【待现场确认】 | Excel 发布或 MES 同步 | 否 | 否 |
| 实际数量 | actualQuantity | decimal | 件 | 实际产量（是否扣返工/报废/冲销见决策） | Σ合格报工 ± 调整项【待现场确认】 | MES | 【待现场确认】 | ~1 分钟 | 否 | 否 |
| 良品数量 | goodQuantity | decimal | 件 | 良品数 | 【待现场确认】 | MES | 【待现场确认】 | ~1–5 分钟 | 否 | 否 |
| 不良数量 | defectQuantity | decimal | 件 | 不良品数 | 【待现场确认】 | MES | 【待现场确认】 | ~3–5 分钟 | 否 | 否 |
| 报废数量 | scrapQuantity | decimal | 件 | 报废数 | 【待现场确认】 | MES | 【待现场确认】 | ~3–5 分钟 | 否 | 否 |
| 达成率 | achievementRate | percent | % | 实际/计划 | actualQuantity / planQuantity（零分母规则见决策） | 计算 | — | 查询时 | 否 | 否 |
| 良率 | yieldRate | percent | % | 良品/检验或实际基数 | 分子分母【待现场确认】 | 计算 | — | 查询时 | 否 | 否 |
| 小时 | hourBucket | string | — | 小时趋势横轴（如 08:00） | 按工厂时区切桶 | MES / 计算 | 【待现场确认】 | ~1 分钟 | 否 | 否 |
| 小时产量 | hourlyActualQuantity | decimal | 件 | 该小时实际产量 | Σ 小时内实际 | MES / 计算 | 【待现场确认】 | ~1 分钟 | 否 | 否 |
| 数据更新时间 | dataUpdatedAt | datetimeoffset | UTC | 源数据最后同步成功时间 | SyncRun 成功时间 | 本地 etl | etl.SyncRun | 同步后 | 否 | 否 |

### 1.2 空值 / 零值 / 返工 / 报废 / 冲销 / 补录

| 场景 | 规划处理 | 状态 |
|---|---|---|
| 无产量行 | 不展示或显示空状态，不显示为 0（Fake 临时：显示空状态） | Fake 临时 / 【待现场确认】 |
| 计划为 0 | 达成率按决策文档处理 | 【待现场确认】 |
| 返工 | 是否计入实际产量【待现场确认】 | 【待现场确认】 |
| 报废 | 是否从良品扣除、是否单独展示 | 单独展示 scrapQuantity；口径【待现场确认】 |
| 冲销 | 同步需支持冲销修订，不丢 Checkpoint | 【待现场确认】映射 |
| 补录 | 允许回溯生产日补录，重算受影响区间 | 【待现场确认】 |

---

## 2. 工单进度（`work_order_progress`）

**用途**：跟踪工单计划、完成、剩余、完成比例与延期状态。  
**更新频率**：约 1～3 分钟（规格建议）。  
**组织权限字段**：FactoryId, WorkshopId, ProductionLineId。  
**可用筛选**：生产日期范围、工厂、车间、产线、工单号、产品、延期状态。

| 中文名称 | 稳定英文编码 | 规划类型 | 单位 | 业务定义 | 计算公式 | 来源系统 | 来源表/视图.字段 | 更新频率 | 可作为筛选 | 权限字段 |
|---|---|---|---|---|---|---|---|---|---|---|
| 工单号 | workOrderNo | string | — | 工单业务编号（始终按文本） | — | MES | 【待现场确认】 | 1–3 分钟 | 是 | 否 |
| 工厂 ID | factoryId | long | — | 归属工厂 | — | MES | 【待现场确认】 | 同步 | 是 | 是 |
| 车间 ID | workshopId | long | — | 归属车间 | — | MES | 【待现场确认】 | 同步 | 是 | 是 |
| 产线 ID | productionLineId | long | — | 归属产线 | — | MES | 【待现场确认】 | 同步 | 是 | 是 |
| 产品编码 | productCode | string | — | 产品编码 | — | MES | 【待现场确认】 | 同步 | 是 | 否 |
| 计划数量 | planQuantity | decimal | 件 | 工单计划产量 | — | MES | 【待现场确认】 | 同步 | 否 | 否 |
| 完成数量 | completedQuantity | decimal | 件 | 已完成产量 | 【待现场确认】 | MES | 【待现场确认】 | 1–3 分钟 | 否 | 否 |
| 剩余数量 | remainingQuantity | decimal | 件 | 未完成量 | planQuantity - completedQuantity（负值处理【待现场确认】） | 计算 | — | 查询时 | 否 | 否 |
| 完成比例 | completionRate | percent | % | 完成进度 | completedQuantity / planQuantity | 计算 | — | 查询时 | 否 | 否 |
| 计划完成时间 | plannedFinishAt | datetimeoffset | — | 计划完工时间 | — | MES | 【待现场确认】 | 同步 | 否 | 否 |
| 实际完成时间 | actualFinishAt | datetimeoffset | — | 实际完工时间，未完成为空 | — | MES | 【待现场确认】 | 同步 | 否 | 否 |
| 工单状态 | workOrderStatus | string | — | 开启/关闭等 | 枚举【待现场确认】 | MES | 【待现场确认】 | 同步 | 是 | 否 |
| 是否延期 | isOverdue | bool | — | 延期标记 | 规则见决策文档 | 计算 | — | 查询时 | 是 | 否 |
| 数据更新时间 | dataUpdatedAt | datetimeoffset | UTC | 同步成功时间 | — | 本地 etl | etl.SyncRun | 同步后 | 否 | 否 |

### 2.1 特殊规则占位

| 场景 | 规划处理 | 状态 |
|---|---|---|
| 关闭后修订 | 同步需覆盖修订 | 【待现场确认】 |
| 延期判断 | 相对计划完成时间与当前/业务时间比较 | 【待现场确认】 |
| 超报（完成>计划） | remainingQuantity 与比率展示规则 | Fake 临时：剩余显示 0，比率可 >100% / 【待现场确认】 |

---

## 3. 质量统计（`quality_statistics`）

**用途**：检验数、不良数、不良率、不良类型排名与每日趋势。  
**更新频率**：约 3～5 分钟。  
**组织权限字段**：FactoryId, WorkshopId, ProductionLineId。  
**可用筛选**：日期范围、工厂、车间、产线、产品、不良类型。

| 中文名称 | 稳定英文编码 | 规划类型 | 单位 | 业务定义 | 计算公式 | 来源系统 | 来源表/视图.字段 | 更新频率 | 可作为筛选 | 权限字段 |
|---|---|---|---|---|---|---|---|---|---|---|
| 生产日期 | businessDate | date | — | 质量归属生产日 | — | MES | 【待现场确认】 | 3–5 分钟 | 是 | 否 |
| 工厂 ID | factoryId | long | — | 工厂 | — | MES | 【待现场确认】 | 同步 | 是 | 是 |
| 车间 ID | workshopId | long | — | 车间 | — | MES | 【待现场确认】 | 同步 | 是 | 是 |
| 产线 ID | productionLineId | long | — | 产线 | — | MES | 【待现场确认】 | 同步 | 是 | 是 |
| 产品编码 | productCode | string | — | 产品 | — | MES | 【待现场确认】 | 同步 | 是 | 否 |
| 检验数量 | inspectedQuantity | decimal | 件 | 检验基数 | 【待现场确认】 | MES | 【待现场确认】 | 3–5 分钟 | 否 | 否 |
| 不良数量 | defectQuantity | decimal | 件 | 不良品数量 | 【待现场确认】 | MES | 【待现场确认】 | 3–5 分钟 | 否 | 否 |
| 不良率 | defectRate | percent | % | 不良占比 | defectQuantity / inspectedQuantity | 计算 | — | 查询时 | 否 | 否 |
| 不良类型编码 | defectTypeCode | string | — | 不良类型 | — | MES | 【待现场确认】 | 同步 | 是 | 否 |
| 不良类型名称 | defectTypeName | string | — | 不良类型显示名 | — | MES | 【待现场确认】 | 同步 | 否 | 否 |
| 不良类型数量 | defectTypeQuantity | decimal | 件 | 该类型不良数（排名用） | Σ | MES / 计算 | 【待现场确认】 | 3–5 分钟 | 否 | 否 |
| 数据更新时间 | dataUpdatedAt | datetimeoffset | UTC | 同步成功时间 | — | 本地 etl | etl.SyncRun | 同步后 | 否 | 否 |

### 3.1 特殊规则占位

| 场景 | 规划处理 | 状态 |
|---|---|---|
| 良率分子分母 | 与生产日报 yieldRate 对齐或独立 | 【待现场确认】 |
| 返工不良是否计入 | — | 【待现场确认】 |
| 检验数为 0 | 不良率空或「—」，不除零 | Fake 临时：显示「—」 |

---

## 4. 生产计划达成率（`production_plan_achievement` 报表；组合数据集规划码曾写作 `plan_achievement`）

**用途**：MES（或 Fake）实际产量 + Excel 已发布计划 → 达成率。  
**关联键（已确认）**：FactoryId + WorkshopId + ProductionLineId + ProductionDate + ProductCode。  
**组织权限**：同时应用于 MES 实际与 Excel 计划两侧。

| 中文名称 | 稳定英文编码 | 规划类型 | 单位 | 业务定义 | 计算公式 | 来源系统 | 来源表/视图.字段 | 更新频率 | 可作为筛选 | 权限字段 |
|---|---|---|---|---|---|---|---|---|---|---|
| 工厂 ID | factoryId | long | — | 关联键组成部分 | — | MES + Excel | 【待现场确认】 / 模板字段 | 各源 | 是 | 是 |
| 车间 ID | workshopId | long | — | 关联键组成部分 | — | MES + Excel | 【待现场确认】 | 各源 | 是 | 是 |
| 生产日期 | productionDate | date | — | 关联键组成部分 | — | MES + Excel | 【待现场确认】 | 各源 | 是 | 否 |
| 产品编码 | productCode | string | — | 关联键组成部分 | — | MES + Excel | 【待现场确认】 | 各源 | 是 | 否 |
| 计划数量 | planQuantity | decimal | 件 | Excel 当前 Active 版本 | — | Excel 已发布版本 | imp.DatasetRecord | 发布后 | 否 | 否 |
| 实际数量 | actualQuantity | decimal | 件 | MES/Fake 实际产量 | 与生产日报口径一致 | MES / Fake | 【待现场确认】 | 同步 | 否 | 否 |
| 差异 | varianceQuantity | decimal | 件 | 实际 − 计划 | actualQuantity - planQuantity | 计算 | — | 查询时 | 否 | 否 |
| 达成率 | achievementRate | percent | % | 实际/计划 | actual / plan（零分母规则见决策） | 计算 | — | 查询时 | 否 | 否 |
| 计划版本号 | planVersionNo | string | — | Excel DatasetVersion | — | 本地 imp | imp.DatasetVersion | 发布后 | 否 | 否 |
| 实际数据更新时间 | actualDataUpdatedAt | datetimeoffset | UTC | MES 同步时间 | — | 本地 etl | etl.SyncRun | 同步后 | 否 | 否 |
| 报表生成时间 | generatedAt | datetimeoffset | UTC | 本次查询生成时间 | 服务器 UTC 现在 | API | — | 查询时 | 否 | 否 |

### 4.1 组合边界（规划）

| 场景 | 规划处理 | 状态 |
|---|---|---|
| 只有计划无实际 | 实际=0，达成率=0% | **已确认**（领域 `PlanAchievementStatus.MissingActual`） |
| 只有实际无计划 | 「未配置计划」，达成率 null | **已确认**（`PlanNotConfigured`） |
| 计划为 0 | 达成率 null | **已确认**（`PlanIsZero`） |
| 重复计划键 | 导入校验拒绝；历史脏数据【待现场确认】 | 导入侧拒绝 |

---

## 5. Excel 月度生产计划（文件数据集模板）

**数据集编码（规划）**：`monthly_production_plan`  
**文件格式**：`.xlsx` / `.csv`；不支持宏。  
**导入模式**：Snapshot / ReplaceScope（范围【待现场确认】）。  
**发布**：不可变 DatasetVersion；ActiveVersionId 切换。

| 中文名称 | 稳定英文编码 | 规划类型 | 必填 | 业务唯一键候选 | 校验 | 权限字段 | 筛选/维度/指标 |
|---|---|---|---|---|---|---|---|
| 工厂编码 | factoryCode | string | 是 | 是（组合） | 组织存在 | 映射到 FactoryId | 维度 |
| 车间编码 | workshopCode | string | 是 | 是（组合） | 组织存在 | 映射到 WorkshopId | 维度 |
| 产线编码 | productionLineCode | string | 否 | 否 | 若填则组织存在 | 可映射 ProductionLineId | 维度 |
| 计划年月 | planYearMonth | string | 是 | 是（组合） | YYYY-MM | 否 | 筛选 |
| 生产日期 | productionDate | date | 是 | 是（组合） | 落在计划年月内【待现场确认】 | 否 | 筛选/维度 |
| 产品编码 | productCode | string | 是 | 是（组合） | 字典/长度【待现场确认】 | 否 | 维度 |
| 计划数量 | planQuantity | decimal | 是 | 否 | ≥0；范围【待现场确认】 | 否 | 指标 |
| 备注 | remark | string | 否 | 否 | 最大长度【待现场确认】 | 否 | 否 |

**建议业务唯一键（开发默认，正式【待现场确认】）**：  
`factoryCode + workshopCode + productionDate + productCode`

**ReplaceScope（开发默认）**：按 `factoryCode + planYearMonth` 替换；正式范围【待现场确认】。

---

## 6. 本地报表库对象（配置/事实，非 MES）

以下为平台自有 Oracle 库规划对象，物理 DDL 在后续阶段用 Migration 落地；类型映射【待现场确认】。

| Schema（规划名） | 用途 | Schema 实际名称 |
|---|---|---|
| sec | 身份、权限、组织 | 【待现场确认】 |
| cfg | 数据源、数据集、报表配置 | 【待现场确认】 |
| imp | 文件导入与版本 | 【待现场确认】 |
| etl | 同步任务与水位 | 【待现场确认】 |
| rpt | 报表维表与事实 | 【待现场确认】 |
| aud | 审计 | 【待现场确认】 |

驱动与访问：Oracle.ManagedDataAccess / ODP.NET；EF Core：Oracle.EntityFrameworkCore。禁止 Microsoft.Data.SqlClient、SqlBulkCopy。

---

## 7. 编码一致性检查清单

| 报表/数据集 | DatasetCode / ReportCode | 文档内一致 |
|---|---|---|
| 生产日报 | production_daily | 是 |
| 工单进度 | work_order_progress | 是 |
| 质量统计 | quality_statistics | 是 |
| 计划达成 | production_plan_achievement（报表稳定编码；旧规划码 plan_achievement） | 是 |
| 月度计划 Excel | monthly_production_plan | 是 |

未知来源一律【待现场确认】，未将未知项标为已确认。
