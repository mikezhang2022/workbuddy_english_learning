# 商业化第一阶段结果（phase-01）

> 区分三层：**代码已实现** / **测试已通过** / **现场已验证**。  
> 本阶段 = 可复现开发基线；**现场已验证：无**。

## 1. 仓库与分支

| 项 | 值 |
|----|-----|
| 仓库 | `mikezhang2022/workbuddy_english_learning` |
| 子项目 | `factory-report-starter/` |
| 开发分支 | `feat/bi-commercial-phase-01` |
| 基线起点（main） | `62b2cbf`（`fix: break Blazor auth DI cycle that froze Client on loading`） |
| 最终提交号 | `bfb8a0a1a841e6e6b140cce5716f0875747dc6f0`（功能基线 `8c1d33c`） |
| Cloud Agent | https://cursor.com/agents/bc-7ba8eb4b-88a0-41f6-9c24-a5437165de46 |
| PR | https://github.com/mikezhang2022/workbuddy_english_learning/pull/4 |
| 同源 Agent 分支 | `cursor/bi-2a49`（与 `feat/bi-commercial-phase-01` 同 tip；评审请以 feat 分支为准） |

## 2. 复用与本阶段变更

### 复用（代码已实现，本阶段验证）

- Domain / Application / Infrastructure 分层与五张报表 API
- Fake 确定性夹具与 Cookie 认证、数据范围
- Blazor WASM Client（MudBlazor）登录壳与生产日报等页面
- 单元测试 + 集成测试套件

### 本阶段新增 / 修复

| 变更 | 说明 |
|------|------|
| `DataMode=Oracle` 显式失败 | 禁止静默回退 Fake（对齐 AccountStore=Oracle） |
| 全局「演示数据 / Fake」横幅 | `DemoDataBanner` + MainLayout |
| 首页报表列表 + `/reports` | Overview / Reports/Index |
| 手机适配 CSS | 防整体横向溢出；宽表容器内滚动 |
| CORS 增加 `127.0.0.1` 开发源 | 修复跨主机开发访问 |
| Client `ApiBaseUrl` | 与开发主机一致（`127.0.0.1:5161`）；文档强调主机名匹配 |
| Bootstrap utilities 引用 | 使 `d-none`/`d-md-*` 生效 |
| 文档 | `current-state.md`、`architecture.md`、`development.md`、`third-party-components.md`、本文件 |
| `scripts/dev-bootstrap.sh` | 可重复安装 SDK + restore/build |
| 单元测试 | Oracle/AccountStore 不得静默 Fake |

## 3. 关键改动文件（主要）

```
factory-report-starter/docs/current-state.md          (新)
factory-report-starter/docs/architecture.md           (重写强化边界)
factory-report-starter/docs/development.md            (新)
factory-report-starter/docs/phase-01-result.md         (新)
factory-report-starter/docs/third-party-components.md  (新)
factory-report-starter/docs/fake-data.md               (Oracle 显式失败说明)
factory-report-starter/scripts/dev-bootstrap.sh        (新)
factory-report-starter/src/FactoryReport.Infrastructure/DependencyInjection.cs
factory-report-starter/src/FactoryReport.Api/appsettings.Development.json
factory-report-starter/src/FactoryReport.Api/appsettings.Example.json
factory-report-starter/src/FactoryReport.Client/...（Layout/Overview/Reports/Index/DemoDataBanner/Login/CSS/index.html/Nav/ProductionDaily/appsettings）
factory-report-starter/tests/FactoryReport.UnitTests/FakeModeTests.cs
factory-report-starter/tests/FactoryReport.UnitTests/FactoryReport.UnitTests.csproj
```

## 4. 构建 / 测试 / 启动命令与结果

环境：Ubuntu 24.04，.NET SDK **10.0.401**（本机原无 SDK，经 `dotnet-install.sh` 安装）。

