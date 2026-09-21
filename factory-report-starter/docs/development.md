# 开发指南（云端与本地）

> 工作目录一律为仓库内 `factory-report-starter/`。  
> **无真实 Oracle、无生产凭据**即可完整启动本基线（Fake 模式）。

## 1. 依赖

| 工具 | 本阶段验证版本 | 说明 |
|------|----------------|------|
| .NET SDK | 10.0.401（目标框架 net10.0） | 必需 |
| Node.js / npm | **不需要** | Client 为 Blazor WASM，无 `package.json` |
| Oracle 客户端 / 数据库 | **不需要** | 开发仅 Fake |
| Docker | 不需要 | 本基线不启 Oracle 容器 |

安装 SDK（可重复）：

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
dotnet --info
```

或执行：

```bash
bash scripts/dev-bootstrap.sh
```

## 2. 配置（仅占位，秘密走环境变量）

| 文件 | 用途 |
|------|------|
| `src/FactoryReport.Api/appsettings.json` | 默认 Fake |
| `src/FactoryReport.Api/appsettings.Development.json` | 开发 CORS（localhost Client） |
| `src/FactoryReport.Api/appsettings.Example.json` | 示例占位（含 Oracle 占位说明） |
| `src/FactoryReport.Client/wwwroot/appsettings.json` | `ApiBaseUrl` → `http://localhost:5161` |
| `src/FactoryReport.Client/wwwroot/appsettings.Example.json` | 示例 |

禁止提交真实连接串、密码、Wallet、证书私钥。现场注入示例：

```bash
# 仅现场/安全环境示例 —— 占位，勿填入真实值后提交
export FactoryReport__Oracle__ConnectionString='【待现场确认】'
export FactoryReport__DataMode='Fake'   # 本阶段必须 Fake；Oracle 会启动失败
```

## 3. 可重复命令

```bash
cd factory-report-starter

dotnet restore FactoryReport.sln
dotnet build FactoryReport.sln --no-restore
dotnet test FactoryReport.sln --no-build
```

分项：

```bash
dotnet test tests/FactoryReport.UnitTests/FactoryReport.UnitTests.csproj
dotnet test tests/FactoryReport.IntegrationTests/FactoryReport.IntegrationTests.csproj
```

CI（仓库根）：`.github/workflows/factory-report-starter.yml` —— 仅当 `factory-report-starter/**` 变更时触发；步骤为 checkout → .NET 10 → restore → build → test。不含 Docker/生产部署。

## 3.1 新增依赖（第二阶段）

| 包 | 版本 | 项目 | 用途 |
|----|------|------|------|
| MiniExcel | 1.41.3 | Infrastructure | xlsx 模板与解析（Apache-2.0） |

详见 `docs/third-party-components.md`、`docs/excel-import.md`。

## 4. 启动（Fake 开发路径）

终端 A — API（默认端口 **5161**）：

```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
cd factory-report-starter
dotnet run --project src/FactoryReport.Api --launch-profile http --urls http://localhost:5161
```

终端 B — Client（默认端口 **5269**）：

```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
cd factory-report-starter
dotnet run --project src/FactoryReport.Client --launch-profile http --urls http://localhost:5269
```

可选：

```bash
dotnet run --project src/FactoryReport.Worker
dotnet run --project src/FactoryReport.Admin
```

健康检查：

```bash
curl -s http://localhost:5161/health
curl -s http://localhost:5161/health/live
curl -s http://localhost:5161/health/ready
```

## 5. 开发登录（Fake，仅开发测试）

账号与口令定义在测试代码 / `docs/authentication.md`（如 `sysadmin` + `FakeLocalAccountStore.DevPassword_SystemAdmin`）。  
**禁止**当作生产默认密码；UI 登录页不展示明文口令。

curl 示例：

```bash
curl -s -c /tmp/fr.cookie -D - -X POST http://localhost:5161/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userName":"sysadmin","password":"Dev-Only-SystemAdmin-Passw0rd!"}'

curl -s -b /tmp/fr.cookie http://localhost:5161/api/v1/auth/me

# 生产日报（夹具有数据日期）
curl -s -b /tmp/fr.cookie \
  "http://localhost:5161/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-11"

# 空结果（Day3 无事实时可按夹具调整；或扩大到无数据区间）
curl -s -b /tmp/fr.cookie \
  "http://localhost:5161/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-12&endDate=2026-03-12"

# 日期错误 → 400
curl -s -b /tmp/fr.cookie \
  "http://localhost:5161/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-11&endDate=2026-03-10"

# 未登录 → 401
curl -s -o /dev/null -w "%{http_code}\n" \
  "http://localhost:5161/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-11"
```

