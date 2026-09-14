# Factory Report

工厂局域网移动报表平台。手机端采用 Web/PWA，运维端采用 Web 后台，服务端使用 ASP.NET Core，支持 **Oracle** 数据集、Excel/CSV 导入数据集、组合数据集以及二维码和 Code 128 扫码查询。

本目录 `factory-report-starter/` 为工厂报表项目根目录。所有开发与文档改动仅限本目录及其子目录。

## 当前状态（阶段 12 完成）

已实现 **移动 PWA 登录与应用壳**（`FactoryReport.Client`）：Cookie 会话、登录/退出、`/me` 与 dataScope 摘要展示、底部导航与各报表「即将接入」占位。报表数据页面与用户管理仍为后续阶段。

阶段 11：**报表授权与组织数据范围强制**（Fake 内存账号 + 服务端范围配置）。阶段 10 Cookie 认证保留；五张只读报表 API 均需登录，并按授权范围求交。

- 默认 **Fake 模式**（进程内确定性夹具 + Fake 测试账号，不连接任何数据库）。
- **认证 API**（见 `docs/authentication.md`）：
  - `POST /api/v1/auth/login`
  - `POST /api/v1/auth/logout`（Antiforgery）
  - `GET /api/v1/auth/me`（含 `dataScope` 摘要）
- **授权与数据范围**（见 `docs/authorization-and-data-scope.md`）：
  - 策略 `ReportRead`：五个角色均可
  - 未登录报表 → 401；组织越权 → 403（不返回空数据伪装成功）
  - 范围来自服务端账号配置，不得靠 query / Header / 角色 Claim 扩大
- **报表 API**（阶段 5–9 + 11 强制认证与范围；`factoryId` 仍必填）：
  - `GET /api/v1/reports/production-daily`
  - `GET /api/v1/reports/work-order-progress`
  - `GET /api/v1/reports/quality-statistics`
  - `GET /api/v1/reports/production-plan-achievement`
  - `GET /api/v1/reports/monthly-production-plan`
- Cookie：HttpOnly；Production 强制 Secure；SameSite=Lax；不存密码/权限明细。
- Fake 测试口令 **仅**存在于测试代码与 `docs/authentication.md`，**不**作为 README 生产默认管理员密码。
- **移动 PWA**（见 `docs/mobile-pwa.md`）：Manifest + Service Worker 应用壳；不缓存 `/api`；离线提示「数据需联网获取」。
- **Client 运行**：先启动 API，再 `dotnet run --project src/FactoryReport.Client`；`wwwroot/appsettings.json` 配置 `FactoryReportClient:ApiBaseUrl`（示例为 localhost，不含内网机密）。
- **本阶段 Client 不包含** 具体报表数据页、用户管理 UI；**服务端后续阶段仍不包含** 正式权限分配 UI、Oracle 账号持久化、Excel 写入、MES 同步（除非另行实现）。
- 已添加 Oracle Provider 的 NuGet 引用，但**未配置真实连接、未连接 Oracle、未建 Schema/迁移**。
- Admin 仍为标识「开发中 / Fake 模式」的空壳首页。
- Worker 仅输出启动/停止/心跳日志，不读取 MES/Oracle。
- 领域说明见 `docs/domain-model.md`；夹具说明见 `docs/fake-data.md`。

## 数据库口径（Oracle）

- 本地报表库与 MES/ERP 只读源均按 **Oracle** 方案设计与实现。
- 使用 Oracle 官方 .NET 驱动（**Oracle.ManagedDataAccess / ODP.NET**）与 EF Core Oracle Provider（**Oracle.EntityFrameworkCore**）。
- Oracle 版本、Schema 名称、字符集、连接方式、现场只读视图均为 **【待现场确认】**。
- 禁止新增任何 SQL Server 专用代码、脚本或配置。
- Cursor Cloud / 模拟开发使用 Fake 内存或文件数据，**不得连接数据库**（含 Oracle）。

## 文档

- `FactoryReport_Cursor_Development_Spec.md`：完整产品与技术规格。
- `FactoryReport_Cursor_Prompts.md`：Cursor Cloud 分阶段提示词。
- `AGENTS.md`：所有代码代理必须遵守的项目规则。
- `docs/architecture.md`：目录职责、依赖规则、Fake/现场边界、Oracle 接入点。
- `docs/operations.md`：日志策略、健康检查语义、配置校验、脱敏规则。
- `docs/authentication.md`：Cookie 登录、角色、Fake/生产边界、Oracle 账号替换点（阶段 10）。
- `docs/authorization-and-data-scope.md`：角色与数据范围、401/403、交集规则、Fake 授权、Oracle 替换点（阶段 11）。
- `docs/mobile-pwa.md`：PWA 登录流程、离线/缓存边界、本地运行方式（阶段 12）。
- `docs/domain-model.md`：领域对象职责、字段对应、已确认/Fake/待确认规则（阶段 3+）。
- `docs/fake-data.md`：Fake 夹具场景、限制与 Oracle 替换点（阶段 4+）。
- `docs/api-production-daily.md`：生产日报查询 API（阶段 5）。
- `docs/api-work-order-progress.md`：工单进度查询 API（阶段 6；参数、响应、Fake 延期规则与待确认项）。
- `docs/api-quality-statistics.md`：质量统计查询 API（阶段 7；参数、响应、Fake 良率/不良率口径与待确认项）。
- `docs/api-production-plan-achievement.md`：生产计划达成查询 API（阶段 8；参数、关联键、四边界状态与待确认项）。
- `docs/api-monthly-production-plan.md`：月度生产计划查询 API（阶段 9；参数、日计划行原则、Fake 版本规则与待确认项）。
- `docs/data-dictionary.md`：数据字典（规划口径）。
- `docs/business-decisions.md`：业务决策记录（已确认 / Fake 临时 / 待现场确认）。
- `docs/source-mapping-template.md`：源系统映射模板（待现场填写）。

