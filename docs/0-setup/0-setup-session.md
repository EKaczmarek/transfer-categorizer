# Transfer Categorizer – stage 0: detailed notes / szczegółowe notatki

Date / Data: 2026-11-16
Order = order in which the problems appeared / Kolejność = kolejność, w jakiej pojawiały się problemy.

- [🇵🇱 Wersja polska](#-wersja-polska)
- [🇬🇧 English version](#-english-version)

---

# 🇵🇱 Wersja polska

## 1. Jakie projekty w solucji

| Projekt | Typ | Komenda |
|---|---|---|
| `Transfers.Contracts`, `Domain`, `Application`, `Infrastructure` | Class Library | `dotnet new classlib -o src/<Nazwa>` |
| `Transfers.Api` | ASP.NET Core Web API | `dotnet new webapi -o src/Transfers.Api` |
| `Transfers.Worker` | Worker Service (`BackgroundService`) | `dotnet new worker -o src/Transfers.Worker` |
| 3 × `*Tests` | xUnit | `dotnet new xunit -o tests/<Nazwa>` |

Referencje (zależności idą do środka):
- Domain, Contracts: brak referencji
- Application: Domain, Contracts
- Infrastructure: Application
- Api, Worker: Application, Infrastructure, Contracts (composition root, miejsce rejestracji DI)
- UnitTests: Domain, Application; IntegrationTests: Api, Worker, Infrastructure; ContractTests: Contracts

```bash
dotnet add src/Transfers.Application reference src/Transfers.Domain src/Transfers.Contracts
```
Dodaje do projektu Application referencje do projektów Domain i Contracts (kod z Domain i Contracts będzie w nim widoczny).

## 2. Po co `.editorconfig`

Plik w katalogu głównym repo, który ustawia wspólne reguły stylu kodu niezależnie od edytora (Visual Studio, Rider, VS Code): wcięcia, końce linii, kodowanie, nazewnictwo (np. `_camelCase` dla pól prywatnych), file-scoped namespaces, poziomy ostrzeżeń analizatora. Dzięki niemu diffy w PR nie mają szumu po formatowaniu, a w CI można wymusić styl:

```bash
dotnet new editorconfig            # generuje domyślny .editorconfig
dotnet format --verify-no-changes  # sprawdza, czy kod jest sformatowany wg .editorconfig (nic nie zmienia, zwraca błąd przy różnicach)
```

## 3. Jak ma wyglądać docker-compose.yml

PostgreSQL 16 + RabbitMQ z panelem zarządzania, w katalogu głównym repo:

```yaml
services:
  postgres:
    image: docker.io/library/postgres:16
    container_name: transfers-postgres
    environment:
      POSTGRES_USER: transfers
      POSTGRES_PASSWORD: transfers
      POSTGRES_DB: transfers
    ports: ["5432:5432"]
    volumes: [postgres-data:/var/lib/postgresql/data]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U transfers -d transfers"]
      interval: 5s
      timeout: 3s
      retries: 10

  rabbitmq:
    image: docker.io/library/rabbitmq:3-management
    container_name: transfers-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    ports:
      - "5672:5672"      # AMQP
      - "15672:15672"    # panel http://localhost:15672
    volumes: [rabbitmq-data:/var/lib/rabbitmq]
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 10s
      timeout: 5s
      retries: 10

volumes:
  postgres-data:
  rabbitmq-data:
```

- `image` z pełną nazwą `docker.io/library/...`: Podman nie zakłada domyślnie Docker Hub.
- `ports`: mapowanie `port-na-komputerze:port-w-kontenerze`.
- `volumes`: dane przeżywają restart kontenera.
- `healthcheck`: kontener dostaje status `(healthy)`, gdy usługa naprawdę odpowiada (przyda się później w `depends_on: condition: service_healthy`).
- Hasła tylko do lokalnego developmentu.
- Connection string: `Host=localhost;Port=5432;Database=transfers;Username=transfers;Password=transfers`.

## 4. Kolejność etapu 0

1. **Repo i pliki bazowe**
   ```bash
   mkdir transfer-categorizer && cd transfer-categorizer
   git init -b main                  # nowe repo, gałąź główna main
   dotnet new gitignore              # .gitignore dla .NET
   dotnet new editorconfig           # .editorconfig
   git checkout -b stage-0-setup     # nowa gałąź na ten etap i przełączenie się na nią
   ```
   Dodaj `README.md`, `docs/PLAN.md`, `docs/PROGRESS.md`, puste repo na GitHubie jako `origin`, pierwszy commit na `main`.
2. **Solucja i projekty**: `dotnet new sln -n TransferCategorizer` (tworzy plik solucji), potem `dotnet new ... -o ...` dla 9 projektów (patrz pkt 1). Usuń `Class1.cs`, `UnitTest1.cs`, `WeatherForecast`. Wspólny `TargetFramework` najlepiej ustawić raz w `Directory.Build.props`.
3. **Dodanie projektów do solucji** (patrz problem 3 niżej).
4. **Referencje** (`dotnet add ... reference ...`, pkt 1).
5. **`dotnet build`** (kompiluje całą solucję) i **`dotnet test`** (uruchamia testy; bez testów kończy się sukcesem), commit.
6. **docker-compose** (patrz problemy 5–11), commit.
7. **`.github/workflows/ci.yml`** (patrz niżej), commit, push.
8. **Weryfikacja na świeżym klonie** i PR (patrz niżej).
9. **Aktualizacja `PROGRESS.md`**: status etapu 0, daty, dziennik, czego się nauczyłam.

## Problemy i rozwiązania, w kolejności

### Problem 1: co znaczy `-o` w `dotnet new classlib -o src/Transfers.Contracts`

`-o` = `--output`: katalog, w którym powstanie projekt. Tworzy `src/Transfers.Contracts` (razem z `src/`), wrzuca tam `.csproj` i `Class1.cs`. Jeśli nie podasz `-n` (`--name`), nazwa projektu (plik `.csproj`, przestrzeń nazw) pochodzi z ostatniego członu ścieżki. Bez `-o` projekt powstałby w bieżącym katalogu.

### Problem 2: `dotnet sln add (ls -r src/**/*.csproj tests/**/*.csproj)`

Co robi (w PowerShellu): `ls -r ...` (`Get-ChildItem -Recurse`) znajduje wszystkie pliki `.csproj` w `src` i `tests`, nawiasy `( )` wykonują to najpierw, a wynik trafia jako argumenty do `dotnet sln add`, który dodaje wszystkie 9 projektów do solucji jednym poleceniem.

**Błąd w cmd.exe:**
```
Nie można odnaleźć projektu ani katalogu „(ls”.
Nie można odnaleźć projektu ani katalogu „-r”.
```
Przyczyna: ta składnia działa tylko w PowerShellu. cmd przekazuje `(ls`, `-r` itd. jako dosłowne argumenty. Prompt PowerShella zaczyna się od `PS C:\...>`, cmd pokazuje tylko `C:\...>`.

**Rozwiązania:**
```powershell
# PowerShell
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj src, tests).FullName
```
```bat
:: cmd: pętla po plikach, po jednym dotnet sln add na projekt
for /r src %f in (*.csproj) do dotnet sln add "%f"
for /r tests %f in (*.csproj) do dotnet sln add "%f"

:: cmd lub dowolna powłoka: po katalogach projektów
dotnet sln add src\Transfers.Contracts src\Transfers.Domain src\Transfers.Application src\Transfers.Infrastructure src\Transfers.Api src\Transfers.Worker
dotnet sln add tests\Transfers.UnitTests tests\Transfers.IntegrationTests tests\Transfers.ContractTests
```
```bash
# bash
dotnet sln add $(find src tests -name "*.csproj")
```
Weryfikacja: `dotnet sln list` (wypisuje projekty w solucji; powinno być 9). W pliku `.bat` użyj `%%f` zamiast `%f`.

### Problem 3: Podman zamiast Dockera

Plik `docker-compose.yml` zostaje, Podman czyta ten sam format. Komendy: `podman compose up -d`, `podman compose ps`, `podman ps`. Na Windowsie Podman działa w maszynie wirtualnej:
```bash
podman machine init     # tworzy maszynę (tylko za pierwszym razem)
podman machine start    # uruchamia maszynę
podman machine list     # pokazuje maszyny i ich stan
```
Używaj pełnych nazw obrazów (`docker.io/library/...`). Testcontainers w etapie 3 z Podmanem wymagają osobnej konfiguracji (socket API, `DOCKER_HOST`, często `TESTCONTAINERS_RYUK_DISABLED=true`), CI na GitHub Actions ma Dockera i tam nic nie trzeba zmieniać.

### Problem 4: `'podman' is not recognized as an internal or external command`

Podman Desktop to tylko GUI, polecenie `podman` (CLI) to osobny program. Przyczyny: terminal otwarty przed instalacją (PATH się nie odświeżył, otwórz nowy), silnik Podmana nie jest zainstalowany (zainstaluj przez onboarding w Podman Desktop albo instalatorem z podman.io / GitHub `containers/podman`), albo jest zainstalowany, ale nie ma go w PATH.
```bat
podman --version                                       :: czy CLI działa
dir "C:\Program Files\RedHat\Podman\podman.exe"        :: czy plik istnieje w domyślnej lokalizacji
```
`dir` zwróciło `Cannot find path ... because it does not exist`, czyli silnika nie było w domyślnym miejscu. Szukanie w PowerShellu:
```powershell
Get-Command podman -ErrorAction SilentlyContinue       # gdzie PATH widzi podmana
Get-ChildItem "C:\Program Files", "$env:LOCALAPPDATA\Programs" -Filter podman.exe -Recurse -ErrorAction SilentlyContinue | Select-Object FullName
```

### Problem 5: `'winget' is not recognized`

`winget` (Windows Package Manager, część „App Installer”) nie był dostępny w tym terminalu. Nie jest potrzebny: silnik Podmana instaluje się przez Podman Desktop albo ręcznym instalatorem. Na marginesie: `winget search podman` szuka pakietu, `winget install <ID>` go instaluje.

### Problem 6: `dism.exe` nie jest rozpoznany (WSL2)

Podman na Windowsie potrzebuje WSL2. `dism.exe` leży w `C:\Windows\System32`, więc jego brak w PATH, razem z brakiem `winget`, wskazywał na uszkodzony PATH w tamtym terminalu.
```powershell
$env:Path -split ';'                    # wypisuje PATH linia po linii; powinny być C:\Windows\system32 i C:\Windows
C:\Windows\System32\dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
C:\Windows\System32\dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
C:\Windows\System32\wsl.exe --install   # jedna komenda zamiast dwóch powyższych
```
`dism ... /enable-feature` włącza funkcje Windows (podsystem Linuxa, platforma maszyn wirtualnych), wymaga terminala **jako administrator** i restartu Windows. Pełna ścieżka omija problem z PATH. Najlepiej otworzyć nowy terminal z menu Start (PowerShell lub Windows Terminal). Zadziałało.

### Problem 7: `Error: looking up compose provider failed`

```
exec: "docker-compose": executable file not found in %PATH%
exec: "podman-compose": executable file not found in %PATH%
```
`podman compose` sam niczego nie robi, tylko przekazuje polecenie do providera. Trzeba zainstalować jeden z nich:
```bat
pip install podman-compose        :: provider w Pythonie (alternatywy: python -m pip ..., py -m pip ...)
docker-compose --version          :: sprawdzenie, jeśli użyłaś binarki docker-compose.exe
podman-compose --version
```
Drugi sposób: pobrać `docker-compose-windows-x86_64.exe` z Releases repo `docker/compose` na GitHubie, zmienić nazwę na `docker-compose.exe` i dodać folder do PATH (bez Docker Desktop). Podman Desktop też ma opcję instalacji Compose w ustawieniach.

### Problem 8: jak dodać folder do PATH

GUI: Start → „Edytuj zmienne środowiskowe dla konta” → **Path** → **Edit** → **New** → wklej **folder** (nie ścieżkę do `.exe`) → OK → zamknij wszystkie terminale i otwórz nowy.
```powershell
# na stałe (konto użytkownika)
$old = [Environment]::GetEnvironmentVariable("Path", "User")
[Environment]::SetEnvironmentVariable("Path", "$old;C:\Tools", "User")
# tylko dla bieżącej sesji
$env:Path += ";C:\Tools"
```
Nie usuwaj istniejących wpisów, dodawaj tylko nowe.

### Problem 9: co znaczy `podman compose up -d`

- `podman`: program do uruchamiania kontenerów (odpowiednik `docker`)
- `compose`: czyta `docker-compose.yml` i zarządza wieloma kontenerami naraz (przez providera)
- `up`: tworzy i uruchamia wszystko z pliku (pobiera obrazy, tworzy sieć i wolumeny, startuje kontenery)
- `-d`: *detached*, w tle; bez tego logi lecą w terminalu i Ctrl+C zatrzymuje kontenery

```bash
podman compose ps            # kontenery z pliku
podman compose logs -f       # logi na żywo (Ctrl+C wychodzi z podglądu)
podman compose stop          # zatrzymuje kontenery, dane zostają
podman compose down          # zatrzymuje i usuwa kontenery
podman compose down -v       # j.w. + usuwa wolumeny (czyste środowisko od zera)
```

### Problem 10: po `podman ps` widać tylko `transfers-postgres`

```bash
podman ps -a                       # też zatrzymane kontenery (Exited / Created)
podman logs transfers-rabbitmq     # logi kontenera, tu widać przyczynę
podman compose up                  # bez -d, błędy wypisują się w terminalu
```
Możliwe przyczyny: krótka nazwa obrazu (użyj `docker.io/library/rabbitmq:3-management`), zajęty lub zarezerwowany port 5672/15672 (`netstat -ano | findstr "5672 15672"`, `netsh interface ipv4 show excludedportrange protocol=tcp`; można zmienić mapowanie na np. `"5673:5672"`), uszkodzony wolumen (`podman compose down -v` i `up -d`), albo RabbitMQ po prostu startuje wolniej (`(starting)` w statusie). Po poprawkach oba kontenery się pojawiły.

Weryfikacja:
```bash
podman ps                                                        # STATUS: Up ... (healthy)
podman exec -it transfers-postgres psql -U transfers -d transfers  # wejście do psql w kontenerze; \l = lista baz, \q = wyjście
```
Panel RabbitMQ: `http://localhost:15672`, login `guest` / `guest`.

### Problem 11: workflow CI `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:          # ręczny start z zakładki Actions

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x     # musi pasować do TargetFramework w .csproj
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release
```
- `on`: kiedy workflow się uruchamia (push do `main`, każdy PR, ręcznie)
- `checkout`: pobiera kod repo; `setup-dotnet`: instaluje .NET SDK
- `restore` / `build` / `test`: przywraca pakiety, kompiluje (Release), uruchamia testy bez ponownej kompilacji
- Puste projekty testowe nie zatrzymają CI

### Problem 12: weryfikacja na świeżym klonie i `cd $env:TEMP` w cmd

`$env:TEMP` to składnia PowerShella, w cmd daje `System nie może odnaleźć określonej ścieżki.`
```bat
:: cmd
cd /d %TEMP%                              :: /d zmienia też dysk
git clone <adres-repo> check-clone
cd check-clone
git checkout stage-0-setup
dotnet build
podman compose up -d
podman ps
podman compose down -v
cd ..
rmdir /s /q check-clone                   :: usuwa folder z zawartością, bez pytania
```
Przed klonem: `git push -u origin stage-0-setup` (inaczej klon nie zobaczy gałęzi) i `podman compose down -v` w starym folderze (stałe nazwy kontenerów i porty kolidują).

### Problem 13: `Remove-Item -Recurse -Force check-clone` w cmd

To cmdlet PowerShella, w cmd nie działa. Odpowiednik: `rmdir /s /q check-clone` (`/s` z zawartością, `/q` bez pytania). Uruchamiać z katalogu nadrzędnego. Jeśli „plik jest używany”: zamknij edytory, Eksplorer i terminale w tym folderze.

| Cel | cmd | PowerShell |
|---|---|---|
| Usuń folder z zawartością | `rmdir /s /q nazwa` | `Remove-Item -Recurse -Force nazwa` |
| Usuń plik | `del nazwa` | `Remove-Item nazwa` |
| Zmienna TEMP | `%TEMP%` | `$env:TEMP` |
| Zmień katalog (z dyskiem) | `cd /d ścieżka` | `cd ścieżka` |

### Problem 14: pusta zakładka Actions na GitHubie

Pusta zakładka zwykle znaczy, że workflow w ogóle się nie uruchomił. Sprawdź kolejno:
1. Ścieżka musi być dokładnie `.github/workflows/ci.yml` (kropka, `workflows` z „s”, rozszerzenie `.yml`, nie `.yml.txt`).
   ```bat
   dir /s /b .github              :: pliki fizycznie na dysku
   git ls-files .github           :: pliki śledzone przez git; powinno pokazać .github/workflows/ci.yml
   ```
2. Plik jest zacommitowany i wypchnięty (`git status`, `git log --oneline -5`, `git push`), widać go na GitHubie na gałęzi `stage-0-setup`.
3. Wyzwalacze: PR uruchamia workflow tylko, jeśli plik jest w gałęzi PR; można dodać `workflow_dispatch` i kliknąć **Run workflow**.
4. Settings → Actions → General: Actions muszą być dozwolone.
5. Błąd składni YAML: wcięcia spacjami, nigdy tabulatorami.
6. Jeśli workflow się uruchomi, ale się wywali: najczęściej `dotnet-version` niezgodny z `TargetFramework`.

### Problem 15: PR i dokumenty

- Tytuł PR: `Stage 0: repo, solution, docker-compose and CI setup`. Opis: co zrobiono, jak zweryfikowano, kryterium „gotowe”. Pozycje „CI zielone” i „weryfikacja na świeżym klonie” wpisuj dopiero po sprawdzeniu.
- `PROGRESS.md` i `PLAN.md` przetłumaczone na angielski, etap 0 oznaczony ✅ (dopiero po zielonym CI i merge), wpisy w dzienniku, „co się nauczyłam” i „problemy i rozwiązania”.
- Po merge:
  ```bash
  git checkout main     # przełącz się na main
  git pull              # pobierz zmergowane zmiany
  ```

### Problem 16: zmiana nazwy gałęzi

```bash
git branch -m old-name new-name           # zmiana nazwy lokalnie
git push origin -u new-name               # wypchnięcie nowej nazwy i ustawienie upstream
git push origin --delete old-name         # usunięcie starej nazwy na GitHubie
git branch -vv                            # sprawdzenie gałęzi i ich upstream
```
Uwaga: usunięcie starej gałęzi zdalnej zamyka otwarty PR. Żeby go zachować, zmień nazwę w UI GitHuba (repo → Branches → ołówek), a lokalnie:
```bash
git branch -m old-name new-name
git fetch origin
git branch -u origin/new-name new-name
```

## Następny krok

Etap 1: encja `Transfer`, EF Core + Npgsql, pierwsza migracja, `POST /transfers` i `GET /transfers/{id}`.

---

# 🇬🇧 English version

## 1. Project types in the solution

| Project | Type | Command |
|---|---|---|
| `Transfers.Contracts`, `Domain`, `Application`, `Infrastructure` | Class Library | `dotnet new classlib -o src/<Name>` |
| `Transfers.Api` | ASP.NET Core Web API | `dotnet new webapi -o src/Transfers.Api` |
| `Transfers.Worker` | Worker Service (`BackgroundService`) | `dotnet new worker -o src/Transfers.Worker` |
| 3 × `*Tests` | xUnit | `dotnet new xunit -o tests/<Name>` |

References (dependencies point inwards):
- Domain, Contracts: no references
- Application: Domain, Contracts
- Infrastructure: Application
- Api, Worker: Application, Infrastructure, Contracts (composition root, where DI is registered)
- UnitTests: Domain, Application; IntegrationTests: Api, Worker, Infrastructure; ContractTests: Contracts

```bash
dotnet add src/Transfers.Application reference src/Transfers.Domain src/Transfers.Contracts
```
Adds references from Application to Domain and Contracts (their code becomes visible in Application).

## 2. What `.editorconfig` is for

A file in the repo root that sets shared code style rules regardless of editor (Visual Studio, Rider, VS Code): indentation, line endings, encoding, naming (e.g. `_camelCase` for private fields), file-scoped namespaces, analyzer severities. PR diffs have no formatting noise, and CI can enforce the style:

```bash
dotnet new editorconfig            # generates a default .editorconfig
dotnet format --verify-no-changes  # checks that code matches .editorconfig (changes nothing, fails on differences)
```

## 3. What docker-compose.yml should look like

PostgreSQL 16 + RabbitMQ with the management UI, in the repo root (the same file as in the Polish section above).

- `image` with the fully qualified name `docker.io/library/...`: Podman does not assume Docker Hub by default.
- `ports`: `host-port:container-port` mapping.
- `volumes`: data survives container restarts.
- `healthcheck`: the container gets a `(healthy)` status when the service really responds (useful later with `depends_on: condition: service_healthy`).
- Passwords are for local development only.
- Connection string: `Host=localhost;Port=5432;Database=transfers;Username=transfers;Password=transfers`.

## 4. Stage 0 order

1. **Repo and base files**
   ```bash
   mkdir transfer-categorizer && cd transfer-categorizer
   git init -b main                  # new repo, main branch
   dotnet new gitignore              # .gitignore for .NET
   dotnet new editorconfig           # .editorconfig
   git checkout -b stage-0-setup     # create and switch to the stage branch
   ```
   Add `README.md`, `docs/PLAN.md`, `docs/PROGRESS.md`, an empty GitHub repo as `origin`, a first commit on `main`.
2. **Solution and projects**: `dotnet new sln -n TransferCategorizer` (creates the solution file), then `dotnet new ... -o ...` for 9 projects (see section 1). Delete `Class1.cs`, `UnitTest1.cs`, `WeatherForecast`. Set a shared `TargetFramework` once in `Directory.Build.props`.
3. **Add projects to the solution** (see problem 2 below).
4. **References** (`dotnet add ... reference ...`, section 1).
5. **`dotnet build`** (compiles the whole solution) and **`dotnet test`** (runs tests; succeeds with no tests), commit.
6. **docker-compose** (see problems 3–10), commit.
7. **`.github/workflows/ci.yml`** (see below), commit, push.
8. **Fresh-clone verification** and PR (see below).
9. **Update `PROGRESS.md`**: stage 0 status, dates, work log, what I learned.

## Problems and solutions, in order

### Problem 1: what `-o` means in `dotnet new classlib -o src/Transfers.Contracts`

`-o` = `--output`: the folder where the project is created. It creates `src/Transfers.Contracts` (including `src/`) and puts the `.csproj` and `Class1.cs` there. Without `-n` (`--name`), the project name (the `.csproj` file, the namespace) comes from the last path segment. Without `-o` the project would be created in the current directory.

### Problem 2: `dotnet sln add (ls -r src/**/*.csproj tests/**/*.csproj)`

What it does (in PowerShell): `ls -r ...` (`Get-ChildItem -Recurse`) finds every `.csproj` file in `src` and `tests`, the parentheses `( )` run it first, and the result is passed as arguments to `dotnet sln add`, which adds all 9 projects to the solution in one command.

**Error in cmd.exe:**
```
Nie można odnaleźć projektu ani katalogu „(ls”.
Nie można odnaleźć projektu ani katalogu „-r”.
```
Cause: this syntax only works in PowerShell. cmd passes `(ls`, `-r` etc. as literal arguments. A PowerShell prompt starts with `PS C:\...>`, cmd shows just `C:\...>`.

**Solutions:**
```powershell
# PowerShell
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj src, tests).FullName
```
```bat
:: cmd: loop over files, one dotnet sln add per project
for /r src %f in (*.csproj) do dotnet sln add "%f"
for /r tests %f in (*.csproj) do dotnet sln add "%f"

:: cmd or any shell: by project folders
dotnet sln add src\Transfers.Contracts src\Transfers.Domain src\Transfers.Application src\Transfers.Infrastructure src\Transfers.Api src\Transfers.Worker
dotnet sln add tests\Transfers.UnitTests tests\Transfers.IntegrationTests tests\Transfers.ContractTests
```
```bash
# bash
dotnet sln add $(find src tests -name "*.csproj")
```
Verify with `dotnet sln list` (lists projects in the solution; should be 9). In a `.bat` file use `%%f` instead of `%f`.

### Problem 3: Podman instead of Docker

`docker-compose.yml` stays as is, Podman reads the same format. Commands: `podman compose up -d`, `podman compose ps`, `podman ps`. On Windows Podman runs in a virtual machine:
```bash
podman machine init     # creates the machine (first time only)
podman machine start    # starts the machine
podman machine list     # shows machines and their state
```
Use fully qualified image names (`docker.io/library/...`). Testcontainers in stage 3 need extra setup with Podman (socket API, `DOCKER_HOST`, often `TESTCONTAINERS_RYUK_DISABLED=true`); CI on GitHub Actions has Docker, so nothing changes there.

### Problem 4: `'podman' is not recognized as an internal or external command`

Podman Desktop is only the GUI; the `podman` command (CLI) is a separate program. Causes: the terminal was opened before installation (PATH not refreshed, open a new one), the Podman engine is not installed (install it through the Podman Desktop onboarding or the installer from podman.io / GitHub `containers/podman`), or it is installed but not on PATH.
```bat
podman --version                                       :: does the CLI work
dir "C:\Program Files\RedHat\Podman\podman.exe"        :: does the file exist in the default location
```
`dir` returned `Cannot find path ... because it does not exist`, so the engine was not in the default place. Searching in PowerShell:
```powershell
Get-Command podman -ErrorAction SilentlyContinue       # where PATH finds podman
Get-ChildItem "C:\Program Files", "$env:LOCALAPPDATA\Programs" -Filter podman.exe -Recurse -ErrorAction SilentlyContinue | Select-Object FullName
```

### Problem 5: `'winget' is not recognized`

`winget` (Windows Package Manager, part of "App Installer") was not available in this terminal. It is not required: the Podman engine installs through Podman Desktop or the manual installer. For reference: `winget search podman` looks for a package, `winget install <ID>` installs it.

### Problem 6: `dism.exe` not recognized (WSL2)

Podman on Windows needs WSL2. `dism.exe` lives in `C:\Windows\System32`, so its absence from PATH, together with the missing `winget`, pointed to a broken PATH in that terminal.
```powershell
$env:Path -split ';'                    # prints PATH line by line; should contain C:\Windows\system32 and C:\Windows
C:\Windows\System32\dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
C:\Windows\System32\dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
C:\Windows\System32\wsl.exe --install   # one command instead of the two above
```
`dism ... /enable-feature` turns on Windows features (Linux subsystem, virtual machine platform); it needs a terminal **as administrator** and a Windows restart. The full path bypasses the PATH problem. Best to open a fresh terminal from the Start menu (PowerShell or Windows Terminal). It worked.

### Problem 7: `Error: looking up compose provider failed`

```
exec: "docker-compose": executable file not found in %PATH%
exec: "podman-compose": executable file not found in %PATH%
```
`podman compose` does nothing by itself, it hands the command to a provider. Install one:
```bat
pip install podman-compose        :: Python-based provider (alternatives: python -m pip ..., py -m pip ...)
docker-compose --version          :: check, if you used the docker-compose.exe binary
podman-compose --version
```
The other way: download `docker-compose-windows-x86_64.exe` from the Releases page of the `docker/compose` repo on GitHub, rename it to `docker-compose.exe` and add its folder to PATH (no Docker Desktop needed). Podman Desktop also has a Compose install option in its settings.

### Problem 8: how to add a folder to PATH

GUI: Start → "Edit environment variables for your account" → **Path** → **Edit** → **New** → paste the **folder** (not the path to the `.exe`) → OK → close all terminals and open a new one.
```powershell
# permanently (user account)
$old = [Environment]::GetEnvironmentVariable("Path", "User")
[Environment]::SetEnvironmentVariable("Path", "$old;C:\Tools", "User")
# current session only
$env:Path += ";C:\Tools"
```
Don't remove existing entries, only add new ones.

### Problem 9: what `podman compose up -d` means

- `podman`: the program for running containers (equivalent of `docker`)
- `compose`: reads `docker-compose.yml` and manages several containers at once (via the provider)
- `up`: creates and starts everything from the file (pulls images, creates network and volumes, starts containers)
- `-d`: *detached*, in the background; without it logs stream in the terminal and Ctrl+C stops the containers

```bash
podman compose ps            # containers from the file
podman compose logs -f       # live logs (Ctrl+C leaves the view)
podman compose stop          # stops containers, data stays
podman compose down          # stops and removes containers
podman compose down -v       # same + removes volumes (clean start from scratch)
```

### Problem 10: only `transfers-postgres` visible after `podman ps`

```bash
podman ps -a                       # also stopped containers (Exited / Created)
podman logs transfers-rabbitmq     # container logs, shows the cause
podman compose up                  # without -d, errors print in the terminal
```
Possible causes: short image name (use `docker.io/library/rabbitmq:3-management`), port 5672/15672 in use or reserved (`netstat -ano | findstr "5672 15672"`, `netsh interface ipv4 show excludedportrange protocol=tcp`; the mapping can be changed to e.g. `"5673:5672"`), a broken volume (`podman compose down -v` then `up -d`), or RabbitMQ simply starts slower (`(starting)` in the status). After the fixes both containers appeared.

Verification:
```bash
podman ps                                                        # STATUS: Up ... (healthy)
podman exec -it transfers-postgres psql -U transfers -d transfers  # opens psql inside the container; \l = list databases, \q = quit
```
RabbitMQ panel: `http://localhost:15672`, login `guest` / `guest`.

### Problem 11: CI workflow `.github/workflows/ci.yml`

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
  workflow_dispatch:          # manual start from the Actions tab

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x     # must match TargetFramework in the .csproj files
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release
```
- `on`: when the workflow runs (push to `main`, every PR, manually)
- `checkout`: fetches the repo code; `setup-dotnet`: installs the .NET SDK
- `restore` / `build` / `test`: restores packages, compiles (Release), runs tests without rebuilding
- Empty test projects will not stop CI

### Problem 12: fresh-clone verification and `cd $env:TEMP` in cmd

`$env:TEMP` is PowerShell syntax; in cmd it gives `System nie może odnaleźć określonej ścieżki.` (the system cannot find the path).
```bat
:: cmd
cd /d %TEMP%                              :: /d also switches the drive
git clone <repo-url> check-clone
cd check-clone
git checkout stage-0-setup
dotnet build
podman compose up -d
podman ps
podman compose down -v
cd ..
rmdir /s /q check-clone                   :: removes the folder with contents, no prompt
```
Before cloning: `git push -u origin stage-0-setup` (otherwise the clone won't see the branch) and `podman compose down -v` in the old folder (fixed container names and ports would clash).

### Problem 13: `Remove-Item -Recurse -Force check-clone` in cmd

It is a PowerShell cmdlet and does not work in cmd. Equivalent: `rmdir /s /q check-clone` (`/s` with contents, `/q` no prompt). Run it from the parent directory. If "file in use": close editors, Explorer and terminals in that folder.

| Goal | cmd | PowerShell |
|---|---|---|
| Delete folder with contents | `rmdir /s /q name` | `Remove-Item -Recurse -Force name` |
| Delete file | `del name` | `Remove-Item name` |
| TEMP variable | `%TEMP%` | `$env:TEMP` |
| Change directory (with drive) | `cd /d path` | `cd path` |

### Problem 14: empty Actions tab on GitHub

An empty tab usually means the workflow never ran. Check in order:
1. The path must be exactly `.github/workflows/ci.yml` (leading dot, `workflows` with an "s", extension `.yml`, not `.yml.txt`).
   ```bat
   dir /s /b .github              :: files physically on disk
   git ls-files .github           :: files tracked by git; should show .github/workflows/ci.yml
   ```
2. The file is committed and pushed (`git status`, `git log --oneline -5`, `git push`), and visible on GitHub on the `stage-0-setup` branch.
3. Triggers: a PR runs the workflow only if the file exists in the PR branch; you can add `workflow_dispatch` and click **Run workflow**.
4. Settings → Actions → General: Actions must be allowed.
5. YAML syntax error: indent with spaces, never tabs.
6. If the workflow runs but fails: most often `dotnet-version` does not match `TargetFramework`.

### Problem 15: PR and documents

- PR title: `Stage 0: repo, solution, docker-compose and CI setup`. Description: what was done, how it was verified, the "done" criterion. Add "CI is green" and "fresh-clone check" only after verifying them.
- `PROGRESS.md` and `PLAN.md` translated to English, stage 0 marked ✅ (only after green CI and merge), entries in the work log, "what I learned" and "problems and solutions".
- After merge:
  ```bash
  git checkout main     # switch to main
  git pull              # fetch the merged changes
  ```

### Problem 16: renaming a branch

```bash
git branch -m old-name new-name           # rename locally
git push origin -u new-name               # push the new name and set upstream
git push origin --delete old-name         # delete the old name on GitHub
git branch -vv                            # check branches and their upstream
```
Note: deleting the old remote branch closes the open PR. To keep it, rename in the GitHub UI (repo → Branches → pencil icon), then locally:
```bash
git branch -m old-name new-name
git fetch origin
git branch -u origin/new-name new-name
```

## Next step

Stage 1: the `Transfer` entity, EF Core + Npgsql, first migration, `POST /transfers` and `GET /transfers/{id}`.
