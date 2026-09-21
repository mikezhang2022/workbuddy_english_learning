# 现状盘点（商业化第二阶段 · 代码为准）

> 盘点日期：2026-09-21。区分三层：**代码已实现** / **测试已通过** / **现场已验证**。  
> 本文件只描述 `factory-report-starter/`，不以历史 README 声称代替代码事实。

## 1. 现有模块

| 项目 | 类型 | 目标框架 | 代码状态 |
|------|------|----------|----------|
| `FactoryReport.Api` | ASP.NET Core Web API | net10.0 | Cookie 认证 + 五张只读报表 + **Excel 导入流水线** + 健康检查 |
| `FactoryReport.Client` | Blazor WASM + PWA（MudBlazor） | net10.0 | 登录壳、概览、报表列表、五张报表页、**导入列表/详情（PC 可写，手机只读）** |
| `FactoryReport.Admin` | Blazor Server | net10.0 | 空壳首页（标识开发中） |
| `FactoryReport.Worker` | Background Worker | net10.0 | 启动/停止/心跳日志，不读 MES/Oracle |
| `FactoryReport.Domain` | 类库 | net10.0 | 组织/主数据/生产/计划/导入/角色策略 + 导入行错误与状态流转辅助 |
| `FactoryReport.Application` | 类库 | net10.0 | 报表服务、**Excel 导入服务/校验/模板**、认证与数据范围 |
| `FactoryReport.Infrastructure` | 类库 | net10.0 | Fake 夹具与仓储、**Fake 导入工作区**、**MiniExcel**、PBKDF2、Oracle **占位** |
| `tests/*` | xUnit | net10.0 | 单元 + 集成（含导入校验/流转/权限） |
| `docs/` | 文档 | — | 含 `excel-import.md`、`phase-02-result.md` |
| `.github/workflows/factory-report-starter.yml` | CI | — | paths 限定 `factory-report-starter/**` |
| `deploy/{iis,oracle,windows-service}/` | 占位目录 | — | **空**，无部署脚本 |

无 Node/Vue/`package.json`；PC 与手机浏览器共用 `FactoryReport.Client`。

## 2. 已验证功能（本阶段在云端执行）

| 能力 | 验证方式 | 层级 |
|------|----------|------|
| 解决方案构建 | `dotnet build` | 测试已通过 |
| 单元/集成测试 | `dotnet test` | 测试已通过（见 `phase-02-result.md`） |
| Excel 上传→校验→发布→回退 | 集成测试 + 手工 API | 测试已通过 |
| 未登录导入 → 401；Viewer 上传 → 403；越权工厂 → 403 | 集成测试 | 测试已通过 |
| `DataMode=Oracle` 启动显式失败 | 单元测试 | 测试已通过 |
| CI workflow（paths 过滤） | 仓库文件已添加 | **代码已实现**（云端 Actions 跑通以 PR 为准） |

浏览器 PC/390px 截图：见 `phase-02-result.md` 产物路径。

## 3. 代码已实现、但本阶段未全部重新手工验收的能力

- 五张报表 API + Client 页；集成测试覆盖 API。
- PWA Manifest + Service Worker 应用壳（不缓存 `/api`）。
- Admin / Worker 可编译启动，但无业务闭环。

## 4. 尚未验证 / 明确不支持

| 项 | 状态 |
|----|------|
| 真实 Oracle 连接与查询 | **不支持**（启动抛错；无静默回退 Fake） |
| 正式账号持久化（`AccountStore=Oracle`） | **不支持** |
| 导入数据 Oracle 持久化 | **未实现**；Fake 内存 |
| 实际导入数据驱动计划达成报表 | **未接线**；发布行仅存导入工作区 |
| MES 同步 | Worker 仅心跳 |
| 生产部署 | `deploy/` 空 |
| 工厂现场网络/证书/对账 | **现场已验证：无** |

## 5. 主要缺口（相对商业化产品）

1. Oracle 业务库与应用配置库真实持久化未实现。
2. Admin 运维端与细粒度权限分配 UI 未实现。
3. 多客户映射模板待现场填写。
4. 第三方组件许可清单已更新；**未**做法律审查。

## 6. 本阶段相对第一阶段的关键增量

- Excel 导入端到端（模板/上传/校验/发布/回退），复用 `ImportBatch` / `DatasetVersionState`。
- MiniExcel 1.41.3（Apache-2.0，无 NuGet 传递依赖）。
- CI：`.github/workflows/factory-report-starter.yml`（paths 限定）。
- 文档：`excel-import.md`、`phase-02-result.md` 等。
