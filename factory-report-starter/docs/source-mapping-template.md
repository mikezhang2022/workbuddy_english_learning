# 源系统映射模板（MES / ERP → 报表字段）

> **用途**：供现场 DBA / MES 工程师填写只读视图与字段映射。  
> **状态**：下列映射表全部留空，均为【待现场确认】。  
> **数据库**：Oracle。驱动：Oracle.ManagedDataAccess / ODP.NET；EF：Oracle.EntityFrameworkCore。  
> **禁止**：填写或提交真实密码；在 Cursor Cloud 连接现场库；引入 SQL Server 专用对象名作为既定事实。  
> **配套**：字段业务含义见 `data-dictionary.md`；口径分类见 `business-decisions.md`。

---

## 填写说明

1. 每行对应一个报表字段（稳定英文编码）。
2. **源系统**：MES / ERP / 其他（请注明）。
3. **源 Schema / 所有者**：Oracle Schema 或用户【待现场确认】。
4. **源对象**：只读视图或表名（优先视图）。
5. **源字段**：列名；表达式需在视图内完成，应用层不拼用户 SQL。
6. **源类型**：Oracle 物理类型（如 NUMBER、VARCHAR2、DATE、TIMESTAMP WITH TIME ZONE）。
7. **转换规则**：单位换算、时区、枚举映射、冲销符号等；无则填「无」。
8. **增量字段**：用于 SyncCheckpoint 的水位列（时间戳或序号）；不适用填「全量」。
9. **过滤器**：只读范围谓词（工厂、未删除等）；由视图封装更佳。
10. **确认人 / 日期**：现场签字栏。
11. 未知项保持空白并保留【待现场确认】，不要编造。

### 连接与环境（总表）

| 项 | 填写值 | 状态 |
|---|---|---|
| Oracle 版本 | | 【待现场确认】 |
| 字符集 / 国家字符集 | | 【待现场确认】 |
| 连接方式（Easy Connect / TNS / Wallet…） | | 【待现场确认】 |
| 服务名或 SID | | 【待现场确认】 |
| 报表库 Schema | | 【待现场确认】 |
| MES 只读账号（名称，不含密码） | | 【待现场确认】 |
| 是否 PDB / 服务名 | | 【待现场确认】 |
| 网络来源限制 | | 【待现场确认】 |
| 确认人 | | |
| 确认日期 | | |

---

## 1. 生产日报 `production_daily`

| 报表字段编码 | 中文名称 | 源系统 | 源 Schema | 源对象（视图/表） | 源字段 | 源 Oracle 类型 | 转换规则 | 增量字段 | 过滤器 | 确认人 | 日期 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| factoryId | 工厂 ID | | | | | | | | | | |
| workshopId | 车间 ID | | | | | | | | | | |
| productionLineId | 产线 ID | | | | | | | | | | |
| businessDate | 生产日期 | | | | | | | | | | |
| shiftCode | 班次编码 | | | | | | | | | | |
| productCode | 产品编码 | | | | | | | | | | |
| productName | 产品名称 | | | | | | | | | | |
| planQuantity | 计划数量 | | | | | | | | | | |
| actualQuantity | 实际数量 | | | | | | | | | | |
| goodQuantity | 良品数量 | | | | | | | | | | |
| defectQuantity | 不良数量 | | | | | | | | | | |
| scrapQuantity | 报废数量 | | | | | | | | | | |
| hourBucket | 小时桶 | | | | | | | | | | |
| hourlyActualQuantity | 小时产量 | | | | | | | | | | |

**视图建议名（示例，非现场事实）**：`V_RPT_PRODUCTION_DAILY` —— 仅作命名参考，【待现场确认】。

---

## 2. 工单进度 `work_order_progress`

