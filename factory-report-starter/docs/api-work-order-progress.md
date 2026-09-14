# 工单进度查询 API（`work_order_progress`）

> **阶段 6**：只读查询。基于 Fake 内存数据层与 Application 抽象，不连接 Oracle / MES。  
> **稳定报表编码**：`work_order_progress`  
> **数据模式**：`DataAccessMode=Fake`（当前强制）

---

## 1. 端点

| 项 | 值 |
|---|---|
| 方法 | `GET` |
| 路径 | `/api/v1/reports/work-order-progress` |
| 认证 | **需要登录**（`ReportRead`）；Cookie 会话。未登录 → 401；组织范围越权 → 403。详见 `docs/authorization-and-data-scope.md` |
| 写入 | **无**；本阶段不提供任何 POST/PUT/PATCH/DELETE |

OpenAPI：Development / Testing 环境可通过 `GET /openapi/v1.json` 查看（`MapOpenApi`）。

---

## 2. 请求参数（Query）

| 参数 | 类型 | 必填 | 语义 |
|---|---|---|---|
| `factoryId` | long | **是**（登录后仍必填；不得省略以扩大范围） | 工厂 ID；正整数。缺失或 ≤0 → 400 ProblemDetails |
| `workshopId` | long | 否 | 车间 ID；若提供须为正整数 |
| `productionLineId` | long | 否 | 产线 ID；若提供须为正整数 |
| `productCode` | string | 否 | 产品编码精确匹配 |
| `workOrderCode` | string | 否 | 工单号精确匹配（领域 `WorkOrderNo`） |
| `status` | string | 否 | 工单状态精确匹配（忽略大小写）。Fake 临时值见 §4 |
| `plannedFinishFrom` | date (`yyyy-MM-dd`) | 否 | 计划完成日起始（按 `PlannedFinishUtc` 的 **UTC 日历日**，闭区间） |
| `plannedFinishTo` | date (`yyyy-MM-dd`) | 否 | 计划完成日结束（UTC 日历日，闭区间）；不得早于 `plannedFinishFrom` |

### 示例请求

```http
GET /api/v1/reports/work-order-progress?factoryId=1&workshopId=10&status=Open&plannedFinishFrom=2026-03-11&plannedFinishTo=2026-03-12
X-Correlation-ID: demo-wop-001
```

---

## 3. 成功响应（200）

### 3.1 `meta`（元数据）

| 字段 | 说明 |
|---|---|
| `reportCode` | 固定 `work_order_progress` |
| `dataAccessMode` | 当前 `Fake` |
| `isFake` | `true` |
| `filters` | 筛选条件回显 |
| `overdueRule` | Fake 延期判定文字说明 |
| `overdueDisclaimer` | **`Fake 测试规则：现场须确认工单状态枚举、时区、延期口径与计划时间来源`** |
| `completionRateRule` | 完成率 Fake 口径说明 |
| `comparedAtUtc` | 本次延期比较所用「当前 UTC」（来自可注入 `IUtcClock`） |
| `generatedAtUtc` | 本次查询生成 UTC 时间 |

### 3.2 `rows[]`

| 字段 | 说明 |
|---|---|
| `factoryId` / `factoryCode` | 工厂标识与编码 |
| `workshopId` / `workshopCode` | 车间标识与编码（可空） |
| `productionLineId` / `productionLineCode` | 产线标识与编码（可空） |
| `workOrderCode` | 工单号（文本） |
| `productCode` | 产品编码 |
| `status` | 工单状态（Fake 临时字符串） |
| `plannedQuantity` | 计划数量（领域 `PlanQuantity`） |
| `actualQuantity` | 实际/完成数量（领域 `CompletedQuantity`） |
| `remainingQuantity` | 剩余；Fake：`max(planned - actual, 0)` |
| `completionRate` | 完成率；可为 `null`（见 §5） |
| `plannedStartUtc` | 计划开始 UTC |
| `plannedFinishUtc` | 计划完成 UTC |
| `isCompleted` | Fake：`status` 为 Completed 或 Closed |
| `isOverdue` | Fake 延期标记（见 §4） |

### 3.3 示例响应（节选）