## 本地构建与测试

在本目录 `factory-report-starter/` 下执行（需已安装 .NET 10 LTS SDK）：

```bash
dotnet restore FactoryReport.sln
dotnet build FactoryReport.sln --no-restore
dotnet test FactoryReport.sln --no-build
```

或一步执行：

```bash
dotnet restore FactoryReport.sln
dotnet build FactoryReport.sln
dotnet test tests/FactoryReport.UnitTests/FactoryReport.UnitTests.csproj
dotnet test tests/FactoryReport.IntegrationTests/FactoryReport.IntegrationTests.csproj
```

## 本地运行与健康检查

```bash
dotnet run --project src/FactoryReport.Api --urls http://localhost:5000
dotnet run --project src/FactoryReport.Worker
dotnet run --project src/FactoryReport.Admin
dotnet run --project src/FactoryReport.Client
```

移动 PWA 需 API 与 Client 同时运行，且 Development 下 API 的 `FactoryReport:MobileClientAllowedOrigins` 需包含 Client 源（见 `appsettings.Development.json`）。详见 `docs/mobile-pwa.md`。

验证健康检查（另开终端）：

```bash
curl -s http://localhost:5000/health
curl -s http://localhost:5000/health/live
curl -s http://localhost:5000/health/ready
curl -s -D - -H "X-Correlation-ID: demo-001" http://localhost:5000/health/ready -o /dev/null
```

期望：`/health/ready` 在 Fake 模式下返回 Healthy，且过程中不连接 Oracle/MES。

登录示例（**仅开发/测试 Fake 账号**；口令见 `docs/authentication.md` / 测试代码，勿用于生产）：

```bash
# 登录后保存 Cookie，并从响应头读取 X-CSRF-TOKEN 供 logout 使用
curl -s -c /tmp/fr.cookie -D - -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"sysadmin","password":"<见测试代码 DevPassword_SystemAdmin>"}'
curl -s -b /tmp/fr.cookie http://localhost:5000/api/v1/auth/me
```

生产日报示例（需先登录；Fake 夹具日期；`factoryId` 仍必填）：

```bash
curl -s -b /tmp/fr.cookie "http://localhost:5000/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-11"
```

工单进度示例：

```bash
curl -s -b /tmp/fr.cookie "http://localhost:5000/api/v1/reports/work-order-progress?factoryId=1&workOrderCode=WO-DEMO-OVERDUE"
```

质量统计示例：

```bash
curl -s -b /tmp/fr.cookie "http://localhost:5000/api/v1/reports/quality-statistics?factoryId=1&startDate=2026-03-10&endDate=2026-03-11"
```

生产计划达成示例：

```bash
curl -s -b /tmp/fr.cookie "http://localhost:5000/api/v1/reports/production-plan-achievement?factoryId=1&startDate=2026-03-10&endDate=2026-03-10"
```

月度生产计划示例：

```bash
curl -s -b /tmp/fr.cookie "http://localhost:5000/api/v1/reports/monthly-production-plan?factoryId=1&planMonth=2026-03"
```

配置示例见各宿主项目的 `appsettings.Example.json`。禁止提交真实密码、Token、连接字符串或 Oracle Wallet。

## 开始方式

1. 在 Cursor Cloud 中选择本仓库，工作目录固定为 `factory-report-starter/`。
2. 要求 Cursor 完整阅读上述规格、提示词、AGENTS 与 README。
3. 按 `FactoryReport_Cursor_Prompts.md` 分阶段推进；每阶段完成构建和测试后再进入下一阶段。
4. 每个阶段完成构建和测试后，再进入下一阶段（阶段 0 不写业务代码、不安装依赖、不部署）。

## 安全边界

本仓库不得提交真实 MES 数据、生产连接字符串、密码、访问令牌、证书私钥、数据库备份或客户内部文档。Cursor Cloud 只能使用虚构或脱敏测试数据，且不得连接工厂内网、真实 Oracle、MES 或 ERP。
