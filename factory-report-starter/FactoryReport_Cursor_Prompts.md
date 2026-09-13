# 工厂移动报表平台 Cursor Cloud 分阶段提示词

配套规格：`FactoryReport_Cursor_Development_Spec.md`  
使用方式：将配套规格和本文件都放入 Cursor 可以访问的 Git 仓库根目录，按照编号逐条发送。每条完成、检查结果并提交代码后，再发送下一条。  

## 使用原则

1. 不要一次发送多个开发阶段。
2. Cursor 提出业务口径问题时，先回答问题，再让它继续。
3. 每阶段都要求 Cursor 运行构建和测试。
4. 如果 Cursor 报告某项测试无法执行，保留记录，不要让它假装通过。
5. Cursor Cloud 不能连接真实 MES、局域网地址或生产数据库。
6. 真实数据库映射、证书、DNS 和现场手机测试最后在工厂环境完成。
7. 如果现有仓库与规格冲突，先要求 Cursor 汇报冲突，不要直接大规模重构。

---

## Prompt 00：仓库检查与执行计划

```text
你正在参与“工厂移动报表平台”项目。请先完整阅读仓库根目录中的：

- FactoryReport_Cursor_Development_Spec.md
- FactoryReport_Cursor_Prompts.md
- README.md（如果存在）
- 所有适用的 AGENTS.md
- 当前解决方案和项目文件（如果存在）

你运行在 Cursor Cloud，不在工厂局域网中。禁止尝试连接真实 MES、ERP、工厂 IP、内部域名或生产数据库；禁止要求我上传生产密码、证书私钥或真实生产数据。

当前任务只做仓库检查和计划，不修改业务代码。

请输出：

1. 当前仓库结构和已有技术栈。
2. 已存在的可复用内容。
3. 与开发规格冲突或不明确的地方。
4. 按依赖顺序排列的开发阶段。
5. 阶段 0 需要我提供或确认的信息。
6. 当前云端环境的 .NET SDK、Node、Docker 和 SQL Server 测试能力。
7. 建议的首个代码变更范围。

不要开始创建完整项目，不要安装无关依赖，不要部署。
```

完成标准：Cursor 只汇报，不产生无关代码改动。

---

## Prompt 01：业务数据字典和待确认清单

```text
请严格按照 FactoryReport_Cursor_Development_Spec.md 执行“阶段 0：业务数据字典”。

本阶段不要编写业务代码，不要猜测真实 MES 表名和字段名。

请创建或更新：

- docs/data-dictionary.md
- docs/business-decisions.md
- docs/source-mapping-template.md

data-dictionary.md 至少覆盖：

1. 生产日报
2. 工单进度
3. 质量统计
4. 生产计划达成率
5. Excel 月度生产计划

每个指标记录：

- 中文名称
- 稳定英文编码
- 数据类型和单位
- 业务定义
- 计算公式
- 来源系统
- 来源表/视图和字段（未知时明确标记“待现场确认”）
- 更新频率
- 可用筛选条件
- 组织数据权限字段
- 空值、零值、返工、报废、冲销和补录规则

business-decisions.md 请用可勾选决策项列出仍需人工确认的问题，重点包括：

- 生产日与自然日
- 夜班归属
- 实际产量口径
- 良率分子和分母
- 工单延期规则
- Excel 计划唯一键
- Excel 按范围替换规则
- 用户角色与组织数据范围

source-mapping-template.md 提供可供现场填写的 MES 字段映射表。

完成后检查文档内部编码一致性，并汇报：新增文件、待确认项数量、阻塞后续真实接入的问题。不要把未知项虚构成已确认。
```

人工动作：查看 `docs/business-decisions.md`，能确认的逐项填写。暂时未知的内容允许保留，但后续只能使用模拟数据。

---

## Prompt 02：创建解决方案骨架

