# 月度生产计划移动端页面（阶段 17）

> `FactoryReport.Client` 页面：`/reports/monthly-production-plan`（兼容别名 `/reports/monthly-plan`）。  
> 接入只读 `GET /api/v1/reports/monthly-production-plan`；**不**实现 Excel 上传/模板下载/校验/发布/激活/回退、导出、打印、图表或写入。

---

## 1. 页面字段与数据来源

| UI 区域 | 内容 | 来源 |
|--------|------|------|
| 标题 | 月度生产计划 | 固定文案 |
| 工厂 | 下拉，必选 | `/api/v1/auth/me` → `dataScope`（见 §4） |
| 计划月份 | 文本，必填 `yyyy-MM` | **用户显式填写**；不预填当前月、不硬编码 Fake 月份冒充有数据 |
| 车间 / 产线 | 可选下拉 | 仅展示授权范围内的选项 |
| 产品编码 | 可选文本 | 原样传给 API `productCode` |
| 元数据 | `reportCode`、Fake 标识、`filters` 回显、激活版本与数据来源 | API `meta` |
| 版本提示 | Published+Active 可见；正式发布/激活/回退【待现场确认】 | 页面固定中文 + `meta.fakeVersionRuleNote` |
| 日行原则 | 「按月上传、按日计划行展示」，禁止前端月均摊 | 页面固定中文 + `meta.dailyPlanLinePrinciple` |
| 汇总卡 | 计划数量合计 / 行数 | 对**当前返回行**做前端展示合计（非正式口径） |
| 明细 | `PlanDate` / 组织 / 产品 / `PlanQuantity` / `PlanVersionId`·`No` / `PublishStatus` / `IsActive` | API `rows[]` **原始日计划行** |

- 使用 `ReportsApiClient.GetMonthlyProductionPlanAsync`；**不**在前端把月计划平均或推算到每天。
- Correlation ID 仅在错误/诊断区域展示。

---

## 2. 计划版本与日行展示规则

| 规则 | 行为 |
|------|------|
| 可见版本（Fake） | 仅 `PublishStatus=Published` **且** `IsActive=true`；Draft 等不返回 |
| 日计划行 | 保留原始 `PlanDate` 与 `PlanQuantity`；按月查询、按日行展示 |
| 禁止 | 前端按天数均摊月总量、伪造日行、预填 Fake/当前月份 |
| 激活版本信息 | 展示 `meta.activePlanVersionId` / `activePlanVersionNo` / `dataSourceIdentifier` |
| 正式规则 | Excel 发布、激活、回退将在后续阶段实现并【待现场确认】 |

API 细节见 `docs/api-monthly-production-plan.md`。Fake 夹具计划月约 `2026-03`（日行约 `2026-03-10`～`2026-03-12`）；页面**不会**自动填入该月份。

---

## 3. Fake 数据标识

成功响应展示：

- `meta.reportCode`（`monthly_production_plan`）
- `meta.dataAccessMode` + `meta.isFake`（Chip：**Fake 数据**）
- `meta.activePlanVersionId` / `activePlanVersionNo` / `dataSourceIdentifier`
- `meta.fakeVersionRuleNote` / `meta.dailyPlanLinePrinciple`

---

## 4. 数据范围与工厂选择

| 场景 | 行为 |
|------|------|
| 仅一个非全局授权工厂 | 自动选中该工厂 |
| 多个工厂 | 必须用户选择 |
| `dataScope.isGlobal`（含 SystemAdmin） | 须**显式**选择一个工厂；选项来自 Fake 演示工厂目录，禁止任意手输 FactoryId |
| 车间/产线 | 不在当前授权范围内的选项不显示；提交前再次校验 |
| PlanMonth | 客户端 `PlanMonthFormat` 严格校验 `yyyy-MM`，与 API 一致 |

前端筛选仅为体验层；**授权与数据范围以 API 服务端为准**（越权 → 403）。复用 `ReportDataScopeOptions`。

---

## 5. 异常与离线边界

| 情况 | 行为 |
|------|------|
| 401 | 清除本地会话，跳转登录页；不保留报表数据 |
| 403 | 展示「无权访问该数据范围」，清除已展示结果 |
| 网络失败 / 离线 | 展示「数据需联网获取」；不缓存、不伪造报表 JSON |
| 空结果 | 200 + 空行 → 空状态提示 |

**禁止**将筛选结果、API 响应、Cookie、CSRF Token 写入 LocalStorage / SessionStorage。

PWA 壳离线规则见 `docs/mobile-pwa.md`；本页查询按钮在离线时禁用。

---

## 6. 测试说明

- **单元测试**：工厂/组织范围选项、PlanMonth `yyyy-MM` 必填与格式、Published+Active 提示与 Draft 不展示语义、PlanDate 属于所选月份、日行原则文案、查询路径拼装、前端合计不作均摊、401/403/网络 ProblemDetails 语义（`tests/FactoryReport.UnitTests/Client/MonthlyProductionPlanClientTests.cs`）。
- **浏览器 E2E**：本 Cloud 环境未配置 Playwright/Selenium；**未执行**真机/浏览器端到端。现场需验证 360px 布局、登录后查询与 Cookie 同源。

---

## 7. 关键代码

| 职责 | 路径 |
|------|------|
| 页面 | `Client/Pages/Reports/MonthlyPlan.razor` |
| 范围选项 | `Client/Services/Display/ReportDataScopeOptions.cs`（复用） |
| 月份校验 | `Client/Services/Display/PlanMonthFormat.cs` |
| 展示提示 | `Client/Services/Display/MonthlyProductionPlanDisplayHints.cs` |
| 展示汇总 | `Client/Services/Display/MonthlyProductionPlanDisplaySummary.cs` |
| API 客户端 | `Client/Services/Api/ReportsApiClient.cs` |
| 加载/空/错 | `Client/Components/ReportQueryFeedback.razor` |
