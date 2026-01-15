# Tworzenie schematu bazy danych

Jesteś architektem baz danych, którego zadaniem jest stworzenie schematu bazy danych PostgreSQL na podstawie informacji dostarczonych z sesji planowania, dokumentu wymagań produktu (PRD) i stacku technologicznym. Twoim celem jest zaprojektowanie wydajnej i skalowalnej struktury bazy danych, która spełnia wymagania projektu.

1. <prd>
{{prd}} <- zamień na referencję do @prd.md
</prd>

Jest to dokument wymagań produktu, który określa cechy, funkcjonalności i wymagania projektu.

2. <session_notes>
<conversation_summary>
<decisions>
1. System jest single-tenant (jedna organizacja) – brak potrzeby multi-tenant oraz RLS; autoryzacja przez RBAC w aplikacji (role Manager/Operator).
2. Model domeny: `Order` powstaje po zapisie formularza; na jego podstawie tworzony jest `Project` (proces „Projektowanie” to pierwszy etap kanbana, nie ma osobnej encji „projekt graficzny”).
3. Relacje: `Order -> Projects` = 1:N (w praktyce zwykle 1 projekt na zlecenie); `Project -> Batches` = 1:N.
4. Po akcji „Wyślij do klienta” nie usuwamy batchy; projekty mają być ukryte na kanbanie poprzez flagę `Project.IsCompleted = true` (historia jest read-only).
5. Etapy produkcji (5) przechodzą tylko do przodu; statusy batchy (New/InProgress/Done) można zmieniać wstecz/przód, ale tylko o jeden krok (bez przeskoków) i każda zmiana ma być zapisana.
6. Limit produkcyjny 20 dotyczy globalnie całego systemu i obejmuje wyłącznie batche w statusie `InProgress` (soft limit – ostrzeżenie, bez blokady).
7. `BatchSplitRules`: wybrany wariant A (reguły oparte o zakresy `min_qty/max_qty`).
8. Wyliczanie batchy odbywa się w kodzie (nie w bazie).
9. Reguła wyliczania: `baseSize = ceil(orderQty * percent / 100)` (zaokrąglanie w górę do jedności, minimum 1), batchowanie po `baseSize`, a ostatni batch stanowi resztę, tak aby suma ilości batchy była równa `orderQty`.
10. Audyt obejmuje tylko zmiany statusu/etapu batcha + datetime (i praktycznie: identyfikacja użytkownika, kto zmienił).
11. Współbieżność operacyjna: zakładamy, że nad danym batchem pracuje jeden operator (brak równoległej edycji tego samego batcha jako wymóg biznesowy).
12. Format produktu może być przechowywany jako nazwa (tabela formatów/lookup), bez konieczności przechowywania wymiarów w MVP.
</decisions>

<matched_recommendations>
1. Zastosować spójne klucze obce (FK) dla relacji `projects.order_id` i `batches.project_id`, nawet jeśli logika będzie zarządzana przez repozytoria – zapewnia to integralność danych i minimalizuje ryzyko „osieroconych” rekordów.
2. Indeksować globalne zliczanie `InProgress` przez partial index w PostgreSQL (np. indeks tylko dla rekordów o statusie `InProgress`) dla szybkich odczytów limitu i dashboardu.
3. Rozdzielić semantykę „zakończenia/wysłania” od statusów batchy: `Project.IsCompleted` (lub `CompletedAt`) determinuje widoczność na kanbanie, a status/etap opisuje produkcję.
4. Zapewnić walidację braku nakładania się aktywnych zakresów w `BatchSplitRules` (co najmniej w aplikacji; opcjonalnie constraint po stronie DB w kolejnej iteracji).
5. Utrzymywać tabelę audytu zmian batchy (kto/kiedy/co) i zapisywać ją transakcyjnie razem ze zmianą statusu/etapu.
6. Dla czytelności i spójności: użyć enumów (lub słowników) dla statusów i etapów, plus ograniczeń (CHECK) na dozwolone wartości w DB.
7. Zapewnić unikalność numeracji batchy w obrębie projektu (np. UNIQUE(project_id, batch_no)) oraz indeksy pod najczęstsze zapytania (po projekcie, po statusie).
</matched_recommendations>

<database_planning_summary>
a. Główne wymagania dotyczące schematu bazy danych
- Baza: PostgreSQL, ORM: EF Core (code-first) z migracjami.
- Encje podstawowe: Orders, Projects, Batches, ProductFormats, BatchSplitRules, AuditLog.
- Kanban pokazuje wyłącznie projekty aktywne: `Project.IsCompleted = false`.
- Soft limit 20 `InProgress` liczony globalnie, bez blokowania operacji.
- Historia zmian batchy jest wymagana w MVP (audyt zmian status/etap).

b. Kluczowe encje i ich relacje
- `orders` (zlecenia): dane wejściowe z formularza (ilość sztuk, format, termin).
- `projects`: tworzone na podstawie `orders`; zawierają flagę `is_completed` do ukrywania z kanbana po wysyłce.
  - Relacja: `orders (1) -> (N) projects`.