```text
请完整阅读 FactoryReport_Cursor_Development_Spec.md，并执行“阶段 1：解决方案骨架”的第一部分。

开始前检查仓库。如果已有解决方案，基于现有结构最小修改，不重复创建项目。

如果仓库为空，请使用当前已安装且受支持的 .NET LTS SDK 创建：

- FactoryReport.MobilePwa：Blazor WebAssembly PWA
- FactoryReport.AdminWeb：Blazor Web App
- FactoryReport.Api：ASP.NET Core Web API
- FactoryReport.Application：类库
- FactoryReport.Domain：类库
- FactoryReport.Infrastructure：类库
- FactoryReport.SyncWorker：Worker Service
- FactoryReport.Shared：类库
- FactoryReport.UnitTests：xUnit
- FactoryReport.IntegrationTests：xUnit

要求：

1. 建立正确的项目引用方向，Domain 不依赖基础设施。
2. 启用 Nullable 和 ImplicitUsings。
3. 添加 Directory.Build.props，统一语言版本、警告和分析器基础配置。
4. 创建 docs、deploy 和 tests/Fixtures 目录。
5. 增加根 README，写明项目用途、云端开发限制和常用命令。
6. 增加 .gitignore，排除 Secret、生产配置、证书、数据库备份和上传临时文件。
7. 不添加真实连接字符串。
8. 只安装当前阶段确实需要的依赖。

验证：

- dotnet restore
- dotnet build
- dotnet test

完成后汇报项目结构、项目引用、执行命令和结果。发现模板或 SDK 差异时依据实际环境调整，并在 README 记录，不要伪造命令成功。
```

---

## Prompt 03：基础设施、错误响应和健康检查

```text
请执行解决方案基础设施阶段。只实现跨模块基础能力，不开始报表、Excel 或扫码业务。

要求：

1. 在 API 中实现统一 ProblemDetails 错误响应。
2. 为每个请求建立或传递 TraceId，并写入响应头。
3. 接入 Serilog，开发环境输出控制台和滚动文件。
4. 敏感请求头、Cookie、密码和连接字符串不得进入日志。
5. 实现：
   - GET /api/health/live
   - GET /api/health/ready
   - GET /api/system/version
6. 建立统一分页模型、错误码模型和时间接口 IClock。
7. Production 环境不返回异常堆栈。
8. 提供 appsettings.Development.example.json 和 appsettings.Production.example.json，只有占位值。
9. 为错误响应、TraceId 和健康检查增加集成测试。

运行格式化、构建和相关测试。完成后列出改动文件、配置方法、测试结果和尚未接入的依赖。
```

---

## Prompt 04：领域模型和数据库迁移

```text
请根据规格中的数据库设计，实现第一版配置数据库模型和 EF Core Migration。暂时不实现页面。

使用 SQL Server，Schema 至少包括：sec、cfg、imp、etl、rpt、aud。

实现以下核心实体及关系：

- Organization
- UserDataScope
- Permission
- RolePermission
- DataSource
- Dataset
- DatasetField
- DatasetRelation
- Report
- ReportVersion
- ReportPermission
- ImportTemplate
- ImportTemplateField
- ImportBatch
- ImportRowError
- DatasetVersion
- DatasetRecord
- SyncJob
- SyncCheckpoint
- SyncRun
- LoginLog
- OperationLog
- ReportQueryLog

要求：

1. 复用 ASP.NET Core Identity 标准用户和角色表，不自己实现密码体系。
2. 配置表使用 rowversion 进行乐观并发。
3. 所有时间字段使用 UTC DateTimeOffset 或明确的 UTC DateTime。
4. 业务生产日期使用 DateOnly/SQL date，不与创建时间混淆。
5. DatasetRecord 抽取 FactoryId、WorkshopId、ProductionLineId、BusinessDate 和 BusinessKey，并保存 DataJson。
6. 增加规格要求的唯一索引和常用查询索引。
7. 删除使用软删除或状态字段；已发布版本不可原地修改。
8. 创建首个 Migration 和可重复执行的数据库初始化说明。
9. 不连接真实数据库。

为关键实体约束增加测试。如果 Cursor Cloud 支持 SQL Server 容器，运行迁移集成测试；否则只运行构建和单元测试，并明确列出未执行的 SQL Server 测试。
```

---

## Prompt 05：登录、用户、角色和功能权限

