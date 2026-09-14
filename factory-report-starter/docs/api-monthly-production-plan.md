# 月度生产计划查询 API（`monthly_production_plan`）

> **阶段 9**：只读查询。基于 Fake 内存数据层返回月度文件内的日计划行，不连接 Oracle / MES。  
> **稳定报表编码**：`monthly_production_plan`  
> **数据模式**：`DataAccessMode=Fake`（当前强制）  
> **日计划原则**：返回 `DailyProductionPlanLine`（保留 `PlanDate`），**不得**将月计划自动平均或推算到每天。

---

## 1. 端点

| 项 | 值 |
|---|---|
| 方法 | `GET` |
| 路径 | `/api/v1/reports/monthly-production-plan` |
| 认证 | 本阶段未启用（后续权限阶段） |
| 写入 | **无**；本阶段不提供 Excel 上传 / 发布 / 回退或任何 POST/PUT/PATCH/DELETE |

OpenAPI：Development / Testing 环境可通过 `GET /openapi/v1.json` 查看（`MapOpenApi`）。

---

## 2. 请求参数（Query）

| 参数 | 类型 | 必填 | 语义 |
|---|---|---|---|
| `factoryId` | long | **是** | 工厂 ID；正整数。缺失或 ≤0 → 400 ProblemDetails |
| `planMonth` | string (`yyyy-MM`) | **是** | 计划月份；严格四位年 + `-` + 两位月。非法格式 → 400 |
| `workshopId` | long | 否 | 车间 ID；若提供须为正整数 |
| `productionLineId` | long | 否 | 产线 ID；若提供须为正整数 |
| `productCode` | string | 否 | 产品编码精确匹配 |

日期语义：`PlanDate` 为业务计划日；筛选范围为 `planMonth` 对应自然月的闭区间首末日。

### 示例请求

```http
GET /api/v1/reports/monthly-production-plan?factoryId=1&planMonth=2026-03&workshopId=10&productCode=PROD-NORMAL
X-Correlation-ID: demo-mpp-001
```

---

## 3. 日计划行原则（关键）

- 响应 `rows` 必须是**月度文件内的日计划行**（领域 `DailyProductionPlanLine`）。
- 每行保留原始 **`PlanDate`** 与 **`PlanQuantity`**。
- **禁止**把月计划总量按天数平均、分摊或推算生成日行。
- 本 API 只读已有日行；不创建、不改写计划。

---

## 4. Fake 数据版本规则（必须标明）

> **声明：以下为 Fake 数据版本行为，非正式现场规则。**  
> 真实 Excel 发布、版本激活、回退将在后续阶段实现，并须现场确认【待现场确认】。

| 规则 | Fake 行为 |
|---|---|
| 可见版本 | 仅 `PublishStatus = Published` **且** `IsActive = true` |
| 不可见 | Draft / Validating / Disabled，或 Published 但非 Active |
| 夹具负向样例 | 工厂 1 含 Draft 版本 `PROD-DRAFT-ONLY` 行；查询不得返回 |
| 版本关联 | 日计划行带 `PlanVersionId`，对齐 `DatasetVersionState.Id` |

响应 `meta.fakeVersionRuleNote` 与 `meta.dataSourceIdentifier` 会再次标注 Fake 来源。

---

## 5. 成功响应（200）

### 5.1 `meta`

| 字段 | 说明 |
|---|---|
| `reportCode` | 固定 `monthly_production_plan` |
| `dataAccessMode` | 当前 `Fake` |
| `isFake` | `true` |
| `filters` | 筛选条件回显（含 `planMonth`） |
| `activePlanVersionId` / `activePlanVersionNo` | 当前可见 Published+Active 版本 |
| `dataSourceIdentifier` | 当前数据来源/版本标识（Fake 夹具标签） |
| `fakeVersionRuleNote` | Fake 版本过滤说明 + 正式规则【待现场确认】 |
| `dailyPlanLinePrinciple` | 不得月均摊到日 |
| `generatedAtUtc` | 本次查询生成 UTC 时间 |

