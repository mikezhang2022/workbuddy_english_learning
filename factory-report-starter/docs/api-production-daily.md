# 生产日报查询 API（`production_daily`）

> **阶段 5**：只读查询。基于 Fake 内存数据层聚合，不连接 Oracle / MES。  
> **稳定报表编码**：`production_daily`  
> **数据模式**：`DataAccessMode=Fake`（当前强制）

---

## 1. 端点

| 项 | 值 |
|---|---|
| 方法 | `GET` |
| 路径 | `/api/v1/reports/production-daily` |
| 认证 | **需要登录**（`ReportRead`）；Cookie 会话。未登录 → 401；组织范围越权 → 403。详见 `docs/authorization-and-data-scope.md` |
| 写入 | **无**；本阶段不提供任何 POST/PUT/PATCH/DELETE |

OpenAPI：Development / Testing 环境可通过 `GET /openapi/v1.json` 查看（`MapOpenApi`）。

---

## 2. 请求参数（Query）

| 参数 | 类型 | 必填 | 语义 |
|---|---|---|---|
| `factoryId` | long | **是**（登录后仍必填；不得省略以扩大范围） | 工厂 ID；正整数。缺失或 ≤0 → 400 ProblemDetails |
| `startDate` | date (`yyyy-MM-dd`) | **是** | 生产日起始日（`DateOnly`，闭区间） |
| `endDate` | date (`yyyy-MM-dd`) | **是** | 生产日结束日（`DateOnly`，闭区间）；不得早于 `startDate` |
| `workshopId` | long | 否 | 车间 ID；若提供须为正整数 |
| `productionLineId` | long | 否 | 产线 ID；若提供须为正整数 |
| `productCode` | string | 否 | 产品编码精确匹配 |

日期语义：业务生产日（`ProductionDate`），与系统 UTC 时间戳分离。

### 示例请求

```http
GET /api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-11&workshopId=10&productCode=PROD-NORMAL
X-Correlation-ID: demo-pd-001
```

---

## 3. 成功响应（200）

聚合键：**生产日期 + FactoryId + WorkshopId + ProductionLineId + ProductCode**。  
同键多条事实（如多班次）数量求和后再计算良率。

### 3.1 `meta`（元数据）

| 字段 | 说明 |
|---|---|
| `reportCode` | 固定 `production_daily` |
| `dataAccessMode` | 当前 `Fake` |
| `isFake` | `true` |
| `filters` | 筛选条件回显（含必填与可选） |
| `yieldRateFormula` | Fake 公式说明 |
| `yieldRateDisclaimer` | **`Fake 测试口径，现场 MES 接入前须确认`** |
| `generatedAtUtc` | 本次查询生成 UTC 时间 |

### 3.2 `rows[]`（聚合行）

| 字段 | 说明 |
|---|---|
| `productionDate` | 生产日 |
| `factoryId` / `factoryCode` | 工厂标识与编码 |
| `workshopId` / `workshopCode` | 车间标识与编码 |
| `productionLineId` / `productionLineCode` | 产线标识与编码 |
| `productCode` | 产品编码 |
| `actualQuantity` | 实际数量 |
| `goodQuantity` | 良品数量 |
| `defectQuantity` | 不良数量 |
| `scrapQuantity` | 报废数量 |
| `reworkQuantity` | 返工数量 |
| `inspectionQuantity` | 检验数量（领域 `InspectedQuantity`） |
| `yieldRate` | 良率（见 §4）；可为 `null` |

### 3.3 示例响应（节选）

```json
{
  "meta": {
    "reportCode": "production_daily",
    "dataAccessMode": "Fake",
    "isFake": true,
    "filters": {
      "factoryId": 1,
      "startDate": "2026-03-10",
      "endDate": "2026-03-10",
      "workshopId": 10,
      "productionLineId": 101,
      "productCode": "PROD-NORMAL"
    },
    "yieldRateFormula": "GoodQuantity / InspectionQuantity (null when InspectionQuantity = 0)",
    "yieldRateDisclaimer": "Fake 测试口径，现场 MES 接入前须确认",
    "generatedAtUtc": "2026-03-10T12:00:00+00:00"
  },
  "rows": [
    {
      "productionDate": "2026-03-10",
      "factoryId": 1,
      "factoryCode": "F-DEMO-01",
      "workshopId": 10,
      "workshopCode": "W-DEMO-A",
      "productionLineId": 101,
      "productionLineCode": "L-A1",
      "productCode": "PROD-NORMAL",
      "actualQuantity": 100,
      "goodQuantity": 90,
      "defectQuantity": 5,
      "scrapQuantity": 3,
      "reworkQuantity": 2,
      "inspectionQuantity": 100,
      "yieldRate": 0.9
    }
  ]
}
```

---

## 4. Fake 良率口径（临时）

| 规则 | 值 |
|---|---|
| 公式 | `YieldRate = GoodQuantity / InspectionQuantity` |
| 分母为 0 | `YieldRate = null`（不除零） |
| 声明 | **Fake 测试口径，现场 MES 接入前须确认** |

正式良率分子/分母、舍入与展示文案均为【待现场确认】。  
本阶段 API 元数据与 DTO 注释已强制标注 Fake 口径，不得当作现场 MES 正式规则。

---

## 5. 边界行为

| 场景 | 行为 |
|---|---|
| 缺失 `factoryId` | `400` RFC 7807 ProblemDetails，`errors.FactoryId` |
| 缺失 `startDate` / `endDate` | `400` ProblemDetails |
| `startDate` > `endDate` | `400` ProblemDetails，`errors.DateRange` |
| 无效可选 ID（≤0） | `400` ProblemDetails |
| 未知 `factoryId`（夹具中不存在） | `200` + 空 `rows`（不串其他工厂） |
| 筛选导致无匹配事实 | `200` + 空 `rows` |
| 跨工厂 | 强制按 `factoryId` 过滤；不同工厂互不串数据 |
| 未登录 | `401` ProblemDetails |
| 组织范围越权 | `403` ProblemDetails |
| 未处理异常 | `500` ProblemDetails + `correlationId`（阶段 2 全局处理） |

ProblemDetails 含 `correlationId` / `traceId`；请求头可传 `X-Correlation-ID`。

---

## 6. 架构约束

- Application：`IProductionDailyReportService` / DTO；仅通过 `IReportDataQueryService` 读数。
- API 层**不得**直接依赖 `Infrastructure.Fake` 实现类。
- Fake → Oracle 替换点仍在 `Infrastructure/DependencyInjection.cs`（本阶段不切换）。

---

## 7. Fake 数据限制

夹具日期与场景见 `docs/fake-data.md`（约 `2026-03-10`～`2026-03-12`）。  
不含小时趋势、计划达成组合、权限矩阵。数量级为演示级，非压测集。
