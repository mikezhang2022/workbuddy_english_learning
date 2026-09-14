# Oracle 应用 Schema 逻辑设计（阶段 18）

> **逻辑设计文档。** 不生成、不提交任何 `.sql`、Migration、DDL 文件。  
> 物理类型、表空间、字符长度上限 【待现场确认】。  
> MES 只读视图字段映射 **留白**，见 `source-mapping-template.md`。  
> 配套：`oracle-integration-plan.md`、`oracle-site-questionnaire.md`。

---

## 0. 约定

### 0.1 命名

- 逻辑表名使用 `UPPER_SNAKE`（Oracle 常见风格）；实现阶段可映射 EF 实体名。
- 主键列优先 `Id`（NUMBER / RAW(16) 等物理类型【待现场确认】）。
- Schema 所有者名称【待现场确认】（下文记作 `APP_SCHEMA`）。

### 0.2 时间与审计

| 约定 | 说明 |
|------|------|
| 语义 | 所有系统时间戳列采用 **UTC** 语义（与领域 `UtcInstant` 一致） |
| 物理类型 | `TIMESTAMP` **或** `TIMESTAMP WITH TIME ZONE` —— 【待现场确认】（二者择一后全库统一） |
| 业务日期 | `DATE` / `DATE` 仅日期部分，对应 `DateOnly`（`ProductionDate` / `PlanDate`），**不是**系统时钟 |
| 标准审计列 | `CreatedAtUtc`、`CreatedBy`、`UpdatedAtUtc`、`UpdatedBy`（适用表）；导入完成等另有业务时间列 |

### 0.3 不在本文件建表的数据

下列由 **MES 只读** 提供，**不**在应用 Schema 落可写事实表（见接入计划 §3.2）：

- `ProductionRecord` / 质量数量事实  
- `WorkOrder` 进度事实  

组织 `Factory` / `Workshop` / `ProductionLine` / `Product`：若现场以 MES 为权威，则仅缓存或只读同义词【待现场确认】；下表给出 **应用自管** 时的逻辑设计，供账号授权与计划校验引用。

---

## 1. 组织与主数据（应用自管时）

### 1.1 `FACTORY` ↔ `Factory`

| 列 | 逻辑类型 | 约束 | 领域字段 |
|----|----------|------|----------|
| `Id` | 整数 PK | PK | `Id` |
| `Code` | 字符串 | UK `UQ_FACTORY_CODE` | `Code` |
| `Name` | 字符串 | NOT NULL | `Name` |
| `CreatedAtUtc` / `CreatedBy` / `UpdatedAtUtc` / `UpdatedBy` | UTC / 用户标识 | | 审计 |

### 1.2 `WORKSHOP` ↔ `Workshop`

| 列 | 逻辑类型 | 约束 | 领域字段 |
|----|----------|------|----------|
| `Id` | 整数 PK | PK | `Id` |
| `FactoryId` | 整数 | FK → `FACTORY.Id`；IX `(FactoryId)` | `FactoryId` |
| `Code` | 字符串 | UK `(FactoryId, Code)` | `Code` |
| `Name` | 字符串 | NOT NULL | `Name` |
| 审计列 | | | |

### 1.3 `PRODUCTION_LINE` ↔ `ProductionLine`

| 列 | 逻辑类型 | 约束 | 领域字段 |
|----|----------|------|----------|
| `Id` | 整数 PK | PK | `Id` |
| `FactoryId` | 整数 | FK → `FACTORY` | `FactoryId` |
| `WorkshopId` | 整数 | FK → `WORKSHOP` | `WorkshopId` |
| `Code` | 字符串 | UK `(FactoryId, Code)` 【待产品确认是否含 Workshop】 | `Code` |
| `Name` | 字符串 | NOT NULL | `Name` |
| 审计列 | | | |

索引：`(FactoryId, WorkshopId)` 供范围过滤。

### 1.4 `PRODUCT` ↔ `Product`

