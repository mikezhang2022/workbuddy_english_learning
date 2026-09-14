# 质量统计查询 API（`quality_statistics`）

> **阶段 7**：只读查询。基于 Fake 内存数据层聚合，不连接 Oracle / MES。  
> **稳定报表编码**：`quality_statistics`  
> **数据模式**：`DataAccessMode=Fake`（当前强制）

---

## 1. 端点

| 项 | 值 |
|---|---|
| 方法 | `GET` |
| 路径 | `/api/v1/reports/quality-statistics` |
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

### 关于 `workOrderCode`

**本阶段不提供 `workOrderCode` 筛选。**  
原因：当前领域 `ProductionRecord` **无工单维度**，质量事实无法按工单过滤。  
正式 MES 是否按工单采集质量数据【待现场确认】；若后续领域扩展，再追加该参数。

日期语义：业务生产日（`ProductionDate`），与系统 UTC 时间戳分离。

### 示例请求

```http
GET /api/v1/reports/quality-statistics?factoryId=1&startDate=2026-03-10&endDate=2026-03-11&workshopId=10&productCode=PROD-NORMAL
X-Correlation-ID: demo-qs-001
```

---

## 3. 成功响应（200）

聚合键：**生产日期 + FactoryId + WorkshopId + ProductionLineId + ProductCode**。  
同键多条事实（如多班次）数量求和后再计算比率。

### 3.1 `meta`（元数据）

| 字段 | 说明 |
|---|---|
| `reportCode` | 固定 `quality_statistics` |
| `dataAccessMode` | 当前 `Fake` |
| `isFake` | `true` |
| `filters` | 筛选条件回显（含必填与可选；不含 workOrderCode） |
| `yieldRateFormula` | Fake 良率公式说明 |
| `defectRateFormula` | Fake 不良率公式说明 |
| `qualityMetricsDisclaimer` | **`Fake 测试口径，现场 MES 接入前须确认`** |
| `workOrderFilterNote` | 说明为何省略工单筛选 |
| `generatedAtUtc` | 本次查询生成 UTC 时间 |

### 3.2 `rows[]`（聚合行）

| 字段 | 说明 |
|---|---|
| `productionDate` | 生产日 |
| `factoryId` / `factoryCode` | 工厂标识与编码 |
| `workshopId` / `workshopCode` | 车间标识与编码 |
| `productionLineId` / `productionLineCode` | 产线标识与编码 |
| `productCode` | 产品编码 |
| `inspectionQuantity` | 检验数量（领域 `InspectedQuantity`） |
| `goodQuantity` | 良品数量 |
| `defectQuantity` | 不良数量（**不**含报废/返工） |
| `scrapQuantity` | 报废数量（分列） |
| `reworkQuantity` | 返工数量（分列） |
| `yieldRate` | 良率（见 §4）；可为 `null` |
| `defectRate` | 不良率（见 §4）；可为 `null` |

### 3.3 示例响应（节选）

```json
{
  "meta": {
    "reportCode": "quality_statistics",
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
    "defectRateFormula": "DefectQuantity / InspectionQuantity (null when InspectionQuantity = 0); Scrap/Rework not merged into Defect",
    "qualityMetricsDisclaimer": "Fake 测试口径，现场 MES 接入前须确认",
    "workOrderFilterNote": "workOrderCode filter omitted: ProductionRecord has no work-order dimension in the current domain model.",
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
      "inspectionQuantity": 100,
      "goodQuantity": 90,
      "defectQuantity": 5,
      "scrapQuantity": 3,
      "reworkQuantity": 2,
      "yieldRate": 0.9,
      "defectRate": 0.05
    }
  ]
}
```

---

## 4. Fake 质量口径（临时）

| 规则 | 值 |
|---|---|
| 良率 | `YieldRate = GoodQuantity / InspectionQuantity` |
| 不良率 | `DefectRate = DefectQuantity / InspectionQuantity` |
| 分母为 0 | `YieldRate = null` **且** `DefectRate = null`（不除零） |
| 数量分列 | Good / Defect / Scrap / Rework **分列展示**；**不**把报废或返工自动合并到不良 |
| 声明 | **Fake 测试口径，现场 MES 接入前须确认** |

正式良率/不良率分子分母、返工与报废是否计入不良、舍入与展示文案均为【待现场确认】。  
本阶段 API 元数据、DTO 注释与本文档已强制标注 Fake 口径，不得当作现场 MES 正式规则。

夹具 `PROD-ZERO-INSP`：检验数为 0，同时保留 Scrap/Rework 分列样例（均为 1），用于断言比率 null 且分列不被合并。

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

- Application：`IQualityStatisticsReportService` / DTO；仅通过 `IReportDataQueryService` 读数。
- API 层**不得**直接依赖 `Infrastructure.Fake` 实现类。
- Fake → Oracle 替换点仍在 `Infrastructure/DependencyInjection.cs`（本阶段不切换）。
- 本阶段**不**实现不良类型排名、小时/每日趋势图、缺陷主数据（领域尚无 DefectType）。

---

## 7. Fake 数据限制

夹具日期与场景见 `docs/fake-data.md`（约 `2026-03-10`～`2026-03-12`）。  
不含不良类型排名明细、权限矩阵。数量级为演示级，非压测集。

---

## 8. 待现场确认

- 正式良率 / 不良率分子与分母；
- 返工、报废是否计入不良或单独规则；
- 检验数为 0 时的正式展示文案；
- 质量事实是否含工单维度及筛选；
- 不良类型编码/名称主数据与排名口径；
- MES 只读视图与字段映射。