```text
请实现认证和功能权限，不实现数据集查询。

要求：

1. 使用 ASP.NET Core Identity。
2. 手机端和后台与 API 同域，使用 Secure、HttpOnly Cookie。
3. 为写请求启用 CSRF 防护或 ASP.NET Core 推荐的同等级防护。
4. 实现：
   - POST /api/auth/login
   - POST /api/auth/logout
   - POST /api/auth/change-password
   - GET /api/me
   - GET /api/me/permissions
5. 建立规格中的权限编码和 Policy。
6. 登录失败限流和临时锁定。
7. 提供安全的初始化管理员流程：通过一次性启动参数、环境 Secret 或受控初始化命令完成，不能在源码中放默认密码。
8. 记录登录成功和失败审计，但不记录密码。
9. AdminWeb 未授权访问应跳转登录；API 未授权返回 401，无权限返回 403。

测试至少覆盖：登录成功、错误密码、锁定、退出、未登录 401、无权限 403、Cookie 安全配置。完成后运行构建和测试并汇报。
```

---

## Prompt 06：组织结构和数据范围权限

```text
请实现组织管理和数据范围授权。

组织层级：Factory -> Workshop -> ProductionLine。

要求：

1. 实现组织增删改查，禁止产生循环层级。
2. 实现用户到 Factory、Workshop、ProductionLine 的数据范围分配。
3. 定义 IUserDataScopeService，返回当前用户允许访问的组织 ID 集合。
4. 组织下拉接口只能返回当前用户允许的对象：
   - GET /api/organizations/factories
   - GET /api/organizations/workshops?factoryId=
   - GET /api/organizations/lines?workshopId=
5. 服务端验证父子组织关系，不能仅相信客户端传参。
6. SystemAdmin 可管理全局；FactoryAdmin 只能管理本工厂授权范围。
7. 增加越权审计。

测试必须证明：

- A 车间用户看不到 B 车间。
- 伪造 WorkshopId 不能越权。
- 仅有功能权限但没有数据范围时查询不到业务数据。
- FactoryAdmin 不能给别人授予超过自己的范围。

不要开始实现具体报表。运行构建和测试后汇报。
```

---

## Prompt 07：云端模拟数据和可替换 Provider

```text
请实现适用于 Cursor Cloud 的模拟数据模式。

要求：

1. 定义 IMesSourceReader、IImportFileStore、IReportDatasetProvider、ISyncCheckpointStore 等必要接口。
2. 实现 FakeMesSourceReader，数据来自 tests/Fixtures 或明确的开发 Fixture 目录。
3. 模拟数据完全虚构，不包含真实客户、人员、产品、设备或工单。
4. 至少覆盖：白班、跨天夜班、延期工单、返工、报废、冲销、缺失计划、零计划和无权限车间。
5. 通过配置 `MesSource:Provider=Fake` 启用，不根据环境偷偷切换。
6. Production 环境如果仍配置 Fake，启动时必须明显告警；可以根据发布策略直接拒绝启动。
7. Seed 数据必须可重复创建，不因重复运行产生重复记录。
8. 增加测试证明 Fake 和接口契约一致。

不要实现真实客户字段映射。完成后运行构建和测试，说明如何启动模拟模式。
```

---

## Prompt 08：手机 PWA 基础框架

```text
请实现 MobilePwa 的基础框架，使用模拟接口，不实现摄像头扫码。

一级导航：

- 首页
- 报表
- 扫码
- 收藏
- 我的

要求：

1. 移动优先，重点适配 360、390、430 CSS 像素宽度。
2. 实现登录页、主布局、网络状态、加载、空数据、错误和 403 页面。
3. 首页先用模拟 API 展示指标卡和数据最后更新时间。
4. 表格手机端默认 3～5 个关键字段，支持进入详情。
5. 静态资源支持 PWA 安装。
6. Service Worker 只缓存应用壳和静态资源，不缓存登录、报表、导出和管理 API。
7. 检测新版本并提示用户刷新；不要在用户填写表单时强制刷新。
8. 不依赖公网字体、图标、脚本或 CDN。
9. 页面具备基础可访问性，触控区域不少于 44×44 CSS 像素。

增加组件测试或适合当前技术栈的测试。运行构建；如果云端可启动应用，提供预览步骤和截图说明，但不要声称完成现场手机测试。
```

