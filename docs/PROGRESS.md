# Transfer Categorizer – progress

Update after every work session. Task details: `PLAN.md`.

## Stage status

| Stage | Description | Status | Start | End |
|---|---|---|---|---|
| 0 | Setup (repo, solution, docker-compose, CI) | ✅ done | | 2026-11-16 |
| 1 | API + database (EF Core, PostgreSQL) | ✅ done | 2026-11-17 | 2026-11-24 |
| 2 | Outbox + RabbitMQ + worker + rules | ⬜ not started | | |
| 3 | Retry, DLQ, idempotency, tests, CI | ⬜ not started | | |
| 4 | Service Bus, Serilog, health checks | ⬜ not started | | |
| 5 | SQL reports | ⬜ not started | | |
| 6 | Databricks (Bronze/Silver/Gold, Unity Catalog) | ⬜ not started | | |
| 7 | ML model + .NET integration | ⬜ not started | | |
| 8 | README, CV, polishing | ⬜ not started | | |

Legend: ⬜ not started · 🟨 in progress · ✅ done

**MVP milestone:** stages 0-3 ✅ → ____ (date)

## Open items

Stage 1 is complete. Optional leftovers:

- [ ] (optional) 2-3 `WebApplicationFactory` tests: `POST /transfers` with an invalid body → 400 `application/problem+json`, `GET /transfers/{id}` with an unknown id → 404
- [ ] Confirm the CI workflow runs `dotnet test` and is green after the Stage 1 changes

Next: Stage 2 – Outbox table, `IMessagePublisher` + RabbitMQ adapter, dispatcher in `Transfers.Worker`, `TransferCreated` consumer, `RuleBasedCategorizer`.

## Work log

| Date | Stage | What I did | Time | Next step |
|---|---|---|---|---|
| 2026-11-16 | 0 | Repo, solution with 9 projects, project references, docker-compose (PostgreSQL + RabbitMQ) running on Podman, CI workflow, PR to `main` | | Stage 1: `Transfer` entity, EF Core + Npgsql, first migration |
| 2026-11-17 | 1 | `Transfer` entity, EF Core + Npgsql setup in `Transfers.Infrastructure`, first migration attempted (not yet verified in the database) | | Verify the migration and table in `psql`, then endpoints, validation + ProblemDetails, mock data |
| 2026-11-24 | 1 | Finished Stage 1: `POST /transfers` and `GET /transfers/{id}`, validation + ProblemDetails, mock data seeded with a console C# app (`TransfersSeed`, 20 Polish transfer descriptions, `foreach` + `PostAsJsonAsync`), domain unit tests (15 tests, all green). Tests found two real bugs in `Transfer.Create` (missing max-length check, `NullReferenceException` for a `null` description) – both fixed | | Stage 2: Outbox table + messaging |

## What I learned / refreshed

