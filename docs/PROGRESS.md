# Transfer Categorizer – progress

Update after every work session. Task details: `PLAN.md`.

## Stage status

| Stage | Description | Status | Start | End |
|---|---|---|---|---|
| 0 | Setup (repo, solution, docker-compose, CI) | ✅ done | | 2026-11-16 |
| 1 | API + database (EF Core, PostgreSQL) | ⬜ not started | | |
| 2 | Outbox + RabbitMQ + worker + rules | ⬜ not started | | |
| 3 | Retry, DLQ, idempotency, tests, CI | ⬜ not started | | |
| 4 | Service Bus, Serilog, health checks | ⬜ not started | | |
| 5 | SQL reports | ⬜ not started | | |
| 6 | Databricks (Bronze/Silver/Gold, Unity Catalog) | ⬜ not started | | |
| 7 | ML model + .NET integration | ⬜ not started | | |
| 8 | README, CV, polishing | ⬜ not started | | |

Legend: ⬜ not started · 🟨 in progress · ✅ done

**MVP milestone:** stages 0-3 ✅ → ____ (date)

## Work log

| Date | Stage | What I did | Time | Next step |
|---|---|---|---|---|
| 2026-11-16 | 0 | Repo, solution with 9 projects, project references, docker-compose (PostgreSQL + RabbitMQ) running on Podman, CI workflow, PR to `main` | | Stage 1: `Transfer` entity, EF Core + Npgsql, first migration |

## What I learned / refreshed

### C# / .NET
- Solution layout: dependencies point inwards (Domain and Contracts have no references; Api and Worker are the composition root)
- `dotnet new <template> -o <path>` creates a project in a given folder; `dotnet sln add` accepts multiple projects at once
- `.editorconfig` keeps code style consistent across editors and can be enforced in CI with `dotnet format --verify-no-changes`
- init ci workflow in github -> .github/workflows/ci.yml

### SQL / PostgreSQL
-

### Messaging / architecture
- Healthchecks in docker-compose will matter later for `depends_on: condition: service_healthy`

### Databricks / data
-

## Problems and solutions

| Problem | Solution | Date |
|---|---|---|
| `podman` not recognized in the terminal | Installed the Podman engine (Desktop is only the GUI) and opened a new terminal so PATH refreshed | 2026-11-16 |
| `podman compose`: compose provider not found | Installed a compose provider (`docker-compose` or `podman-compose`) and added it to PATH | 2026-11-16 |
| Only the PostgreSQL container was running | Checked `podman ps -a` and logs, fixed the cause, restarted with `podman compose up -d` | 2026-11-16 |
| PowerShell commands failing in cmd.exe (`$env:TEMP`, `Remove-Item`, `(ls ...)`) | Stick to one shell; cmd equivalents: `%TEMP%`, `rmdir /s /q` | 2026-11-16 |

## Questions to review / for job interviews
- [ ] Why Outbox instead of publishing directly after a database write?
- [ ] How do I ensure consumer idempotency?
- [ ] Why put the broker adapter behind an interface?
- [ ] How did I decide on indexes and how did I verify it (EXPLAIN)?
- [ ] How does the Bronze → Silver → Gold pipeline work in my project?

## Design decisions
| Date | Decision | Reason |
|---|---|---|
| | PostgreSQL as the database | lightweight in Docker, simple JDBC from Databricks |
| | RabbitMQ as the first broker | simple local setup, Service Bus as the second adapter |
| 2026-11-16 | Podman instead of Docker for local containers | already installed; CI on GitHub Actions still uses Docker |