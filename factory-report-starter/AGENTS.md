# AGENTS.md

## Project instructions

- Project root is `factory-report-starter/`. Only read or modify this directory and its subdirectories.
- Read `FactoryReport_Cursor_Development_Spec.md` and `FactoryReport_Cursor_Prompts.md` before making changes.
- Execute only the prompt phase explicitly requested by the user.
- Inspect existing code before editing and preserve unrelated user changes.
- Do not connect to real MES, ERP, Oracle, factory IP addresses, internal domains, or production databases from Cursor Cloud.
- Do not request or commit real passwords, tokens, certificates, customer data, or production files.
- Mobile clients must never connect directly to a MES database.
- Enforce functional permissions and organization data scope in the server API.
- Do not allow ordinary administrators to execute arbitrary SQL.
- Use parameterized queries, query timeouts, date-range limits, pagination, and maximum row limits.
- Use local bundled JavaScript assets. The deployed factory system must not depend on public CDNs.
- Add or update tests for every implemented business rule.
- Run formatting, build, and relevant tests before reporting completion (except Phase 0, which must not build, install dependencies, or write business code).
- Report tests as passed, failed, or not executed. Never treat mock or cloud tests as factory-site acceptance.
- Do not deploy or push to production unless the user explicitly requests it and the target is verified.

## Database (Oracle only)

- The local report database and MES/ERP read-only sources are Oracle.
- Use Oracle official .NET drivers: Oracle.ManagedDataAccess / ODP.NET, and EF Core provider Oracle.EntityFrameworkCore.
- Oracle version, schema name, character set, connection method, and on-site read-only views are all marked 【待现场确认】 until confirmed.
- Do not add any SQL Server-specific code, scripts, packages, or configuration (including Microsoft.Data.SqlClient, SqlBulkCopy, or SQL Server containers).
- Fake / demo data must use in-memory or file fixtures only; never connect to a database for simulation.

## Cursor Cloud

- Develop against fake providers and fictional fixtures (memory/file). Do not connect to Oracle for cloud simulation.
- Use Oracle containers only when the environment supports Docker and the user explicitly requests container-based integration tests; otherwise keep Oracle integration tests in CI and report them as not executed locally.
- If Oracle integration tests cannot run, keep them in CI and report them as not executed locally. Do not pretend EF InMemory tests are Oracle integration tests.
- Generate example configuration files only. Production secrets are injected on the factory server.
- Real MES mapping, internal DNS, HTTPS certificates, phone camera testing, backup recovery, and production data reconciliation require factory-site validation.