---

## Prompt 09：数据源管理和凭据保护

```text
请实现 SQL Server 数据源管理，只供 SystemAdmin 使用。

要求：

1. 实现数据源列表、新增、编辑、停用和测试连接。
2. 优先支持 Windows 集成认证；SQL 账号模式只保存加密后的凭据引用。
3. 使用 ASP.NET Core Data Protection 保护凭据，并说明密钥持久化与 ACL 要求。
4. API 和日志绝不返回完整连接字符串或密码。
5. 测试连接设置短超时并取消长时间阻塞。
6. 仅支持 SQL Server，不提前抽象未使用的数据库实现。
7. 数据源必须声明用途和只读要求。
8. 禁止 UI 输入并执行任意 SQL。
9. 增加连接测试审计。
10. 云端测试使用 Fake 或容器数据库，不能连接工厂数据库。

完成后运行安全相关测试、构建和集成测试。说明生产 Secret 应如何由现场人员注入。
```

---

## Prompt 10：MES 同步 Worker

```text
请实现第一版同步 Worker，先基于 FakeMesSourceReader 完成闭环，同时保留 SqlServerMesSourceReader 接口位置。

要求：

1. 读取 SyncCheckpoint。
2. 增量读取数据。
3. 转换为本地维度和事实模型。
4. 使用事务写入。
5. 成功后推进 Checkpoint。
6. 失败不推进 Checkpoint。
7. 同一同步任务不可并发执行。
8. 实现有限次数重试和取消处理，不无限重试。
9. 保存 SyncRun 的开始、结束、数量、耗时和错误摘要。
10. 支持后台手工触发，但触发接口只负责排队，不阻塞至完成。
11. 实现夜班跨天计算接口，具体业务时间保留配置。
12. 提供 Windows Service 运行方式。

测试覆盖：幂等、重复运行、同步失败、Checkpoint、不完整批次、取消和并发触发。

如果 SQL Server 容器可用，增加真实 SQL Server 的写入集成测试；否则明确未执行。不要接入真实 MES。
```

---

## Prompt 11：报表引擎和配置版本

```text
请实现通用报表引擎的最小可用版本，不做自由拖拽设计器。

要求：

1. 实现 IReportDatasetProvider 注册表，以稳定 DatasetCode 查找 Provider。
2. 实现 Report、ReportVersion、ReportPermission 生命周期：Draft、Validating、Published、Disabled。
3. 已发布版本不可原地修改；修改必须生成新草稿版本。
4. 支持筛选器、指标卡、折线图、柱状图、饼图和表格列配置。
5. 支持角色权限、明细权限、导出权限、最大日期跨度、最大行数和分页。
6. 实现：
   - GET /api/reports
   - GET /api/reports/{code}/definition
   - POST /api/reports/{code}/query
   - POST /api/reports/{code}/drill-down
7. 查询流程必须执行登录、发布状态、功能权限、数据范围、字段类型、日期跨度、分页和超时检查。
8. 排序字段和输出字段来自服务端白名单，不接受客户端 SQL 片段。
9. 记录查询耗时、用户、报表、筛选摘要和结果数量，不记录敏感结果正文。
10. 建立统一 ReportQueryResult 响应。

测试覆盖非法筛选字段、超长日期范围、越权、未发布版本、分页上限、取消和超时。
```

---

## Prompt 12：三张基础报表

```text
请基于报表引擎实现三张基础报表，当前使用已确认数据字典和模拟数据。遇到未确认业务口径时，不要猜测；将其做成明确配置或记录为现场待确认，并使用标记清楚的测试口径。

报表：

1. production_daily：生产日报
2. work_order_progress：工单进度
3. quality_statistics：质量统计

要求：

- 每张报表实现独立 DatasetProvider 或清晰的查询处理器。
- 使用 Dapper 参数化查询。
- 应用用户 Factory、Workshop、ProductionLine 数据范围。
- 完成筛选器、指标卡、图表、表格和下钻。
- 手机端显示数据最后更新时间。
- 数量、百分比、日期和工单号格式正确。
- 工单号和产品编码始终按文本处理。
- 处理空数据和零分母。
- 查询最大 1,000 行，明细分页。

生产日报至少包含计划数、实际数、良品数、不良数、报废数、达成率、良率和小时趋势。

工单进度至少包含计划数、完成数、剩余数、完成比例、计划完成时间和延期状态。

质量统计至少包含检验数、不良数、不良率、不良类型排名和每日趋势。

为每张报表增加结果计算和权限集成测试。输出抽样数据对账表，便于后续现场核对。
```

