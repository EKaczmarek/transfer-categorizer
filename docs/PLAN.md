# Transfer Categorizer – project plan

## Goal
A portfolio project that at the same time:
- refreshes **.NET / C#** (ASP.NET Core, EF Core, async, DI, testing),
- strengthens **SQL** (PostgreSQL: indexes, CTEs, window functions, EXPLAIN),
- uses my **Databricks** knowledge (medallion, Auto Loader, Delta, Unity Catalog),
- shows the requirements from job postings: messaging, event-driven design, unit/integration/contract tests, CI/CD, logging.

## Assumptions
- Database: **PostgreSQL 16**. Initial broker: **RabbitMQ**, then Azure Service Bus as a second implementation behind the same interface.
- Categorizer behind the `ITransferCategorizer` interface: rules first, ML model later.
- Rule: **a narrow MVP first (stages 0-3)**, the rest only once the core works and is tested.

## Flow
1. `POST /transfers` saves the transfer and a row in the Outbox table in a single transaction.
2. The dispatcher reads the Outbox and publishes `TransferCreated` to the broker.
3. The worker receives the event, categorizes the description, saves the category and publishes `TransferCategorized`.
4. Processing failure: a few retries, then the dead-letter queue.
5. `GET /transfers/{id}` shows the status (Pending → Categorized).

## Repository structure
```
transfer-categorizer/
├─ src/
│  ├─ Transfers.Contracts/        # events
│  ├─ Transfers.Domain/           # entities, rules
│  ├─ Transfers.Application/      # use cases, interfaces
│  ├─ Transfers.Infrastructure/   # EF Core, Outbox, broker adapters
│  ├─ Transfers.Api/              # ASP.NET Core
│  └─ Transfers.Worker/           # BackgroundService
├─ tests/
│  ├─ Transfers.UnitTests/
│  ├─ Transfers.IntegrationTests/
│  └─ Transfers.ContractTests/
├─ docs/
│  ├─ PLAN.md
│  └─ PROGRESS.md
├─ docker-compose.yml
├─ .github/workflows/ci.yml
└─ README.md
```

---

## Stage 0 – Setup (0.5 day) ✅
- [x] GitHub repo, `.gitignore`, `.editorconfig`
- [x] Solution + 6 src projects + 3 test projects, references following the dependency direction (Domain with no dependencies)
- [x] `docker-compose.yml`: PostgreSQL 16 + RabbitMQ (management)
- [x] Empty CI workflow: build + test

**Done when:** `dotnet build` and `docker compose up` work from scratch.

## Stage 1 – API + database (day 1-2)
- [ ] `Transfer` entity (Id, Amount, Currency, Description, CreatedAt, Status, Category)
- [ ] EF Core + Npgsql, first migration
- [ ] `POST /transfers`, `GET /transfers/{id}`
- [ ] Input validation, ProblemDetails for errors
- [ ] Domain unit tests

**SQL:** design the table deliberately (types, constraints, `NOT NULL`, index on `Status` and `CreatedAt`).

## Stage 2 – Outbox + messaging (week 1)
- [ ] Outbox table, saving the transfer and the message in one transaction
- [ ] `IMessagePublisher` interface, RabbitMQ adapter
- [ ] Dispatcher in `Transfers.Worker` (BackgroundService, `CancellationToken`)
- [ ] `TransferCreated` consumer
- [ ] `RuleBasedCategorizer` (keywords) behind `ITransferCategorizer`
- [ ] Publish `TransferCategorized`, status → Categorized

**Done when:** POST → after a moment GET shows the category.

## Stage 3 – Reliability and tests (week 2)
- [ ] Retry with backoff, dead-letter queue
- [ ] Idempotency: table of processed `MessageId`s
- [ ] Integration tests with Testcontainers (real PostgreSQL and RabbitMQ)
- [ ] Contract tests for event shapes
- [ ] CI: build, tests, Docker image

**SQL:** a query with `EXPLAIN` before and after adding an index.

> After stage 3 you have a complete MVP. Later stages are extensions.

## Stage 4 – Second broker implementation and observability (week 3)
- [ ] Azure Service Bus adapter (per the official emulator instructions: aka.ms/servicebusemulator)
- [ ] Broker selection from configuration (DI)
- [ ] Serilog (structured logs), health checks
- [ ] Optional: OpenTelemetry, Kafka as a third adapter

## Stage 5 – SQL reports (week 3-4)
Hand-written queries, exposed as report endpoints or scripts in `docs/sql/`:
- [ ] Spending per category and month (`GROUP BY`, `date_trunc`)
- [ ] Top 10 merchants (CTE)
- [ ] Category share of the total (window functions: `SUM() OVER`)
- [ ] Month-over-month change (`LAG`)
- [ ] Description of each query and its execution plan in the README

## Stage 6 – Databricks (week 4-5)
Goal: a data pipeline based on the project's transfers.
- [ ] Generate or anonymize a set of transfers (a few thousand records)
- [ ] Export from PostgreSQL to files (CSV/JSON/Parquet) or read via JDBC
- [ ] **Bronze:** raw data, ingestion with Auto Loader
- [ ] **Silver:** cleaning descriptions, deduplication, normalizing merchant names
- [ ] **Gold:** spending aggregates + training set (description → category)
- [ ] Organization in Unity Catalog (catalog / schema / permissions)
- [ ] Notebooks and architecture description in `docs/databricks/`

Check product names and available options (including the free environment) in the official documentation: docs.databricks.com.

## Stage 7 – ML model (week 5-6)
- [ ] Train a text classifier (scikit-learn, optionally Keras later) on the Gold