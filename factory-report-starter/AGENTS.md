# AGENTS.md

## Project instructions

- Read `FactoryReport_Cursor_Development_Spec.md` and `FactoryReport_Cursor_Prompts.md` before making changes.
- Execute only the prompt phase explicitly requested by the user.
- Inspect existing code before editing and preserve unrelated user changes.
- Do not connect to real MES, ERP, factory IP addresses, internal domains, or production databases from Cursor Cloud.
- Do not request or commit real passwords, tokens, certificates, customer data, or production files.
- Mobile clients must never connect directly to a MES database.
- Enforce functional permissions and organization data scope in the server API.
- Do not allow ordinary administrators to execute arbitrary SQL.
- Use parameterized queries, query timeouts, date-range limits, pagination, and maximum row limits.
- Use local bundled JavaScript assets. The deployed factory system must not depend on public CDNs.
- Add or update tests for every implemented business rule.
- Run formatting, build, and relevant tests before reporting completion.
- Report tests as passed, failed, or not executed. Never treat mock or cloud tests as factory-site acceptance.
- Do not deploy or push to production unless the user explicitly requests it and the target is verified.

## Cursor Cloud

- Develop against fake providers and fictional fixtures.
- Use SQL Server containers only when the environment supports Docker.
- If SQL Server integration tests cannot run, keep them in CI and report them as not executed locally.
- Generate example configuration files only. Production secrets are injected on the factory server.
- Real MES mapping, internal DNS, HTTPS certificates, phone camera testing, backup recovery, and production data reconciliation require factory-site validation.