---

## Prompt 13：报表后台配置、预览和发布

```text
请实现 AdminWeb 的报表配置和发布流程。

第一版使用表单和排序控件，不做自由画布拖拽。

配置步骤：

1. 基本信息
2. 选择已有数据集
3. 筛选器
4. 指标卡
5. 图表
6. 表格列
7. 角色与权限
8. 手机宽度预览
9. 验证并发布

要求：

- 管理员只能选择 DatasetField 中允许的字段。
- 发布前检查字段存在、类型匹配、数据权限字段、日期范围和至少一个输出组件。
- 发布生成不可变版本。
- 支持停用和回退。
- 普通管理员不能输入 SQL。
- 并发修改使用 rowversion，冲突时提示刷新。
- 发布后 MobilePwa 无需重新构建即可看到新报表。
- 每次发布和回退写操作审计。

增加测试：无效字段不能发布、并发发布仅一个成功、回退恢复旧定义、未授权角色不能设计报表。
```

---

## Prompt 14：Excel/CSV 模板和导入后端

```text
请实现文件数据集导入后端。第一版支持固定模板 `.xlsx` 和 `.csv`，不支持宏和任意格式。

要求：

1. 使用 ClosedXML 读取和生成 `.xlsx` 模板，使用 CsvHelper 读取 `.csv`。
2. 实现模板编码、模板版本、字段定义、必填、类型、长度、范围、枚举、组织和唯一键校验。
3. `.xlsx` 公式单元格默认拒绝，提示粘贴为值。
4. 拒绝 `.xlsm`、加密文件、异常压缩包、超大文件和超行数。
5. 上传目录位于 WebRoot 外，使用随机服务器文件名。
6. 计算 SHA-256 并检测重复上传。
7. HTTP 上传后创建 ImportBatch，由后台任务异步解析。
8. 错误精确记录行号、字段、原始值和原因。
9. 有错误时不能发布。
10. 无错误时生成新增、变化、重复和影响范围预览。
11. 实现 Snapshot 和 ReplaceScope 两种模式。
12. 发布生成不可变 DatasetVersion，并在事务中切换 ActiveVersionId。
13. 支持回退历史版本。
14. 用户上传范围必须受其数据权限限制。
15. 导出的错误文件必须防止电子表格公式注入。

实现规格中的导入 API。测试至少覆盖：缺表头、错误类型、公式、重复键、无权限组织、重复文件、发布、并发发布和回退。

使用完全虚构的测试 Excel 文件，不使用真实生产文件。
```

---

## Prompt 15：Excel 导入后台页面

```text
请实现 AdminWeb 的 Excel/CSV 导入操作界面。

页面流程：

1. 选择文件数据集。
2. 下载当前模板。
3. 上传文件。
4. 查看解析进度。
5. 查看错误或下载错误明细。
6. 查看新增、更新、重复和影响范围预览。
7. DataPublisher 确认发布。
8. 查看历史版本并回退。

要求：

- DataImporter 可以上传但不能发布，除非同时拥有 import.publish。
- 上传过程中显示真实进度状态，不伪造百分比。
- 页面刷新后可根据 batchId 恢复任务状态。
- 错误表格适合大量错误分页查看。
- 发布前二次确认数据集、版本、工厂和覆盖范围。
- 防止重复点击发布。
- 操作完成后显示明确结果和 TraceId。
- 不在浏览器长期保存上传文件正文。

增加前端状态处理和后端权限集成测试。运行构建和测试后汇报。
```

---

## Prompt 16：生产计划达成组合数据集