```json
{
  "meta": {
    "reportCode": "work_order_progress",
    "dataAccessMode": "Fake",
    "isFake": true,
    "filters": {
      "factoryId": 1,
      "workshopId": 10,
      "productionLineId": null,
      "productCode": null,
      "workOrderCode": "WO-DEMO-OVERDUE",
      "status": null,
      "plannedFinishFrom": null,
      "plannedFinishTo": null
    },
    "overdueRule": "IsOverdue = (!IsCompleted) AND (PlannedFinishUtc is not null) AND (UtcNow > PlannedFinishUtc); IsCompleted when Status is Completed or Closed (Fake temporary status codes).",
    "overdueDisclaimer": "Fake 测试规则：现场须确认工单状态枚举、时区、延期口径与计划时间来源",
    "completionRateRule": "CompletionRate = ActualQuantity / PlannedQuantity (null when PlannedQuantity = 0); may exceed 100%.",
    "comparedAtUtc": "2026-03-12T10:00:00+00:00",
    "generatedAtUtc": "2026-03-12T10:00:00+00:00"
  },
  "rows": [
    {
      "factoryId": 1,
      "factoryCode": "F-DEMO-01",
      "workshopId": 10,
      "workshopCode": "W-DEMO-A",
      "productionLineId": 101,
      "productionLineCode": "L-A1",
      "workOrderCode": "WO-DEMO-OVERDUE",
      "productCode": "PROD-ACTUAL-ONLY",
      "status": "Open",
      "plannedQuantity": 60,
      "actualQuantity": 20,
      "remainingQuantity": 40,
      "completionRate": 0.3333333333333333,
      "plannedStartUtc": "2026-03-10T08:00:00+00:00",
      "plannedFinishUtc": "2026-03-11T12:00:00+00:00",
      "isCompleted": false,
      "isOverdue": true
    }
  ]
}
```

---

## 4. Fake 延期规则（临时）

| 规则项 | Fake 临时口径 |
|---|---|
| 比较时间 | 经可注入 `IUtcClock`（UTC）；测试不得依赖系统实时时钟 |
| 已完成/已关闭 | `status` ∈ {`Completed`, `Closed`}（忽略大小写）→ `isCompleted = true` |
| 延期条件 | **同时**满足：`!isCompleted` **且** `plannedFinishUtc` 有值 **且** `comparedAtUtc > plannedFinishUtc` → `isOverdue = true` |
| 无计划完成时间 | `isOverdue = false` |
| 声明 | **Fake 测试规则：现场须确认工单状态枚举、时区、延期口径与计划时间来源** |

`Open` / `Completed` / `Closed` **仅为 Fake 临时状态字符串**，**不是** MES 正式枚举。【待现场确认】

---

## 5. Fake 完成率口径

| 场景 | 行为 |
|---|---|
| `plannedQuantity > 0` | `completionRate = actualQuantity / plannedQuantity`（可 &gt; 100%） |
| `plannedQuantity = 0` | `completionRate = null`（参照达成率零分母 / `PlanIsZero`） |
| 超报剩余 | `remainingQuantity = 0`（Fake；正式【待现场确认】） |

---

## 6. 边界行为

| 场景 | 行为 |
|---|---|
| 缺失 `factoryId` | `400` RFC 7807 ProblemDetails，`errors.FactoryId` |
| 无效可选 ID（≤0） | `400` ProblemDetails |
| `plannedFinishFrom` > `plannedFinishTo` | `400`，`errors.PlannedFinishDateRange` |
| 未知 `factoryId` | `200` + 空 `rows`（不串其他工厂） |
| 筛选无匹配 | `200` + 空 `rows` |
| 跨工厂 | 强制按 `factoryId` 过滤 |
| 未登录 | `401` ProblemDetails |
| 组织范围越权 | `403` ProblemDetails |
| 未处理异常 | `500` ProblemDetails + `correlationId` |

ProblemDetails 含 `correlationId` / `traceId`；请求头可传 `X-Correlation-ID`。

---

## 7. 架构约束

- Application：`IWorkOrderProgressReportService` / DTO；仅通过 `IReportDataQueryService` 读数。
- 延期比较时间必须经 `IUtcClock`（可注入固定时间）。
- API 层**不得**直接依赖 `Infrastructure.Fake` 实现类。
- Fake → Oracle 替换点仍在 `Infrastructure/DependencyInjection.cs`（本阶段不切换）。

---

## 8. Fake 数据限制

夹具工单与固定 UTC 时间见 `docs/fake-data.md`。  
演示级数量，不含真实 MES 状态机、冲销流水或权限矩阵。

---

## 9. 待现场确认

- 工单状态正式枚举与「完成 / 关闭」判定；
- 延期口径（业务日 vs UTC、是否含宽限、是否看实际完成时间）；
- 计划开始/完成时间字段来源与时区；
- 完成率零分母与超报展示正式文案；
- MES 只读视图与字段映射。
