# Podsumowanie sesji planowania PRD - KanbanLite MVP

## Decyzje podjęte podczas planowania

1. **Zakres produktu (MVP):** System do zarządzania produkcją kartek świątecznych w 5 etapach: Projektowanie → Druk → Cięcie → Pakowanie → Wysyłka.

2. **Role i uprawnienia:** Dwie role: **Menedżer** i **Operator**. Tylko Menedżer ma dostęp do **Panelu Menedżera** i może wykonać akcję **"Wyślij do klienta"**.

3. **Widoki aplikacji:** Dwie zakładki: **Kanban** oraz **Panel Menedżera** (tylko dla Menedżera).

4. **UI główne (Kanban):** Prosty widok listy/tabeli batchy, z lookupami/dropdownami do wyboru **statusu** i **etapu**. Batch pokazuje: numer zlecenia, numer batcha, liczbę sztuk, progress bar oraz link do projektu.

5. **Statusy batchy:** 3 statusy: **New**, **InProgress**, **Done**.

6. **Limit produkcyjny:** Limit **20 dotyczy wyłącznie liczby batchy w statusie InProgress** (soft limit).

7. **Komunikat ostrzegawczy:** Gdy przekroczymy/przekroczymy potencjalnie limit InProgress: **tylko komunikat + ikona**, bez blokowania działania.

8. **Zmiana etapu batcha:** Zmiana tylko "do przodu" po enumie etapów (bez potwierdzania ilości).

9. **Alerty o opóźnieniach:** Nie implementujemy mechanizmu alertów o opóźnieniach w MVP.

10. **Konfiguracja podziału na batche:** W Panelu Menedżera edytowalna tabela reguł: "ilość sztuk w zleceniu → % wielkości batcha" (np. 200 szt. → 30% → batch 60 szt.).

11. **Formaty produktów:** Format wybierany przy tworzeniu zlecenia; osobna tabela formatów (CRUD w Panelu Menedżera). Domyślnie 3 formaty: **A6 (10x15 cm)**, **Kwadrat (15x15 cm)**, **A5 (14.8x21 cm)**.

12. **Wysyłka i archiwum:** "Wyślij do klienta" dostępne dopiero, gdy wszystkie batche spełniają warunki zakończenia (w PRD: etap Wysyłka + status Done). Po wysyłce projekt trafia do historii, a na tablicy zostają tylko projekty niewysłane (technicznie: `Project.IsCompleted = true`, opcjonalnie z `CompletedAt/CompletedBy`).

13. **Historia zmian:** Logujemy historię zmian batchy (kto/kiedy + zmiany etapu/statusu).

14. **Użytkownicy startowi:** Bez self-registration; tworzymy skrypt seedujący 2 konta: menedżer (menago/menago) i operator (operator/operator).

15. **Technologia:** @tech-stack.md

16. **Skalowanie MVP:** Maksymalnie 20 batchy **InProgress** jednocześnie (kontrola przez soft limit + monitoring).

17. **Harmonogram:** Fazowanie prac jak zaproponowano: Phase 1 (2–3 tyg), Phase 2 (1–2 tyg), Phase 3 (1 tyg) = 4–6 tyg.

---

## Dopasowane rekomendacje

1. **RBAC (Menedżer/Operator):** Różne uprawnienia i osobny Panel Menedżera.

2. **Prosty UI listy batchy:** Minimalny widok listy z filtrami, dropdownami i progress barem.

3. **Wyliczanie progress:** Proste mapowanie etapu na procent (20/40/60/80/100).

4. **Widok projektu + gating wysyłki:** Widok szczegółowy projektu z warunkowym odblokowaniem "Wyślij do klienta".

5. **Audyt zmian:** Historia zmian batchy (kto/kiedy/co) już w MVP.

6. **Konfiguracja podziału na batche w UI:** Edytowalna tabela reguł w Panelu Menedżera.

7. **Formaty produktów jako dane (CRUD):** Nie hardcode — tabela w DB i UI do zarządzania.