```text
请实现“生产计划达成率”组合数据集和报表。

输入：

- MES/模拟数据库数据集：实际产量
- Excel 当前已发布版本：计划产量

关联键根据 docs/business-decisions.md 中已确认规则；如果尚未确认，使用明确的开发测试键并在 UI 和文档标记“待现场确认”，不能伪装为正式口径。

要求：

1. 实现 Composite Dataset Provider。
2. 明确处理：只有计划、只有实际、计划为零、重复计划和无权限组织。
3. 达成率使用 Decimal 计算，统一舍入规则。
4. 报表包含计划、实际、差异、达成率和趋势。
5. 返回计划版本号、实际数据更新时间和整体报表生成时间。
6. Excel 发布或回退后，查询使用新的 ActiveVersion。
7. 数据权限同时应用于两个来源。
8. 为组合逻辑增加单元测试和集成测试。

完成后提供至少 5 个边界场景的期望值与实际测试结果。
```

---

## Prompt 17：摄像头扫描二维码和一维码

```text
请实现 MobilePwa 摄像头扫码，第一版只启用 QR Code 和 Code 128。

要求：

1. 使用 getUserMedia 调用摄像头。
2. 优先使用后置摄像头：facingMode=environment。
3. 使用本地打包的 ZXing-js，不使用 CDN。
4. 仅在用户点击后请求摄像头权限。
5. 扫描成功、取消、异常、组件 Dispose 和路由离开时全部停止 MediaStream tracks。
6. 同一码在 1 秒内重复识别时忽略。
7. 支持手工输入。
8. 解析 WO、EQ、LOT 三种前缀，限制长度和允许字符。
9. 调用 POST /api/scanner/resolve，由服务端检查对象归属和数据权限。
10. 服务端只返回允许的站内路由，不接受客户端传入跳转 URL。
11. 授权拒绝、无摄像头、HTTPS 不可用和识别超时均显示可操作提示。
12. 不把摄像头画面或扫码图片上传服务器。

测试：

- 编码解析单元测试。
- 无权限对象测试。
- 非法前缀和超长编码测试。
- JavaScript 生命周期测试或清晰的人工测试清单。

Cursor Cloud 无法替代手机实机测试。完成后生成 docs/scanner-test-checklist.md，列出 Android Chrome 和 iOS Safari 现场测试步骤。
```

---

## Prompt 18：收藏、详情和移动体验完善

```text
请完善 MobilePwa 的用户体验，不新增超出规格的业务模块。

实现：

- 报表收藏和取消收藏
- 首页快捷入口
- 工单、设备和批次详情页基础框架
- 筛选条件保持
- 分页和加载更多
- 网络断开与恢复提示
- 数据最后更新时间
- 空数据、无权限、超时和服务不可用状态
- PWA 新版本提示

要求：

- 不让 Service Worker 缓存敏感报表响应。
- 不在 localStorage 保存长期认证 Token。
- 页面返回时避免无意义重复查询。
- 工单号和产品编码保持原样。
- 图表和表格在 360、390、430 像素宽度下可用。
- 不使用公网资源。

运行构建和测试，并输出移动端人工检查清单。
```

---

## Prompt 19：运维监控和审计

```text
请完成 AdminWeb 的运维监控和审计模块。

实现：

- 同步任务列表
- 最近运行结果
- 手工触发任务
- 导入任务状态
- 登录日志
- 操作日志
- 报表查询日志
- 系统版本
- live/ready 健康状态

要求：

- 日志列表分页并限制查询日期范围。
- 错误详情不泄露密码、连接字符串、SQL 文本、服务器路径或用户上传文件正文。
- 支持按用户、操作类型、结果和日期筛选。
- 手工触发同步只进入队列，不在 HTTP 请求中等待任务完成。
- 对高风险动作记录操作前后对象标识，但不记录敏感正文。
- 只有相应权限可以查看审计或触发同步。

增加权限和脱敏测试。运行构建和测试后汇报。
```

---

## Prompt 20：安全加固检查