```bash
cd factory-report-starter
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet restore FactoryReport.sln
dotnet build FactoryReport.sln
# 结果：Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test FactoryReport.sln
# IntegrationTests: Passed 77
# UnitTests: Passed 219
# 合计 Passed 296，Failed 0
```

历史警告：本次构建 **0 警告**（无历史警告清单可列）。

启动：

```bash
dotnet run --project src/FactoryReport.Api --urls http://127.0.0.1:5161
dotnet run --project src/FactoryReport.Client --urls http://127.0.0.1:5269
```

### API 手工验证（测试已通过）

| 场景 | 结果 |
|------|------|
| `GET /health` | Healthy，`mode=Fake`，`isFake=true` |
| 未登录报表 | **401** |
| 登录 sysadmin | 200 + Cookie |
| 生产日报 2026-03-10～11 | **6 行**，`isFake=true` |
| 2026-03-12～12 | **0 行**（空结果） |
| startDate > endDate | **400**，StartDate must not be later than EndDate |

### 浏览器验证（测试已通过）

Playwright + 系统 Chrome（headless）：

| 场景 | 结果 |
|------|------|
| 登录 → 概览 | 演示横幅 + 报表列表 |
| 生产日报查询 | 6 行明细 + Fake 标识 |
| 390px 视口 | `scrollWidth==clientWidth==390`（无整体横向溢出）；卡片明细；查询按钮可用 |
| 日期错误 | 客户端提示「开始日期不能晚于结束日期」 |

截图产物：

- `/opt/cursor/artifacts/pc-overview-after-login.png`
- `/opt/cursor/artifacts/pc-production-daily-query.png`
- `/opt/cursor/artifacts/mobile-390-production-daily.png`
- `/opt/cursor/artifacts/mobile-date-validation.png`

## 5. 干净环境启动步骤

1. 安装 .NET 10：`bash scripts/dev-bootstrap.sh`（或见 `docs/development.md`）
2. 工作目录：`factory-report-starter/`
3. 启动 API：`http://127.0.0.1:5161`
4. 启动 Client：`http://127.0.0.1:5269`
5. 浏览器打开 **同主机名** 的 Client；登录 Fake 账号见 `docs/authentication.md` / `FakeLocalAccountStore`（如 `sysadmin` + `Dev-Only-SystemAdmin-Passw0rd!`，**仅开发**）
6. 生产日报夹具日期：`2026-03-10`～`2026-03-11`；工厂 Id=`1`

## 6. 未验证 / 阻塞 / 风险

| 项 | 层级 |
|----|------|
| 真实 Oracle 连接与查询 | **不支持**（配置即失败）；现场未验证 |
| Excel 导入流水线 | 仅领域/只读边界；未实现上传发布 |
| Admin / Worker 业务闭环 | 空壳 / 心跳 |
| 其它四张报表浏览器手工点选 | API 集成测试已通过；浏览器以生产日报为代表 |
| Oracle 驱动再分发许可 | 已记录于 `third-party-components.md`，**未**做法律审查 |
| 生产部署 | `deploy/` 空 |
| 根级 `.cursor/environment.json` | **未新增**（避免影响 monorepo 其它项目） |

## 7. 第二阶段建议（勿在本阶段开始）

1. 在现场问卷完成后实现 `Persistence/Oracle` 仓储，按 `DataMode` 切换，保留 Fake。
2. 账号 `AccountStore=Oracle` 与权限分配最小 Admin UI。
3. Excel 导入：模板校验 → ImportBatch → 发布/激活（按已有领域模型）。
4. 固化 CI（restore/build/test）与可选容器化 Fake 冒烟。
5. 完成第三方许可法务确认（尤其 Oracle 驱动）。

## 8. 验收对照

| 完成标准 | 状态 |
|----------|------|
| 开发基线可复现 | **测试已通过** |
| 现有成果保留 | **代码已实现** + 测试通过 |
| 至少一张报表 PC/手机可查 | **测试已通过**（生产日报） |
| 现场 Oracle/生产验收 | **未做** |
