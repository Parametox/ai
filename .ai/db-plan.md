# Schemat bazy danych (PostgreSQL) — KanbanLite MVP

Poniżej znajduje się docelowy schemat bazy danych pod **PostgreSQL** dla MVP, zoptymalizowany pod **EF Core Code‑First** oraz wymagania z PRD i ustaleń sesji (single-tenant, brak RLS, RBAC w aplikacji).

## 1. Lista tabel z kolumnami, typami danych i ograniczeniami

### 1.1 `product_formats`
Słownik formatów produktów (CRUD w Panelu Managera).

- `id` **bigserial** PK
- `name` **text** NOT NULL
- `is_active` **boolean** NOT NULL DEFAULT true
- `created_at` **timestamptz** NOT NULL DEFAULT now()

Ograniczenia:
- `uq_product_formats_name`: UNIQUE (`name`)

---

### 1.2 `orders`
Zlecenie tworzone z formularza (ilość, format, termin).

- `id` **bigserial** PK
- `order_number` **text** NOT NULL
- `quantity` **int** NOT NULL
- `product_format_id` **bigint** NOT NULL (FK → `product_formats.id`)
- `due_date` **date** NOT NULL
- `created_at` **timestamptz** NOT NULL DEFAULT now()

Ograniczenia:
- `uq_orders_order_number`: UNIQUE (`order_number`)
- `ck_orders_quantity_range`: CHECK (`quantity` BETWEEN 1 AND 100000)
  - Walidacja „termin ≥ dziś + 7 dni” egzekwowana w aplikacji (ze względu na użycie czasu bieżącego).

---

### 1.3 `projects`
Projekt produkcyjny tworzony na podstawie `orders`. Kanban pokazuje tylko projekty aktywne.

- `id` **bigserial** PK
- `order_id` **bigint** NOT NULL (FK → `orders.id`)
- `project_number` **text** NOT NULL
- `is_completed` **boolean** NOT NULL DEFAULT false
- `completed_at` **timestamptz** NULL
- `completed_by_user_id` **text** NULL (FK → `AspNetUsers.Id` z ASP.NET Core Identity)
- `created_at` **timestamptz** NOT NULL DEFAULT now()

Ograniczenia:
- `uq_projects_project_number`: UNIQUE (`project_number`)
- `ck_projects_completed_consistency`: CHECK (
  (`is_completed` = false AND `completed_at` IS NULL)
  OR
  (`is_completed` = true AND `completed_at` IS NOT NULL)
)

Uwagi:
- `completed_*` odpowiada akcji biznesowej „**Wyślij do klienta**” (projekty „completed” są ukryte na Kanbanie i widoczne w historii).

---

### 1.4 `batches`
Batche produkcyjne w ramach projektu.

- `id` **bigserial** PK
- `project_id` **bigint** NOT NULL (FK → `projects.id`)
- `batch_no` **int** NOT NULL
- `quantity` **int** NOT NULL
- `status` **text** NOT NULL  *(wartości: `New`, `InProgress`, `Done`)*
- `stage` **smallint** NOT NULL *(wartości 1..5: Projektowanie=1, Druk=2, Cięcie=3, Pakowanie=4, Wysyłka=5)*
- `created_at` **timestamptz** NOT NULL DEFAULT now()
- `updated_at` **timestamptz** NOT NULL DEFAULT now()

Ograniczenia:
- `uq_batches_project_batch_no`: UNIQUE (`project_id`, `batch_no`)
- `ck_batches_quantity_positive`: CHECK (`quantity` > 0)
- `ck_batches_status_enum`: CHECK (`status` IN ('New','InProgress','Done'))
- `ck_batches_stage_range`: CHECK (`stage` BETWEEN 1 AND 5)

Uwagi:
- Reguły przejść (status „o 1 krok”, etapy tylko do przodu) są egzekwowane w logice aplikacji; DB zapewnia jedynie poprawność domeny (CHECK).

---

### 1.5 `batch_split_rules`
Konfiguracja dzielenia zleceń na batche (wariant A: zakresy ilości → procent).

- `id` **bigserial** PK
- `min_qty` **int** NOT NULL
- `max_qty` **int** NULL
- `percent` **numeric(5,2)** NOT NULL
- `min_batch_size` **int** NOT NULL DEFAULT 1
- `max_batches_per_project` **int** NULL
- `is_active` **boolean** NOT NULL DEFAULT true
- `created_at` **timestamptz** NOT NULL DEFAULT now()

Ograniczenia:
- `ck_batch_split_rules_min_qty`: CHECK (`min_qty` >= 1)
- `ck_batch_split_rules_max_qty`: CHECK (`max_qty` IS NULL OR `max_qty` >= `min_qty`)
- `ck_batch_split_rules_percent`: CHECK (`percent` > 0 AND `percent` <= 100)
- `ck_batch_split_rules_min_batch_size`: CHECK (`min_batch_size` >= 1)
- `ck_batch_split_rules_max_batches`: CHECK (`max_batches_per_project` IS NULL OR `max_batches_per_project` >= 1)

Uwagi:
- Brak nakładania się aktywnych zakresów: walidacja w aplikacji (zalecenie: dodatkowy constraint Postgres — patrz sekcja 5).
- Wyliczanie batchy odbywa się w kodzie: `baseSize = ceil(orderQty * percent / 100)`, następnie batche po `baseSize`, ostatni batch = reszta; suma batchy = `orderQty`.