| 列 | 逻辑类型 | 约束 | 领域字段 |
|----|----------|------|----------|
| `Id` | 整数 PK | PK | `Id` |
| `FactoryId` | 整数 | FK → `FACTORY` | `FactoryId` |
| `ProductCode` | 字符串 | UK `(FactoryId, ProductCode)` | `ProductCode` |
| `Name` | 字符串 | NOT NULL | `Name` |
| 审计列 | | | |

> 若产品权威在 MES：本表可为同步缓存或取消；计划导入校验字典来源【待现场确认】。

---

## 2. 本地账号、角色与组织授权

对应 `LocalAccount`、`ILocalAccountStore`、`DataScopeGrant`。

### 2.1 `LOCAL_ACCOUNT`

| 列 | 逻辑类型 | 约束 | 说明 |
|----|----------|------|------|
| `UserId` | 字符串或 GUID | PK | `LocalAccount.UserId` |
| `UserName` | 字符串 | UK `UQ_LOCAL_ACCOUNT_USERNAME`（大小写规则【待产品确认】） | 登录名 |
| `DisplayName` | 字符串 | NOT NULL | |
| `PasswordHash` | 字符串 | NOT NULL | PBKDF2 / Identity 兼容哈希；**禁止明文** |
| `IsEnabled` | 布尔 | NOT NULL DEFAULT true | 【待产品确认】停用语义 |
| `IsTestOnlyAccount` | 布尔 | NOT NULL DEFAULT false | 生产必须为 false |
| `CreatedAtUtc` / `CreatedBy` / `UpdatedAtUtc` / `UpdatedBy` | | | 审计 |
| `PasswordChangedAtUtc` | UTC | 可空 | 【待产品确认】轮换策略 |

### 2.2 `LOCAL_ACCOUNT_ROLE`

| 列 | 逻辑类型 | 约束 |
|----|----------|------|
| `UserId` | | PK 复合；FK → `LOCAL_ACCOUNT` |
| `RoleCode` | 字符串 | PK 复合；取值对齐 `AppRoles`（SystemAdmin / FactoryAdmin / …） |

### 2.3 `LOCAL_ACCOUNT_DATA_SCOPE` ↔ `DataScopeGrant`

| 列 | 逻辑类型 | 约束 | 说明 |
|----|----------|------|------|
| `Id` | 整数或 GUID | PK | |
| `UserId` | | FK → `LOCAL_ACCOUNT`；IX `(UserId)` | |
| `IsGlobal` | 布尔 | | 仅 SystemAdmin 类；与行级授权互斥规则【待产品确认】 |
| `FactoryId` | 整数 | 可空；非全局时建议 NOT NULL | `DataScopeGrant.FactoryId` |
| `WorkshopId` | 整数 | 可空 | 空=工厂级授权 |
| `ProductionLineId` | 整数 | 可空 | 空=车间或工厂级 |
| 审计列 | | | |

逻辑约束：

- 非全局行：`FactoryId` 必填；`ProductionLineId` 非空时 `WorkshopId` 宜非空【待产品确认】。
- **不**默认继承同级组织（见 `authorization-and-data-scope.md`）。
- Cookie **不得**持久化范围明细；范围仅服务端读本表。

---

## 3. 计划导入、版本与日计划行

### 3.1 `IMPORT_BATCH` ↔ `ImportBatch`

| 列 | 逻辑类型 | 约束 | 领域字段 |
|----|----------|------|----------|
| `Id` | GUID | PK | `Id` |
| `FactoryId` | 整数 | NOT NULL；IX `(FactoryId, CreatedAtUtc)` | `FactoryId` |
| `DatasetCode` | 字符串 | NOT NULL | 如 `monthly_production_plan` |
| `Status` | 枚举/小整数 | NOT NULL | `ImportBatchStatus` |
| `TargetPublishStatus` | 枚举/小整数 | NOT NULL | `DatasetPublishStatus` |
| `SourceFileName` | 字符串 | 可空 | 仅文件名，无路径机密 |
| `TotalRows` / `ErrorRows` | 整数 | ≥0 | |
| `CreatedAtUtc` | UTC | NOT NULL | `CreatedAtUtc` |
| `CompletedAtUtc` | UTC | 可空 | `CompletedAtUtc` |
| `CreatedBy` / `UpdatedAtUtc` / `UpdatedBy` | | | 审计 |

