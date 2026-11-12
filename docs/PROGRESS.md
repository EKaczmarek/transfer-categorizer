# Transfer Categorizer – progress

Aktualizuj po każdej sesji pracy. Szczegóły zadań: `PLAN.md`.

## Status etapów

| Etap | Opis | Status | Start | Koniec |
|---|---|---|---|---|
| 0 | Setup (repo, solution, docker-compose, CI) | ⬜ nie zaczęty | | |
| 1 | API + baza (EF Core, PostgreSQL) | ⬜ nie zaczęty | | |
| 2 | Outbox + RabbitMQ + worker + reguły | ⬜ nie zaczęty | | |
| 3 | Retry, DLQ, idempotencja, testy, CI | ⬜ nie zaczęty | | |
| 4 | Service Bus, Serilog, health checks | ⬜ nie zaczęty | | |
| 5 | Raporty SQL | ⬜ nie zaczęty | | |
| 6 | Databricks (Bronze/Silver/Gold, Unity Catalog) | ⬜ nie zaczęty | | |
| 7 | Model ML + integracja z .NET | ⬜ nie zaczęty | | |
| 8 | README, CV, dopracowanie | ⬜ nie zaczęty | | |

Legenda: ⬜ nie zaczęty · 🟨 w toku · ✅ gotowe

**Kamień milowy MVP:** etapy 0-3 ✅ → ____ (data)

## Dziennik pracy

| Data | Etap | Co zrobiłam | Czas | Następny krok |
|---|---|---|---|---|
| | | | | |

## Czego się nauczyłam / odświeżyłam

### C# / .NET
-

### SQL / PostgreSQL
-

### Messaging / architektura
-

### Databricks / dane
-

## Trudności i rozwiązania

| Problem | Rozwiązanie | Data |
|---|---|---|
| | | |

## Pytania do sprawdzenia / do rozmowy rekrutacyjnej
- [ ] Dlaczego Outbox, a nie bezpośrednia publikacja po zapisie do bazy?
- [ ] Jak zapewniam idempotencję konsumenta?
- [ ] Dlaczego adapter brokera za interfejsem?
- [ ] Jak zdecydowałam o indeksach i jak to zweryfikowałam (EXPLAIN)?
- [ ] Jak działa potok Bronze → Silver → Gold w moim projekcie?

## Decyzje projektowe
| Data | Decyzja | Powód |
|---|---|---|
| | PostgreSQL jako baza | lekki w Dockerze, prosty JDBC z Databricks |
| | RabbitMQ jako pierwszy broker | prosty lokalny setup, Service Bus jako drugi adapter |