- `batches`: tworzone dla `projects`; zawierają `status` (New/InProgress/Done), `stage` (5 etapów), `quantity`, `batch_no`.
  - Relacja: `projects (1) -> (N) batches`.
- `product_formats`: lookup/CRUD formatów (w MVP może być tylko `name` + aktywność).
- `batch_split_rules`: konfiguracja batchowania w Panelu Managera:
  - wariant A: `min_qty`, `max_qty`, `percent`, `is_active`.
  - wybór reguły po `orderQty` i zakresie.
- `audit_log` (historia zmian batchy): rejestruje zmiany `status` i `stage` oraz timestamp (i użytkownika).

c. Ważne kwestie dotyczące bezpieczeństwa i skalowalności
- Bezpieczeństwo: single-tenant, brak RLS; RBAC w aplikacji (Manager/Operator).
- Integralność: FK + NOT NULL + CHECK na ilościach i dozwolonych wartościach status/etap.
- Wydajność:
  - indeks po `batches.project_id` dla listowania batchy projektu,
  - partial index dla `batches` w statusie `InProgress` dla szybkiego zliczania limitu i metryk,
  - unikalność batchy w projekcie: UNIQUE(project_id, batch_no).
- Skalowalność MVP: założenie limitu 20 `InProgress` globalnie; batchowanie liczone w kodzie, zapisy wykonywane transakcyjnie.

d. Obszary wymagające implementacyjnej uwagi
- Batchowanie w kodzie: `baseSize = ceil(orderQty * percent / 100)`; generowanie batchy po `baseSize`, ostatni batch = reszta; zapis w jednej transakcji razem z Order/Project.
- Przejścia statusów: dozwolone tylko o jeden krok; etapy tylko do przodu – egzekwowane w logice aplikacji, a nie wyłącznie constraintami DB.
</database_planning_summary>

<unresolved_issues>
1. Czy dopuszczalne są konkretne cofki statusów: `Done -> InProgress` oraz `InProgress -> New` (w ramach zasady „o jeden krok”), czy ograniczamy cofanie do wybranych przypadków biznesowych.
2. Czy poza `IsCompleted` chcemy od razu dodać pola `CompletedAt` i `CompletedByUserId` (rekomendowane do historii i raportów, ale nie wymagane).
3. Dokładny kształt numeracji identyfikatorów biznesowych (np. `order_number`, `project_number`, `batch_no`) i wymagane unikalności poza `project_id + batch_no`.
</unresolved_issues>
</conversation_summary>
</session_notes>

Są to notatki z sesji planowania schematu bazy danych. Mogą one zawierać ważne decyzje, rozważania i konkretne wymagania omówione podczas spotkania.

3. <tech_stack>
{{tech-stack}} <- zamień na referencje do tech-stack.md
</tech_stack>

Opisuje stack technologiczny, który zostanie wykorzystany w projekcie, co może wpłynąć na decyzje dotyczące projektu bazy danych.

Wykonaj następujące kroki, aby utworzyć schemat bazy danych:

1. Dokładnie przeanalizuj notatki z sesji, identyfikując kluczowe jednostki, atrybuty i relacje omawiane podczas sesji planowania.
2. Przejrzyj PRD, aby upewnić się, że wszystkie wymagane funkcje i funkcjonalności są obsługiwane przez schemat bazy danych.
3. Przeanalizuj stack technologiczny i upewnij się, że projekt bazy danych jest zoptymalizowany pod kątem wybranych technologii.

4. Stworzenie kompleksowego schematu bazy danych, który obejmuje
   a. Tabele z odpowiednimi nazwami kolumn i typami danych
   b. Klucze podstawowe i klucze obce
   c. Indeksy poprawiające wydajność zapytań
   d. Wszelkie niezbędne ograniczenia (np. unikalność, not null)

5. Zdefiniuj relacje między tabelami, określając kardynalność (jeden-do-jednego, jeden-do-wielu, wiele-do-wielu) i wszelkie tabele łączące wymagane dla relacji wiele-do-wielu.

6. Opracowanie zasad PostgreSQL dla zabezpieczeń na poziomie wiersza (RLS), jeśli dotyczy, w oparciu o wymagania określone w notatkach z sesji lub PRD.

7. Upewnij się, że schemat jest zgodny z najlepszymi praktykami projektowania baz danych, w tym normalizacji do odpowiedniego poziomu (zwykle 3NF, chyba że denormalizacja jest uzasadniona ze względu na wydajność).

Ostateczny wynik powinien mieć następującą strukturę:
```markdown
1. Lista tabel z ich kolumnami, typami danych i ograniczeniami
2. Relacje między tabelami
3. Indeksy
4. Zasady PostgreSQL (jeśli dotyczy)
5. Wszelkie dodatkowe uwagi lub wyjaśnienia dotyczące decyzji projektowych
```

W odpowiedzi należy podać tylko ostateczny schemat bazy danych w formacie markdown, który zapiszesz w pliku .ai/db-plan.md bez uwzględniania procesu myślowego lub kroków pośrednich. Upewnij się, że schemat jest kompleksowy, dobrze zorganizowany i gotowy do wykorzystania jako podstawa do tworzenia migracji baz danych.