8. **Brak rejestracji użytkowników:** Konta seedowane skryptem dla bezpieczeństwa i prostoty.

9. **Dashboard w Panelu Menedżera:** Statystyki operacyjne zgodnie z rekomendacją.

10. **Soft limit i monitoring:** Zamiast blokad — ostrzeżenia + widoczny licznik (szczególnie dla InProgress).

11. **Blazor Server + MudBlazor:** Szybkie MVP bez REST API; opcjonalnie SignalR.

---

## Szczegółowe podsumowanie planowania PRD

### a) Główne wymagania funkcjonalne produktu

#### Autentykacja i role
- Logowanie użytkowników, role Menedżer/Operator.
- Menedżer widzi: Kanban + Panel Menedżera; Operator widzi: tylko Kanban.

#### Zlecenia/projekty
- Formularz utworzenia zlecenia: liczba sztuk, format produktu (lookup), termin realizacji (walidacje: 1–100000, termin ≥ dziś+7 dni).
- Automatyczny podział zlecenia na batche na podstawie tabeli konfiguracyjnej (%).

#### Batche
- Każdy batch ma: numer (zlecenie.batch), ilość sztuk, **status (New/InProgress/Done)**, etap produkcji (5 etapów), progress bar.
- Zmiana etapu: tylko do przodu.
- Zmiana statusu: obsługa New/InProgress/Done (InProgress wlicza się do limitu).

#### Limit 20 InProgress
- Limit dotyczy **tylko batchy w statusie InProgress** (globalnie).
- Po przekroczeniu: komunikat ostrzegawczy + ikona, bez blokady.

#### Wysyłka
- "Wyślij do klienta" tylko dla Menedżera i tylko, gdy wszystkie batche spełniają warunek zakończenia (etap Wysyłka + status Done).
- Po wysyłce: projekt przeniesiony do historii; na tablicy tylko niewysłane projekty.

#### Panel Menedżera
- Dashboard metryk (zgodnie z rekomendacją).
- Konfiguracja batchy (edytowalna tabela progów i %).
- Formaty produktów (CRUD).
- Historia zleceń (archiwum + podgląd szczegółów).

#### Audyt
- Historia zmian statusów/etapów batchy (user, timestamp, zmiana).

---

### b) Kluczowe historie użytkownika i ścieżki korzystania

#### US-001: Logowanie
**Jako** użytkownik  
**Chcę** zalogować się do systemu  
**Aby** korzystać z funkcji odpowiednich dla mojej roli

**Kryteria akceptacji:**
- Poprawne logowanie
- Przekierowanie do Kanban
- Widoczna rola użytkownika

---

#### US-002: Utworzenie zlecenia (Menedżer)
**Jako** Menedżer  
**Chcę** utworzyć zlecenie (ilość, format, termin)  
**Aby** rozpocząć produkcję

**Kryteria akceptacji:**
- Walidacje pól (liczba sztuk 1-100000, termin ≥ dziś+7 dni)
- Automatyczne utworzenie batchy wg tabeli konfiguracyjnej
- Wszystkie batchy startują jako New

---

#### US-003: Start batcha (Operator/Menedżer)
**Jako** Operator  
**Chcę** zmienić status batcha z New na InProgress  
**Aby** rozpocząć pracę nad batchem

**Kryteria akceptacji:**
- Status zmieniony na InProgress
- Licznik InProgress aktualizuje się automatycznie
- Ostrzeżenie wyświetla się gdy InProgress > 20

---

#### US-004: Przesunięcie etapu batcha (Operator/Menedżer)
**Jako** Operator  
**Chcę** przesunąć batch do kolejnego etapu  
**Aby** odzwierciedlić postęp pracy

**Kryteria akceptacji:**
- Zmiana etapu tylko do przodu
- Progress bar aktualizuje się automatycznie
- Wpis w historii zmian

---

