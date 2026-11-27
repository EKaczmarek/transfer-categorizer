# Stage 2 – Outbox + messaging: podsumowanie i "dlaczego tak"

Dokument do spokojnego przeczytania po pracy. Najpierw architektura i uzasadnienie (część 1-4), potem problemy, które wystąpiły w sesji (część 5), pytania do przemyślenia (część 6) i stan prac (część 7).

---

## 1. Problem, który rozwiązujemy

Chcemy, żeby po `POST /transfers` przelew został **zapisany**, a potem **skategoryzowany** (Biedronka → Groceries). Kategoryzacja ma się dziać **osobno i asynchronicznie**, a nie w trakcie obsługi żądania HTTP.

Dlaczego asynchronicznie?

- API odpowiada szybko (201 Created), niezależnie od tego, jak długo trwa kategoryzacja (dziś słownik słów, w Stage 7 model ML).
- Jeśli kategoryzator się zepsuje, przelewy nadal da się przyjmować.
- Każdą część można skalować i wdrażać osobno.
- To dokładnie wzorzec z ogłoszeń o pracę: event-driven, messaging, niezawodność.

---

## 2. Architektura w pigułce

```
 klient
   │  POST /transfers
   ▼
┌─────────────┐   jedna transakcja    ┌──────────────────────────┐
│ Transfers   │ ────────────────────► │ PostgreSQL               │
│ .Api        │  transfers + outbox   │  • transfers             │
└─────────────┘                       │  • outbox_messages       │
                                      └───────────┬──────────────┘
                                                  │ co 1 s: "daj niewysłane"
                                      ┌───────────▼──────────────┐
                                      │ Transfers.Worker         │
                                      │  1) OutboxDispatcher     │
                                      └───────────┬──────────────┘
                                                  │ publish (z potwierdzeniem)
                                      ┌───────────▼──────────────┐
                                      │ RabbitMQ                 │
                                      │ exchange "transfers"     │
                                      │  ├─ kolejka categorizer  │
                                      │  └─ kolejka audit        │
                                      └───────────┬──────────────┘
                                                  │ dostarcza
                                      ┌───────────▼──────────────┐
                                      │ Transfers.Worker         │
                                      │  2) TransferCreated-     │
                                      │     Consumer             │
                                      │  → kategoryzuje          │
                                      │  → zapisuje status       │
                                      │  → dodaje do outboxa     │
                                      │    "TransferCategorized" │
                                      └──────────────────────────┘
```

Zwróć uwagę: **API nigdy nie rozmawia z RabbitMQ.** Rozmawia tylko z bazą. To jest kluczowa decyzja, wyjaśniona w części 4.

---

## 3. Czym jest RabbitMQ

RabbitMQ to **broker wiadomości**: osobny serwer, który przyjmuje wiadomości od jednych programów i dostarcza je innym. Pomyśl o nim jak o poczcie: nadawca nie musi wiedzieć, kto i kiedy odbierze list, a odbiorca nie musi być włączony w momencie nadania. Poczta przechowuje list, aż ktoś go odbierze.

### Pojęcia, które pojawiły się w kodzie

| Pojęcie | Znaczenie | Analogia |
|---|---|---|
| **Producer / Publisher** | program, który wysyła wiadomość | nadawca listu |
| **Consumer** | program, który odbiera wiadomości | odbiorca |
| **Exchange** (`transfers`) | punkt wejścia; decyduje, do której kolejki trafi wiadomość | sortownia na poczcie |
| **Routing key** (`transfer.created`) | "adres" na wiadomości | kod pocztowy |
| **Binding** | reguła: "wiadomości z kluczem X idą do kolejki Y" | tabela sortownicza |
| **Queue** (kolejka) | miejsce, gdzie wiadomości czekają na odbiór | skrytka pocztowa |
| **Ack** | odbiorca potwierdza: "przetworzyłem, możesz usunąć" | pokwitowanie odbioru |
| **Nack** | "nie udało się" | odesłanie listu |
| **Durable + Persistent** | kolejka i wiadomość przeżywają restart brokera (zapis na dysk) | list nie ginie, gdy poczta ma awarię prądu |
| **Publisher confirm** | broker potwierdza nadawcy: "przyjąłem i zapisałem" | pokwitowanie nadania |

### Dlaczego typ `topic`?

