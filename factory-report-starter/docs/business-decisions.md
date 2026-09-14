# 业务决策记录

> 本文件强制区分三类决策，避免把 Fake 演示口径误当作现场真实规则。  
> 数据库统一为 **Oracle**（Oracle.ManagedDataAccess / ODP.NET + Oracle.EntityFrameworkCore）。  
> Oracle 版本、Schema、字符集、连接方式、只读视图均为【待现场确认】。  
> 模拟数据不得连接数据库。

图例：

- `[x]` 已确认  
- `[ ]` 未确认（待勾选）  
- **① 已确认的产品规则**  
- **② Fake 测试临时口径**（仅模拟/演示，不代表现场）  
- **③ 必须现场确认的 Oracle / MES 映射项**

---

## ① 已确认的产品规则

以下来自产品规格，开发与验收必须遵守（与具体 MES 表无关）：

- [x] 第一版不做手机报工、审批或修改 MES 生产数据。
- [x] 手机端不得直连 MES / Oracle；所有查询经服务端 API。
- [x] 普通管理员不得在 UI 中输入或执行任意 SQL。
- [x] 数据库数据集第一版只注册 DBA 提供的只读视图（或等价只读对象），不在 UI 写任意 SQL。
- [x] 功能权限与组织数据范围必须在服务端强制执行。
- [x] 单次查询最大返回 1,000 行；默认分页 50。
- [x] 参数化查询、超时、日期跨度与分页上限必须启用。
- [x] 本地报表库与 MES 只读源按 **Oracle** 方案实现；禁止 SQL Server 专用驱动/脚本/配置（含 Microsoft.Data.SqlClient、SqlBulkCopy）。
- [x] 使用 Oracle.ManagedDataAccess / ODP.NET 与 Oracle.EntityFrameworkCore。
- [x] Cursor Cloud 使用 Fake 内存/文件数据；**不得连接数据库**（含 Oracle）。
- [x] 不提交真实密码、Token、证书、生产数据。
- [x] 报表生命周期：Draft → Validating → Published → Disabled；已发布版本不可原地修改。
- [x] Excel 第一版仅 `.xlsx`/`.csv`，拒绝宏与默认拒绝公式单元格。
- [x] 扫码第一版仅 QR Code 与 Code 128；服务端解析并校验数据权限。
- [x] 组合数据集只组合已注册数据集，不直连原始库。
- [x] 系统时间库内按 UTC 保存；业务生产日期与班次单独保存。
- [x] 项目根目录固定为 `factory-report-starter/`，不改动仓库内英语学习等其他项目。
- [x] 业务表保留 FactoryId（多工厂）；数量字段分列保存，不在同步/领域层混合计算。
- [x] 达成率关联键：FactoryId + WorkshopId + ProductionLineId + ProductionDate + ProductCode。
- [x] 计划为 0 → 达成率为 null；有实际无计划 → 「未配置计划」；有计划无实际 → 实际为 0、达成率 0%。
- [x] 稳定报表编码：`production_daily` / `work_order_progress` / `quality_statistics` / `production_plan_achievement` / `monthly_production_plan`。

---

## ② Fake 测试临时口径

> 仅用于 `FakeMesSourceReader`、Fixtures 与演示。现场接入前必须以 ③ 确认结果替换。  
> UI/文档若展示这些规则，应标明「测试口径」或「待现场确认」。

### 2.1 生产日与班次

- [x] **Fake**：白班归属当天自然日；跨天夜班示例将产量归属「下班日的生产日」用固定配置（如 20:00–08:00 归属开始日）。**不代表现场规则。**
- [x] **Fake**：工厂时区演示值使用 `Asia/Shanghai`。**不代表现场时区。**

### 2.2 产量与质量

- [x] **Fake**：`actualQuantity` = 报工合计，演示数据中返工另字段展示且默认不计入实际。**正式是否计入【见 ③】。**
- [x] **Fake**：`yieldRate` = goodQuantity / (goodQuantity + defectQuantity)；分母为 0 时显示「—」。
- [x] **Fake**：`defectRate` = defectQuantity / inspectedQuantity；检验为 0 时显示「—」。
- [x] **Fake**：冲销以负数量行出现在 Fixtures；同步逻辑应能消化，但字段映射非正式。

### 2.3 工单延期

- [x] **Fake**：`isOverdue` = 当前 UTC 时间 > plannedFinishAt 且工单未关闭。**正式规则【见 ③】。**
- [x] **Fake**：完成数 > 计划时，`remainingQuantity` 显示 0，`completionRate` 允许 > 100%。

### 2.4 计划达成与 Excel

