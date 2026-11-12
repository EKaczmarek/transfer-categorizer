# Transfer Categorizer – plan projektu

## Cel
Projekt portfolio, który jednocześnie:
- odświeża **.NET / C#** (ASP.NET Core, EF Core, async, DI, testy),
- wzmacnia **SQL** (PostgreSQL: indeksy, CTE, funkcje okna, EXPLAIN),
- wykorzystuje wiedzę z **Databricks** (medallion, Auto Loader, Delta, Unity Catalog),
- pokazuje wymagania z ogłoszeń: messaging, event-driven, testy unit/integration/contract, CI/CD, logging.

## Założenia
- Baza: **PostgreSQL 16**. Broker na start: **RabbitMQ**, potem Azure Service Bus jako druga implementacja za tym samym interfejsem.
- Kategoryzator za interfejsem `ITransferCategorizer`: najpierw reguły, później model ML.
- Zasada: **najpierw wąskie MVP (etapy 0-3)**, reszta dopiero gdy rdzeń działa i ma testy.

## Przepływ
1. `POST /transfers` zapisuje przelew i wiersz w tabeli Outbox w jednej transakcji.
2. Dispatcher czyta Outbox i publikuje `TransferCreated` do brokera.
3. Worker odbiera zdarzenie, kategoryzuje opis, zapisuje kategorię i publikuje `TransferCategorized`.
4. Błąd przetwarzania: kilka ponowień, potem dead-letter queue.
5. `GET /transfers/{id}` pokazuje status (Pending → Categorized).

