# Transfer Categorizer – ściąga z etapu 1

## Stan
- Zrobione: encja `Transfer` + testy, EF Core + Npgsql, migracja, `POST /transfers` i `GET /transfers/{id}` (POST zwraca `201 Created`, status `Pending`, `category: null`).
- Zostało: test GET i błędu 400, walidacja + ProblemDetails (jutro), mock data, sprzątanie, odhaczenie w `PLAN.md`.

## Codzienny start (PowerShell)
```powershell
podman machine start
podman compose up -d
podman ps                       # postgres ma być Up (healthy)
dotnet ef database update --project src/Transfers.Infrastructure --startup-project src/Transfers.Api
dotnet run --project src/Transfers.Api    # port w logu: "Now listening on"
```
- Przed `dotnet ef`: `$env:ASPNETCORE_ENVIRONMENT = "Development"` (cmd: `set ...`).
- API tu: `http://localhost:5088`. Zatrzymanie: `Ctrl+C`.

## Podman
- `podman machine list|init|start|stop` – maszyna WSL2 (init tylko raz)
- `podman compose up -d | ps | logs -f | stop | start | down`
- `podman compose down -v` – kasuje też dane bazy!
- `podman ps -a` – także zatrzymane; `podman logs --tail 50 <nazwa>`
- Zdrowie: `podman inspect --format '{{.State.Health.Status}}' <nazwa>`, `podman exec <nazwa> pg_isready -U <user> -d <db>`
- `Cannot connect to Podman` → `podman machine start`
- Kontener `Exited` → logi; zajęty port (5432, 5672, 15672) → zmień mapowanie
- Kontener: `transfers-postgres`; `podman exec` przyjmuje nazwę lub ID, nie obraz

## psql
```powershell
podman exec -it transfers-postgres psql -U <user> -d <db>
```
`<user>`, `<db>`: z `POSTGRES_USER` i `POSTGRES_DB` w `docker-compose.yml`.

| Komenda | Co robi |
|---|---|
| `\q` | wyjście (Ctrl+D bywa zawodne na Windowsie) |
| `\conninfo` | jaka baza, user, port |
| `\l`, `\c <db>` | lista baz, zmiana bazy |
| `\dt`, `\d transfers`, `\di` | tabele, struktura, indeksy |
| `\x` | widok pionowy |
| `\timing` | czas zapytań |
| `\pset pager off` | bez pagera (wyjście z pagera: `q`) |

- Prompt `-#` zamiast `=#` → brak `;` albo niezamknięty apostrof; `Ctrl+C` czyści bufor.
- Tabela `transfers` bez cudzysłowu; kolumny PascalCase **z** cudzysłowem: `"CreatedAt"`, `"Status"`. Historia migracji: `SELECT * FROM "__EFMigrationsHistory";`
- Pusta historia albo brak tabeli → `SELECT current_database();` (najczęściej inna baza niż ta z migracji).

```sql
SELECT * FROM transfers ORDER BY "CreatedAt" DESC LIMIT 10;
EXPLAIN ANALYZE SELECT * FROM transfers WHERE "Status" = 'Pending';
```

Oczekiwane kolumny: `Id uuid` (PK), `Amount numeric(18,2)`, `Currency char(3)`, `Description varchar(500)`, `CreatedAt timestamptz`, `Status varchar(20)` (wszystkie NOT NULL), `Category varchar(30)` (NULL). Indeksy: `ix_transfers_status`, `ix_transfers_created_at`, PK.

## Kod endpointów
- `Program.cs`: usługi przed `builder.Build()`, `app.MapTransfers()` po, `app.Run()` ostatnie.
- Rejestracje: `AddInfrastructure(...)` i `builder.Services.AddSingleton(TimeProvider.System);`
- Klucz connection stringa to `Postgres` (`ConnectionStrings:Postgres`).
- `Transfer.Create(amount, currency, description, createdAt)`: w endpoincie `createdAt = clock.GetUtcNow().UtcDateTime` (Npgsql wymaga `Kind=Utc` dla `timestamptz`).
- `ArgumentException` z domeny łapane w endpoincie → `Results.Problem(..., 400)`. To rozwiązanie doraźne.
- Grupa: `MapGroup("/transfers")`, POST jako `MapPost("", ...)`, GET jako `MapGet("/{id:guid}", ...)`.