- [x] **Fake Excel 唯一键**：factoryCode + workshopCode + productionDate + productCode。（正式【见 ③】；领域关联键已确认含 ProductionLineId，见 ①）
- [x] **Fake ReplaceScope**：factoryCode + planYearMonth。
- [x] **Fake**：达成率 Decimal 计算，展示四舍五入到 1 位小数；UI「—」文案为演示展示。**边界计算规则见 ①；正式舍入/展示【见 ③】。**

### 2.5 组织与角色（演示 Seed）

- [x] **Fake**：演示工厂 `F-DEMO-01`，车间 `W-DEMO-A/B`，产线各 1～2 条；B 车间用于越权测试。
- [x] **Fake**：角色编码使用规格中的 SystemAdmin / FactoryAdmin / Viewer 等；权限矩阵以规格权限码为准做最小 Seed。

---

## ③ 必须现场确认的 Oracle / MES 映射项

### 3.1 Oracle 平台与连接

- [ ] Oracle 版本与部署形态（单机 / RAC / CDB-PDB 等）【待现场确认】
- [ ] 本地报表库 Schema（用户）名称与权限模型【待现场确认】
- [ ] 字符集与国家字符集【待现场确认】
- [ ] 连接方式（Easy Connect、TNS、Wallet 等）【待现场确认】
- [ ] 认证方式（DB 用户 / Wallet / OS 认证等）【待现场确认】
- [ ] 是否需要 Instant Client 或其他现场组件（托管 ODP.NET 优先）【待现场确认】
- [ ] 表空间、备份（RMAN）与保留策略【待现场确认】
- [ ] 乐观并发令牌的 Oracle 物理实现（如 NUMBER Version 列）【待现场确认】
- [ ] 规划类型到 Oracle 物理类型的最终映射（见 `data-dictionary.md`）【待现场确认】

### 3.2 MES / ERP 只读源

- [ ] MES / ERP 是否同库或同实例、只读账号与最小权限【待现场确认】
- [ ] 允许访问的只读视图 / 同义词清单【待现场确认】
- [ ] 工单、产品、车间、产线主键与业务编码字段【待现场确认】
- [ ] 产量、质量、工单进度各视图的增量字段与水位规则【待现场确认】
- [ ] 源系统时区与服务器时区差异处理【待现场确认】

### 3.3 业务口径

- [ ] 生产日与自然日关系【待现场确认】
- [ ] 夜班归属规则【待现场确认】
- [ ] 实际产量是否扣除返工、报废、冲销【待现场确认】
- [ ] 良率分子与分母【待现场确认】
- [ ] 不良率与检验口径【待现场确认】
- [ ] 工单延期判断规则【待现场确认】
- [ ] 工单关闭后修订如何反映到报表【待现场确认】
- [ ] 达成率零分母、缺失计划、缺失实际的**正式展示文案与舍入**（边界计算规则已在领域确认，见 ①）【待现场确认】
- [ ] 比率舍入与单位（件/%）【待现场确认】

### 3.4 Excel 计划

- [ ] Excel 生产计划业务唯一键【待现场确认】
- [ ] ReplaceScope 覆盖范围定义【待现场确认】
- [ ] 计划是否含产线维度【待现场确认】
- [ ] 产品编码校验字典来源【待现场确认】

### 3.5 组织、角色与现场基础设施

- [ ] 正式组织树与编码【待现场确认】
- [ ] 角色与数据范围矩阵【待现场确认】
- [ ] 工厂域名、内部 DNS、HTTPS 证书【待现场确认】
- [ ] 服务器操作系统版本【待现场确认】
- [ ] 数据保留、审计与备份周期【待现场确认】

### 3.6 字段级映射

逐字段映射请在 `source-mapping-template.md` 填写；本处仅作总开关：

- [ ] 生产日报字段映射已现场签字【待现场确认】
- [ ] 工单进度字段映射已现场签字【待现场确认】
- [ ] 质量统计字段映射已现场签字【待现场确认】
- [ ] 计划达成关联键已现场签字【待现场确认】

---

## 变更记录

| 日期 | 变更 | 作者 |
|---|---|---|
| 2026-09-14 | 阶段 4：落地确定性 Fake 夹具与只读仓储；口径仍属 ②，不含真实生产数据 | Cursor Cloud 阶段 4 |
| 2026-09-14 | 阶段 3：确认达成率关联键（含产线）与三边界规则；稳定报表编码写入领域模型 | Cursor Cloud 阶段 3 |
| 2026-09-14 | 阶段 2：补充运行基础设施（健康检查 / 日志脱敏 / Options）；业务口径无变更 | Cursor Cloud 阶段 2 |
| 2026-09-13 | 阶段 0 初始化：区分 ①②③，统一 Oracle 口径 | Cursor Cloud 阶段 0 |
