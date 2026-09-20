# 架构说明（商业化基线）

> 以当前代码为准。更新于商业化第一阶段（2026-09-20）。  
> 保留既有分层与业务规则；发现口径冲突时记录于 `business-decisions.md`，**不静默更改指标口径**。

## 1. 目录职责

| 路径 | 职责 |
|------|------|
| `src/FactoryReport.Domain` | 领域模型与规则（组织、主数据、生产事实、计划、导入状态、报表编码、达成率、角色/策略名）。无基础设施、无 UI、无数据库驱动、无 Fake。 |
| `src/FactoryReport.Application` | 应用服务、DTO、运行配置 POCO、数据访问只读仓储抽象、`IReportDataQueryService`、五张报表服务、认证与数据范围抽象。只依赖 Domain。 |
| `src/FactoryReport.Infrastructure` | Fake 确定性夹具与内存仓储、Fake 本地账号、PBKDF2、Options 校验、UTC 时钟、`Persistence/Oracle/` **占位**。依赖 Application + Domain。 |
| `src/FactoryReport.Api` | HTTP API：健康检查、ProblemDetails、Correlation ID、Cookie 认证与 Antiforgery、认证端点、只读报表端点。 |
| `src/FactoryReport.Client` | Blazor WebAssembly PWA（MudBlazor）：PC 与手机浏览器共用。依赖 Application，不依赖 Admin/Api 项目。 |
| `src/FactoryReport.Admin` | Blazor Server 运维端空壳。 |
| `src/FactoryReport.Worker` | 后台 Worker：Fake 下心跳日志；不连 Oracle/MES。 |
| `tests/*` | 单元 / 集成测试（云端以 Fake 为准）。 |
| `docs/` | 规格与架构文档。 |
| `deploy/` | 部署脚本占位（当前为空）。 |

## 2. 依赖规则

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

- Domain 依赖 Infrastructure / Application / UI / EF / Oracle 驱动
- Application 依赖 Infrastructure 或 UI
- Client ↔ Admin ↔ Api 互相项目引用
- 引入 SQL Server 专用包或代码
- 浏览器持有 Oracle 连接凭据或直连 Oracle
- 复制 DataEase / DataGear / 积木报表等产品源码拼装私有核心

## 3. 商业化数据边界（必须遵守）

### 3.1 Oracle 业务库 vs 产品配置/导入数据 —— 职责分离

| 存储 | 职责 | 现状 |
|------|------|------|
| **Oracle 业务/MES 只读源** | 生产事实、工单、质量等现场数据（只读） | 未接入；表名/视图【待现场确认】 |
| **产品应用库（规划 Oracle Schema）** | 账号、授权范围、导入批次、数据集版本、报表配置等产品自有数据 | 仅有逻辑设计文档；**无 DDL、无 DbContext 实现** |
| **Fake 内存夹具** | 开发验证用确定性数据 | **代码已实现**；**不能**作为 Oracle 对接完成证明 |

不得把 MES 写权限交给本产品；不得把 Fake 结果写成「已完成现场对接」。

### 3.2 浏览器不得持有 Oracle 凭据

- Client 仅配置 `FactoryReportClient:ApiBaseUrl`（HTTP API 基址）。
- 所有业务查询经 `FactoryReport.Api` 服务端执行。
- Cookie 会话为 HttpOnly；不在 LocalStorage 存 Token/密码/连接串。

### 3.3 业务查询通过服务端执行

```text
Browser (Client) --HTTPS/HTTP+Cookie--> Api --(Fake 仓储 | 未来 Oracle 仓储)--> 数据
```

报表入口：`I*ReportService` → `IReportDataQueryService` → `I*ReadRepository`。

### 3.4 Fake 仅用于开发验证

- `FactoryReport:DataMode=Fake` + `Authentication:AccountStore=Fake` 为唯一可运行开发路径。
- **`DataMode=Oracle`：本阶段未实现，启动显式失败，禁止静默回退 Fake。**
- **`AccountStore=Oracle`：同上。**
- Fake 通过 ≠ 现场 Oracle 验收通过。

### 3.5 多客户差异：映射与适配，不复制产品代码

- 现场差异通过 `docs/source-mapping-template.md`、仓储适配与配置映射处理。
- 禁止为每个客户复制整套 Domain/Application/Client。

### 3.6 业务规则口径

- 已确认规则见 `docs/business-decisions.md` 与各 API 文档。
- Fake 临时口径必须标注「仅开发测试」。
- 发现冲突：记录决策，不静默改指标公式。

## 4. Fake 与现场边界

| 项 | Fake（默认，可运行） | 现场（未来） |
|----|----------------------|--------------|
| 数据 | `DeterministicFakeFixture` 内存 | Oracle 持久化 + MES Reader |
| 连接 | **禁止**连库 | 服务器注入连接串/密钥（不入库） |
| `DataMode` | `Fake` | `Oracle`（实现前配置即失败） |
| 健康检查 ready | 成功且不探测外部 | 可加 Oracle 探测【待现场确认】 |
| Cursor Cloud | 仅 Fake | 不得连工厂内网/生产库 |

## 5. API 运行时能力（代码已实现）

- `GET /health`、`/health/live`、`/health/ready`（匿名）
- `POST /api/v1/auth/login`、`POST /api/v1/auth/logout`、`GET /api/v1/auth/me`
- 五张报表 `GET /api/v1/reports/...`：均 `RequireAuthorization(ReportRead)` + 组织范围强制
- 校验失败 → 400；越权 → 403；未登录 → 401
- Production 环境禁止 Fake 账号存储（启动失败）
- 全局异常 → ProblemDetails；生产不返回堆栈

## 6. 前端边界

- 单一 Client：首页 `/`、报表列表 `/reports`、五张报表页、账号页、登录页。
- 全局展示「演示数据 / Fake」。
- 宽表容器内横向滚动；页面避免整体横向溢出。
- 不向用户展示数据库密码、内部堆栈。

## 7. Oracle 接入位置【待现场确认】

目录：`src/FactoryReport.Infrastructure/Persistence/Oracle/`（当前 `OraclePersistencePlaceholder`）。

后续实现顺序（未开始）：

1. EF Core `DbContext`（应用 Schema 可写数据）
2. ODP.NET 连接（凭据环境注入）
3. 实现 `I*ReadRepository`，按 `DataMode=Oracle` 注册（替换当前 throw）
4. MES Reader 只读层
5. `ILocalAccountStore` Oracle 实现

真实连接串、密码、Wallet **不得**写入仓库；仅 `appsettings.Example.json` 占位。

设计文档：`oracle-integration-plan.md`、`oracle-schema-design.md`、`oracle-site-questionnaire.md`。

## 8. Excel 导入边界（本阶段）

| 已有 | 未有 |
|------|------|
| `ImportBatch` / `DatasetVersionState` 领域模型 | HTTP 上传、解析、校验 UI |
| `IImportBatchReadRepository` + Fake 实现 | 发布/激活/回退流水线 |
| 月度计划 Fake 版本过滤（Published/Active） | 真实 Excel 文件处理 |

样例字段：`Id, FactoryId, DatasetCode, Status, SourceFileName, TotalRows, ErrorRows, CreatedAtUtc, CompletedAtUtc`（见 Domain）。正式模板与唯一键【待现场确认】。

## 9. 相关文档

- 运维：`operations.md`
- 认证 / 授权：`authentication.md`、`authorization-and-data-scope.md`
- Fake：`fake-data.md`
- 开发启动：`development.md`
- 第三方组件：`third-party-components.md`