### 5.2 `rows[]`

| 字段 | 说明 |
|---|---|
| `planDate` | 计划日（必须保留） |
| `factoryId` / `factoryCode` | 工厂标识与编码 |
| `workshopId` / `workshopCode` | 车间标识与编码 |
| `productionLineId` / `productionLineCode` | 产线标识与编码（可空） |
| `productCode` | 产品编码 |
| `planQuantity` | 日计划数量（原始行） |
| `planVersionId` / `planVersionNo` | 计划版本标识 |
| `publishStatus` / `isActive` | 发布状态与是否 Active（Fake 临时；正式枚举【待现场确认】） |
| `remark` | 可选备注 |

### 5.3 示例响应（节选）

```json
{
  "meta": {
    "reportCode": "monthly_production_plan",
    "dataAccessMode": "Fake",
    "isFake": true,
    "filters": {
      "factoryId": 1,
      "planMonth": "2026-03",
      "workshopId": 10,
      "productionLineId": 101,
      "productCode": "PROD-NORMAL"
    },
    "activePlanVersionId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
    "activePlanVersionNo": "v2026.03.10-fake-f1",
    "dataSourceIdentifier": "FakeFixture:DeterministicFakeFixture:v2026.03.10-fake-f1",
    "fakeVersionRuleNote": "FAKE DATA VERSION BEHAVIOR ONLY: return rows from DatasetVersion where PublishStatus=Published AND IsActive=true. Real Excel publish, version activation, and rollback rules will be implemented in later phases and must be confirmed on-site 【待现场确认】.",
    "dailyPlanLinePrinciple": "Return DailyProductionPlanLine rows from the monthly file as-is (retain PlanDate). Do not average or allocate monthly totals across days.",
    "generatedAtUtc": "2026-03-10T12:00:00+00:00"
  },
  "rows": [
    {
      "planDate": "2026-03-10",
      "factoryId": 1,
      "factoryCode": "F-DEMO-01",
      "workshopId": 10,
      "workshopCode": "W-DEMO-A",
      "productionLineId": 101,
      "productionLineCode": "L-A1",
      "productCode": "PROD-NORMAL",
      "planQuantity": 120,
      "planVersionId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
      "planVersionNo": "v2026.03.10-fake-f1",
      "publishStatus": "Published",
      "isActive": true,
      "remark": "scenario-a-normal"
    }
  ]
}
```

---

## 6. 边界行为（HTTP）

| 场景 | 行为 |
|---|---|
| 缺失 `factoryId` | `400` RFC 7807 ProblemDetails，`errors.FactoryId` |
| 缺失或非法 `planMonth`（非 `yyyy-MM`） | `400` ProblemDetails，`errors.PlanMonth` |
| 无效可选 ID（≤0） | `400` ProblemDetails |
| 未知 `factoryId` | `200` + 空 `rows` |
| 月份内无 Published/Active 计划 | `200` + 空 `rows` |
| Draft 等非可见版本行 | **不返回** |
| 跨工厂 | 强制按 `factoryId` 过滤 |
| 未处理异常 | `500` ProblemDetails + `correlationId` |

---

## 7. 架构约束

- Application：`IMonthlyProductionPlanReportService` / DTO；经 `IReportDataQueryService` 读日计划与数据集版本。
- API 层**不得**直接依赖 `Infrastructure.Fake` 实现类。
- 本阶段**无**写入端点、无 Excel、无 Oracle DbContext/Migration。

---

## 8. Fake 数据限制

夹具计划月：`2026-03`（日行约 `2026-03-10`～`2026-03-12`）。  
含工厂 1 Draft 版本负向样例。演示级数量，不含真实 Excel 发布流水。

---

## 9. 待现场确认

- Excel 正式发布、Active 版本切换、回退事务与不可变版本规则；
- `DatasetPublishStatus` 是否作为现场正式枚举（当前为领域规划 + Fake 演示）；
- 日计划行是否必须落在 `planYearMonth` 内的正式校验；
- 无产线计划行的权限与展示口径；
- MES/Oracle 映射与物理存储。