| 报表字段编码 | 中文名称 | 源系统 | 源 Schema | 源对象（视图/表） | 源字段 | 源 Oracle 类型 | 转换规则 | 增量字段 | 过滤器 | 确认人 | 日期 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| workOrderNo | 工单号 | | | | | | | | | | |
| factoryId | 工厂 ID | | | | | | | | | | |
| workshopId | 车间 ID | | | | | | | | | | |
| productionLineId | 产线 ID | | | | | | | | | | |
| productCode | 产品编码 | | | | | | | | | | |
| planQuantity | 计划数量 | | | | | | | | | | |
| completedQuantity | 完成数量 | | | | | | | | | | |
| plannedFinishAt | 计划完成时间 | | | | | | | | | | |
| actualFinishAt | 实际完成时间 | | | | | | | | | | |
| workOrderStatus | 工单状态 | | | | | | | | | | |

**视图建议名（示例）**：`V_RPT_WORK_ORDER_PROGRESS` —— 【待现场确认】。

---

## 3. 质量统计 `quality_statistics`

| 报表字段编码 | 中文名称 | 源系统 | 源 Schema | 源对象（视图/表） | 源字段 | 源 Oracle 类型 | 转换规则 | 增量字段 | 过滤器 | 确认人 | 日期 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| businessDate | 生产日期 | | | | | | | | | | |
| factoryId | 工厂 ID | | | | | | | | | | |
| workshopId | 车间 ID | | | | | | | | | | |
| productionLineId | 产线 ID | | | | | | | | | | |
| productCode | 产品编码 | | | | | | | | | | |
| inspectedQuantity | 检验数量 | | | | | | | | | | |
| defectQuantity | 不良数量 | | | | | | | | | | |
| defectTypeCode | 不良类型编码 | | | | | | | | | | |
| defectTypeName | 不良类型名称 | | | | | | | | | | |
| defectTypeQuantity | 不良类型数量 | | | | | | | | | | |

**视图建议名（示例）**：`V_RPT_QUALITY_DAILY` / `V_RPT_QUALITY_DEFECT` —— 【待现场确认】。

---

## 4. 计划达成 `plan_achievement`（组合）

实际侧使用生产日报/产量映射；计划侧来自 Excel 已发布版本（非 MES）。请确认关联键：

| 关联键组成部分 | 实际侧源字段 | 计划侧（Excel 模板字段） | 转换规则 | 确认人 | 日期 |
|---|---|---|---|---|---|
| factoryId | | factoryCode → FactoryId | | | |
| workshopId | | workshopCode → WorkshopId | | | |
| productionDate | | productionDate | | | |
| productCode | | productCode | | | |

全部【待现场确认】。开发 Fake 临时键见 `business-decisions.md` ②。

---

## 5. 组织与基础资料（可选，供维度同步）

| 报表/维度字段 | 中文名称 | 源系统 | 源 Schema | 源对象 | 源字段 | 源 Oracle 类型 | 转换规则 | 确认人 | 日期 |
|---|---|---|---|---|---|---|---|---|---|
| factoryCode | 工厂编码 | | | | | | | | |
| factoryName | 工厂名称 | | | | | | | | |
| workshopCode | 车间编码 | | | | | | | | |
| workshopName | 车间名称 | | | | | | | | |
| productionLineCode | 产线编码 | | | | | | | | |
| productionLineName | 产线名称 | | | | | | | | |
| shiftCode | 班次编码 | | | | | | | | |
| shiftName | 班次名称 | | | | | | | | |
| productCode | 产品编码 | | | | | | | | |
| productName | 产品名称 | | | | | | | | |

---

## 6. 扫码对象解析（WO / EQ / LOT）

| 前缀 | 业务含义 | 源系统 | 源对象 | 业务编号字段 | 组织归属字段 | 确认人 | 日期 |
|---|---|---|---|---|---|---|---|
| WO | 工单 | | | | | | |
| EQ | 设备 | | | | | | |
| LOT | 批次 | | | | | | |

全部【待现场确认】。

---

## 7. 签字

| 角色 | 姓名 | 日期 | 备注 |
|---|---|---|---|
| 业务负责人 | | | |
| MES 负责人 | | | |
| DBA | | | |
| 报表项目负责人 | | | |
