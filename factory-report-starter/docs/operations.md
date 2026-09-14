# 运维与可观测性（阶段 2）

## 健康检查语义

| 端点 | 含义 | Fake 模式行为 |
|------|------|----------------|
| `GET /health` | 详细状态（含 DataMode / Fake 标识） | 返回 Healthy + `isFake=true` |
| `GET /health/live` | 存活（进程在） | 始终 Healthy；不探测外部依赖 |
| `GET /health/ready` | 就绪（可接流量） | **必须成功**；**禁止**连接 Oracle、MES 或任何外部服务 |

现场 Oracle 就绪探测【待现场确认】，后续阶段再接入；本阶段 readiness 仅表示进程与 DI 可用。

## 全局异常与 ProblemDetails

- 未处理异常由 `IExceptionHandler` 转为 RFC 7807 `ProblemDetails`（`application/problem+json`）。
- 响应扩展字段包含 `correlationId`、`traceId`。
- Development / Testing：可包含异常消息与类型；Production：**不返回堆栈**，Detail 为通用说明。
- 404 等状态码通过 `UseStatusCodePages` 走 ProblemDetails 管道。

## Correlation ID

- 请求头：`X-Correlation-ID`（亦接受 `X-Request-ID` 作为透传来源）。
- 若缺失则生成 GUID（N 格式），写入响应头 `X-Correlation-ID`。
- 写入日志 Scope（`CorrelationId`），便于集中采集关联。

## 日志策略

| 环境 | 格式 |
|------|------|
| Development / Testing | 控制台可读（SimpleConsole，含 Scope） |
| Production | JSON Console（适合集中采集） |

### 日志脱敏规则（强制）

**禁止**写入日志：

- 密码、Token、Bearer / Cookie / Authorization 头完整值
- Oracle 连接字符串、Wallet 路径与内容、密钥材料
- 请求体中的凭证字段、客户敏感业务明细（后续报表查询结果默认不落完整明细）

允许：CorrelationId、路径、方法、状态码、耗时、异常类型与脱敏后的消息。

异常处理日志仅记录 Path / Method / CorrelationId，不记录 Headers 全集或 Body。

## 配置校验（Options）

- 节名：`FactoryReport`
- 绑定类型：`FactoryReportOptions`（`IOptions<T>` + `ValidateOnStart` + `IValidateOptions`）
- `DataMode`：仅允许 `Fake` / `Oracle`（大小写不敏感）；**默认 Fake**
- `Worker:HeartbeatIntervalSeconds`：5–3600，默认 30
- 示例文件：`appsettings.Example.json`（API / Worker）；仅占位，**严禁**真实连接串 / IP / 账号 / 密码 / Wallet

阶段 2 无论配置为何，Infrastructure 仍强制注册 Fake 实现，避免误连。

## Fake 与现场边界

| 项 | Fake（默认 / 云端） | 现场 |
|----|---------------------|------|
| API readiness | 成功，不连外部 | 后续可加 Oracle 检查【待现场确认】 |
| Worker | 启动/停止/心跳日志；明确 Fake；不同步 | 后续 MES 同步阶段 |
| 机密 | 仅 Example 占位 | 服务器环境变量 / 密钥库注入 |

Cursor Cloud 与本地默认开发必须保持 Fake，不得连接工厂内网、真实 Oracle 或 MES。