```text
请对当前项目执行安全加固。先审计，再进行范围明确的修复，不重写整个架构。

检查并修复：

- Cookie Secure、HttpOnly、SameSite
- CSRF 防护
- 登录限流和锁定
- API 默认认证策略
- 功能权限和数据权限
- SQL 参数化与标识符白名单
- CommandTimeout、日期跨度、分页和最大行数
- 上传文件路径穿越、格式、大小、签名和压缩炸弹风险
- Excel 公式与导出公式注入
- Secret 和连接字符串泄露
- Production 堆栈信息泄露
- 日志脱敏
- 安全响应头
- PWA 敏感 API 缓存
- 依赖漏洞和不必要包

要求：

1. 生成 docs/security-review.md。
2. 每个发现标记严重程度、证据、修复和验证方法。
3. 不以“局域网系统”为理由忽略认证和权限。
4. 不添加会破坏手机摄像头或 PWA 的安全头配置。
5. 不在没有证据时声称系统绝对安全。

运行依赖审计、构建和安全相关测试，汇报仍需现场完成的项目。
```

---

## Prompt 21：完整自动化测试和性能基线

```text
请执行第一版完整测试补强和性能基线工作，不新增业务功能。

要求：

1. 运行全部单元测试。
2. SQL Server 容器可用时运行全部集成测试；不可用时准备 CI 工作流并明确未执行项。
3. 增加测试覆盖：
   - 401/403
   - 数据范围越权
   - 报表版本发布和回退
   - Excel 错误、发布和回退
   - MES 同步幂等和 Checkpoint
   - 组合数据集边界
   - 扫码解析
4. 建立性能测试或可重复执行的基线脚本：
   - 50 用户首页读取
   - 20 用户常用报表
   - 50,000 行 Excel 导入
   - Worker 同步期间查询
5. 不用模拟等待时间伪造性能结果。
6. 生成 docs/test-report.md，列出环境、数据量、命令、通过项、失败项和未执行项。
7. 修复当前范围内确认的问题，未修复问题进入明确清单。

完成后运行格式化、构建和全部可运行测试，并给出是否达到规格性能目标的证据。
```

---

## Prompt 22：CI 流水线

```text
请为项目建立云端 CI。先识别当前 Git 托管平台，使用其现有机制；不要擅自迁移平台。

CI 至少包含：

1. Restore
2. Format 或代码风格检查
3. Build（Release）
4. Unit Tests
5. SQL Server 服务容器
6. 应用 Migration
7. Integration Tests
8. MobilePwa 和 AdminWeb 发布构建
9. 保存测试报告和构建产物

要求：

- 不在 CI 中连接工厂网络。
- 不提交 Secret。
- 示例配置与 Secret 名称分离。
- 缓存必须不包含凭据。
- Migration 只针对临时测试数据库。
- CI 失败时不生成“已通过”发布标签。
- 记录本地或 Cursor Cloud 无法运行而由 CI 覆盖的测试。

如果当前仓库权限不允许配置 CI，只生成待审核的工作流文件和说明，不尝试绕过权限。
```

---

## Prompt 23：局域网部署包和手册

```text
请生成可供现场人员执行的发布与部署材料。你运行在 Cursor Cloud，不能声称已经完成现场部署。

生成或更新：

- docs/deployment.md
- docs/factory-prerequisites.md
- docs/site-acceptance-checklist.md
- deploy/iis/ 下的配置模板
- deploy/windows-service/ 下的安装和卸载脚本
- appsettings.Production.example.json
- 发布构建脚本

部署内容必须覆盖：

1. Windows Server 和 IIS 前置条件。
2. .NET Hosting Bundle。
3. SQL Server 数据库创建和 Migration。
4. 同域路径 `/mobile`、`/admin`、`/api`。
5. 内部 DNS 和 HTTPS 证书。
6. Data Protection 密钥目录及权限。
7. SyncWorker Windows Service 安装、账号和恢复策略。
8. 现场 Secret 注入。
9. 从 FakeMesSourceReader 切换到 SqlServerMesSourceReader。
10. 日志目录和 ACL。
11. 数据库、Data Protection 密钥和已发布导入文件备份。
12. 回滚应用版本和数据库注意事项。
13. PWA 更新验证。
14. Android Chrome 和 iOS Safari 的摄像头验证。

脚本必须可读、幂等优先、使用占位变量，不得包含真实服务器地址和密码。运行云端可执行的发布构建和脚本静态检查，并列出所有现场步骤。
```

---

## Prompt 24：现场 MES 接入准备包