Exchange typu `topic` kieruje wiadomości po wzorcu routing key. Dzięki temu `transfer.created` trafia do kolejki kategoryzatora, a `transfer.categorized` do kolejki audit. Nowych odbiorców można dodawać (np. powiadomienia), **nie zmieniając nadawcy**. To luźne powiązanie (loose coupling).

### Dlaczego nie po prostu wywołać kategoryzatora w kodzie?

Bo wtedy API musiałoby znać kategoryzator, czekać na niego i padać razem z nim. Broker rozdziela te światy. Poza tym na tym samym interfejsie `IMessagePublisher` w Stage 4 podmienisz RabbitMQ na Azure Service Bus bez zmian w logice biznesowej.

---

## 4. Dlaczego Worker i dlaczego tabela `outbox_messages`

### 4.1. Po co Worker (`Transfers.Worker`)

Worker to **osobna aplikacja bez interfejsu HTTP**, która działa w tle (`BackgroundService`). Robi dwie rzeczy:

1. **OutboxDispatcher**: co sekundę zagląda do `outbox_messages`, wysyła niewysłane wiadomości do RabbitMQ.
2. **TransferCreatedConsumer**: odbiera `TransferCreated`, kategoryzuje, zapisuje wynik.

Dlaczego nie w API?

- **Inny cykl życia.** API żyje "od żądania do żądania". Praca w tle ma trwać ciągle.
- **Niezależne skalowanie.** Przy dużym ruchu możesz mieć 2 instancje API i 10 workerów.
- **Izolacja awarii.** Jeśli RabbitMQ padnie, API dalej przyjmuje przelewy (zapisuje do bazy i outboxa). Worker nadrobi później. To bezpośrednia korzyść z tego, że API nie zna brokera.
- **Czytelność.** Kod "obsługa HTTP" i kod "przetwarzanie zdarzeń" nie mieszają się.

### 4.2. Problem "podwójnego zapisu" (dual write)

To jest sedno całego wzorca. Wyobraź sobie naiwną wersję endpointu:

```csharp
db.Transfers.Add(transfer);
await db.SaveChangesAsync();        // (1) zapis do bazy
await broker.PublishAsync(event);   // (2) wysłanie do brokera
```

Są **dwa różne systemy** (baza i broker) i **nie da się** objąć ich jedną transakcją. Co może pójść nie tak:

| Scenariusz | Skutek |
|---|---|
| Aplikacja pada **między (1) a (2)** | przelew jest w bazie, ale nikt się o nim nie dowie. **Zdarzenie zgubione**, przelew na zawsze `Pending` |
| Odwrócona kolejność: najpierw (2), potem (1), a (1) się nie uda | zdarzenie poszło, ale przelewu nie ma w bazie. **Zdarzenie o czymś, co nie istnieje** |
| Broker chwilowo niedostępny | albo błąd dla użytkownika, mimo że przelew zapisano, albo zgubione zdarzenie |

### 4.3. Rozwiązanie: Transactional Outbox

Zamiast wysyłać do brokera w trakcie żądania, **zapisujemy zamiar wysłania do bazy, w tej samej transakcji co przelew**:

```csharp
db.Transfers.Add(transfer);
outbox.Add("transfer.created", event, messageId);   // tylko dodaje wiersz do tego samego DbContext
await db.SaveChangesAsync();                        // JEDNA transakcja: oba wiersze albo żaden
```

Teraz baza gwarantuje atomowość: **albo są oba wiersze (przelew + wiadomość do wysłania), albo żaden.** Nie ma stanu "przelew bez zdarzenia".

Wysyłką do brokera zajmuje się potem dispatcher, **bez pośpiechu i z ponawianiem**. Jeśli RabbitMQ jest wyłączony, wiersze po prostu czekają w tabeli z `ProcessedAt = NULL`. Gdy broker wróci, zostaną wysłane.

Analogia: szuflada "DO WYSŁANIA" na biurku. Kartkę wkładasz do szuflady **razem** z odłożeniem przelewu do teczki (jeden ruch). Goniec zanosi je na pocztę kiedy może. Kartka nie ginie, bo cały czas leży w szufladzie, dopóki goniec nie odnotuje wysyłki.

### 4.4. Co daje outbox, a czego nie

**Daje:** gwarancję, że każde zdarzenie, którego przelew dotyczy, **zostanie wysłane co najmniej raz** (*at-least-once delivery*).