### C# / .NET
- Solution layout: dependencies point inwards (Domain and Contracts have no references; Api and Worker are the composition root)
- `dotnet new <template> -o <path>` creates a project in a given folder; `dotnet sln add` accepts multiple projects at once
- `.editorconfig` keeps code style consistent across editors and can be enforced in CI with `dotnet format --verify-no-changes`
- init ci workflow in github -> .github/workflows/ci.yml
- Domain validation lives in `Transfer.Create` (single source of truth); the endpoint only catches `ArgumentException` and returns 400 `ProblemDetails`
- Limits (e.g. `Transfer.MaxDescriptionLength`) are one `const` shared by the domain, the EF config (`HasMaxLength`) and the tests, so they cannot drift apart
- Text validation order: `string.IsNullOrWhiteSpace` → `Trim()` → length check
- xUnit: `Assert.Throws<T>` needs the exact exception type; `Assert.ThrowsAny<T>` also accepts derived types (e.g. `ArgumentNullException`)
- Inject `TimeProvider` instead of calling `DateTime.UtcNow`, so tests are deterministic
- `dotnet test` is run from the repo root (where the `.sln`/`.slnx` is), not from `tests/`; a single project: `dotnet test tests/Transfers.UnitTests`
- A `.http` file has no loops (requests are separated by `###`); bulk seeding is done with a script (PowerShell / C# console app)

### SQL / PostgreSQL
-

### Podman

- **Maszyna (Windows)**
  - `podman machine list` – czy maszyna istnieje
  - `podman machine init` – jednorazowo
  - `podman machine start`
  - `podman machine stop`

- **Compose**
  - `podman compose up -d` – start w tle
  - `podman compose ps` – status usług
  - `podman compose logs -f` – logi na żywo
  - `podman compose stop` – zatrzymaj, dane zostają
  - `podman compose start` – wznów
  - `podman compose down` – usuń kontenery (wolumeny zostają)
  - `podman compose down -v` – usuń też wolumeny (kasuje dane bazy!)

- **Kontenery**
  - `podman ps` – działające
  - `podman ps -a` – wszystkie, także zatrzymane
  - `podman start <nazwa>`
  - `podman stop <nazwa>`
  - `podman restart <nazwa>`
  - `podman rm <nazwa>` – usuń kontener
  - `podman logs --tail 50 <nazwa>`
  - `podman logs -f <nazwa>`
  - `podman exec -it <nazwa> sh` – shell w kontenerze

- **Zdrowie kontenerów**
  - `podman healthcheck run <nazwa>` – kod wyjścia 0 = zdrowy
  - `podman inspect --format '{{.State.Health.Status}}' <nazwa>` – healthy / unhealthy / starting
  - `podman exec <postgres> pg_isready -U <user> -d <baza>`
  - `podman exec <rabbit> rabbitmq-diagnostics -q ping`
  - RabbitMQ Management UI: http://localhost:15672

- **Obrazy i wolumeny**
  - `podman images`
  - `podman pull postgres:16`
  - `podman volume ls`
  - `podman volume rm <nazwa>`
  - `podman system prune` – sprzątanie nieużywanych zasobów

- **Typowe problemy**
  - `Cannot connect to Podman` → `podman machine start`
  - Kontener od razu `Exited` → `podman logs <nazwa>`
  - Port zajęty (5432, 5672, 15672) → zmień mapowanie portów albo zatrzymaj to, co go zajmuje
  - Status `starting` w healthchecku → sprawdź ręcznie `podman healthcheck run <nazwa>`


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
| Seeding with a JSON array failed | `POST /transfers` accepts a single object, not an array. Sent one request per record (`###` in `.http`) and then used a `foreach` loop in a console C# app (`TransfersSeed`) | 2026-11-24 |
| `The name 'MaxDescriptionLength' does not exist` (CS0103) | The constant was only an example; defined `public const int MaxDescriptionLength` in `Transfer` and used it in the EF config and tests | 2026-11-24 |
| `Create_WithEmptyDescription_Throws` failed for `"   nnn gvv"` | Test data typo: after `Trim()` it is a valid description. Changed to `"   "` | 2026-11-24 |
| `Create_WithTooLongDescription_Throws` failed (no exception) | `Transfer.Create` had no length check. Added it after `Trim()`, using `MaxDescriptionLength` (same value as `HasMaxLength`, so no new migration) | 2026-11-24 |
| `NullReferenceException` for `description == null` | `.Trim()` was called before the null check. Fixed the order: `IsNullOrWhiteSpace` → `Trim()` → length | 2026-11-24 |
| `Assert.Throws` failed on `ArgumentNullException` (exact type match) | Switched to `Assert.ThrowsAny<ArgumentException>` | 2026-11-24 |

## Questions to review / for job interviews
- [ ] Why Outbox instead of publishing directly after a database write?
- [ ] How do I ensure consumer idempotency?
- [ ] Why put the broker adapter behind an interface?
- [ ] How did I decide on indexes and how did I verify it (EXPLAIN)?
- [ ] How does the Bronze → Silver → Gold pipeline work in my project?
- [ ] Why keep validation rules in the domain (`Transfer.Create`) and not only in the endpoint?
- [ ] Why not use the EF Core InMemory provider for tests that touch the database (Testcontainers instead, Stage 3)?

## Design decisions
| Date | Decision | Reason |
|---|---|---|
| | PostgreSQL as the database | lightweight in Docker, simple JDBC from Databricks |
| | RabbitMQ as the first broker | simple local setup, Service Bus as the second adapter |
| 2026-11-16 | Podman instead of Docker for local containers | already installed; CI on GitHub Actions still uses Docker |
| 2026-11-24 | Validation in `Transfer.Create`; endpoint maps `ArgumentException` to 400 `ProblemDetails` | one source of truth for the rules, endpoint stays thin |
| 2026-11-24 | One shared constant for limits (`Transfer.MaxDescriptionLength`) used by domain, EF config and tests | domain and database limits cannot drift apart |
| 2026-11-24 | Mock data seeded with a separate console app (`TransfersSeed`) | reusable for Stage 2 categorizer tests; the few-thousand-record dataset is only needed in Stage 6 |