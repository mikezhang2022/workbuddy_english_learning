# Factory Report

工厂局域网移动报表平台。手机端采用 Web/PWA，运维端采用 Web 后台，服务端使用 ASP.NET Core，支持 **Oracle** 数据集、Excel/CSV 导入数据集、组合数据集以及二维码和 Code 128 扫码查询。

本目录 `factory-report-starter/` 为工厂报表项目根目录。所有开发与文档改动仅限本目录及其子目录。

## 数据库口径（Oracle）

- 本地报表库与 MES/ERP 只读源均按 **Oracle** 方案设计与实现。
- 使用 Oracle 官方 .NET 驱动（**Oracle.ManagedDataAccess / ODP.NET**）与 EF Core Oracle Provider（**Oracle.EntityFrameworkCore**）。
- Oracle 版本、Schema 名称、字符集、连接方式、现场只读视图均为 **【待现场确认】**。
- 禁止新增任何 SQL Server 专用代码、脚本或配置。
- Cursor Cloud / 模拟开发使用 Fake 内存或文件数据，**不得连接数据库**（含 Oracle）。

## 文档

- `FactoryReport_Cursor_Development_Spec.md`：完整产品与技术规格。
- `FactoryReport_Cursor_Prompts.md`：Cursor Cloud 分阶段提示词。
- `AGENTS.md`：所有代码代理必须遵守的项目规则。
- `docs/data-dictionary.md`：数据字典（规划口径）。
- `docs/business-decisions.md`：业务决策记录（已确认 / Fake 临时 / 待现场确认）。
- `docs/source-mapping-template.md`：源系统映射模板（待现场填写）。

## 开始方式

1. 在 Cursor Cloud 中选择本仓库，工作目录固定为 `factory-report-starter/`。
2. 要求 Cursor 完整阅读上述规格、提示词、AGENTS 与 README。
3. 先执行 `FactoryReport_Cursor_Prompts.md` 中的 Prompt 00 / 阶段 0。
4. 每个阶段完成构建和测试后，再进入下一阶段（阶段 0 不写业务代码、不安装依赖、不部署）。

## 安全边界

本仓库不得提交真实 MES 数据、生产连接字符串、密码、访问令牌、证书私钥、数据库备份或客户内部文档。Cursor Cloud 只能使用虚构或脱敏测试数据，且不得连接工厂内网、真实 Oracle、MES 或 ERP。
