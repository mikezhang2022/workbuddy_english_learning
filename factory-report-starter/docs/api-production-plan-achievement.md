# 生产计划达成查询 API（`production_plan_achievement`）

> **阶段 8**：只读查询。基于 Fake 内存数据层组合日计划与实际产量，不连接 Oracle / MES。  
> **稳定报表编码**：`production_plan_achievement`  
> **数据模式**：`DataAccessMode=Fake`（当前强制）  
> **边界计算规则**：已确认产品规则（非 Fake 临时口径）。

---

## 1. 端点

| 项 | 值 |
|---|---|
| 方法 | `GET` |
| 路径 | `/api/v1/reports/production-plan-achievement` |
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

日期语义：业务生产日（`ProductionDate` / 日计划 `PlanDate`），与系统 UTC 时间戳分离。

### 示例请求

```http
GET /api/v1/reports/production-plan-achievement?factoryId=1&startDate=2026-03-10&endDate=2026-03-10&workshopId=10&productCode=PROD-NORMAL
X-Correlation-ID: demo-ppa-001
```

---

## 3. 关联键（已确认）

计划与实际匹配关联键：

**`FactoryId + WorkshopId + ProductionLineId + ProductionDate + ProductCode`**

- 日计划侧使用 `PlanDate` 与键中的 `ProductionDate` 对齐。
- 仅使用带 `ProductionLineId` 的日计划行参与匹配（关联键要求产线）。
- **不得**用月计划平均推算日计划；本 API 只读 `DailyProductionPlanLine`。

---

## 4. 成功响应（200）

### 4.1 `meta`（元数据）

| 字段 | 说明 |
|---|---|
| `reportCode` | 固定 `production_plan_achievement` |
| `dataAccessMode` | 当前 `Fake` |
| `isFake` | `true` |
| `filters` | 筛选条件回显 |
| `matchKey` | 关联键文字说明 |
| `achievementRules` | 已确认达成率边界规则说明 |
| `dataStatusNote` | 数据状态：边界规则已确认；数量来源当前为 Fake |
| `generatedAtUtc` | 本次查询生成 UTC 时间 |

### 4.2 `rows[]`

| 字段 | 说明 |
|---|---|
| `productionDate` | 生产日 |
| `factoryId` / `factoryCode` | 工厂标识与编码 |
| `workshopId` / `workshopCode` | 车间标识与编码 |
| `productionLineId` / `productionLineCode` | 产线标识与编码 |
| `productCode` | 产品编码 |
| `planQuantity` | 计划数量；未配置计划时为 `null` |
| `actualQuantity` | 实际数量；有计划无实际时为 `0` |
| `achievementRate` | 达成率；可为 `null`（见 §5） |
| `planStatus` | 状态：`Calculated` / `PlanIsZero` / `PlanNotConfigured` / `MissingActual` |

### 4.3 示例响应（节选）

```json
{
  "meta": {
    "reportCode": "production_plan_achievement",
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
    "matchKey": "FactoryId + WorkshopId + ProductionLineId + ProductionDate + ProductCode",
    "achievementRules": "Plan>0: AchievementRate=Actual/Plan (Calculated); Plan=0: AchievementRate=null (PlanIsZero); Actual without plan: AchievementRate=null (PlanNotConfigured, never show 0%); Plan without actual: Actual=0, AchievementRate=0 (MissingActual); Do not derive daily plan from monthly average.",
    "dataStatusNote": "Boundary achievement rules are confirmed product rules; source quantities currently come from Fake fixtures (not MES/Oracle).",
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
      "planQuantity": 120,
      "actualQuantity": 100,
      "achievementRate": 0.8333333333333334,
      "planStatus": "Calculated"
    }
  ]
}
```

---

## 5. 四种边界状态（已确认产品规则）

| 场景 | 夹具产品 | PlanQuantity | ActualQuantity | AchievementRate | planStatus |
|---|---|---|---|---|---|
| 有计划有实际（计划 &gt; 0） | `PROD-NORMAL` | 120 | 100 | `100/120` | `Calculated` |
| 计划数量为 0 | `PROD-PLAN-ZERO` | 0 | 40 | `null` | `PlanIsZero` |
| 有实际但无计划 | `PROD-ACTUAL-ONLY` | `null` | 55 | `null` | `PlanNotConfigured` |
| 有计划但无实际 | `PROD-PLAN-ONLY` | 80 | 0 | `0` | `MissingActual` |

硬性约束：

- **不得**将「无计划」（`PlanNotConfigured`）错误展示为 0%。
- **不得**用月计划平均推算日计划。
- 阶段说明中的 `PlanQuantityZero` 与领域枚举 **`PlanIsZero`** 同义；API 对外状态值为 `PlanIsZero`。

正式舍入位数与 UI「—」文案【待现场确认】；本阶段返回 Decimal 原始比率，不做展示舍入。

---

## 6. 边界行为（HTTP）

| 场景 | 行为 |
|---|---|
| 缺失 `factoryId` | `400` RFC 7807 ProblemDetails，`errors.FactoryId` |
| 缺失 `startDate` / `endDate` | `400` ProblemDetails |
| `startDate` > `endDate` | `400` ProblemDetails，`errors.DateRange` |
| 无效可选 ID（≤0） | `400` ProblemDetails |
| 未知 `factoryId`（夹具中不存在） | `200` + 空 `rows`（不串其他工厂） |
| 筛选导致无匹配 | `200` + 空 `rows` |
| 跨工厂 | 强制按 `factoryId` 过滤；不同工厂互不串数据 |
| 未登录 | `401` ProblemDetails |
| 组织范围越权 | `403` ProblemDetails |
| 未处理异常 | `500` ProblemDetails + `correlationId` |

ProblemDetails 含 `correlationId` / `traceId`；请求头可传 `X-Correlation-ID`。

---

## 7. 架构约束

- Application：`IProductionPlanAchievementReportService` / DTO；经 `IReportDataQueryService` 读生产事实与日计划；用 `PlanAchievementEvaluator` 计算。
- API 层**不得**直接依赖 `Infrastructure.Fake` 实现类。
- Fake → Oracle 替换点仍在 `Infrastructure/DependencyInjection.cs`（本阶段不切换）。

---

## 8. Fake 数据限制

夹具日期与四边界场景见 `docs/fake-data.md`（约 `2026-03-10`～`2026-03-12`）。  
演示级数量，不含真实 MES 冲销、Excel 发布流水或权限矩阵。  
当前数量来源为 Fake；边界计算规则本身为已确认产品规则。

---

## 9. 待现场确认

- 达成率展示舍入位数与「—」等正式文案；
- 无产线计划行是否参与正式匹配（当前关联键要求 ProductionLineId）；
- Excel 日计划唯一键与 Active 版本切换后的正式数据源；
- MES 实际产量口径与只读视图映射；
- 关联键现场签字确认。