**Nie daje:** gwarancji, że **dokładnie raz**. Przykład: dispatcher wysłał wiadomość, broker ją przyjął, ale proces padł **zanim** zapisał `ProcessedAt`. Po restarcie wyśle ją drugi raz. Odbiorca dostanie duplikat.

Dlatego:
- w consumerze jest prosta osłona `if (transfer.Status == Categorized) return;`,
- w Stage 3 dojdzie tabela przetworzonych `MessageId` (idempotencja): "tę wiadomość już widziałem, pomijam".

To ważne zdanie na rozmowę rekrutacyjną: *"Outbox zamienia problem zgubionych wiadomości w problem duplikatów, a duplikaty rozwiązuje się idempotentnym konsumentem."*

### 4.5. Szczegóły techniczne dispatchera, które mają uzasadnienie

| Element | Po co |
|---|---|
| `FOR UPDATE SKIP LOCKED` | kilka instancji workera może działać naraz; każda bierze **inne** wiersze, bo zablokowane są pomijane (brak podwójnej wysyłki i brak czekania na blokadę) |
| `LIMIT 50` | wysyłka w paczkach, bez ładowania całej tabeli |
| `ORDER BY "OccurredAt"` | zachowanie kolejności zdarzeń (w przybliżeniu) |
| Indeks częściowy `WHERE "ProcessedAt" IS NULL` | dispatcher pyta tylko o niewysłane; indeks zawiera wyłącznie je, więc jest mały i szybki nawet przy milionach wysłanych wierszy |
| `PeriodicTimer` + `try/catch` w pętli | chwilowy błąd brokera nie zabija serwisu, następny tick ponawia |
| Publisher confirms | bez nich nie wiesz, czy broker faktycznie przyjął wiadomość, a wtedy oznaczenie `ProcessedAt` byłoby kłamstwem i outbox traciłby sens |
| Ręczny ack w consumerze | potwierdzamy dopiero **po** zapisie do bazy; gdy proces padnie wcześniej, broker dostarczy wiadomość ponownie |

### 4.6. Dlaczego odpowiedź (`TransferCategorized`) też idzie przez outbox

Consumer w jednej transakcji: zmienia status przelewu **i** dodaje wiersz outboxa `transfer.categorized`. Ten sam problem dual write wystąpiłby, gdyby consumer zapisywał status, a potem publikował wprost. Dzięki temu masz **jeden spójny wzorzec** w całym systemie i nie dublujesz kodu wysyłki.

---

## 5. Problemy napotkane w sesji (i czego uczą)

### 5.1. `AppDbContext could not be found` (CS0246)

- **Przyczyna:** w przykładach użyłem nazwy `AppDbContext`, którą sam wymyśliłem. W Twoim projekcie klasa nazywa się `TransfersDbContext`. To był **mój błąd** w przykładach, nie Twój.
- **Rozwiązanie:** zamienić wszędzie na `TransfersDbContext`.
- **Lekcja:** kopiując przykłady, zawsze dopasuj nazwy do własnego kodu. Błąd kompilacji "type not found" prawie zawsze znaczy: zła nazwa albo brak `using`/referencji projektu.

### 5.2. Gdzie powinien leżeć `OutboxMessage`?

- **Pierwotnie napisałem** "Domain + Infrastructure", co było nieprecyzyjne.
- **Poprawnie:** `OutboxMessage` leży w **`Transfers.Infrastructure`**. Outbox to mechanizm techniczny (niezawodna wysyłka), a nie pojęcie biznesowe, więc nie powinien trafiać do `Domain` (które ma pozostać bez zależności).
- **Jak use case go używa:** przez interfejs `IOutbox` zdefiniowany w `Application`, a zaimplementowany (`EfOutbox`) w `Infrastructure`. To jest **odwrócenie zależności** (Dependency Inversion): wyższa warstwa definiuje, czego potrzebuje, niższa to dostarcza.
- **Lekcja:** reguła zależności w "czystej architekturze" to `Domain ← Application ← Infrastructure/Api`. Strzałki zależności wskazują do środka.

### 5.3. `jsonb` i `timestamptz` "nie istnieją" w C#

