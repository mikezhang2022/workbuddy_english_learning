# 认证与本地账号（阶段 10）

> 本阶段建立 **工厂本地账号密码 + ASP.NET Core 安全 Cookie** 认证基础。  
> **不连接 Oracle**；正式账号持久化、密码策略、锁定与重置均为后续阶段 / 【待现场确认】。

---

## 1. Cookie 登录流程

```text
客户端 POST /api/v1/auth/login { userName, password }
  → ILocalAccountAuthenticationService 校验（ILocalAccountStore + IPasswordHasher）
  → 失败：统一 401「Invalid username or password.」（不区分用户名不存在 / 密码错误）
  → 成功：SignIn Cookie（HttpOnly；Production 强制 Secure；SameSite=Lax）
  → 响应头附带 X-CSRF-TOKEN（供 logout / 未来写入端点）
  → 返回当前用户摘要（不含密码；阶段 11 起含可读 `dataScope` 摘要，非 Cookie Claim）

GET /api/v1/auth/me
  → 已登录：200 + 用户摘要
  → 未登录：401

POST /api/v1/auth/logout
  → 必须携带有效 Antiforgery（请求头 X-CSRF-TOKEN + Antiforgery Cookie）
  → SignOut，会话失效
```

| Cookie | 名称 | HttpOnly | Secure | SameSite |
|--------|------|----------|--------|----------|
| 认证会话 | `.FactoryReport.Auth` | 是 | Production=Always | Lax |
| Antiforgery | `.FactoryReport.Antiforgery` | 是 | Production=Always | Lax |

Cookie **不得**保存密码或敏感权限明细；仅承载用户标识、显示名与角色 Claim。

禁止：明文密码、浏览器 LocalStorage Token、自制弱加密。

---

## 2. 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/v1/auth/login` | 登录；AllowAnonymous |
| POST | `/api/v1/auth/logout` | 退出；Antiforgery 必填 |
| GET | `/api/v1/auth/me` | 当前用户；未登录 401 |

阶段 11 起，五张报表只读 API 均 `RequireAuthorization(ReportRead)`，并强制组织数据范围（见 `docs/authorization-and-data-scope.md`）。

---

## 3. 第一版角色

| 角色 | 说明 |
|------|------|
| `SystemAdmin` | 系统级管理（后续） |
| `FactoryAdmin` | 本工厂管理（后续） |
| `ProductionManager` | 生产相关查看（后续授权） |
| `QualityUser` | 质量相关查看（后续授权） |
| `Viewer` | 只读查看（后续授权） |

策略名称见 `Domain/Security/AuthorizationPolicies`（如 `RequireSystemAdmin`、`CanViewProductionReports`）。  
组织数据范围强制见阶段 11 / `docs/authorization-and-data-scope.md`。

规格中另有 `ReportDesigner` / `DataImporter` / `DataPublisher` 等角色，本阶段未启用。

---

## 4. Fake 与生产边界

| 项 | Development / Testing | Production |
|----|----------------------|------------|
| `FactoryReport:Authentication:AccountStore` | 默认 `Fake` | **禁止** `Fake` |
| 账号来源 | `FakeLocalAccountStore` 进程内内存 | 未来 `Oracle` LocalAccountStore |
| 测试口令 | 仅测试代码 / 本说明标注「仅开发测试」 | 不得写入 README 作默认管理员密码 |
| 启动行为 | 允许 Fake | Fake 时 **抛异常拒绝启动** |

Fake 账号全部 `IsTestOnlyAccount=true`，**不得当作正式生产账号**。

配置示例见 `appsettings.Example.json`（不含真实密码）。

---

## 5. 未来 Oracle 持久化位置

替换接口：`Application.Security.ILocalAccountStore`  

实现目录：`src/FactoryReport.Infrastructure/Persistence/Oracle/`  

导航占位：`OraclePersistencePlaceholder`（已列入 `ILocalAccountStore`）  

本阶段 **禁止**：DbContext、Migration、DDL、真实连接串、Wallet。

切换方式（未来）：`FactoryReport:Authentication:AccountStore=Oracle`，在 `DependencyInjection` 注册 Oracle 实现并移除 Fake；凭据由服务器环境注入。

---

## 6. 待后续实现 / 确认

- [ ] 正式密码复杂度与轮换策略【待现场确认】
- [ ] 账号创建、停用、重置流程【待后续阶段】
- [ ] 登录失败限流与临时锁定【待后续阶段】
- [ ] 审计日志字段与保留【待后续阶段】
- [x] 组织数据范围强制（阶段 11；正式分配 UI 仍待后续）
- [ ] 用户管理 UI【后续阶段】
- [ ] ASP.NET Core Identity 与 Oracle 表结构【待现场确认】

---

## 7. 日志脱敏（扩展阶段 2）

认证相关日志 **禁止**记录：

- 密码、完整请求体
- Cookie 值、Authorization / 认证头完整值
- Antiforgery 令牌明文（失败仅记结果）

允许：CorrelationId、Path、Method、UserId、脱敏后的失败原因类别。

详见 `docs/operations.md`。

---

## 8. 关键代码位置

| 职责 | 位置 |
|------|------|
| 角色 / 策略名 | `Domain/Security/` |
| `ICurrentUserAccessor` / `ILocalAccountStore` / 认证服务 | `Application/Security/` |
| Fake 账号 + PBKDF2 哈希 | `Infrastructure/Fake/FakeLocalAccountStore`、`Infrastructure/Security/` |
| Cookie / Antiforgery / 端点 | `Api/Security/`、`Api/Endpoints/AuthEndpoints.cs` |
| Production 守卫 | `FakeAccountStoreProductionGuard` |