---

### 1.6 `batch_audit_log`
Historia zmian statusu/etapu batchy (kto/kiedy/co).

- `id` **bigserial** PK
- `batch_id` **bigint** NOT NULL (FK → `batches.id`)
- `changed_at` **timestamptz** NOT NULL DEFAULT now()
- `changed_by_user_id` **text** NOT NULL (FK → `AspNetUsers.Id` z ASP.NET Core Identity)
- `old_status` **text** NULL
- `new_status` **text** NULL
- `old_stage` **smallint** NULL
- `new_stage` **smallint** NULL

Ograniczenia:
- `ck_batch_audit_status_enum_old`: CHECK (`old_status` IS NULL OR `old_status` IN ('New','InProgress','Done'))
- `ck_batch_audit_status_enum_new`: CHECK (`new_status` IS NULL OR `new_status` IN ('New','InProgress','Done'))
- `ck_batch_audit_stage_range_old`: CHECK (`old_stage` IS NULL OR (`old_stage` BETWEEN 1 AND 5))
- `ck_batch_audit_stage_range_new`: CHECK (`new_stage` IS NULL OR (`new_stage` BETWEEN 1 AND 5))
- `ck_batch_audit_has_change`: CHECK (
  (`old_status` IS DISTINCT FROM `new_status`)
  OR
  (`old_stage` IS DISTINCT FROM `new_stage`)
)

Uwagi:
- Logowanie powinno być transakcyjne razem ze zmianą `batches.status/stage`.

---

### 1.7 Tabele ASP.NET Core Identity
Uwierzytelnienie i role są oparte o **ASP.NET Core Identity** (cookie auth). EF Core utworzy standardowe tabele, m.in.:
- `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`

W tym schemacie odwołujemy się do `AspNetUsers.Id` dla pól: `projects.completed_by_user_id`, `batch_audit_log.changed_by_user_id`.

## 2. Relacje między tabelami

- `orders.product_format_id` → `product_formats.id` (**N:1**)
- `projects.order_id` → `orders.id` (**N:1**, Order 1:N Projects)
- `batches.project_id` → `projects.id` (**N:1**, Project 1:N Batches)
- `batch_audit_log.batch_id` → `batches.id` (**N:1**, Batch 1:N Audit events)
- `projects.completed_by_user_id` → `AspNetUsers.Id` (**N:1**, opcjonalne)
- `batch_audit_log.changed_by_user_id` → `AspNetUsers.Id` (**N:1**, wymagane)

Zalecane akcje FK:
- `projects.order_id`: `ON DELETE RESTRICT`
- `batches.project_id`: `ON DELETE RESTRICT`
- `batch_audit_log.batch_id`: `ON DELETE CASCADE` **lub** `RESTRICT` (preferowane `RESTRICT`, jeśli audyt ma przetrwać nawet gdy batchy kiedyś byłyby czyszczone)

## 3. Indeksy

### 3.1 Indeksy funkcjonalne pod Kanban/odczyty
- `ix_projects_active`: partial index po projektach aktywnych:
  - `CREATE INDEX ix_projects_active ON projects (id) WHERE is_completed = false;`

- `ix_batches_project_id`: szybkie listowanie batchy w projekcie:
  - `CREATE INDEX ix_batches_project_id ON batches (project_id);`

### 3.2 Soft limit 20 `InProgress` (globalnie)
- `ix_batches_inprogress`: szybkie zliczanie batchy `InProgress`:
  - `CREATE INDEX ix_batches_inprogress ON batches (id) WHERE status = 'InProgress';`

### 3.3 Dashboard / raporty
- `ix_orders_due_date`: filtrowanie pilnych zleceń:
  - `CREATE INDEX ix_orders_due_date ON orders (due_date);`
- `ix_batch_audit_batch_changed_at`: historia zmian batcha:
  - `CREATE INDEX ix_batch_audit_batch_changed_at ON batch_audit_log (batch_id, changed_at DESC);`

## 4. Zasady PostgreSQL (RLS)

- **RLS nie dotyczy** (system single-tenant). Autoryzacja odbywa się na poziomie aplikacji przez **ASP.NET Core Identity** i role (**Manager/Operator**).

## 5. Dodatkowe uwagi / decyzje projektowe

- **Etapy i statusy**: w DB zastosowano CHECK, a reguły przejść są egzekwowane w logice aplikacji (wymóg: etapy tylko do przodu; statusy zmieniane „o 1 krok”).
- **Walidacja `batch_split_rules` (bez nakładania zakresów)**:
  - Minimum: walidacja w aplikacji (Panel Managera) przed zapisem.
  - Opcjonalnie (mocniej, po stronie DB): constraint na range z wykluczeniem nakładania dla aktywnych reguł (wymaga `btree_gist` i użycia `int4range`).
- **Ukrywanie po wysyłce**: zamiast kasowania, `projects.is_completed=true` + `completed_at` (historia read-only).
- **N+1 w EF Core**: listy Kanban/Dashboard powinny używać projekcji i `Include` tylko tam, gdzie konieczne; liczniki (`InProgress`) najlepiej liczyć agregacją po indeksie partial.
