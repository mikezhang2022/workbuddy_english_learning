# Oracle 现场接入准备计划（阶段 18）

> **本阶段为纯设计文档。** 不连接 Oracle / MES，不索取密码或证书，不实现持久化、Migration、DDL 或业务 API 变更。  
> 配套：`oracle-schema-design.md`（逻辑表）、`oracle-site-questionnaire.md`（现场问卷）、`source-mapping-template.md`（MES 字段映射留白）。

---

## 1. 现场环境待确认清单

下列项全部标注 **【待现场确认】**，由现场 DBA / 运维在 `oracle-site-questionnaire.md` 填写（**不含密码、Wallet 文件、完整连接串**）。

| 类别 | 项 | 状态 |
|------|----|------|
| 版本 | Oracle Database 主版本 / Patch 级别（如 19c / 21c） | 【待现场确认】 |
| 部署形态 | 单实例 / RAC；是否 CDB-PDB；服务名或 SID 形式 | 【待现场确认】 |
| Schema | 应用报表库 Schema（所有者）名称 | 【待现场确认】 |
| 字符集 | 数据库字符集 / 国家字符集（期望 AL32UTF8 等） | 【待现场确认】 |
| 时区 | DBTIMEZONE / 会话时区；与业务「生产日」关系 | 【待现场确认】 |
| 连接方式 | TNS 别名 / Easy Connect（EZConnect）形式；是否需 Instant Client | 【待现场确认】 |
| 认证方式 | DB 用户名密码 / Wallet / OS 认证 / mTLS 形式（只问形态，不收文件） | 【待现场确认】 |
| TLS | 是否强制加密、证书颁发方、是否需 Oracle Wallet | 【待现场确认】 |
| 网络 | 应用服务器到 DB 的可达性、端口、来源 IP 限制 | 【待现场确认】 |

驱动约定（已确认产品规则，见 `business-decisions.md` ①）：

- 应用侧使用 **Oracle.ManagedDataAccess / ODP.NET** 与 **Oracle.EntityFrameworkCore**。
- 禁止 SQL Server 专用驱动或脚本。
- Cursor Cloud **禁止**启用真实 Oracle 连接。

---

## 2. 最小权限原则

应用使用 **两个独立账号**（名称由现场提供；密码仅注入服务器 Secret，**永不入库**）。

### 2.1 应用库账号（读写应用 Schema）

用途：持久化本地账号、角色、组织授权、计划导入与版本、日计划行、审计记录。

| 权限范围（逻辑需求） | 说明 | 状态 |
|----------------------|------|------|
| 对本 Schema 对象的 `SELECT` / `INSERT` / `UPDATE` / `DELETE` | 仅限应用表（见 `oracle-schema-design.md`） | 【待现场确认】 |
| 对本 Schema 序列 / Identity 的使用 | 若使用 SEQUENCE 或 IDENTITY | 【待现场确认】 |
| **禁止** `CREATE USER` / `GRANT ANY` / `DBA` / `SYSDBA` | 最小权限 | 已确认原则 |
| **禁止** 对 MES Schema 的写权限 | 应用不得写 MES | 已确认原则 |
| 可选：对本 Schema 的 `CREATE TABLE` 等 DDL | 仅部署账号；**运行时应用账号建议无 DDL** | 【待现场确认】 |

部署账号（CI/运维执行 Migration）与运行时应用账号应分离【待现场确认】。

### 2.2 MES 源库只读账号

用途：经只读视图/同义词读取生产事实、质量、工单等；**应用侧永不写入 MES**。

| 权限范围（逻辑需求） | 说明 | 状态 |
|----------------------|------|------|
| 对约定只读视图（或同义词）的 `SELECT` | 视图清单与字段见 `source-mapping-template.md` | 【待现场确认】 |
| **禁止** `INSERT` / `UPDATE` / `DELETE` / `MERGE` 对 MES 业务表 | 硬性边界 | 已确认原则 |
| **禁止** 任意表扫描超出白名单视图 | 由 DBA 用视图封装过滤 | 【待现场确认】 |
| 会话资源限制（超时、并行度等） | 防拖垮 MES | 【待现场确认】 |

手机端 / Client **不得**直连 MES 或 Oracle；一律经服务端 API（已确认）。

---

## 3. 数据归属划分

### 3.1 由应用 Oracle Schema 管理（读写）

| 数据 | 领域对象 / 抽象 | 说明 |
|------|-----------------|------|
| 本地账号 | `LocalAccount` / `ILocalAccountStore` | 用户名、显示名、密码哈希（禁止明文）、启用状态 |
| 角色 | `AppRoles` 绑定 | 账号↔角色多对多或等价结构 |
| 组织授权 | `DataScopeGrant` / `IDataScope` | 工厂 / 车间 / 产线授权范围 |
| 计划导入批次 | `ImportBatch` | Excel/CSV 导入追踪与状态 |
| 计划版本 | `DatasetVersionState`（亦称 PlanVersion） | Draft → Validating → Published → Disabled；`IsActive` |
| 月度计划头 | `MonthlyProductionPlan` | 工厂 + 计划月 + 版本 |
| 日计划行 | `DailyProductionPlanLine` | **必须保留 PlanDate**；不得月均摊生成 |
| 审计记录 | （后续正式模型） | 登录、导入、发布/激活等；字段【待产品确认】 |
| 组织主数据（可选） | `Factory` / `Workshop` / `ProductionLine` | 若现场不以 MES 为组织权威，则落应用库；是否同步自 MES【待现场确认】 |
| 产品主数据（可选） | `Product` | 同上；计划校验字典来源【待现场确认】 |

