# 现状盘点（商业化第一阶段 · 代码为准）

> 盘点日期：2026-09-20。区分三层：**代码已实现** / **测试已通过** / **现场已验证**。  
> 本文件只描述 `factory-report-starter/`，不以历史 README 声称代替代码事实。

## 1. 现有模块

| 项目 | 类型 | 目标框架 | 代码状态 |
|------|------|----------|----------|
| `FactoryReport.Api` | ASP.NET Core Web API | net10.0 | Cookie 认证 + 五张只读报表 + 健康检查 |
| `FactoryReport.Client` | Blazor WASM + PWA（MudBlazor） | net10.0 | 登录壳、概览、报表列表、五张报表页 |
| `FactoryReport.Admin` | Blazor Server | net10.0 | 空壳首页（标识开发中） |
| `FactoryReport.Worker` | Background Worker | net10.0 | 启动/停止/心跳日志，不读 MES/Oracle |
| `FactoryReport.Domain` | 类库 | net10.0 | 组织/主数据/生产/计划/导入/角色策略 |
| `FactoryReport.Application` | 类库 | net10.0 | 报表服务、数据访问抽象、认证与数据范围 |
| `FactoryReport.Infrastructure` | 类库 | net10.0 | Fake 夹具与仓储、PBKDF2、Oracle **占位** |
| `tests/FactoryReport.UnitTests` | xUnit | net10.0 | 领域/Fake/报表服务/Client 展示与认证 DI |
| `tests/FactoryReport.IntegrationTests` | xUnit + WebApplicationFactory | net10.0 | 健康检查、认证、授权、五张报表 API |
| `docs/` | 文档 | — | 架构、认证、五张 API/UI、Oracle 设计、Fake 等 |
| `deploy/{iis,oracle,windows-service}/` | 占位目录 | — | **空**，无部署脚本 |

无 Node/Vue/`package.json`；PC 与手机浏览器共用 `FactoryReport.Client`。

## 2. 已验证功能（本阶段在云端执行）

下列项在本阶段构建/测试/启动后标记为 **测试已通过**（仍 **非** 现场已验证）：

| 能力 | 验证方式 | 层级 |
|------|----------|------|
| 解决方案构建 | `dotnet build FactoryReport.sln` | 测试已通过 |
| 单元测试 / 集成测试 | `dotnet test` | 测试已通过（见 `phase-01-result.md`） |
| Fake 模式 API 启动与健康检查 | `dotnet run` + curl `/health*` | 测试已通过 |
| Cookie 登录 / `/me` | curl | 测试已通过 |
| 未登录访问报表 → 401 | curl | 测试已通过 |
| 生产日报有数据 / 空结果 / 日期错误 | curl | 测试已通过 |
| `DataMode=Oracle` / `AccountStore=Oracle` 启动显式失败 | 单元测试 | 测试已通过 |
| Client 构建 | `dotnet build` Client 项目 | 测试已通过 |

浏览器端 PC/390px 截图：以 `phase-01-result.md` 实际结果为准（云端无图形浏览器时不得伪称通过）。

## 3. 代码已实现、但本阶段未全部重新手工验收的能力

- 工单进度 / 质量统计 / 计划达成 / 月度生产计划：API + Client 页均存在；集成测试覆盖 API；本阶段手工查询以生产日报为代表。
- PWA Manifest + Service Worker 应用壳（不缓存 `/api`）。
- 组织数据范围求交与 403（集成测试覆盖）。
- Admin / Worker 可编译启动，但无业务闭环。

## 4. 尚未验证 / 明确不支持

| 项 | 状态 |
|----|------|
| 真实 Oracle 连接与查询 | **不支持**（`DataMode=Oracle` 启动抛错；无静默回退 Fake） |
| 正式账号持久化（`AccountStore=Oracle`） | **不支持**（启动抛错） |
| Excel 上传/校验/发布/回退 | **未实现**；仅有 `ImportBatch` / `DatasetVersionState` 领域与 Fake 只读边界 |
| MES 同步 | Worker 仅心跳；无 MES Reader 实现 |
| 生产部署（IIS / Windows Service） | `deploy/` 空目录 |
| 工厂现场网络、证书、手机摄像头、数据对账 | **现场已验证：无** |
| 自由拖拽设计器 / 多租户 SaaS / 支付 / AI 问数 / 原生 App | 本阶段明确不新增 |

## 5. 主要缺口（相对商业化产品）

1. Oracle 业务库与应用配置库的真实持久化与映射适配层未实现。
2. Excel 导入流水线未实现（接口边界已有）。
3. Admin 运维端与用户/权限分配 UI 未实现。
4. 多客户差异化映射模板需现场填写（`source-mapping-template.md`）。
5. 第三方组件许可清单需随依赖变更维护（见 `third-party-components.md`；**未**做法律审查）。

## 6. 本阶段相对代码基线的关键修正

- **禁止** `DataMode=Oracle` 静默注册 Fake 仓储：改为显式 `InvalidOperationException`。
- Client：全局「演示数据 / Fake」横幅；概览与 `/reports` 报表列表；生产日报宽表容器内横向滚动；390px 防整体横向溢出；登录页标明演示环境（不展示口令）。
- 补齐商业化阶段文档：`current-state.md`、`architecture.md`（强化边界）、`development.md`、`phase-01-result.md`、`third-party-components.md`。