### Excel 导入（第二阶段）

浏览器：登录后打开 `/imports`（PC 可上传/校验/发布/回退；手机只读列表/详情）。  
API 契约见 `docs/excel-import.md`。手工 curl 需先取 CSRF（登录响应头或 `GET /api/v1/auth/csrf`），再 `multipart` 上传。

```bash
# 模板下载（需登录 Cookie）
curl -s -b /tmp/fr.cookie -o /tmp/plan-template.xlsx \
  "http://127.0.0.1:5161/api/v1/imports/templates/monthly_production_plan/download"

# 未登录上传 → 401
curl -s -o /dev/null -w "%{http_code}\n" -X POST \
  "http://127.0.0.1:5161/api/v1/imports/batches"
```

浏览器：打开 `http://127.0.0.1:5269/login`（或 `http://localhost:5269/login`）。

**主机名必须一致**：`FactoryReportClient:ApiBaseUrl` 的主机与浏览器地址栏主机须同为 `127.0.0.1` 或同为 `localhost`。跨主机时 Cookie（SameSite=Lax）不会随 fetch 发送，表现为登录成功后仍 401。

## 6. 长驻服务与依赖安装分离

| 阶段 | 做什么 |
|------|--------|
| 安装（一次性/可重复） | 安装 SDK、`dotnet restore`、`dotnet build` |
| 长驻 | `dotnet run` API / Client（分终端或 tmux） |

不要在 restore/build 脚本里长期挂起 `run`。

## 7. Oracle 模式

本阶段 **不支持**。设置 `FactoryReport:DataMode=Oracle` 或 `Authentication:AccountStore=Oracle` 将在启动/DI 时 **显式失败**，不会悄悄改用 Fake。

## 8. 手机视口手工检查（目标环境）

```bash
# Chromium 示例（需本机有图形浏览器）
chromium --window-size=390,844 http://localhost:5269/reports/production-daily
```

或 DevTools 设备模式宽度 390。检查：筛选「查询」按钮可用；页面无整体横向溢出；宽表在容器内滚动。

## 9. 仓库级 Cursor 环境说明

本仓库为多项目 monorepo。根级 `.cursor/environment.json` **本阶段未新增**，以免影响 `phonics-app` 等其它目录。  
`factory-report-starter` 依赖安装步骤以本文与 `scripts/dev-bootstrap.sh` 为准。

## 10. 常见问题（Troubleshooting）

本节记录**开发机 / 受限沙箱**上的环境陷阱，与产品代码无关。

### 10.1 系统代理劫持 localhost

**现象**：开发机开启系统代理（例如 Clash / FlClash 监听 `127.0.0.1:7890`）时，浏览器访问本地前端 `http://127.0.0.1:5269` 会被代理劫持，页面停留在 `about:blank` 或全白，看起来像应用未启动或渲染失败。

**判断**：命令行 `curl http://127.0.0.1:5269/` 返回 **200**，但浏览器打开是白屏 → 基本可判定为代理劫持。

**解决**：

1. 浏览器进程启动前设置环境变量：`NO_PROXY=localhost,127.0.0.1`
2. Chromium 系浏览器再加启动参数：`--proxy-bypass-list="<-loopback>"`

后端 Api 服务本身不受影响。此问题只影响开发机浏览器访问本地前端，与产品代码无关。

### 10.2 受限沙箱 / CI 中 curl 的 cookie jar 不落盘

**现象**：`curl -c cookies.txt` 登录后 `cookies.txt` 不生成，后续带 `-b cookies.txt` 的请求恒返回 **401**「Not authenticated」，容易被误判为产品认证缺陷。

**正确做法**：在受限环境验证 Cookie 认证时，改用 Python `urllib` + `http.cookiejar.CookieJar` 的脚本方式，或使用 `--cookie` 显式传值，不要依赖沙箱可能禁止落盘的 `-c` cookie jar 文件。