## Test requestów
- Plik `requests.http` albo `Transfers.Api.http` (VS Code + REST Client lub Visual Studio). Szablon z `weatherforecast` nie przeszkadza, można go zastąpić.
- Format: osobne linie, **pusta linia** przed JSON-em:
```http
POST http://localhost:5088/transfers
Content-Type: application/json

{ "amount": 42.50, "currency": "PLN", "description": "Biedronka zakupy" }

### GET (wklej id z odpowiedzi)
GET http://localhost:5088/transfers/<id>

### Błąd (oczekiwane 400)
POST http://localhost:5088/transfers
Content-Type: application/json

{ "amount": 0, "currency": "PLN", "description": "Test" }
```
- Bez rozszerzeń: `Invoke-RestMethod -Method Post -Uri ... -ContentType "application/json" -Body $body` (`$body` z `ConvertTo-Json`). Nie używaj `curl` w PowerShellu (alias).

## Błędy i naprawy
| Błąd | Przyczyna | Naprawa |
|---|---|---|
| CS0053 | enumy bez `public` | `public enum Category`, `public enum TransferStatus` |
| CS8618 (`Description` null) | brak wartości w konstruktorze | `Create` ustawia pola, prywatny konstruktor EF z `null!` |
| CS1061 (`AddInfrastructure`) | brak `using`, referencji Api → Infrastructure albo `public static` | `using Transfers.Infrastructure;`, referencja, `public static class` |
| brak `EntityFrameworkCore.Design` | pakiet nie w projekcie startowym | `dotnet add src/Transfers.Api package Microsoft.EntityFrameworkCore.Design` |
| brak `DbContextOptions` | `AddInfrastructure` nie wywołane | wywołać przed `builder.Build()` |
| `__EFMigrationsHistory` pusta/brak | migracja niezastosowana lub inna baza | `database update`, `current_database()`, `\dt` |
| `404` na `POST /transfers` (`Server: Kestrel`) | `MapPost("/")` rejestruje `POST /transfers/` z ukośnikiem | `MapPost("")`, restart API; diagnoza: tymczasowa trasa `/routes` |
| `Unable to resolve TimeProvider` | brak rejestracji | `AddSingleton(TimeProvider.System)` |
| `Cannot write DateTime Kind=Local` | lokalny czas do `timestamptz` | zawsze UTC (`GetUtcNow().UtcDateTime`) |
| zmiana kodu bez efektu | stary proces `dotnet run` | `Ctrl+C`, uruchom od nowa; `netstat -ano \| findstr :5088` (PID w ostatniej kolumnie wiersza `LISTENING`) |

Diagnostyka tras (tymczasowo, potem usuń):
```csharp
app.MapGet("/ping", () => "ok");
app.MapGet("/routes", (IEnumerable<EndpointDataSource> s) =>
    s.SelectMany(x => x.Endpoints).Select(e => e.DisplayName));
```


## Pytania i odpowiedzi: C# i ASP.NET Core

### 1. Czym jest `sealed` i dlaczego w Contracts takie deklaracje?
```csharp
public sealed record CreateTransferRequest(decimal Amount, string Currency, string Description);
```
- **`sealed`**: nikt nie może dziedziczyć po tym typie. `class X : CreateTransferRequest` daje błąd CS0509. Rekordy domyślnie można dziedziczyć, więc `sealed` trzeba dopisać świadomie.
- **`record`**: typ na dane, z gotowymi elementami:
  - właściwości tylko do inicjalizacji (niezmienne po utworzeniu),
  - porównywanie po wartościach (`a == b`, gdy wszystkie pola są równe),
  - `with` do kopii z jedną zmianą,
  - czytelny `ToString()`.
- **Zapis pozycyjny** `(decimal Amount, ...)`: jedna linia zamiast klasy z konstruktorem i właściwościami.
- **Dlaczego w Contracts**: DTO i zdarzenia (`TransferCreated`) to czysty kształt danych, bez hierarchii. Dziedziczenie komplikuje serializację i wersjonowanie, a niezmienność chroni przed przypadkową zmianą wiadomości po drodze. Porównanie po wartościach przyda się w testach kontraktowych (Etap 3): `Assert.Equal(expected, actual)` działa bez dodatkowego kodu.
- `sealed` nie jest wymagane, aplikacja zadziała bez niego. To konwencja (wyraża intencję, daje kompilatorowi nieco więcej możliwości optymalizacji i analizatory same je podpowiadają).