- **Przyczyna:** to typy kolumn **PostgreSQL**, a nie typy C#. W encji używasz `string` i `DateTimeOffset`, a EF Core/Npgsql tłumaczy je na kolumny.
- **Konfiguracja:** `HasColumnType("jsonb")` dla `Payload` (domyślnie byłby `text`). `DateTimeOffset` Npgsql sam mapuje na `timestamptz`.
- **Lekcja:** model w C# i schemat w bazie to dwa osobne światy, a EF Core jest tłumaczem między nimi. Zawsze patrz na **wygenerowaną migrację** i `\d tabela` w `psql`, żeby zobaczyć, co faktycznie powstało.

### 5.4. `varchar(100)` zamiast enum dla `Type`

- **Pytanie:** dlaczego nie enum?
- **Odpowiedź:** `Type` służy jako klucz routingu, który wędruje do brokera jako tekst i będzie się rozrastał. Opcje: (a) `varchar` + stałe w kodzie (`OutboxMessageTypes`), (b) C# enum zapisany jako tekst (`HasConversion<string>()`), (c) natywny enum PostgreSQL (odradzany: dodanie wartości to kłopotliwe `ALTER TYPE`).
- **Lekcja:** to kompromis, a nie jedyna poprawna odpowiedź. Unikaj zapisu enuma jako liczby w bazie (nieczytelne `0`/`1` w `SELECT`).

### 5.5. Rejestracja w DI przy dwóch tabelach

- **Pytanie:** "Jak z `AddInfrastructure`, skoro mamy dwie tabele?"
- **Odpowiedź:** **jeden `DbContext` = jedna rejestracja**, niezależnie od liczby `DbSet`. Doszły tylko nowe usługi (`IOutbox`, publisher, opcje).
- **Rozdzielenie na `AddInfrastructure` (baza + outbox) i `AddMessaging` (broker):** API nie potrzebuje brokera, więc działa nawet przy wyłączonym RabbitMQ.
- **Czasy życia (lifetime):** `DbContext` i `IOutbox` są *scoped* (jeden na żądanie), połączenie i publisher RabbitMQ są *singleton* (drogie w tworzeniu). Scoped może zależeć od singletona, **nie odwrotnie**.
- **Lekcja:** `BackgroundService` jest singletonem, więc nie wstrzykniesz do niego `DbContext` wprost. Używasz `IServiceScopeFactory` i tworzysz scope w każdej iteracji.

### 5.6. `dotnet ef migrations add AddOutbox` – jak to ma działać

- Potrzebne: narzędzie `dotnet-ef`, paczka `Microsoft.EntityFrameworkCore.Design` w **projekcie startowym** (Api), migracje w projekcie z `DbContext` (Infrastructure).
- Polecenie:
  ```powershell
  dotnet ef migrations add AddOutbox --project src/Transfers.Infrastructure --startup-project src/Transfers.Api
  ```
- **Lekcja:** `migrations add` tylko **generuje plik**. Zmiany w bazie dzieją się dopiero po `dotnet ef database update`. Projekt startowy jest potrzebny, bo EF musi uruchomić aplikację, żeby poznać konfigurację `DbContext`.

### 5.7. Jak sprawdzić bazę w `psql`

- Kontener musi działać (`podman compose up -d`), potem:
  ```powershell
  podman exec -it <nazwa-kontenera> psql -U transfers -d transfers
  ```
- Przydatne: `\dt`, `\d outbox_messages`, `SELECT * FROM "__EFMigrationsHistory";`.
- **Lekcja:** nazwy z wielką literą trzeba brać w cudzysłów (`"__EFMigrationsHistory"`, `"ProcessedAt"`), inaczej PostgreSQL zamienia je na małe litery i nie znajduje obiektu.

### 5.8. `psql` i "connection string"

- `psql` nie zwraca connection stringa w formacie .NET. `\conninfo` pokazuje bazę i użytkownika **wewnątrz kontenera** (przez socket).
- Connection string dla aplikacji składasz sama:
  `Host=localhost;Port=5432;Database=transfers;Username=transfers;Password=...`
- **Host to `localhost`** (aplikacja działa na komputerze, poza kontenerem), a **port** to lewa strona mapowania `ports:` w `docker-compose.yml`.
- **Lekcja:** "wewnątrz kontenera" i "z komputera" to dwa różne punkty widzenia sieciowego.

### 5.9. DBeaver nie pokazuje tabeli `outbox_messages`

