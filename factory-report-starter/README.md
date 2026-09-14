# Factory Report

工厂局域网移动报表平台。手机端采用 Web/PWA，运维端采用 Web 后台，服务端使用 ASP.NET Core，支持 **Oracle** 数据集、Excel/CSV 导入数据集、组合数据集以及二维码和 Code 128 扫码查询。

本目录 `factory-report-starter/` 为工厂报表项目根目录。所有开发与文档改动仅限本目录及其子目录。

## 当前状态（阶段 2）

已建立运行基础设施：健康检查、ProblemDetails、Correlation ID、结构化日志、Options 校验、Worker 心跳。

- 默认 **Fake 模式**（内存数据，不连接任何数据库）。
- 已添加 Oracle Provider 的 NuGet 引用，但**未配置真实连接、未连接 Oracle、未建 Schema/迁移**。
- Client / Admin 仅为标识「开发中 / Fake 模式」的空壳首页。
- Worker 仅输出启动/停止/心跳日志，不读取 MES/Oracle。

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

验证健康检查（另开终端）：

```bash
curl -s http://localhost:5000/health
curl -s http://localhost:5000/health/live
curl -s http://localhost:5000/health/ready
curl -s -D - -H "X-Correlation-ID: demo-001" http://localhost:5000/health/ready -o /dev/null
```

期望：`/health/ready` 在 Fake 模式下返回 Healthy，且过程中不连接 Oracle/MES。

配置示例见各宿主项目的 `appsettings.Example.json`。禁止提交真实密码、Token、连接字符串或 Oracle Wallet。

## 开始方式

1. 在 Cursor Cloud 中选择本仓库，工作目录固定为 `factory-report-starter/`。
2. 要求 Cursor 完整阅读上述规格、提示词、AGENTS 与 README。
3. 按 `FactoryReport_Cursor_Prompts.md` 分阶段推进；每阶段完成构建和测试后再进入下一阶段。
4. 每个阶段完成构建和测试后，再进入下一阶段（阶段 0 不写业务代码、不安装依赖、不部署）。

## 安全边界

本仓库不得提交真实 MES 数据、生产连接字符串、密码、访问令牌、证书私钥、数据库备份或客户内部文档。Cursor Cloud 只能使用虚构或脱敏测试数据，且不得连接工厂内网、真实 Oracle、MES 或 ERP。