### 2. Dlaczego `TransfersEndpoints` jest statyczna i gdzie jest `[HttpGet("{id}")]`?
- Klasa jest `static`, bo zawiera tylko metodę rozszerzającą `MapTransfers`. Nie ma stanu i nie trzeba tworzyć jej instancji. Metody rozszerzające **muszą** być w klasie statycznej.
- Atrybutów `[HttpGet]` i `[Route]` nie ma, bo Minimal API ma trasę i metodę HTTP **w wywołaniu**: `MapGet("/{id:guid}", handler)`.
- Poprawna składnia atrybutu w kontrolerach to `[HttpGet("{id}")]`, z cudzysłowem i bez backslasha.

Ten sam endpoint w starym stylu (kontroler):
```csharp
[ApiController]
[Route("transfers")]
public class TransfersController(TransfersDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var t = await db.Transfers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? NotFound() : Ok(TransferResponse.From(t));
    }
}
// Program.cs: builder.Services.AddControllers(); ... app.MapControllers();
```

| | Kontrolery | Minimal API |
|---|---|---|
| Trasa i metoda | atrybuty `[Route]`, `[HttpGet]` | `MapGet(...)`, `MapPost(...)` |
| Kształt kodu | klasa z instancją, dziedziczy `ControllerBase` | metoda statyczna z lambdą |
| DI | konstruktor | parametry handlera |
| Rejestracja | `AddControllers()` + `MapControllers()` | `app.MapTransfers()` |
| Walidacja | `[ApiController]` robi automatyczne 400 | brak automatu w .NET 8/9, robimy ją sami (jutro) |

Oba style są w pełni wspierane. Minimal API wybrałyśmy, bo daje mniej ceremonii przy małym API.

### 3. Dlaczego `IEndpointRouteBuilder`?
```csharp
public static IEndpointRouteBuilder MapTransfers(this IEndpointRouteBuilder app)
```
- `IEndpointRouteBuilder` to interfejs mówiący „do tego można dodawać endpointy”. `MapGet`, `MapPost` i `MapGroup` to metody rozszerzające właśnie ten interfejs.
- `WebApplication` go implementuje, więc `app.MapTransfers()` działa w `Program.cs`.
- Grupa z `MapGroup` (`RouteGroupBuilder`) też go implementuje. Dzięki temu `MapTransfers` zadziała na `app` i na grupie.
- Zasada: przyjmuj najmniejszy interfejs, który wystarcza, a nie konkretną klasę `WebApplication`.
- `this` robi z metody rozszerzenie (wywołanie `app.MapTransfers()`), a zwracanie `app` pozwala łączyć wywołania w łańcuch.

### 4. Dlaczego `IEndpointRouteBuilder`, a nie „tablica transferów”?
Zakładam, że chodzi o pytanie, czemu endpointy nie operują po prostu na liście przelewów w pamięci. To dwie różne rzeczy:
- `IEndpointRouteBuilder` to **rejestr tras** (tabela „adres + metoda HTTP → kod”). Jego zawartość pokazał Twój `/routes`. To nie są dane.
- Przelewy to **dane**. Mają żyć w PostgreSQL, dostępne przez `DbContext`.
- `static List<Transfer>` w pamięci: znikałaby przy każdym restarcie, nie byłaby bezpieczna wątkowo, nie działałaby przy dwóch instancjach API i niczego byś się przy niej nie nauczyła o EF Core i SQL.
- Metoda `MapTransfers` rejestruje trasy, a handlery biorą dane z bazy.

### 5. Po co `MapGroup`?
```csharp
var group = app.MapGroup("/transfers");
group.MapPost("", ...);            // POST /transfers
group.MapGet("/{id:guid}", ...);   // GET  /transfers/{id}
```
- Wspólny prefiks w jednym miejscu, bez powtarzania `"/transfers"` w każdej trasie (w .NET 6 pisało się `app.MapPost("/transfers", ...)`; `MapGroup` doszło w .NET 7).
- Wspólna konfiguracja raz na całą grupę:
```csharp
var group = app.MapGroup("/transfers")
    .WithTags("Transfers")
    .RequireAuthorization();   // przykład, jeśli kiedyś dojdzie autoryzacja
```
- Prefiks i wzorzec się **sklejają**: `MapPost("/")` daje `/transfers/` (stąd wcześniejszy 404), a `MapPost("")` daje `/transfers`.

