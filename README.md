# KanbanLite (MVP) — zarządzanie produkcją kartek świątecznych

Minimalna aplikacja (MVP) do śledzenia produkcji kartek świątecznych w **5 etapach**: Projektowanie → Druk → Cięcie → Pakowanie → Wysyłka.  
Repozytorium zawiera na ten moment przede wszystkim **dokumentację i ustalenia** (PRD + stack technologiczny).

## Spis treści

- [Nazwa projektu](#nazwa-projektu)
- [Opis projektu](#opis-projektu)
- [Stos technologiczny](#stos-technologiczny)
- [Uruchomienie lokalnie](#uruchomienie-lokalnie)
- [Dostępne skrypty](#dostępne-skrypty)
- [Zakres (scope)](#zakres-scope)
- [Status projektu](#status-projektu)
- [Licencja](#licencja)

## Nazwa projektu

**KanbanLite (MVP)**

## Opis projektu

Produkt wspiera dwa typy użytkowników:

- **Manager**: ma dostęp do **Kanban** oraz **Panelu Managera** (w tym konfiguracja, dashboard, wysyłka do klienta).
- **Operator**: ma dostęp do **Kanban** (aktualizacja statusu i etapu batchy).

Główne pojęcia domenowe:

- **Zlecenie/Projekt**: tworzone przez Managera (ilość sztuk, format produktu, termin realizacji).
- **Batch**: automatycznie tworzony podział zlecenia; każdy batch ma status, etap produkcji i postęp.

Kluczowe reguły biznesowe (MVP):

- **Statusy batchy**: `New`, `InProgress`, `Done`
- **Etapy produkcji**: 5 etapów (od Projektowania do Wysyłki)
- **Zmiana etapu**: tylko „do przodu” (po enumie)
- **Soft limit 20**: dotyczy wyłącznie liczby batchy w statusie **InProgress** (ostrzeżenie/ikona, bez blokowania)
- **Wysyłka do klienta**: tylko dla Managera i dopiero gdy wszystkie batche spełniają warunek zakończenia (**etap Wysyłka** + **status Done**)
- **Audyt**: log zmian batchy (kto/kiedy + zmiany statusu/etapu)
- **Brak self‑registration**: konta startowe seedowane (menago/menago, operator/operator)

## Stos technologiczny

Zgodnie z `.ai/tech-stack.md`:

- **Frontend/UI**: **Blazor Server (SSR)** + **MudBlazor**
- **Backend**: **.NET 8 (ASP.NET Core)** (w tym samym hostcie co UI — jeden projekt na MVP)
- **Auth/RBAC**: **ASP.NET Core Identity** (cookie auth) + role Manager/Operator
- **Baza danych / ORM**: **PostgreSQL** + **EF Core (Code‑First)** + migracje
- **Audyt**: tabela zdarzeń (transakcyjnie razem ze zmianą)
- **Real-time**: Blazor Server bazuje na SignalR (opcjonalne huby do broadcastu zmian)
- **Observability**: logowanie (np. Serilog) + podstawowe metryki/healthchecks
- **CI/CD**: GitHub Actions (restore/build/test + publikacja + deploy; migracje kontrolowane)
- **Hosting**: Azure App Service + Azure Database for PostgreSQL (MVP)

## Uruchomienie lokalnie

Repozytorium zawiera już podstawową strukturę `.NET` oraz **warstwę dostępu do danych** (EF Core + PostgreSQL) z migracjami.
Kontrakt DTO/Command Models (na potrzeby warstwy serwisów / UI Blazor Server) jest w `src/Types.cs`.

### Konfiguracja środowisk

Aplikacja obsługuje dwa środowiska:

| Środowisko | Baza danych | Konfiguracja |
|------------|-------------|--------------|
| **Development** (Debug) | Lokalna PostgreSQL (localhost:5432) | `appsettings.Development.json` |
| **Production** (Release/CI) | Supabase Cloud | `appsettings.json` + Environment Variable |

#### Lokalne uruchomienie (Development)
```powershell
dotnet run --project src/Web
# lub
dotnet run --project src/Web --configuration Debug
```
Używa `appsettings.Development.json` z connection stringiem do localhost (User: `postgres`, Pass: `postgres`).

#### Uruchomienie produkcyjne (Release)
```powershell
dotnet run --project src/Web --configuration Release
```
Używa `appsettings.json`. Wymaga ustawienia zmiennej środowiskowej `ConnectionStrings__KanbanConnectionString` lub sekretów do połączenia z Supabase.

### Wymagania

- **.NET SDK 9** (wymagane przez aktualne projekty)
- Lokalny **PostgreSQL**:
  - rekomendowane: **Docker Desktop** + `docker compose`
  - alternatywnie: lokalna instalacja PostgreSQL 16/17

### Baza danych (Docker)

Jeśli masz Docker Desktop:

```bash
docker compose up -d
```

Domyślne parametry w `docker-compose.yml`:
- DB: `kanbanlite`
- user: `kanbanlite`
- password: `kanbanlite`
- port: `5432`

### Baza danych (lokalna instalacja PostgreSQL) — wariant 2

Jeśli instalujesz PostgreSQL lokalnie na Windows, upewnij się, że masz w PATH narzędzie `psql` (folder `...\PostgreSQL\XX\bin`).

1) Ustaw hasło admina (z instalatora) jako `PGPASSWORD`:

```powershell
$env:PGPASSWORD="TwojeHasloPostgres"
```

2) Utwórz rolę + bazę:

```powershell
.\scripts\setup-local-postgres.ps1
```

3) Zastosuj migracje:

```powershell
.\scripts\apply-migrations.ps1
```

### Migracje EF Core

Migracje są w projekcie `src/DataAccess` (Code-First).

#### Lokalnie (Development)
Domyślnie używa localhost. Można uruchomić migracje:

```powershell
# Przez DbMigrator (Development = localhost)
dotnet run --project src/DbMigrator

# Lub przez dotnet-ef
dotnet tool run dotnet-ef database update --project src/DataAccess --startup-project src/DbMigrator
```

#### Produkcja (Supabase)
Ustaw connection string do Supabase:

```powershell
$env:ConnectionStrings__KanbanConnectionString="Host=db.xxx.supabase.co;Port=6543;Database=postgres;Username=postgres;Password=TWOJE_HASLO;Pooling=true;Trust Server Certificate=true;"
dotnet run --project src/DbMigrator --configuration Release
```

### CI/CD (GitHub Actions)

Pipeline używa Supabase jako bazy danych. Wymagane sekrety w GitHub:

| Secret | Opis |
|--------|------|
| `SUPABASE_CONNECTION_STRING` | Pełny connection string do Supabase |
| `SUPABASE_PUBLISHABLE_KEY` | Klucz API Supabase (publishable) |

#### Konfiguracja sekretów w GitHub:
1. Przejdź do **Settings** → **Secrets and variables** → **Actions**
2. Dodaj `SUPABASE_CONNECTION_STRING`:
   ```
   Host=db.xxx.supabase.co;Port=6543;Database=postgres;Username=postgres;Password=TWOJE_HASLO;Pooling=true;Trust Server Certificate=true;
   ```
3. Dodaj `SUPABASE_PUBLISHABLE_KEY`:
   ```
   sb_publishable_xxx
   ```

### Struktura solucji

- `KanbanLite.sln` — solucja
- `src/DataAccess` — encje, `AppDbContext`, migracje
- `src/Application` — **warstwa aplikacyjna** (serwisy/use case’y in-process), **Result Pattern**, RBAC po stronie serwisu
- `src/DbMigrator` — minimalny projekt startowy do uruchamiania migracji
- `tests/KanbanLite.Application.UnitTests` — testy jednostkowe warstwy Application (xUnit + FluentAssertions + NSubstitute)

## Stan implementacji (backend in-process)

**Backend warstwy Application jest zaimplementowany i gotowy do użycia.**

### Zaimplementowane serwisy

- **`BatchService`**: Kanban (projekcja join bez **N+1**), zmiana statusu i etapu (walidacje + audyt transakcyjny + soft limit 20), `GetInProgressCountAsync`.
- **`ProjectService`**: lista projektów (paginacja + filtrowanie `IsCompleted`), szczegóły projektu (projekcja bez **N+1**), „Wyślij do klienta” (walidacja gotowości + RBAC Manager-only).
- **`BatchAuditService`**: stronicowany odczyt audytu batcha (RBAC Manager+Operator).
- **`OrderService`**: tworzenie zlecenia (walidacje + transakcja) + automatyczny projekt i batche wg aktywnej reguły splitu, lista zleceń (filtrowanie po `dueFrom/dueTo` i `q` obejmujące `orderNumber`/`projectNumber`), szczegóły zlecenia.
- **`ProductFormatService`**: lookup aktywnych formatów (Manager+Operator), zarządzanie formatami (Manager-only: CRUD + deaktywacja).
- **`BatchSplitRuleService`**: zarządzanie regułami splitu (Manager-only: CRUD + aktywacja/dezaktywacja) + walidacja braku overlapów aktywnych zakresów.
- **`DashboardService`**: dashboard Managera (agregacje po statusach/etapach na projektach aktywnych) + lista pilnych (due < dziś+7) + ostrzeżenie soft‑limit `InProgress > 20`.

### Rejestracja DI

- **`KanbanLite.Application.DependencyInjection.AddKanbanLiteApplication()`**: rejestruje wszystkie serwisy Application.
- **Wymagane w hoście**: rejestracja `ICurrentUser` (np. `ClaimsPrincipalCurrentUser` z `IHttpContextAccessor` dla ASP.NET Core/Blazor Server).
- **Testy jednostkowe**: 24 testy przechodzą (`tests/KanbanLite.Application.UnitTests`).

## Dostępne skrypty

W repo są skrypty PowerShell w `scripts/` (m.in. przygotowanie lokalnego Postgresa i zastosowanie migracji).

## Zakres (scope)

### Widoki

- **Kanban**: lista/tabela batchy z lookupami / dropdownami do wyboru **statusu** i **etapu**, z:
  - numerem zlecenia,
  - numerem batcha,
  - liczbą sztuk,
  - progress barem,
  - linkiem do projektu.
- **Panel Managera** (tylko Manager):
  - dashboard metryk operacyjnych,
  - konfiguracja reguł batchowania,
  - CRUD formatów produktów,
  - historia zleceń (archiwum).

### Funkcje (MVP)

- logowanie i RBAC (Manager/Operator),
- tworzenie zlecenia (ilość, format, termin; walidacje: 1–100000, termin ≥ dziś + 7 dni),
- automatyczny podział zlecenia na batche wg tabeli reguł,
- zarządzanie batchami (statusy, etapy, postęp),
- soft limit 20 batchy `InProgress` (ostrzeżenie),
- „Wyślij do klienta” z warunkiem gotowości,
- audyt zmian batchy.

## Status projektu

- **Status**: dokumentacja gotowa, implementacja MVP do rozpoczęcia
- **Harmonogram (szacunek)**: **4–6 tygodni** (Phase 1: 2–3 tyg, Phase 2: 1–2 tyg, Phase 3: 1 tyg)

## Licencja

**Nie określono** (brak pliku `LICENSE` w repo).