- **Przyczyny, od najczęstszej:** (1) cache nawigatora, więc **F5 / Refresh**, (2) DBeaver połączony z inną bazą (`postgres` zamiast `transfers`), (3) dwie instancje PostgreSQL na jednym porcie (lokalna i kontener).
- **Diagnostyka jednym zapytaniem** w obu narzędziach: `SELECT system_identifier FROM pg_control_system();`. Inny identyfikator oznacza inny serwer.
- **Lekcja:** gdy dwa narzędzia pokazują różne rzeczy, nie zgaduj. Znajdź zapytanie, które jednoznacznie identyfikuje, na co patrzysz.

### 5.10. Endpoint POST: `outbox.Add` musiało być przed `SaveChangesAsync`

- W Twoim kodzie `SaveChangesAsync` było **przed** `messageId`, więc zdarzenie nigdy nie trafiłoby do outboxa.
- **Poprawnie:** `db.Transfers.Add` → `outbox.Add(..., messageId)` → **jedno** `SaveChangesAsync`.
- Jedna transakcja działa tylko dlatego, że `IOutbox` i endpoint dostają **ten sam** `TransfersDbContext` (scoped w obrębie jednego żądania).
- Zwróć uwagę: zdarzenie używa `transfer.Id` **przed** zapisem, więc `Id` musi być generowane w kodzie (`Guid`), a nie przez bazę.

### 5.11. Worker: `Missing connection string 'Default'`

- **Przyczyna:** Worker to **osobna aplikacja z własną konfiguracją**. To, co ustawiłaś w Api (appsettings, User Secrets), nie działa w Workerze.
- **Rozwiązania:** `dotnet user-secrets set ... --project src/Transfers.Worker`, albo `appsettings.Development.json` w Workerze, albo zmienna środowiskowa `ConnectionStrings__Default`.
- **Pułapka środowiska:** `Host.CreateApplicationBuilder` domyślnie używa środowiska `Production`, a User Secrets i `appsettings.Development.json` ładują się tylko w `Development`. Ustaw `$env:DOTNET_ENVIRONMENT="Development"`.
- **Lekcja:** każdy projekt uruchamialny (Api, Worker) ma własne źródła konfiguracji. Błąd typu "brak klucza" to zwykle problem konfiguracji, a nie kodu.

### 5.12. Nazwy kolumn w surowym SQL

- Dispatcher używa `FromSqlRaw`. EF **nie tłumaczy** surowego SQL, więc piszesz nazwy tak, jak są **w bazie** (np. `"ProcessedAt"`), a nie jak właściwości w C#.
- Jeśli włączone jest `UseSnakeCaseNamingConvention()`, kolumny to `processed_at`, a nie `"ProcessedAt"`. Sprawdzasz w `\d outbox_messages`.

---

## 6. Pytania do przemyślenia (sprawdź, czy rozumiesz)

1. Co dokładnie stałoby się z przelewem, gdyby API zapisywało do bazy, a potem publikowało do RabbitMQ, i proces padłby pomiędzy? Jak outbox to rozwiązuje?
2. Dlaczego outbox daje *at-least-once*, a nie *exactly-once*? W którym dokładnie momencie może powstać duplikat?
3. Dlaczego consumer robi `Ack` **po** zapisie do bazy, a nie przed? Co by się stało przy odwrotnej kolejności?
4. Dlaczego API działa, gdy RabbitMQ jest wyłączony? Które elementy architektury za to odpowiadają?
5. Do czego służy `SKIP LOCKED`? Co by się stało z dwiema instancjami workera bez niego?
6. Dlaczego `IOutbox` jest zdefiniowany w `Application`, a zaimplementowany w `Infrastructure`? Co by się zepsuło, gdyby `Application` znało `DbContext`?
7. Dlaczego `DbContext` jest *scoped*, a połączenie RabbitMQ *singleton*? Dlaczego `BackgroundService` potrzebuje `IServiceScopeFactory`?
8. Co zyskujesz na tym, że `IMessagePublisher` przyjmuje gotowy `string` JSON, a nie typowany obiekt?
9. Jaki jest koszt wzorca outbox? (Podpowiedź: dodatkowa tabela, opóźnienie do 1 s, dodatkowy proces, duplikaty.) Kiedy **nie** warto go stosować?
10. Co jeszcze brakuje do "produkcyjnej" niezawodności? (Podpowiedź: Stage 3: retry, dead-letter queue, idempotencja.)