### 6. `AsNoTracking()` na przykładach
EF Core domyślnie **śledzi** wczytane encje (change tracker): trzyma ich kopię i przy `SaveChangesAsync` wykrywa zmiany.

```csharp
// ze śledzeniem: do zmiany danych
var t = await db.Transfers.FirstAsync(x => x.Id == id, ct);
t.MarkCategorized(Category.Food);      // przykładowa metoda domenowa
await db.SaveChangesAsync(ct);         // EF wykrył zmianę i zrobił UPDATE

// bez śledzenia: tylko odczyt
var t = await db.Transfers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
t.MarkCategorized(Category.Food);
await db.SaveChangesAsync(ct);         // nic się nie zapisze: EF nie wie o tej encji
```
- Odczyt bez śledzenia jest szybszy i zużywa mniej pamięci. Dlatego w `GET` używamy `AsNoTracking()`.
- W `POST` nie jest potrzebne, bo tam `Add` + `SaveChangesAsync`.
- W Workerze (Etap 2), który zmienia status na `Categorized`, śledzenie jest **potrzebne**.
- Projekcja na DTO (`.Select(x => new TransferResponse(...))`) nie jest śledzona i nie wymaga `AsNoTracking()`.
- Globalnie: `UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` w konfiguracji `DbContext`.
- `AsNoTracking()` stawiasz przed metodą kończącą zapytanie (`FirstOrDefaultAsync`, `ToListAsync`).

### 7. `FirstOrDefaultAsync` na przykładach
Zwraca pierwszy element spełniający warunek albo `null` (dla typów referencyjnych), jeśli nic nie pasuje. EF zamienia lambdę na SQL, np. `... WHERE "Id" = @id LIMIT 1`.

```csharp
// po Id: GET /transfers/{id}
var t = await db.Transfers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
if (t is null) return Results.NotFound();

// najnowszy przelew (bez OrderBy "pierwszy" jest przypadkowy)
var last = await db.Transfers.AsNoTracking()
    .OrderByDescending(x => x.CreatedAt)
    .FirstOrDefaultAsync(ct);
```

| Metoda | Gdy brak wyniku | Gdy więcej niż jeden |
|---|---|---|
| `FirstOrDefaultAsync` | `null` | zwraca pierwszy |
| `FirstAsync` | wyjątek | zwraca pierwszy |
| `SingleOrDefaultAsync` | `null` | wyjątek |
| `FindAsync(id)` | `null` (szuka po kluczu głównym, najpierw w change trackerze) | n/d |

- `Async` + `await`: wątek nie czeka bezczynnie na bazę, tylko wraca do puli. `ct` przerywa zapytanie, gdy klient zrezygnuje.
- Błąd „does not contain a definition for FirstOrDefaultAsync” zwykle oznacza brak `using Microsoft.EntityFrameworkCore;`.
- Uwaga: przy `Select(x => x.Amount).FirstOrDefaultAsync()` brak wyniku daje `0`, a nie `null` (typ wartościowy).

### 8. `AddSingleton(TimeProvider.System)` i kolejność w `Program.cs`
**Po co.** `TimeProvider` to abstrakcja czasu (.NET 8+). `TimeProvider.System` to prawdziwy zegar. Rejestrujesz go w DI, żeby endpoint dostał `TimeProvider clock` w parametrze. W testach podmieniasz go na `FakeTimeProvider` (pakiet `Microsoft.Extensions.TimeProvider.Testing`) ze stałą datą, a z twardym `DateTime.UtcNow` w kodzie testy byłyby niedeterministyczne.

**Dlaczego `Singleton`.** Zegar nie ma stanu, więc jedna instancja na całe życie aplikacji wystarczy. `Scoped` to jedna instancja na żądanie (np. `DbContext`), a `Transient` to nowa przy każdym wstrzyknięciu.

