# 授权与组织数据范围（阶段 11）

> 本阶段在 Fake 模式下强制 **报表只读功能权限（ReportRead）** 与 **组织数据范围**。  
> **不连接 Oracle**；不实现用户管理、授权分配 UI、角色×报表细粒度矩阵。

---

## 1. 角色与数据范围规则

组织层级：`Factory → Workshop → ProductionLine`。

| 角色 | 功能权限（本阶段） | 数据范围 |
|------|-------------------|----------|
| `SystemAdmin` | `ReportRead` | 全局（服务端账号 `IsGlobal=true`） |
| `FactoryAdmin` | `ReportRead` | 仅明确授权的工厂（工厂级：该厂全部车间/产线） |
| `ProductionManager` | `ReportRead` | 仅明确授权的组织范围 |
| `QualityUser` | `ReportRead` | 仅明确授权的组织范围 |
| `Viewer` | `ReportRead` | 仅明确授权的组织范围 |

要点：

- **不默认继承同级组织范围**（授权车间 A 不自动获得车间 B）。
- 数据范围来自 **服务端账号配置**（`IUserScopeResolver` / `ILocalAccountStore`），**不是** Cookie 中的角色 Claim、也不是客户端 `factoryId` / 自定义 Header。
- 五个角色均可 `ReportRead`。更细的「角色可访问哪些报表」矩阵 **【待后续确认】**，本阶段不虚构限制。

策略名：`AuthorizationPolicies.ReportRead`。

---

## 2. 401 与 403 语义

| 状态 | 条件 |
|------|------|
| **401** | 未登录访问受保护报表 API；`/me` 未登录 |
| **403** | 已登录，但请求的 `factoryId` / `workshopId` / `productionLineId` **超出**服务端授权范围 |
| **400** | 参数校验失败（如缺失必填 `factoryId`、日期非法）——与权限无关 |

超出范围时 **必须 403**，不得返回空 `rows` 伪装成 200 成功。

匿名仍可访问：`/health`、`/health/live`、`/health/ready`、`POST /api/v1/auth/login`（以及 logout 的 Antiforgery 流程本身）。

---

## 3. 查询范围交集规则

1. 请求仍须带必填筛选（如 `factoryId`）；参数校验先于范围强制时返回 400。
2. 解析当前用户 **服务端** `IDataScope`。
3. 显式请求的工厂/车间/产线若不在授权内 → **403**。
4. 工厂级授权时，额外校验车间/产线是否归属该工厂（防伪造异厂组织 Id）。
5. 未显式传车间/产线时，用授权 **收窄** 有效筛选（单车间/单产线授权会写入有效 `WorkshopId`/`ProductionLineId`；多授权时用 `Authorized*Ids` 集合过滤）。
6. 客户端 query、伪造 Header、伪造角色 Claim **均不得扩大** 范围。

关键类型：

- `DataScopeGrant` / `IDataScope` / `UserDataScope`
- `IUserScopeResolver` / `IReportQueryScopeService`
- `DataScopeIntersection`

---

## 4. Fake 授权数据（确定性）

对齐 `DeterministicFakeFixture` 组织 Id：

| 用户名 | 角色 | 数据范围 |
|--------|------|----------|
| `sysadmin` | SystemAdmin | 全局 |
| `factoryadmin` | FactoryAdmin | 工厂 1（`FactoryDemo1Id=1`）工厂级 |
| `prodmanager` | ProductionManager | 工厂 1 / 车间 A（`WorkshopAId=10`） |
| `qualityuser` | QualityUser | 工厂 1 / 车间 B / 产线 B1（`20` / `201`） |
| `viewer` | Viewer | 工厂 2（`FactoryDemo2Id=2`）工厂级 |

测试口令仅见测试代码与 `docs/authentication.md`，不得写入 README 作生产默认密码。

`GET /api/v1/auth/me`（及登录响应）返回 `dataScope` 摘要：`isGlobal` + `grants[]`（factoryId / workshopId / productionLineId）。

---

## 5. 受保护报表 API

均 `RequireAuthorization(ReportRead)`：

- `GET /api/v1/reports/production-daily`
- `GET /api/v1/reports/work-order-progress`
- `GET /api/v1/reports/quality-statistics`
- `GET /api/v1/reports/production-plan-achievement`
- `GET /api/v1/reports/monthly-production-plan`

`factoryId` 仍为必填筛选条件（缺失 → 400）。

---

## 6. 未来 Oracle 替换点【待现场确认】

| 位置 | 说明 |
|------|------|
| `ILocalAccountStore` Oracle 实现 | 正式账号、角色、**组织授权范围**持久化 |
| `IUserScopeResolver` | 可复用 `LocalAccountUserScopeResolver`，或独立 Oracle 范围表 |
| `Infrastructure/Persistence/Oracle/` | 见 `OraclePersistencePlaceholder` |
| 配置 | `FactoryReport:Authentication:AccountStore=Oracle`（本阶段未实现，配置即失败） |

本阶段禁止：DbContext、Migration、DDL、真实连接。

---

## 7. 明确待后续确认

- [ ] 正式权限分配流程与用户管理 UI
- [ ] 角色 × 报表细粒度访问矩阵（本阶段五角色均可 ReportRead）
- [ ] 授权审计字段与保留策略
- [ ] Oracle 账号/范围表结构

Cookie、Antiforgery、日志脱敏、Correlation ID、ProblemDetails 行为与阶段 10/2 保持一致；数据范围拒绝映射为 403 ProblemDetails。