---

## 7. Stan prac i co dalej

### Zrobione

- [x] Kontrakty zdarzeń (`TransferCreated`, `TransferCategorized`)
- [x] Tabela `outbox_messages` (migracja, indeks częściowy), widoczna w `psql`
- [x] `DbContext` rozszerzony o `DbSet<OutboxMessage>`, konfiguracja encji
- [x] Rejestracja w DI (`AddInfrastructure`, `AddMessaging`)
- [x] Endpoint `POST /transfers` zapisuje przelew i outbox w jednej transakcji
- [~] Adapter RabbitMQ (kod rozpisany; sprawdź, czy się kompiluje)
- [~] Worker: `Program.cs` i konfiguracja (ostatni problem: connection string)

### Do zrobienia

- [x] Uruchomić Workera bez błędów, sprawdzić, że `ProcessedAt` się wypełnia, a w kolejce `transfers.categorizer` jest wiadomość
- [x] `ITransferCategorizer` + `RuleBasedCategorizer` + `Transfer.MarkCategorized`
- [ ] `ITransferCategorizer` + `RuleBasedCategorizer` + testy jednostkowe + `Transfer.MarkCategorized`
- [ ] `TransferCreatedConsumer`
- [ ] Test "Done when": POST → po c hwili GET pokazuje `Categorized`

### Czego nie robić teraz

Retry, dead-letter queue, idempotencja i testy integracyjne to **Stage 3**. Reguła z planu: wąskie MVP najpierw.

---

## 8. Słowniczek

| Termin | Znaczenie |
|---|---|
| **Broker** | serwer pośredniczący w przekazywaniu wiadomości (RabbitMQ, Azure Service Bus, Kafka) |
| **Event / zdarzenie** | fakt, który się wydarzył ("przelew utworzony"), niezmienny |
| **Event-driven** | styl architektury, w którym części systemu komunikują się zdarzeniami |
| **Outbox** | tabela w bazie, w której zapisujesz wiadomości do wysłania razem z danymi biznesowymi |
| **Dual write** | próba zapisu do dwóch systemów (baza + broker) bez wspólnej transakcji; źródło niespójności |
| **At-least-once** | wiadomość dotrze co najmniej raz (możliwe duplikaty) |
| **Idempotencja** | wielokrotne przetworzenie tej samej wiadomości daje taki sam efekt jak jednokrotne |
| **Dead-letter queue (DLQ)** | kolejka na wiadomości, których nie udało się przetworzyć po kilku próbach |
| **Ack / Nack** | potwierdzenie / odrzucenie wiadomości przez konsumenta |
| **Scoped / Singleton** | czas życia obiektu w DI: jeden na żądanie / jeden na całą aplikację |
| **Dependency Inversion** | wyższa warstwa definiuje interfejs, niższa go implementuje |
| **BackgroundService** | klasa .NET uruchamiana w tle przez hosta, działająca przez cały czas życia aplikacji |
| **Partial index** | indeks obejmujący tylko wiersze spełniające warunek (`WHERE ...`) |

---

## 9. Ściągawka komend (PowerShell)

```powershell
# infrastruktura
podman compose up -d
podman ps

# migracje
dotnet ef migrations add NazwaMigracji --project src/Transfers.Infrastructure --startup-project src/Transfers.Api
dotnet ef database update               --project src/Transfers.Infrastructure --startup-project src/Transfers.Api
dotnet ef migrations remove             --project src/Transfers.Infrastructure --startup-project src/Transfers.Api

# psql w kontenerze
podman exec -it <nazwa-kontenera> psql -U transfers -d transfers
#   \dt   \d outbox_messages   SELECT * FROM "__EFMigrationsHistory";

# uruchomienie aplikacji (dwa terminale)
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project src/Transfers.Api
dotnet run --project src/Transfers.Worker

# User Secrets dla Workera
dotnet user-secrets init --project src/Transfers.Worker
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=transfers;Username=transfers;Password=..." --project src/Transfers.Worker

# test POST
curl.exe -i -X POST http://localhost:5000/transfers -H "Content-Type: application/json" -d "{\"amount\":49.99,\"currency\":\"PLN\",\"description\":\"Biedronka zakupy\"}"
```

UI RabbitMQ: <http://localhost:15672> (guest / guest)
