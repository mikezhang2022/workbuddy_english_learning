# 商业化第二阶段结果（phase-02）

> 区分三层：**代码已实现** / **测试已通过** / **现场已验证**。  
> 本阶段 = Excel 导入流水线 + CI 固化；**现场已验证：无**。

## 1. 仓库与分支

| 项 | 值 |
|----|-----|
| 仓库 | `mikezhang2022/workbuddy_english_learning` |
| 子项目 | `factory-report-starter/` |
| 基线 tip | `86a1776`（第一阶段 PR #4 头） |
| 开发分支 | `cursor/bc-82b70fdf-f902-46e4-b16b-d2db46a0e6da-7c2f`（基于 `cursor/bi-2a49` tip，未 rebase） |
| 最终提交号 | `5bbc2ceb8b0e20b622fd8fa41042dfb9dd6ce426` |
| PR | https://github.com/mikezhang2022/workbuddy_english_learning/pull/5 （草稿） |

## 2. 复用与本阶段变更

### 复用

- Domain `ImportBatch` / `DatasetVersionState` / `ImportBatchStatus` / `DatasetPublishStatus`
- Cookie 认证、Antiforgery、组织数据范围、`IReportQueryScopeService`
- Fake 夹具组织/产品主数据（校验字典）
- 五张报表与全部第一阶段测试路径

### 本阶段新增

| 变更 | 说明 |
|------|------|
| Excel 导入服务端流水线 | 模板下载、上传解析、逐行校验、发布/激活、回退 |
| Fake 导入工作区 | 内存批次/错误/版本/计划发布行；不写 Oracle |
| MiniExcel 1.41.3 | Apache-2.0；无 NuGet 传递依赖 |
| Client `/imports` | PC 上传与操作；手机只读 |
| 权限策略 | `ImportRead` / `ImportManage` |
| CI | `.github/workflows/factory-report-starter.yml`（paths 过滤） |
| 文档 | `excel-import.md`、`phase-02-result.md`、更新 current-state/development/third-party |

## 3. 关键改动文件（主要）

```
.github/workflows/factory-report-starter.yml
factory-report-starter/docs/excel-import.md
factory-report-starter/docs/phase-02-result.md
factory-report-starter/docs/current-state.md
factory-report-starter/docs/development.md
factory-report-starter/docs/third-party-components.md
factory-report-starter/src/FactoryReport.Domain/Import/*
factory-report-starter/src/FactoryReport.Domain/Security/AuthorizationPolicies.cs
factory-report-starter/src/FactoryReport.Application/Import/*
factory-report-starter/src/FactoryReport.Infrastructure/Excel/*
factory-report-starter/src/FactoryReport.Infrastructure/Import/*
factory-report-starter/src/FactoryReport.Infrastructure/Fake/FakeImportBatchReadRepository.cs
factory-report-starter/src/FactoryReport.Infrastructure/Fake/FakeDailyProductionPlanReadRepository.cs
factory-report-starter/src/FactoryReport.Infrastructure/DependencyInjection.cs
factory-report-starter/src/FactoryReport.Infrastructure/FactoryReport.Infrastructure.csproj
factory-report-starter/src/FactoryReport.Api/Endpoints/ExcelImportEndpoints.cs
factory-report-starter/src/FactoryReport.Api/Program.cs
factory-report-starter/src/FactoryReport.Api/Security/AuthenticationServiceCollectionExtensions.cs
factory-report-starter/src/FactoryReport.Api/Infrastructure/GlobalExceptionHandler.cs
factory-report-starter/src/FactoryReport.Client/Pages/Imports/*
factory-report-starter/src/FactoryReport.Client/Services/Api/ImportApiClient.cs
factory-report-starter/tests/FactoryReport.UnitTests/Import/*
factory-report-starter/tests/FactoryReport.IntegrationTests/ExcelImportApiTests.cs
```

## 4. 构建 / 测试 / 启动

```bash
cd factory-report-starter
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet build FactoryReport.sln
# Build succeeded. 0 Warning(s). 0 Error(s).

dotnet test FactoryReport.sln
# UnitTests: Passed 223
# IntegrationTests: Passed 83
# 合计 Passed 306，Failed 0
```

历史警告：本次构建 **0 警告**。

## 5. 手工验证摘要

见本文件后续「验证证据」与交付说明（API 上传→校验→发布→回退；401/403；PC/390px 截图）。

## 6. 干净环境跑通导入

1. `bash scripts/dev-bootstrap.sh`
2. 启动 API `http://127.0.0.1:5161` 与 Client `http://127.0.0.1:5269`（主机名一致）
3. 登录 `sysadmin` / `Dev-Only-SystemAdmin-Passw0rd!`（仅开发）
4. 打开 `/imports` → 下载计划模板 → 上传 → 校验 → 发布 → 回退

## 7. 未验证 / 阻塞 / 风险

| 项 | 层级 |
|----|------|
| Oracle 持久化导入 | **不支持** |
| 实际导入驱动计划达成 | **代码未接线** |
| MiniExcel/Apache 法务审查 | **未做**（仅记录） |
| 现场验收 | **无** |
| 回退夹具预置已发布批次可能影响演示月度计划 | 风险：测试环境共享 Fake 单例 |

## 8. 第三阶段建议（勿在本阶段开始）

1. 现场问卷回收后实现 Oracle 应用 Schema 持久化导入批次/版本。
2. 将已发布计划/实际接入计划达成对比正式查询。
3. Admin 用户/权限分配最小 UI。
4. 完成第三方许可法务确认。
