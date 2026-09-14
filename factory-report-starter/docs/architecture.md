# 架构说明（阶段 2）

## 目录职责

| 路径 | 职责 |
|------|------|
| `src/FactoryReport.Domain` | 领域模型与领域规则。无基础设施、无 UI、无数据库驱动。 |
| `src/FactoryReport.Application` | 应用服务、用例接口、DTO/抽象、强类型运行配置 POCOs。只依赖 Domain。 |
| `src/FactoryReport.Infrastructure` | 技术实现：Fake 内存数据、Options 校验、未来 Oracle 持久化。依赖 Application + Domain。 |
| `src/FactoryReport.Api` | ASP.NET Core HTTP API：健康检查、ProblemDetails、Correlation ID、结构化日志。 |
| `src/FactoryReport.Client` | Blazor WebAssembly PWA 手机端空壳。依赖 Application（共享契约），不依赖 Admin/Api 项目。 |
| `src/FactoryReport.Admin` | Blazor 管理后台空壳。依赖 Application，不依赖 Client。 |
| `src/FactoryReport.Worker` | 后台 Worker：Fake 模式下启动/停止/周期心跳日志；不连 Oracle/MES。 |
| `tests/FactoryReport.UnitTests` | 单元测试。 |
| `tests/FactoryReport.IntegrationTests` | 集成测试（云端以 Fake 为准；真实 Oracle 测试【待现场确认】/CI）。 |
| `tests/Fixtures` | 虚构/脱敏测试夹具。 |
| `docs/` | 规格与架构文档（含 `operations.md` 运维可观测性）。 |
| `deploy/` | 部署脚本占位（iis / windows-service / oracle）。 |

## 依赖规则

允许：

```text
Api            -> Application, Infrastructure
Admin          -> Application
Client         -> Application
Worker         -> Application, Infrastructure
Infrastructure -> Application, Domain
Application    -> Domain
Tests          -> 被测项目
```

禁止：

- Domain 依赖 Infrastructure / Application / 任何 UI / EF / Oracle 驱动
- Application 依赖 Infrastructure 或 UI 层
- Client ↔ Admin ↔ Api 之间互相项目引用
- 任何项目引入 SQL Server 专用包或代码（`Microsoft.Data.SqlClient`、`System.Data.SqlClient`、`SqlBulkCopy` 等）

## Fake 模式与现场环境边界

| 项 | Fake（默认） | 现场 |
|----|--------------|------|
| 数据 | 内存实现（`Infrastructure/Fake`） | Oracle 持久化 |
| 连接 | **禁止**连接任何数据库 | 服务器注入连接串/密钥 |
| 配置 | `FactoryReport:DataMode=Fake` | `Oracle`（【待现场确认】） |
| MES 同步 | Worker 仅心跳，不读 MES | 后续阶段 + 现场只读视图 |
| 健康检查 ready | 成功且不探测外部 | 后续可加 Oracle 检查【待现场确认】 |
| Cursor Cloud | 仅 Fake | 不得在云端连工厂内网/生产库 |

阶段 2 无论配置如何，Infrastructure 注册均强制 Fake 实现，避免误连。

运维细节（日志脱敏、Correlation ID、健康检查语义、配置校验）见 `docs/operations.md`。

## API 运行时基础（阶段 2）

- `GET /health`：详细状态 JSON（含 Fake 标识）
- `GET /health/live`：存活检查
- `GET /health/ready`：就绪检查（Fake 下不连外部）
- 全局异常 → RFC 7807 ProblemDetails
- `X-Correlation-ID` 透传/生成并写入响应头与日志 Scope
- Development：可读 Console；Production：JSON Console

## 未来 Oracle 接入位置【待现场确认】

接入点固定在：

`src/FactoryReport.Infrastructure/Persistence/Oracle/`

后续在此实现（本阶段仅为占位）：

1. EF Core `DbContext`（`Oracle.EntityFrameworkCore`）
2. ODP.NET / `Oracle.ManagedDataAccess.Core` 连接与批量写入
3. 实现 Application 中的仓储/数据抽象，替换 Fake
4. Schema、字符集、连接方式、只读视图名称 —— 全部【待现场确认】

真实连接字符串、密码、Wallet、Token **不得**写入仓库；仅允许 `appsettings.Example.json` 占位说明。