### 3.2 `DATASET_VERSION` ↔ `DatasetVersionState`（PlanVersion）

| 列 | 逻辑类型 | 约束 | 领域字段 |
|----|----------|------|----------|
| `Id` | GUID | PK | `Id`（= `PlanVersionId`） |
| `FactoryId` | 整数 | NOT NULL | `FactoryId` |
| `DatasetCode` | 字符串 | NOT NULL | `DatasetCode` |
| `VersionNo` | 字符串 | UK `(FactoryId, DatasetCode, VersionNo)` | `VersionNo` |
| `PublishStatus` | 枚举 | NOT NULL | `DatasetPublishStatus` |
| `IsActive` | 布尔 | NOT NULL | `IsActive` |
| `ImportBatchId` | GUID | 可空 FK → `IMPORT_BATCH` | 溯源【待产品确认】 |
| `CreatedAtUtc` | UTC | NOT NULL | |
| `PublishedAtUtc` | UTC | Published 时必填（领域已要求） | |
| `CreatedBy` / `UpdatedAtUtc` / `UpdatedBy` | | | |

#### 版本状态与 Active —— 【待产品确认】

| 规则 | 状态 |
|------|------|
| 生命周期 Draft → Validating → Published → Disabled（已发布不可原地改内容） | 产品规格已确认方向；表约束实现细节【待产品确认】 |
| 仅 `PublishStatus = Published` 可 `IsActive = true`（领域构造器已强制） | 已对齐领域；DB CHECK【待产品确认】 |
| 同一 `(FactoryId, DatasetCode)`（或含 `PlanYearMonth`）下 **至多一个** Active | 【待产品确认】唯一/部分索引语义 |
| Active 切换是否事务内先灭活再激活 | 【待产品确认】 |
| 回退：是否允许重新激活历史 Published 版本；Disabled 是否可再发布 | 【待产品确认】 |
| Fake 现行行为：查询仅 `Published && IsActive` | 非正式；正式过滤【待产品确认】 |

### 3.3 `MONTHLY_PRODUCTION_PLAN` ↔ `MonthlyProductionPlan`

| 列 | 逻辑类型 | 约束 | 领域字段 |
|----|----------|------|----------|
| `Id` | 整数 | PK | `Id` |
| `FactoryId` | 整数 | NOT NULL | `FactoryId` |
| `PlanYearMonth` | 字符串(YYYY-MM) | NOT NULL | `PlanYearMonth` |
| `PlanVersionId` | GUID | FK → `DATASET_VERSION.Id` | `PlanVersionId` |
| `CreatedAtUtc` | UTC | NOT NULL | `CreatedAtUtc` |
| 审计列 | | | |

唯一键候选 —— **【待产品确认】**：

- `(FactoryId, PlanYearMonth, PlanVersionId)`（一版本一月一头），或  
- `(FactoryId, PlanYearMonth)` 仅指向当前 Active（与多版本并存冲突时需澄清）。

### 3.4 `DAILY_PRODUCTION_PLAN_LINE` ↔ `DailyProductionPlanLine`

| 列 | 逻辑类型 | 约束 | 领域字段 |
|----|----------|------|----------|
| `Id` | 整数或 GUID | PK（代理键，领域行当前无 Id） | 实现需要【待产品确认】 |
| `FactoryId` | 整数 | NOT NULL | `FactoryId` |
| `WorkshopId` | 整数 | NOT NULL | `WorkshopId` |
| `ProductionLineId` | 整数 | 可空 | `ProductionLineId` |
| `PlanDate` | 业务日期 | NOT NULL | **`PlanDate`（必须保留）** |
| `ProductCode` | 字符串 | NOT NULL | `ProductCode` |
| `PlanQuantity` | 小数 | ≥0 | `PlanQuantity` |
| `PlanVersionId` | GUID | FK → `DATASET_VERSION`；IX | `PlanVersionId` |
| `Remark` | 字符串 | 可空 | `Remark` |
| `CreatedAtUtc` / `CreatedBy` / `UpdatedAtUtc` / `UpdatedBy` | | | |

