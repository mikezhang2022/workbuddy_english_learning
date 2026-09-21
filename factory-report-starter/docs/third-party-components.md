# 第三方组件清单（商业化依赖基线）

> 盘点范围：`factory-report-starter/` 直接依赖（NuGet / 捆绑前端静态资源）。  
> **本文件不是法律审查结论**。无法确认处标「待核实」。  
> 不自动删除已有依赖；许可风险仅记录模块与替代建议。  
> 保留第三方版权声明；不得将第三方代码标为完全自主开发。  
> 商业化约束：不复制 DataEase / DataGear / 积木报表产品源码；新依赖优先选择适合闭源分发的许可证并遵守通知要求。

## 1. 运行时 / 框架

| 名称 | 实际版本 | 用途 | 许可证 | 来源 | 备注 |
|------|----------|------|--------|------|------|
| .NET / ASP.NET Core（含 Blazor WASM） | SDK 10.0.401 / 共享框架 10.0.12；项目 `net10.0` | 后端 API、WASM 前端、Worker、Admin | MIT（.NET） | https://dotnet.microsoft.com | 主框架许可 **不** 覆盖 Oracle 驱动等 |
| Microsoft.AspNetCore.OpenApi | 10.0.12 | Development OpenAPI | MIT | NuGet | Api |
| Microsoft.AspNetCore.Components.WebAssembly | 10.0.12 | Blazor WASM 宿主 | MIT | NuGet | Client |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 10.0.12 | 开发服务器 | MIT | NuGet | Client；PrivateAssets |
| Microsoft.AspNetCore.Components.Authorization | 10.0.12 | 前端授权状态 | MIT | NuGet | Client |
| Microsoft.Extensions.Http | 10.0.0 | HttpClient 工厂 | MIT | NuGet | Client |
| Microsoft.Extensions.*（Hosting/Options/Configuration 等） | 10.0.12（Worker/Infrastructure/测试） | DI、配置、宿主 | MIT | NuGet | 以各 csproj 为准 |
| Microsoft.NET.Test.Sdk | 17.14.1 | 测试 SDK | 待核实（微软测试组件惯用条款） | NuGet | 仅测试 |
| xunit | 2.9.3 | 单元/集成测试 | Apache-2.0 | NuGet | 仅测试 |
| xunit.runner.visualstudio | 3.1.4 | VS 测试适配 | Apache-2.0 | NuGet | 仅测试 |
| coverlet.collector | 6.0.4 | 覆盖率收集 | MIT | NuGet | 仅测试 |

## 2. UI 控件 / 图标 / 字体

| 名称 | 实际版本 | 用途 | 许可证 | 来源 | 备注 |
|------|----------|------|--------|------|------|
| MudBlazor | 8.15.0 | Client UI 组件库 | MIT | NuGet `MudBlazor`；官方 https://mudblazor.com | 含 Material 风格图标字体资源（随包分发）；**不得**用 ASP.NET 许可代替 |
| Bootstrap CSS utilities（本地捆绑） | 仓库内 `wwwroot/lib/bootstrap/dist`（未在 csproj 钉版本；静态拷贝） | `d-none` / `d-md-*` 等响应式工具类 | MIT | getbootstrap.com；本地 `lib/bootstrap` | 非 CDN；版本以目录内文件为准，**待核实精确版本号** |
| 自定义 `wwwroot/css/app.css` | — | 布局与报表样式 | 项目自有 | 本仓库 | — |
| PWA 图标 `icon-192.png` / `icon-512.png` / `favicon.png` | — | 应用图标 | 待核实（仓库内资源来源未单独声明） | 本仓库 Client/wwwroot | 商用发布前确认权属 |

## 3. Excel（第二阶段选用）

### 3.1 选型说明（非法律审查）

| 候选 | 许可证印象 | 传递依赖观察 | 结论 |
|------|------------|--------------|------|
| **MiniExcel** | Apache-2.0 | **无**额外 NuGet 传递依赖（实测 `dotnet list package --include-transitive`） | **已选用** |
| NPOI | Apache-2.0 | 含 SixLabors.Fonts / ImageSharp / BouncyCastle 等 | 未选（传递面更大，且 ImageSharp 有已知 advisory） |
| ClosedXML | MIT | 传递依赖含 SixLabors.Fonts（Six Labors Split License，商业分发可能有附加条件） | 未选；若未来选用必须单独评估 Split License 风险 |
| DocumentFormat.OpenXml | MIT | `DocumentFormat.OpenXml.Framework` + `System.IO.Packaging` | 未选（API 更底层；可作为替代后备） |

### 3.2 实际引入

| 名称 | 实际版本 | 用途 | 许可证 | 来源 | 备注 |
|------|----------|------|--------|------|------|
| MiniExcel | 1.41.3 | xlsx 模板生成与行解析（导入流水线） | Apache-2.0 | NuGet；https://github.com/mini-software/MiniExcel | 引用项目：`FactoryReport.Infrastructure` |
| （传递依赖） | — | — | — | — | **无 NuGet 传递依赖**（以 `dotnet list package --include-transitive` 为准） |

**未宣称已完成法律审查。** 商用闭源分发前须由法务确认 Apache-2.0 通知与 NOTICE 义务。

## 4. Oracle 驱动（已引用、未运行连接）

| 名称 | 实际版本 | 用途 | 许可证 | 来源 | 备注 |
|------|----------|------|--------|------|------|
| Oracle.ManagedDataAccess.Core | 23.26.301 | ODP.NET 托管驱动（未来现场） | **Oracle 专有许可**（ODT/ODP.NET） | NuGet | **非** MIT；闭源分发须遵守 Oracle 许可与通知；本阶段 **不连接** |
| Oracle.EntityFrameworkCore | 10.23.26301 | EF Core Oracle Provider（未来现场） | **Oracle 专有许可** | NuGet | 同上；仅占位引用，无 DbContext 实现 |

**许可风险记录**：Oracle 驱动不适合用「主框架 MIT」概括。若客户交付物需嵌入驱动，须由法务/商务确认 Oracle 再分发条款。  
**替代建议（未执行删除）**：现场由客户自行安装驱动、或运行时再分发并附 Oracle 许可全文；开发期可继续仅 Fake 而不加载连接。

## 5. 明确未引入

- DataEase / DataGear / 积木报表源码或二次打包
- ClosedXML / NPOI（已评估未引入）
- 商业报表设计器控件（未引入）
- SQL Server 客户端包
- 公共 CDN 运行时依赖（部署要求本地捆绑）

## 6. 维护规则

1. 新增 NuGet/静态资源时同步更新本表（名称、版本、用途、许可证、来源）。
2. 无法确认许可证时写「待核实」，不宣称已完成法律审查。
3. 发现 copyleft 或再分发限制时：记录涉及模块与替代建议，**不**静默删除依赖（除非产品决策明确要求）。
4. **传递依赖必须单独列出**，不得用主框架许可证代替。
