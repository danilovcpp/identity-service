# Codex instructions for Identity

## Product and source of truth

The product name is **Identity**. This repository implements a standalone self-hosted user identity and authentication service. It must not refer to or depend on any external business application.

**Read `Identity_Codex_Spec.md` before changing anything.** The spec is the authoritative requirements document, including scope, layered architecture, security controls, API contracts, DB schema, acceptance criteria, and staged delivery plan. If implementation and spec differ, do not silently change requirements: state the discrepancy in your report.

## Workflow

1. Implement **only** the stage explicitly requested by the human (stages 0–7 in the base spec, 8–12 in `Identity_Admin_Codex_Addendum.md`). Read the addendum for admin work; it extends v1.0 scope only for Identity's own administrative functionality. Do not implement later stages in advance.
2. Before editing, inspect the existing repository. Prefer minimal, focused, maintainable changes.
3. Follow Clean Architecture dependencies exactly: Domain standalone; Application -> Domain; Infrastructure -> Application + Domain; API -> Application + Infrastructure.
4. Use ASP.NET Core **Controllers** for business HTTP operations. Use custom CQRS interfaces and dispatchers. **Do not add MediatR, Minimal API business endpoints, a generic repository, ASP.NET Core Identity user management or a ready-made authentication server.**
5. No external product concepts, permissions, organizations, tenants or domain-specific roles in Identity.
6. No cryptographic improvisation. Do not store or log raw passwords, refresh tokens, one-time tokens, private keys or SMTP secrets. Check the spec's transactional email outbox encryption requirements.
7. Use real PostgreSQL for integration tests; never substitute EF InMemory as evidence of production correctness. Test concurrent registration and refresh rotation/replay.
8. Before finishing, run applicable `dotnet restore`, `dotnet build`, `dotnet test`, migration checks, Docker smoke checks. Report any checks you could not run. Never claim unexecuted tests passed.
9. Update corresponding docs and configuration samples as part of each change. No committed secrets or production keys.
10. End every stage with the report structure from section 13 of the spec, then stop. Do not deploy or take actions in external accounts without explicit approval.

## Default start command

Read `Identity_Codex_Spec.md`, implement only stage 0, run available checks, report using spec section 13, and stop.