#### 生产计划业务唯一键 —— 【待产品确认】

候选（与 `business-decisions.md` ③ Excel 唯一键对齐后定稿）：

1. `(PlanVersionId, FactoryId, WorkshopId, ProductionLineId, PlanDate, ProductCode)`  
2. 不含 `ProductionLineId` 的变体（若计划无产线维度）  
3. Fake 临时键曾用 `factoryCode + workshopCode + productionDate + productCode`（**非正式**）

索引建议（逻辑）：

- `(FactoryId, PlanDate)` — 报表日期范围  
- `(PlanVersionId, PlanDate)` — 按版本取日行  
- `(FactoryId, WorkshopId, ProductionLineId, PlanDate, ProductCode)` — 达成率关联  

**禁止**：由月总量均摊生成日行（领域与 API 已禁止）。

---

## 4. 审计记录

正式审计实体尚未在 Domain 落地；逻辑预留：

### 4.1 `AUDIT_LOG`（逻辑）

| 列 | 说明 | 状态 |
|----|------|------|
| `Id` | PK | 【待产品确认】 |
| `OccurredAtUtc` | UTC | |
| `ActorUserId` | 操作者 | |
| `ActionCode` | 如 LoginSuccess / ImportSucceeded / VersionActivated | 【待产品确认】枚举 |
| `EntityType` / `EntityId` | 关联对象 | |
| `FactoryId` | 可选组织上下文 | |
| `DetailJson` | 脱敏后变更摘要；**禁止**密码、连接串 | |
| `CorrelationId` | 与 API 日志对齐 | |

保留期与归档见现场问卷；不得在日志中记录密码或 Cookie。

---

## 5. 领域对象对照总表

| 领域 / 应用对象 | 逻辑表 | 归属 |
|-----------------|--------|------|
| `Factory` | `FACTORY` | 应用自管 **或** MES 维度【待现场确认】 |
| `Workshop` | `WORKSHOP` | 同上 |
| `ProductionLine` | `PRODUCTION_LINE` | 同上 |
| `Product` | `PRODUCT` | 同上 |
| `WorkOrder` | （无应用可写表） | **MES 只读** → `source-mapping-template.md` §2 |
| `ProductionRecord` | （无应用可写表） | **MES 只读** → §1 / §3 |
| `MonthlyProductionPlan` | `MONTHLY_PRODUCTION_PLAN` | 应用 Schema |
| `DailyProductionPlanLine` | `DAILY_PRODUCTION_PLAN_LINE` | 应用 Schema |
| `ImportBatch` | `IMPORT_BATCH` | 应用 Schema |
| `DatasetVersionState` | `DATASET_VERSION` | 应用 Schema |
| `LocalAccount` | `LOCAL_ACCOUNT` (+ ROLE / DATA_SCOPE) | 应用 Schema |
| `DataScopeGrant` | `LOCAL_ACCOUNT_DATA_SCOPE` | 应用 Schema |
| 审计 | `AUDIT_LOG` | 应用 Schema【待产品确认】 |

---

## 6. MES 只读映射（留白）

不在此设计 MES 物理表。现场填写：

- `docs/source-mapping-template.md`（生产日报 / 工单 / 质量 / 组织维度 / 扫码）  
- 视图命名仅示例（如 `V_RPT_PRODUCTION_DAILY`），**非**现场事实  

应用侧未来 `MES Reader` 只对白名单视图 `SELECT`，结果映射为领域 `ProductionRecord` / `WorkOrder` 等。

---

## 7. 明确不在本阶段产出

- 任何 `.sql` / EF Migration / `DbContext` / 连接串  
- 对唯一键、Active 切换、回退的**正式产品定论**（仅标注【待产品确认】）  
- 将 Fake 临时口径写入为正式 CHECK 约束
