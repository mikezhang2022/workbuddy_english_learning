# Excel 导入（商业化第二阶段）

> 区分：**代码已实现** / **测试已通过** / **现场已验证**。  
> 导入数据存于产品应用存储（本阶段 Fake 内存），**不得写入 Oracle 业务/MES 表**。  
> 浏览器不持数据库凭据。

## 1. 模板字段定义

### 1.1 计划数据（`monthly_production_plan`）

| 列名 | 显示名 | 类型 | 必填 | 示例 | 说明 |
|------|--------|------|------|------|------|
| factoryCode | 工厂编码 | string | 是 | F-DEMO-01 | 组织须存在 |
| workshopCode | 车间编码 | string | 是 | W-DEMO-A | 属于该工厂 |
| productionLineCode | 产线编码 | string | 否 | L-A1 | 若填则须存在 |
| planYearMonth | 计划年月 | string | 是 | 2026-03 | YYYY-MM |
| productionDate | 生产日期 | date | 是 | 2026-03-10 | yyyy-MM-dd；须落在计划年月 |
| productCode | 产品编码 | string | 是 | PROD-NORMAL | 产品主数据存在 |
| planQuantity | 计划数量 | decimal | 是 | 100 | ≥ 0 |
| remark | 备注 | string | 否 | demo | 可选 |

模板版本：`1.0`；工作表：`Data`；字段说明页：`FieldNotes`。

### 1.2 实际数据（`production_actual`）

| 列名 | 显示名 | 类型 | 必填 | 示例 | 说明 |
|------|--------|------|------|------|------|
| factoryCode | 工厂编码 | string | 是 | F-DEMO-01 | 组织须存在 |
| workshopCode | 车间编码 | string | 是 | W-DEMO-A | 属于该工厂 |
| productionLineCode | 产线编码 | string | 否 | L-A1 | 若填则须存在 |
| productionDate | 生产日期 | date | 是 | 2026-03-10 | yyyy-MM-dd |
| productCode | 产品编码 | string | 是 | PROD-NORMAL | 产品主数据存在 |
| actualQuantity | 实际数量 | decimal | 是 | 95 | ≥ 0 |
| goodQuantity | 良品数量 | decimal | 否 | 90 | ≥ 0 |
| defectQuantity | 不良数量 | decimal | 否 | 5 | ≥ 0 |
| shiftCode | 班次编码 | string | 否 | DAY | 可选 |
| remark | 备注 | string | 否 | demo | 可选 |

## 2. 校验规则

| 规则 | 错误码 | 说明 |
|------|--------|------|
| 必填缺失 | `required` | 必填列为空 |
| 类型错误 | `type` | 数值/YYYY-MM 无法解析 |
| 日期格式非法 | `date_format` | 非可解析日期 |
| 日期超出合理范围 | `date_range` | 不在 2000-01-01～2100-12-31；或计划日不在 planYearMonth |
| 组织编码不存在 | `org_not_found` | 工厂/车间/产线 |
| 产品编码不存在 | `product_not_found` | 产品主数据 |
| 数值为负 | `negative` | decimal &lt; 0 |
| 同文件重复行 | `duplicate` | Fake 唯一键：factoryCode+workshopCode+productionDate+productCode |

错误明细必含：**行号 + 列名 + 原因**。错误行不得静默丢弃。有错误时 **不得发布**。

文件限制：仅 `.xlsx`；最大 **20 MB**；最大 **50,000** 数据行。解析失败返回可读 ProblemDetails，不向用户抛堆栈。

## 3. 批次状态机（复用既有枚举）

产品语义 ↔ 领域模型：

```text
草稿          → ImportBatchStatus.Pending，TargetPublishStatus=Draft
校验中        → Validating
已校验        → Succeeded（ErrorRows=0） / Failed（有错误）
已发布/已激活 → Succeeded + TargetPublishStatus=Published
               + DatasetVersionState(Published, IsActive=true)
已回退        → TargetPublishStatus=Disabled；当前版本 Disabled；
               可选恢复先前 Published 版本为 Active
```

约束：

- 已发布批次不可再校验。
- `ErrorRows > 0` 或非 Succeeded → 不可发布。
- 仅已发布批次可回退。

## 4. 接口契约

基址：`/api/v1/imports`。Cookie 认证。写入需 Antiforgery（`X-CSRF-TOKEN`）。

| 方法 | 路径 | 策略 | 说明 |
|------|------|------|------|
| GET | `/templates` | ImportRead | 模板元数据 |
| GET | `/templates/{datasetCode}/download` | ImportRead | 下载 xlsx |
| GET | `/batches?factoryId=` | ImportRead | 批次列表 |
| GET | `/batches/{batchId}` | ImportRead | 详情+错误明细 |
| POST | `/batches` multipart | ImportManage | 上传→草稿 |
| POST | `/batches/{id}/validate` | ImportManage | 校验 |
| POST | `/batches/{id}/publish` | ImportManage | 发布+激活 |
| POST | `/batches/{id}/rollback` | ImportManage | 回退 |

权限：

- `ImportRead`：SystemAdmin / FactoryAdmin / ProductionManager / QualityUser / Viewer
- `ImportManage`：仅 SystemAdmin / FactoryAdmin
- 组织范围：与报表相同，越权 **403**；未登录 **401**

上传表单字段：`factoryId`、`datasetCode`、`file`。

## 5. 组件

Excel：`MiniExcel` 1.41.3（Apache-2.0）。见 `third-party-components.md`。

## 6. 验证层级

| 项 | 层级 |
|----|------|
| 单元/集成测试覆盖校验、流转、401/403 | 测试已通过 |
| Fake 存储端到端 | 测试已通过 / 手工可复现 |
| 现场 Oracle 持久化 | **不支持**；现场已验证：无 |
