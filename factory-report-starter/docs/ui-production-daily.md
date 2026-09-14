# 生产日报移动端页面（阶段 13）

> `FactoryReport.Client` 页面：`/reports/production-daily`。  
> 接入只读 `GET /api/v1/reports/production-daily`；**不**实现导出、打印、图表、编辑或写入。

---

## 1. 页面字段与数据来源

| UI 区域 | 内容 | 来源 |
|--------|------|------|
| 标题 | 生产日报 | 固定文案 |
| 工厂 | 下拉，必选 | `/api/v1/auth/me` → `dataScope`（见 §3） |
| 开始/结束日期 | 日期选择，必选 | **用户选择或确认**；不预填「今天」、不硬编码 Fake 日期冒充有数据 |
| 车间 / 产线 | 可选下拉 | 仅展示授权范围内的选项 |
| 产品编码 | 可选文本 | 原样传给 API `productCode` |
| 元数据 | `reportCode`、Fake 标识、`filters` 回显、良率口径声明 | API `meta` |
| 汇总卡 | 实际/良品/不良/报废/返工/检验 | 对**当前返回行**做前端展示合计（非正式口径） |
| 明细 | 逐日/组织/产品行 + `yieldRate` | API `rows[]` 原始字段 |

- 使用 `ReportsApiClient.GetProductionDailyAsync`；**不**在前端重算良率或混合数量口径。
- `YieldRate == null` 显示 **「—」**，不得显示 `0%`。
- Correlation ID 仅在错误/诊断区域展示。

---

## 2. Fake 数据标识

成功响应展示：

- `meta.reportCode`（`production_daily`）
- `meta.dataAccessMode` + `meta.isFake`（Chip：**Fake 数据**）
- `meta.yieldRateDisclaimer`（Fake 测试口径声明）

Fake 夹具日期见 `docs/fake-data.md`（约 `2026-03-10`～`2026-03-12`）。页面**不会**自动填入这些日期；用户需自行选择后查询。

---

## 3. 数据范围与工厂选择

| 场景 | 行为 |
|------|------|
| 仅一个非全局授权工厂 | 自动选中该工厂 |
| 多个工厂 | 必须用户选择 |
| `dataScope.isGlobal`（含 SystemAdmin） | 须**显式**选择一个工厂；选项来自 Fake 演示工厂目录（与夹具 Id 对齐），禁止任意手输 FactoryId |
| 车间/产线 | 不在当前授权范围内的选项不显示；提交前再次校验 |
| 工厂级/全局 | 车间/产线可从 Fake 演示目录展开（仍须落在所选工厂下） |

前端筛选仅为体验层；**授权与数据范围以 API 服务端为准**（越权 → 403）。

---

## 4. 异常与离线边界

| 情况 | 行为 |
|------|------|
| 401 | 清除本地会话，跳转登录页；不保留报表数据 |
| 403 | 展示「无权访问该数据范围」，清除已展示结果 |
| 网络失败 / 离线 | 展示「数据需联网获取」；不缓存、不伪造报表 JSON |
| 空结果 | 200 + 空行 → 空状态提示 |

**禁止**将筛选结果、API 响应、Cookie、CSRF Token 写入 LocalStorage / SessionStorage。

PWA 壳离线规则见 `docs/mobile-pwa.md`；本页查询按钮在离线时禁用。

---

## 5. 测试说明

- **单元测试**：筛选范围选项、`YieldRate` null 展示、查询路径拼装、401/403/网络 ProblemDetails 语义（`tests/FactoryReport.UnitTests/Client/`）。
- **浏览器 E2E**：本 Cloud 环境未配置 Playwright/Selenium；**未执行**真机/浏览器端到端。现场需验证 360px 布局、登录后查询与 Cookie 同源。

---

## 6. 关键代码

| 职责 | 路径 |
|------|------|
| 页面 | `Client/Pages/Reports/ProductionDaily.razor` |
| 范围选项 | `Client/Services/Display/ReportDataScopeOptions.cs` |
| 比率展示 | `Client/Services/Display/RatioDisplayFormatter.cs` |
| 展示汇总 | `Client/Services/Display/ProductionDailyDisplaySummary.cs` |
| API 客户端 | `Client/Services/Api/ReportsApiClient.cs` |
| 加载/空/错 | `Client/Components/ReportQueryFeedback.razor` |