逻辑表设计见 `oracle-schema-design.md`。

### 3.2 仅从 MES 只读获取（应用不写）

| 数据 | 领域对象 | 说明 |
|------|----------|------|
| 生产事实 | `ProductionRecord` / `ProductionQuantities` | 产量分列；夜班归属等口径【待现场确认】 |
| 质量事实 | （质量报表聚合自生产/检验视图） | 良率分子分母【待现场确认】 |
| 工单事实 | `WorkOrder` | 状态枚举、延期规则【待现场确认】 |

字段级映射全部留白，填写模板：`docs/source-mapping-template.md`。

**组合报表**（如计划达成）：计划侧来自应用 Schema 已发布版本；实际侧来自 MES 只读；关联键见领域 `PlanAchievementKey`。

---

## 4. 替换边界与 DI 切换点

### 4.1 分层示意

```text
Application 抽象（不变）
  ├─ I*ReadRepository / IReportDataQueryService / ILocalAccountStore / IUserScopeResolver
  │
Infrastructure 实现（按模式切换）
  ├─ Fake（现行默认）
  │     Infrastructure/Fake/*
  │     DeterministicFakeFixture + Fake*ReadRepository（6 个）+ FakeLocalAccountStore
  │
  ├─ OracleRepository（未来 · 占位已存在）
  │     Infrastructure/Persistence/Oracle/
  │     应用 Schema：账号 / 授权 / ImportBatch / PlanVersion / DailyProductionPlanLine / 审计
  │     以及可选组织/产品维度表
  │
  └─ MES Reader（未来新增 · 只读访问层）
        建议目录：Infrastructure/Persistence/Oracle/Mes/ 或 Infrastructure/Mes/
        仅 SELECT 白名单视图 → 映射为 ProductionRecord / WorkOrder / 质量行
        实现 IProductionRecordReadRepository / IWorkOrderReadRepository 等「事实」接口
        （或由 Oracle 组合 Facade 委托 MES Reader；【待实现阶段确认】）
```

当前占位导航：`Infrastructure/Persistence/Oracle/OraclePersistencePlaceholder.cs`  
（列出待替换的 Application 接口名；无运行时行为。）

### 4.2 接口对应（Fake → 未来）

| Application 接口 | 现行 Fake | 未来归属（设计） |
|------------------|-----------|------------------|
| `IOrganizationReadRepository` | `FakeOrganizationReadRepository` | Oracle 应用 Schema **或** MES 只读维度【待现场确认】 |
| `IProductReadRepository` | `FakeProductReadRepository` | 同上 |
| `IWorkOrderReadRepository` | `FakeWorkOrderReadRepository` | **MES Reader** |
| `IProductionRecordReadRepository` | `FakeProductionRecordReadRepository` | **MES Reader** |
| `IDailyProductionPlanReadRepository` | `FakeDailyProductionPlanReadRepository` | **Oracle 应用 Schema** |
| `IImportBatchReadRepository` | `FakeImportBatchReadRepository` | **Oracle 应用 Schema** |
| `ILocalAccountStore` | `FakeLocalAccountStore` | **Oracle 应用 Schema** |
| `IUserScopeResolver` | `LocalAccountUserScopeResolver`（读 Store） | 可复用；Store 换 Oracle |

报表服务（`IProductionDailyReportService` 等）**只依赖抽象**，切换实现时 API 契约保持不变。

### 4.3 DI 切换点（按 DataAccessMode / AccountStore）

位置：`src/FactoryReport.Infrastructure/DependencyInjection.cs`

| 配置键 | 现行 | 未来目标行为 |
|--------|------|----------------|
| `FactoryReport:DataMode` | 校验允许 `Fake`/`Oracle`，**运行时强制 Fake 仓储** | `Fake` → Fake*；`Oracle` → OracleRepository + MES Reader |
| `FactoryReport:Authentication:AccountStore` | `Fake` 注册 `FakeLocalAccountStore`；`Oracle` 显式抛未实现 | `Oracle` → `OracleLocalAccountStore` |
| `IDataAccessModeProvider` | `FakeDataAccessModeProvider` 恒为 Fake | 按配置返回真实模式（报表 Meta.DataAccessMode） |

约束（本阶段及云端持续有效）：

1. **不得**在 Cursor Cloud 启用 `DataMode=Oracle`。
2. Production **禁止** `AccountStore=Fake`（已有 `FakeAccountStoreProductionGuard`）。
3. 连接串、密码、Wallet 路径仅通过 **环境变量 / ASP.NET Core Secret / 宿主密钥管理** 注入；示例仅出现在 `appsettings.Example.json` 占位。
4. 本阶段 **不**创建 DbContext、Migration、DDL、连接串或 MES 同步代码。

---

## 5. 本阶段明确不包含

- 不连接任何 Oracle / MES / 工厂内网。
- 不索取或提交密码、证书、Wallet、真实连接串。
- 不开始实现持久化、用户管理 UI、Excel 写入、MES 同步。
- 不修改任何业务 API 行为。

---

## 6. 后续解锁（需现场问卷 + Schema 定稿后）

1. 应用 Schema 的 EF/ODP.NET 持久化与账号正式存储。  
2. MES 只读 Reader 与五张报表切到真实事实数据。  
3. Excel 导入 / 发布 / Active 切换与回退（规则【待产品确认】）。  
4. 健康检查 ready 增加 Oracle 探测【待现场确认】。  
5. 审计与备份保留策略落地。

下一步唯一建议见阶段汇报；实现前必须完成 `oracle-site-questionnaire.md` 最小项与 `source-mapping-template.md` 签字栏。
