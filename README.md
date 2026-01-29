# KanbanLite (MVP) — zarządzanie produkcją kartek świątecznych

Aplikacja do śledzenia produkcji kartek świątecznych w **5 etapach**: Projektowanie → Druk → Cięcie → Pakowanie → Wysyłka.

## Spis treści

- [Opis](#opis-projektu)
- [Funkcjonalność](#funkcjonalność-mvp)
- [Stos technologiczny](#stos-technologiczny)
- [Status](#status)

## Opis projektu

Produkt wspiera dwa typy użytkowników:

- **Manager**: dostęp do Kanban i Panelu Managera (dashboard, konfiguracja, wysyłka)
- **Operator**: dostęp do Kanban (aktualizacja statusu i etapu)

### Główne pojęcia

- **Zlecenie**: tworzone przez Managera (ilość, format, termin)
- **Batch**: automatycznie tworzony podział zlecenia z statusem i etapem produkcji

### Funkcjonalność (MVP)

- Logowanie i role (Manager/Operator)
- Tworzenie zleceń (walidacje: 1–100000 sztuk, termin ≥ 7 dni)
- Automatyczny podział na batche wg reguł
- Zarządzanie batchami (zmiana statusu i etapu)
  - **Statusy**: New → InProgress → Done
  - **Etapy produkcji**: 5 etapów (zmiana tylko „do przodu")
  - **Soft limit 20**: ostrzeżenie gdy InProgress > 20
- Wysyłka do klienta (Manager-only, gdy wszystkie batche gotowe)
- Audyt zmian (kto/kiedy)
- Dashboard managera (metryki, zlecenia pilne, ostrzeżenia)
- Konfiguracja formatów i reguł splitowania

### Widoki

- **Kanban**: tabela batchy z możliwością zmiany statusu/etapu, progress bar, link do zlecenia
- **Panel Managera**: dashboard metryk, konfiguracja, zarządzanie formatami, historia zleceń

## Stos technologiczny

- .NET 9 (ASP.NET Core)
- Blazor Server + MudBlazor (UI)
- PostgreSQL + EF Core (baza danych)
- ASP.NET Core Identity (logowanie, RBAC)
- xUnit + Playwright (testy)

## Struktura projektu

- `KanbanLite.sln` — solucja
- `src/Web` — Blazor Server UI, kontrolery, widoki
- `src/DataAccess` — EF Core, migracje, repozytoria
- `src/Application` — serwisy biznesowe, RBAC, Result Pattern
- `src/DbMigrator` — narzędzie do migracji bazy
- `src/Types.cs` — wspólne typy (DTO, Commands)
- `tests/KanbanLite.Application.UnitTests` — testy jednostkowe
- `tests/KanbanLite.E2E.Tests` — testy end-to-end (Playwright)

## Status

**MVP zaimplementowane i gotowe do uruchomienia**

- Backend w pełni zaimplementowany
- Frontend (Blazor Server) zaimplementowany
- Testy E2E pokrywają ścieżki krytyczne
- 24 testy jednostkowe przechodzą
