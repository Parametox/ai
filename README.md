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

- **Menedżer**: ma dostęp do **Kanban** oraz **Panelu Menedżera** (w tym konfiguracja, dashboard, wysyłka do klienta).
- **Operator**: ma dostęp do **Kanban** (aktualizacja statusu i etapu batchy).

Główne pojęcia domenowe:

- **Zlecenie/Projekt**: tworzone przez Menedżera (ilość sztuk, format produktu, termin realizacji).
- **Batch**: automatycznie tworzony podział zlecenia; każdy batch ma status, etap produkcji i postęp.

Kluczowe reguły biznesowe (MVP):

- **Statusy batchy**: `New`, `InProgress`, `Done`
- **Etapy produkcji**: 5 etapów (od Projektowania do Wysyłki)
- **Zmiana etapu**: tylko „do przodu” (po enumie)
- **Soft limit 20**: dotyczy wyłącznie liczby batchy w statusie **InProgress** (ostrzeżenie/ikona, bez blokowania)
- **Wysyłka do klienta**: tylko dla Menedżera i dopiero gdy wszystkie batche spełniają warunek zakończenia (**etap Wysyłka** + **status Done**)
- **Audyt**: log zmian batchy (kto/kiedy + zmiany statusu/etapu)
- **Brak self‑registration**: konta startowe seedowane (menago/menago, operator/operator)

## Stos technologiczny

Zgodnie z `.ai/tech-stack.md`:

- **Frontend/UI**: **Blazor Server (SSR)** + **MudBlazor**
- **Backend**: **.NET 8 (ASP.NET Core)** (w tym samym hostcie co UI — jeden projekt na MVP)
- **Auth/RBAC**: **ASP.NET Core Identity** (cookie auth) + role Menedżer/Operator
- **Baza danych / ORM**: **PostgreSQL** + **EF Core (Code‑First)** + migracje
- **Audyt**: tabela zdarzeń (transakcyjnie razem ze zmianą)
- **Real-time**: Blazor Server bazuje na SignalR (opcjonalne huby do broadcastu zmian)
- **Observability**: logowanie (np. Serilog) + podstawowe metryki/healthchecks
- **CI/CD**: GitHub Actions (restore/build/test + publikacja + deploy; migracje kontrolowane)
- **Hosting**: Azure App Service + Azure Database for PostgreSQL (MVP)

## Uruchomienie lokalnie

W repozytorium **nie ma jeszcze kodu aplikacji** ani plików projektu (`*.csproj`) — na ten moment są tu głównie pliki w `.ai/` (PRD i ustalenia).

Gdy pojawi się implementacja (Blazor Server + EF Core), README zostanie uzupełnione o:

- wymagania (np. .NET SDK 8/9),
- konfigurację połączenia do PostgreSQL,
- komendy `dotnet restore`, `dotnet ef database update`, `dotnet run`.

## Dostępne skrypty

Na ten moment **brak** zdefiniowanych skryptów (brak `package.json` oraz brak plików projektu `.NET` w repo).

## Zakres (scope)

### Widoki

- **Kanban**: lista/tabela batchy z lookupami / dropdownami do wyboru **statusu** i **etapu**, z:
  - numerem zlecenia,
  - numerem batcha,
  - liczbą sztuk,
  - progress barem,
  - linkiem do projektu.
- **Panel Menedżera** (tylko Menedżer):
  - dashboard metryk operacyjnych,
  - konfiguracja reguł batchowania,
  - CRUD formatów produktów,
  - historia zleceń (archiwum).

### Funkcje (MVP)

- logowanie i RBAC (Menedżer/Operator),
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

