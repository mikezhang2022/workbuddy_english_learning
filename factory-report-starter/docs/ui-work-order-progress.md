# 工单进度移动端页面（阶段 14）

> `FactoryReport.Client` 页面：`/reports/work-order-progress`。  
> 接入只读 `GET /api/v1/reports/work-order-progress`；**不**实现导出、打印、图表、编辑或写入。

---

## 1. 页面字段与数据来源

| UI 区域 | 内容 | 来源 |
|--------|------|------|
| 标题 | 工单进度 | 固定文案 |
| 工厂 | 下拉，必选 | `/api/v1/auth/me` → `dataScope`（见 §3） |
| 车间 / 产线 | 可选下拉 | 仅展示授权范围内的选项 |
| 产品编码 / 工单号 | 可选文本 | 原样传给 API `productCode` / `workOrderCode` |
| 状态 | 可选下拉 | Fake 临时值：`Open` / `Completed` / `Closed`（非 MES 正式枚举） |
| 计划完成日起止 | 可选日期 | `plannedFinishFrom` / `plannedFinishTo`；**不**预填「今天」、不硬编码 Fake 日期 |
| 元数据 | `reportCode`、Fake 标识、`filters` 回显、延期/完成率口径声明 | API `meta` |
| 明细 | 工单行字段 + `completionRate` / `isOverdue` | API `rows[]` 原始字段 |

- 使用 `ReportsApiClient.GetWorkOrderProgressAsync`；**不**在前端重算完成率或延期。
- `CompletionRate == null`（计划数量为 0）显示 **「—」**，不得显示 `0%`。
- `IsOverdue=true` 用红色 Chip + 左侧强调边框标记。
- Correlation ID 仅在错误/诊断区域展示。

---

## 2. Fake 延期规则（临时）

页面展示 API `meta.overdueDisclaimer`，并固定提示：

> 延期判定为 Fake 临时规则（未完成且超过 PlannedFinishUtc），正式口径待现场确认。

服务端口径（仅展示，不重算）：

- `IsCompleted`：`status` ∈ {Completed, Closed}
- `IsOverdue`：`!IsCompleted` 且 `PlannedFinishUtc` 有值且比较时间晚于计划完成时间

详见 `docs/api-work-order-progress.md`。

---

## 3. Fake 数据标识

成功响应展示：

- `meta.reportCode`（`work_order_progress`）
- `meta.dataAccessMode` + `meta.isFake`（Chip：**Fake 数据**）
- `meta.overdueDisclaimer` / `meta.completionRateRule`

Fake 夹具工单见 `docs/fake-data.md`（如 `WO-DEMO-OVERDUE`）。页面**不会**自动填入筛选默认值冒充有数据。

---

## 4. 数据范围与工厂选择

| 场景 | 行为 |
|------|------|
| 仅一个非全局授权工厂 | 自动选中该工厂 |
| 多个工厂 | 必须用户选择 |
| `dataScope.isGlobal`（含 SystemAdmin） | 须**显式**选择一个工厂；选项来自 Fake 演示工厂目录，禁止任意手输 FactoryId |
| 车间/产线 | 不在当前授权范围内的选项不显示；提交前再次校验 |

前端筛选仅为体验层；**授权与数据范围以 API 服务端为准**（越权 → 403）。复用阶段 13 的 `ReportDataScopeOptions`。

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

- **单元测试**：筛选范围选项（复用 `ReportDataScopeOptionsTests`）、`CompletionRate` null →「—」、`IsOverdue` 展示标记、查询路径拼装、401/403/网络 ProblemDetails 语义（`tests/FactoryReport.UnitTests/Client/`）。
- **浏览器 E2E**：本 Cloud 环境未配置 Playwright/Selenium；**未执行**真机/浏览器端到端。现场需验证 360px 布局、登录后查询与 Cookie 同源。

---

## 7. 关键代码

| 职责 | 路径 |
|------|------|
| 页面 | `Client/Pages/Reports/WorkOrderProgress.razor` |
| 范围选项 | `Client/Services/Display/ReportDataScopeOptions.cs`（阶段 13 复用） |
| 比率展示 | `Client/Services/Display/RatioDisplayFormatter.cs`（完成率） |
| 延期展示 | `Client/Services/Display/WorkOrderOverdueDisplay.cs` |
| API 客户端 | `Client/Services/Api/ReportsApiClient.cs` |
| 加载/空/错 | `Client/Components/ReportQueryFeedback.razor` |