## Struktura repozytorium
```
transfer-categorizer/
├─ src/
│  ├─ Transfers.Contracts/        # zdarzenia
│  ├─ Transfers.Domain/           # encje, reguły
│  ├─ Transfers.Application/      # use case'y, interfejsy
│  ├─ Transfers.Infrastructure/   # EF Core, Outbox, adaptery brokerów
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

## Etap 0 – Setup (0,5 dnia)
- [ ] Repo na GitHub, `.gitignore`, `.editorconfig`
- [ ] Solution + 6 projektów src + 3 testowe, referencje zgodne z kierunkiem zależności (Domain bez zależności)
- [ ] `docker-compose.yml`: PostgreSQL 16 + RabbitMQ (management)
- [ ] Pusty workflow CI: build + test

**Gotowe, gdy:** `dotnet build` i `docker compose up` działają od zera.

## Etap 1 – API + baza (dzień 1-2)
- [ ] Encja `Transfer` (Id, Amount, Currency, Description, CreatedAt, Status, Category)
- [ ] EF Core + Npgsql, pierwsza migracja
- [ ] `POST /transfers`, `GET /transfers/{id}`
- [ ] Walidacja wejścia, ProblemDetails dla błędów
- [ ] Testy jednostkowe domeny

**SQL:** zaprojektuj tabelę świadomie (typy, ograniczenia, `NOT NULL`, indeks po `Status` i `CreatedAt`).

## Etap 2 – Outbox + messaging (tydzień 1)
- [ ] Tabela Outbox, zapis przelewu i wiadomości w jednej transakcji
- [ ] Interfejs `IMessagePublisher`, adapter RabbitMQ
- [ ] Dispatcher w `Transfers.Worker` (BackgroundService, `CancellationToken`)
- [ ] Konsument `TransferCreated`
- [ ] `RuleBasedCategorizer` (słowa kluczowe) za `ITransferCategorizer`
- [ ] Publikacja `TransferCategorized`, status → Categorized

**Gotowe, gdy:** POST → po chwili GET pokazuje kategorię.

## Etap 3 – Niezawodność i testy (tydzień 2)
- [ ] Retry z backoffem, dead-letter queue
- [ ] Idempotencja: tabela przetworzonych `MessageId`
- [ ] Testy integracyjne z Testcontainers (prawdziwy PostgreSQL i RabbitMQ)
- [ ] Testy kontraktowe kształtu zdarzeń
- [ ] CI: build, testy, obraz Dockera

**SQL:** zapytanie z `EXPLAIN` przed i po dodaniu indeksu.

> Po etapie 3 masz kompletne MVP. Dalsze etapy to rozszerzenia.

## Etap 4 – Druga implementacja brokera i obserwowalność (tydzień 3)
- [ ] Adapter Azure Service Bus (wg oficjalnej instrukcji emulatora: aka.ms/servicebusemulator)
- [ ] Wybór brokera z konfiguracji (DI)
- [ ] Serilog (logi strukturalne), health checks
- [ ] Opcjonalnie: OpenTelemetry, Kafka jako trzeci adapter

## Etap 5 – Raporty SQL (tydzień 3-4)
Zapytania pisane ręcznie, wystawione jako endpointy raportowe lub skrypty w `docs/sql/`:
- [ ] Wydatki per kategoria i miesiąc (`GROUP BY`, `date_trunc`)
- [ ] Top 10 merchantów (CTE)
- [ ] Udział kategorii w sumie (funkcje okna: `SUM() OVER`)
- [ ] Zmiana miesiąc do miesiąca (`LAG`)
- [ ] Opis każdego zapytania i planu wykonania w README

## Etap 6 – Databricks (tydzień 4-5)
Cel: potok danych oparty o przelewy z projektu.
- [ ] Wygeneruj lub zanonimizuj zbiór przelewów (kilka tysięcy rekordów)
- [ ] Eksport z PostgreSQL do plików (CSV/JSON/Parquet) albo odczyt przez JDBC
- [ ] **Bronze:** surowe dane, ingestia przez Auto Loader
- [ ] **Silver:** czyszczenie opisów, deduplikacja, normalizacja nazw merchantów
- [ ] **Gold:** agregaty wydatków + zbiór treningowy (opis → kategoria)
- [ ] Organizacja w Unity Catalog (catalog / schema / uprawnienia)
- [ ] Notebooki i opis architektury w `docs/databricks/`

Nazwy produktów i dostępne opcje (w tym darmowe środowisko) sprawdź w oficjalnej dokumentacji: docs.databricks.com.

## Etap 7 – Model ML (tydzień 5-6)
- [ ] Trening klasyfikatora tekstu (scikit-learn, potem opcjonalnie Keras) na zbiorze Gold
- [ ] Ewaluacja: accuracy, macierz pomyłek, porównanie z regułami
- [ ] Eksport do ONNX i wywołanie z .NET **albo** mała usługa HTTP
- [ ] `MlTransferCategorizer` jako druga implementacja `ITransferCategorizer`
- [ ] Fallback do reguł przy niskiej pewności modelu

## Etap 8 – Dopracowanie (tydzień 6)
- [ ] README: diagram przepływu, decyzje architektoniczne („dlaczego Outbox", „dlaczego adapter brokera"), uruchomienie jedną komendą, uruchomienie testów
- [ ] Badge CI, zrzuty ekranu/GIF z działania
- [ ] Opis projektu do CV i LinkedIn (3-4 zdania)
- [ ] Przygotowanie odpowiedzi na pytania rekrutacyjne o Outbox, idempotencję, retry

## Mapa wymagań
| Wymaganie z ofert | Gdzie w projekcie |
|---|---|
| OOP, SOLID, clean code | warstwy, interfejsy w Application |
| ASP.NET Core, REST | Transfers.Api |
| async, wielowątkowość | BackgroundService, CancellationToken |
| messaging, event-driven | RabbitMQ + Service Bus za jednym interfejsem |
| DI | rejestracja adapterów, wybór z konfiguracji |
| EF Core, SQL | migracje, indeksy, raporty SQL |
| testy unit/integration/contract | trzy projekty testowe |
| CI/CD, Git | GitHub Actions |
| logging, monitoring | Serilog, health checks, OpenTelemetry |
| Data Engineering | etap 6 (Databricks) |

## Zasady pracy
- Jeden etap = jedna gałąź + PR do `main` (nawet samej sobie – ćwiczy workflow Git).
- Commity małe, z sensownymi komunikatami.
- Po każdym etapie uzupełnij `PROGRESS.md`: co zrobione, czego się nauczyłam, co było trudne.
- Kod pisz sama; pytania zadawaj o fragmenty, które się zacinają.