```text
请根据当前代码生成“现场 MES 接入准备包”，但不要连接真实 MES。

产出：

- docs/mes-integration-guide.md
- docs/mes-field-mapping.xlsx 或等价的可填写映射文件（若仓库流程不适合生成 xlsx，则生成结构化 CSV/Markdown 模板并说明）
- SqlServerMesSourceReader 的实现骨架和契约测试
- 示例只读视图定义，使用虚构表名并明确标记为示例
- 数据口径核对脚本模板

要求：

- 真实表名、字段名、连接信息全部留给现场填写。
- Provider 不拼接客户端输入 SQL。
- 支持命令超时、取消、增量水位和批次大小。
- 连接测试不输出完整连接字符串。
- 明确最小数据库权限。
- 说明如何先连接测试库，再切换到生产只读视图。
- 生成生产日报、工单进度和质量统计的抽样对账步骤。

完成后运行构建和契约测试。不能把实现骨架描述为已经接入真实 MES。
```

---

## Prompt 25：发布前最终审查

```text
请按照 FactoryReport_Cursor_Development_Spec.md 对当前仓库执行发布前最终审查。本阶段原则上不新增功能，只修复明确缺陷。

检查：

1. 第一版范围是否全部实现。
2. 是否存在超范围但未完成的半成品。
3. 项目引用和架构依赖是否正确。
4. Migration、Seed 和升级路径是否清晰。
5. 权限是否在服务端执行。
6. 是否存在任意 SQL、越权、Secret、日志泄露和不安全上传。
7. PWA 是否错误缓存敏感数据。
8. Excel 导入是否具备校验、预览、发布、版本和回退。
9. 扫码是否具备释放摄像头和手工输入兜底。
10. 同步失败是否不推进 Checkpoint。
11. 全部文档、配置示例和部署脚本是否一致。
12. 所有自动化测试和构建是否通过。

生成 docs/release-readiness.md，使用以下分类：

- 已通过
- 阻塞发布
- 非阻塞问题
- 必须现场验证
- 后续版本建议

运行所有云端可执行的格式化、构建、单元测试、集成测试和发布构建。不要把“未执行”写成“通过”。只有在没有云端阻塞项时，才可以说明“代码已具备现场测试条件”，不能说明“系统已经上线”。
```

---

## Prompt 26：现场测试完成后的缺陷修复

这条提示词在你完成第一次工厂现场测试后使用。将真实问题描述替换到方括号中，但不要粘贴密码或敏感生产数据。

```text
我们已经进行了工厂现场测试，下面是经过脱敏的问题记录：

[在这里粘贴问题、复现步骤、期望结果、实际结果、TraceId 和非敏感日志摘要]

请先阅读 FactoryReport_Cursor_Development_Spec.md 和现有实现，然后：

1. 根据证据定位根因，不先假设原因。
2. 区分代码缺陷、现场配置、数据口径、网络证书和 MES 数据质量问题。
3. 对代码缺陷做最小范围修复。
4. 为每个代码缺陷增加回归测试。
5. 不把现场特有表名或规则硬编码进通用核心。
6. 不要求上传真实数据库、密码或未脱敏数据。
7. 运行构建和相关测试。
8. 输出修复项、未修复项、现场复测步骤和回滚办法。

如果证据不足，请明确要求补充哪些非敏感信息，不要猜测修复。
```

---

## 每次 Cursor 完成后的人工检查

每完成一个 Prompt，至少确认：

- Cursor 是否只做了本阶段内容。
- 是否修改了不相关文件。
- 是否放入真实密码或地址。
- `dotnet build` 是否真的成功。
- 测试是通过、失败还是未执行。
- 是否把模拟测试说成现场验证。
- 是否产生需要你确认的数据口径问题。
- 下一阶段的前置条件是否满足。

发现问题时，不要直接进入下一阶段。可以发送：

```text
先不要进入下一阶段。请针对你刚才的改动执行一次范围审查：

1. 列出所有修改文件及修改原因。
2. 撤回与当前阶段无关的改动，但不要删除用户原有修改。
3. 修复当前构建或测试失败。
4. 再次运行构建和本阶段测试。
5. 明确区分通过、失败和未执行项目。
```

