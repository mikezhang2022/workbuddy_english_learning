# 移动 PWA 客户端（阶段 12）

> `FactoryReport.Client`：移动优先 Blazor WebAssembly PWA，接入 Cookie 认证与报表 API 基座。  
> **本阶段不包含** 具体报表数据页面、用户管理、Excel、Oracle。

---

## 1. 运行方式

### 1.1 本地开发（API 与 Client 分端口）

1. 启动 API（Development 已配置 CORS 允许 Client 源）：

```bash
dotnet run --project src/FactoryReport.Api
```

默认 API：`http://localhost:5161`（以 launchSettings 为准）。

2. 配置 Client API 基地址：`src/FactoryReport.Client/wwwroot/appsettings.json` 中 `FactoryReportClient:ApiBaseUrl` 与 API 一致。

3. 启动 Client：

```bash
dotnet run --project src/FactoryReport.Client
```

默认 Client：`http://localhost:5269`。

4. 使用 Fake 测试账号登录（口令 **仅** 见 `docs/authentication.md` 与测试代码，勿写入生产配置）。

### 1.2 现场部署建议

- 推荐反向代理 **同源** 提供静态 PWA 与 `/api`（减少跨源 Cookie 复杂度）。
- Production 使用 HTTPS；Cookie `Secure` 由 API 强制。
- 示例配置不含内网地址、账号或机密；现场注入 `appsettings` 或环境变量。

---

## 2. 登录与会话流程

```text
用户提交登录表单
  → POST /api/v1/auth/login（credentials: include）
  → 成功：HttpOnly 会话 Cookie + 响应头 X-CSRF-TOKEN（仅内存保存，不写 Web Storage）
  → GET /api/v1/auth/me 恢复显示名、角色与 dataScope 摘要
  → 未登录访问 [Authorize] 页面 → 跳转 /login

退出：
  → GET /api/v1/auth/csrf（已登录，刷新 X-CSRF-TOKEN）
  → POST /api/v1/auth/logout（请求头 X-CSRF-TOKEN + Antiforgery Cookie）
  → 清除内存会话并跳转登录页
```

- 登录失败统一提示「用户名或密码不正确」，不暴露账号是否存在。
- **401**：会话失效，应重新登录。
- **403**：已登录但数据范围不允许（页面显示「无权访问该数据范围」）。

---

## 3. PWA 与离线边界

| 可离线 | 不可离线 / 不缓存 |
|--------|-------------------|
| 应用壳（index、Blazor 程序集、CSS、MudBlazor 静态资源、图标） | `/api/*` 任何响应 |
| 登录页与占位导航 UI | 认证 Cookie、Antiforgery、用户数据 |
| 「即将接入」占位文案 | 报表 JSON（不得当实时数据展示） |

- 离线时顶部显示：**「当前离线，报表数据需联网获取。」**
- Service Worker（发布版 `service-worker.published.js`）对 `/api/` **网络优先、不入缓存**。
- 开发版 `service-worker.js` 不启用离线缓存，便于调试。

---

## 4. 前端安全边界

- 密码、Cookie、CSRF Token **不** 写入 LocalStorage / SessionStorage / 日志。
- CSRF Token 仅保存在 `AuthSessionService` 进程内存；刷新页面后通过 `/api/v1/auth/csrf` 重新获取（需有效会话 Cookie）。
- 菜单与占位页隐藏未实现功能仅为体验；**授权与数据范围以服务端为准**。
- `dataScope` 仅展示 `/me` 返回的授权摘要，不推测或展示未授权组织。

---

## 5. 关键代码位置

| 职责 | 路径 |
|------|------|
| API 基地址配置 | `Client/wwwroot/appsettings.json`、`Configuration/ClientApiOptions.cs` |
| 认证 API 客户端 | `Client/Services/Api/AuthApiClient.cs` |
| 会话状态 | `Client/Services/Auth/AuthSessionService.cs` |
| 报表 API 基类 | `Client/Services/Api/ReportsApiClient.cs` |
| ProblemDetails / Correlation ID | `Client/Services/Api/ApiResponseHandler.cs` |
| 移动壳与导航 | `Client/Layout/MainLayout.razor`、`Components/MobileBottomNav.razor` |
| PWA 清单 / SW | `Client/wwwroot/manifest.webmanifest`、`service-worker*.js` |
| API CORS（开发） | `Api/Program.cs`、`FactoryReport:MobileClientAllowedOrigins` |

---

## 6. 测试说明

- **单元测试**：ProblemDetails 解析、数据范围展示格式化（`tests/FactoryReport.UnitTests/Client/`）。
- **集成测试**：`/api/v1/auth/csrf` 与既有 Auth API（`AuthApiTests`）。
- **浏览器端到端**：本 Cloud 环境未配置 Playwright/Selenium 流水线；未声称 PWA 安装或真机 E2E 已通过。现场需自行验证安装、离线壳与 Cookie 同源策略。

---

## 7. 阶段 13：生产日报数据页

已实现第一个真实可查询报表页：`/reports/production-daily`。字段、Fake 标识、范围与离线边界见 **`docs/ui-production-daily.md`**。其余四张报表仍为占位。