**Kolejność:**
```csharp
using Transfers.Infrastructure;

var builder = WebApplication.CreateBuilder(args);          // 1. builder

builder.Services.AddInfrastructure(                        // 2. rejestracja usług
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured."));
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();                                 // 3. Build: kontener DI zamrożony

// 4. middleware (app.Use...): tu kolejność ma znaczenie, wykonują się po kolei
app.MapTransfers();                                        // 5. endpointy

app.Run();                                                 // 6. start serwera, zawsze ostatni
```
- **Dwie fazy**: przed `Build()` konfigurujesz kontener usług (`builder.Services`), po `Build()` konfigurujesz potok żądań (`app`). Dodanie usługi po `Build()` rzuca `InvalidOperationException` (kolekcja usług jest tylko do odczytu).
- Kolejność samych `AddX` przed `Build()` zwykle nie ma znaczenia (wyjątek: ten sam typ zarejestrowany kilka razy, wygrywa ostatni).
- Middleware wykonuje się w kolejności rejestracji, np. obsługa wyjątków powinna być pierwsza, żeby łapała błędy z kolejnych elementów (jutro dojdzie tu `IExceptionHandler`).
- `Run()` blokuje wątek, więc kod pod nim wykona się dopiero po zamknięciu aplikacji.
- `?? throw ...` przy connection stringu to „fail fast”: brak konfiguracji kończy start jasnym komunikatem, a nie błędem przy pierwszym zapytaniu.
- `AddInfrastructure(...)` to metoda rozszerzająca, która zbiera rejestrację warstwy infrastruktury (`DbContext`, Npgsql) w jednym miejscu, żeby API nie znało szczegółów bazy.

## Do zrobienia
1. [ ] GET `/transfers/{id}`: `200` dla istniejącego, `404` dla nieistniejącego Guida
2. [ ] Test `amount: 0` → `400`
3. [ ] Sprawdzić wiersz w `transfers` i `\d transfers`
4. [ ] Usunąć `/ping` i `/routes` z `Program.cs`
5. [ ] Jutro: walidacja DTO (np. długość `Description` zgodna z kolumną) + globalny `IExceptionHandler` → ProblemDetails, zamiast `try/catch` w endpoincie
6. [ ] Mock data: 10-20 requestów z polskimi opisami (np. „Biedronka zakupy", „Netflix", „Czynsz listopad")
7. [ ] Testy domenowe dla `Create`
8. [ ] W `docs/PLAN.md` zamienić `- [ ] ⚠ **TO FINISH:** ...` na `- [x] ...` po przejściu testów

## Stage 1 – problemy i rozwiązania

| # | Problem | Przyczyna | Rozwiązanie |
|---|---------|-----------|-------------|
| 1 | Seed z tablicą JSON zwracał błąd | Endpoint `POST /transfers` przyjmuje pojedynczy obiekt, a nie tablicę | Osobne żądania rozdzielone `###` w `.http` albo pętla `foreach` w skrypcie (PowerShell / C#) |
| 2 | Test `Create_WithEmptyDescription_Throws` padał dla `"   nnn gvv"` | Po przycięciu to poprawny opis, więc wyjątek nie jest rzucany (literówka w danych testu) | Zmiana danych testowych na `"   "` |
| 3 | Test za długiego opisu padał | `Transfer.Create` nie sprawdzało długości opisu | Stała `Transfer.MaxDescriptionLength` + sprawdzenie w `Create` po `Trim()`; ta sama stała w `HasMaxLength` w konfiguracji EF |
| 4 | `NullReferenceException` dla `description == null` | `.Trim()` wywołane przed sprawdzeniem null | Kolejność: `string.IsNullOrWhiteSpace` → `Trim()` → sprawdzenie długości |
| 5 | `Assert.Throws` nie przechodził dla `ArgumentNullException` | `Throws<T>` wymaga dokładnego typu wyjątku | Użycie `Assert.ThrowsAny<ArgumentException>` |
| 6 | `The name 'MaxDescriptionLength' does not exist` | Stała była tylko przykładem i nie istniała w kodzie | Zdefiniowanie `public const int MaxDescriptionLength` w klasie `Transfer` |

### Wnioski

- Walidacja domenowa żyje w `Transfer.Create`, a endpoint tylko zamienia `ArgumentException` na 400 `ProblemDetails`.
- Limity (np. długość opisu) trzymać w jednej stałej używanej w domenie, konfiguracji EF i testach.
- Przy walidacji tekstu: najpierw null/blank, potem `Trim()`, potem długość.
- `dotnet test` uruchamiamy z katalogu głównego repozytorium (tam, gdzie jest plik `.sln`/`.slnx`), nie z `tests/`.