# Factory Report

工厂局域网移动报表平台。手机端采用 Web/PWA，运维端采用 Web 后台，服务端使用 ASP.NET Core，支持 SQL Server 数据集、Excel/CSV 导入数据集、组合数据集以及二维码和 Code 128 扫码查询。

## 文档

- `FactoryReport_Cursor_Development_Spec.md`：完整产品与技术规格。
- `FactoryReport_Cursor_Prompts.md`：Cursor Cloud 分阶段提示词。
- `AGENTS.md`：所有代码代理必须遵守的项目规则。

## 开始方式

1. 在 Cursor Cloud 中选择本仓库。
2. 要求 Cursor 完整阅读上述三个文件。
3. 先执行 `FactoryReport_Cursor_Prompts.md` 中的 Prompt 00。
4. 每个阶段完成构建和测试后，再进入下一阶段。

## 安全边界

本仓库不得提交真实 MES 数据、生产连接字符串、密码、访问令牌、证书私钥、数据库备份或客户内部文档。Cursor Cloud 只能使用虚构或脱敏测试数据。