#### US-005: Zakończenie batcha (Operator/Menedżer)
**Jako** Operator  
**Chcę** zmienić status batcha z InProgress na Done  
**Aby** zakończyć pracę nad batchem

**Kryteria akceptacji:**
- Status zmieniony na Done
- Licznik InProgress zmniejszony
- Historia zmian zapisana

---

#### US-006: Podgląd projektu (Operator/Menedżer)
**Jako** użytkownik  
**Chcę** wejść w szczegóły projektu  
**Aby** zobaczyć wszystkie batche i statystyki

**Kryteria akceptacji:**
- Widoczne wszystkie batche zlecenia
- Progres projektu (średnia wszystkich batchy)
- Status gotowości do wysyłki
- Dla Menedżera: akcje administracyjne (edycja terminu, wysyłka)

---

#### US-007: Wysyłka do klienta (Menedżer)
**Jako** Menedżer  
**Chcę** wysłać zlecenie do klienta  
**Aby** zamknąć projekt

**Kryteria akceptacji:**
- Przycisk aktywny tylko gdy wszystkie batche: etap Wysyłka + status Done
- Dialog potwierdzenia przed wysyłką
- Przeniesienie do historii zleceń
- Zniknięcie z listy aktywnych batchy

---

#### US-008: Konfiguracja reguł batchowania (Menedżer)
**Jako** Menedżer  
**Chcę** edytować progi i % batchy  
**Aby** dopasować produkcję do potrzeb

**Kryteria akceptacji:**
- Edycja tabeli reguł (zakresy ilości → %)
- Walidacja zakresów (nie mogą się nakładać)
- Wpływ tylko na nowe zlecenia

---

#### US-009: Zarządzanie formatami (Menedżer)
**Jako** Menedżer  
**Chcę** dodawać/edytować formaty  
**Aby** były dostępne przy tworzeniu zlecenia

**Kryteria akceptacji:**
- CRUD formatów produktów
- Domyślne 3 formaty obecne w systemie
- Formaty widoczne w dropdownie przy tworzeniu zlecenia

---

#### US-010: Dashboard (Menedżer)
**Jako** Menedżer  
**Chcę** widzieć metryki produkcji  
**Aby** zarządzać obciążeniem

**Kryteria akceptacji:**
- Rozkład batchy po statusach (New/InProgress/Done)
- Rozkład batchy po etapach produkcji
- Lista pilnych zleceń (termin < 7 dni)
- Średni czas realizacji zlecenia

---

### c) Kryteria sukcesu i pomiar

- **Automatyczne batchowanie:** Utworzenie zlecenia 10k → auto-batche wg konfiguracji
- **Widoczność i kontrola:** Operator aktualizuje status/etap batcha z listy; progress spójny
- **Gating wysyłki:** Brak możliwości wysyłki, dopóki nie spełnione warunki batchy
- **Kontrola obciążenia:** Licznik InProgress działa; ostrzeżenie przy przekroczeniu 20
- **Sprawność operacyjna:** Czas utworzenia zlecenia < 2 min, mniejsza liczba błędów w śledzeniu

---


// End of Selection
```

## Nierozwiązane kwestie

1. **Dokładna semantyka ostrzeżenia limitu:** Czy ostrzegamy tylko przy faktycznym przekroczeniu (przy zmianie statusu na InProgress), czy też "potencjalnie" już na etapie tworzenia zlecenia?

2. **Reguły przejść statusów:** Czy dopuszczamy powroty (np. InProgress → New / Done → InProgress), czy tylko "do przodu" jak etapy?

3. **Definicja "Done" vs etap:** Czy batch może mieć status Done zanim osiągnie etap Wysyłka, czy Done oznacza "ukończony etapowo" (Shipping + Done)?

4. **Szczegóły dashboardu:** Dokładny zakres danych (np. "ostatnie 10 zakończonych") i sposób liczenia średniego czasu realizacji.

---

**Dokument przygotowany:** 12 stycznia 2026  
**Status:** Gotowy do implementacji
