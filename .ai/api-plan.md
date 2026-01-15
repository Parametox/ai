# REST API Plan (Service Layer Contract for Blazor Server Monolith)

> Uwaga: W MVP nie wystawiamy osobnego publicznego REST API. Ten dokument opisuje **kontrakt operacji** w stylu REST (Method/URL + DTO), który mapuje się na **metody warstwy serwisów** wywoływane in-process przez Blazor Server.
>
> Docelowy plan serwisów (C# + DI + walidacje/RBAC) jest opisany w: `.ai/service-layer-plan.md`.

## 1. Resources
- **ProductFormat** → `product_formats`
- **Order** → `orders`
- **Project** → `projects`
- **Batch** → `batches`
- **BatchSplitRule** → `batch_split_rules`
- **BatchAuditEvent** → `batch_audit_log`
- **Identity (User/Role)** → ASP.NET Core Identity tables (e.g., `AspNetUsers`, `AspNetRoles`, …) *(infrastructure; not domain CRUD in MVP)*

## 2. Endpoints

### Conventions (applies to all endpoints)
- **Auth**: Cookie authentication via ASP.NET Core Identity.
- **Authorization**:
  - `Manager` (Manager): Kanban + Manager Panel + “Ship to customer”.
  - `Operator`: Kanban only.
- **Error envelope (conceptual)**:

```json
{
  "error": {
    "code": "ValidationError",
    "message": "Human readable summary",
    "details": [{ "field": "quantity", "message": "Must be between 1 and 100000" }]
  }
}
```

- **Result codes mapping** (HTTP-like → service layer result):
  - `200 OK`: success (query/update)
  - `201 Created`: created
  - `400 Bad Request`: malformed input
  - `401 Unauthorized`: not logged in
  - `403 Forbidden`: logged in but not allowed (role/policy)
  - `404 Not Found`: resource missing
  - `409 Conflict`: unique constraint / invalid state transition / concurrency conflict
  - `422 Unprocessable Entity`: validation/business rule violation
  - `500 Internal Server Error`: unexpected error

---

### 2.1 Product formats (`product_formats`) — Manager Panel

#### List product formats
- **Method**: GET
- **URL**: `/product-formats`
- **Description**: List formats for dropdowns and administration.
- **Query params**:
  - `isActive` (bool, optional)
  - `q` (string, optional) – search by name (starts-with/contains)
  - `page` (int, optional), `pageSize` (int, optional)
- **Response (200)**:

```json
{
  "items": [
    { "id": 1, "name": "A6 (10x15 cm)", "isActive": true, "createdAt": "2026-01-12T10:00:00Z" }
  ],
  "page": 1,
  "pageSize": 50,
  "total": 3
}
```

- **Errors**: `401`, `403` *(Manager-only for full list; optional: allow Operator to list active formats only for UI lookups)*.

#### Create product format
- **Method**: POST
- **URL**: `/product-formats`
- **Description**: Create new product format.
- **Request**:

```json
{ "name": "A5 (14.8x21 cm)", "isActive": true }
```

- **Response (201)**:

```json
{ "id": 4, "name": "A5 (14.8x21 cm)", "isActive": true, "createdAt": "2026-01-15T12:00:00Z" }
```

- **Errors**:
  - `409`: name already exists (`uq_product_formats_name`)
  - `422`: invalid name (empty/too long per application constraints)

#### Update product format
- **Method**: PUT
- **URL**: `/product-formats/{id}`
- **Description**: Rename / activate-deactivate.
- **Request**:

```json
{ "name": "Square (15x15 cm)", "isActive": true }
```

- **Response (200)**:

```json
{ "id": 2, "name": "Square (15x15 cm)", "isActive": true, "createdAt": "2026-01-12T10:00:00Z" }
```

- **Errors**: `404`, `409`, `422`.

#### Delete product format (optional)
- **Method**: DELETE
- **URL**: `/product-formats/{id}`
- **Description**: For MVP prefer **soft deactivation** (`is_active=false`) to avoid FK issues with orders.
- **Response (204)**: no content
- **Errors**:
  - `409`: cannot delete due to existing orders referencing the format

---

### 2.2 Orders (`orders`) — Manager Panel

#### Create order (and auto-create project + batches)
- **Method**: POST
- **URL**: `/orders`
- **Description**: Create an order; system creates a project and batches based on active split rules.
- **Request**:

```json
{
  "orderNumber": "ORD-2026-0001",
  "quantity": 200,
  "productFormatId": 1,
  "dueDate": "2026-02-01"
}
```

- **Response (201)**:

```json
{
  "order": {
    "id": 10,
    "orderNumber": "ORD-2026-0001",
    "quantity": 200,
    "productFormatId": 1,
    "dueDate": "2026-02-01",
    "createdAt": "2026-01-15T12:00:00Z"
  },
  "project": {
    "id": 10,
    "orderId": 10,
    "projectNumber": "PRJ-2026-0001",
    "isCompleted": false,
    "createdAt": "2026-01-15T12:00:00Z"
  },
  "batches": [
    { "id": 100, "projectId": 10, "batchNo": 1, "quantity": 60, "status": "New", "stage": 1, "createdAt": "2026-01-15T12:00:00Z", "updatedAt": "2026-01-15T12:00:00Z" },
    { "id": 101, "projectId": 10, "batchNo": 2, "quantity": 60, "status": "New", "stage": 1, "createdAt": "2026-01-15T12:00:00Z", "updatedAt": "2026-01-15T12:00:00Z" },
    { "id": 102, "projectId": 10, "batchNo": 3, "quantity": 60, "status": "New", "stage": 1, "createdAt": "2026-01-15T12:00:00Z", "updatedAt": "2026-01-15T12:00:00Z" },
    { "id": 103, "projectId": 10, "batchNo": 4, "quantity": 20, "status": "New", "stage": 1, "createdAt": "2026-01-15T12:00:00Z", "updatedAt": "2026-01-15T12:00:00Z" }
  ]
}
```

- **Errors**:
  - `403`: Operator not allowed
  - `409`: `orderNumber` already exists (`uq_orders_order_number`)
  - `422`: validation failures (quantity range; due date rule; split rules missing/invalid; resulting batch sizes invalid)

#### List orders (history)
- **Method**: GET
- **URL**: `/orders`
- **Description**: List orders for history view (Manager Panel).
- **Query params**:
  - `q` (string, optional): orderNumber/projectNumber search
  - `dueFrom`, `dueTo` (date, optional)
  - `page`, `pageSize`
- **Response (200)**:

```json
{
  "items": [
    { "id": 10, "orderNumber": "ORD-2026-0001", "quantity": 200, "dueDate": "2026-02-01", "productFormatName": "A6 (10x15 cm)", "createdAt": "2026-01-15T12:00:00Z" }
  ],
  "page": 1,
  "pageSize": 50,
  "total": 1
}
```

- **Errors**: `401`, `403` (Manager-only).

#### Get order details
- **Method**: GET
- **URL**: `/orders/{id}`
- **Description**: Order + linked project summary.
- **Response (200)**:

```json
{
  "id": 10,
  "orderNumber": "ORD-2026-0001",
  "quantity": 200,
  "dueDate": "2026-02-01",
  "productFormat": { "id": 1, "name": "A6 (10x15 cm)" },
  "project": { "id": 10, "projectNumber": "PRJ-2026-0001", "isCompleted": false }
}
```

- **Errors**: `404`, `401`, `403` (Manager-only).

---

### 2.3 Projects (`projects`) — Kanban + Manager actions

#### List active projects (optional)
- **Method**: GET
- **URL**: `/projects`
- **Description**: Active projects for quick navigation. Kanban is primarily batch-based.
- **Query params**:
  - `isCompleted=false` (default)
  - `page`, `pageSize`
- **Response (200)**:

```json
{
  "items": [
    { "id": 10, "projectNumber": "PRJ-2026-0001", "orderNumber": "ORD-2026-0001", "dueDate": "2026-02-01", "isCompleted": false }
  ],
  "page": 1,
  "pageSize": 50,
  "total": 1
}
```

#### Get project details (includes batches + readiness to ship)
- **Method**: GET
- **URL**: `/projects/{id}`
- **Description**: Project details view (Operator/Manager).
- **Response (200)**:

```json
{
  "id": 10,
  "projectNumber": "PRJ-2026-0001",
  "order": { "id": 10, "orderNumber": "ORD-2026-0001", "quantity": 200, "dueDate": "2026-02-01", "productFormatName": "A6 (10x15 cm)" },
  "isCompleted": false,
  "completion": { "canShip": false, "reason": "All batches must be at stage=5 (Shipping) and status=Done." },
  "batches": [
    { "id": 100, "batchNo": 1, "quantity": 60, "status": "InProgress", "stage": 2, "progressPercent": 40, "updatedAt": "2026-01-15T12:10:00Z" }
  ]
}
```

- **Errors**: `404`, `401`.

#### Ship project to customer (mark completed)
- **Method**: POST
- **URL**: `/projects/{id}/ship`
- **Description**: Manager action “Wyślij do klienta”. Marks project completed and removes it from active Kanban.
- **Request** (optional):

```json
{ "note": "Optional shipping note" }
```

- **Response (200)**:

```json
{
  "id": 10,
  "isCompleted": true,
  "completedAt": "2026-01-15T13:00:00Z",
  "completedByUserId": "identity-user-id"
}
```

- **Errors**:
  - `403`: Operator not allowed
  - `409` / `422`: cannot ship because not all batches meet criteria
  - `404`: project not found

---

### 2.4 Batches (`batches`) — Kanban operations

#### Kanban list (active batches)
- **Method**: GET
- **URL**: `/kanban/batches`
- **Description**: Main Kanban read model: batches joined with order/project context (active projects only).
- **Query params**:
  - `status` (New|InProgress|Done, optional)
  - `stage` (1..5, optional)
  - `q` (string, optional): orderNumber/projectNumber
  - `sort` (e.g., `dueDateAsc`, `dueDateDesc`, `updatedAtDesc`, optional)
  - `page`, `pageSize`
- **Response (200)**:

```json
{
  "items": [
    {
      "batchId": 100,
      "projectId": 10,
      "projectNumber": "PRJ-2026-0001",
      "orderNumber": "ORD-2026-0001",
      "dueDate": "2026-02-01",
      "batchNo": 1,
      "quantity": 60,
      "status": "InProgress",
      "stage": 2,
      "progressPercent": 40,
      "updatedAt": "2026-01-15T12:10:00Z"
    }
  ],
  "inProgressCount": 21,
  "warnings": [
    { "code": "InProgressSoftLimitExceeded", "message": "InProgress batches count is 21 (soft limit is 20)." }
  ],
  "page": 1,
  "pageSize": 50,
  "total": 1
}
```

- **Errors**: `401`.

#### Update batch status
- **Method**: PATCH
- **URL**: `/batches/{id}/status`
- **Description**: Change status (New → InProgress → Done). Persists audit log entry.
- **Request**:

```json
{ "newStatus": "InProgress" }
```

- **Response (200)**:

```json
{
  "batchId": 100,
  "oldStatus": "New",
  "newStatus": "InProgress",
  "stage": 2,
  "updatedAt": "2026-01-15T12:10:00Z",
  "inProgressCount": 21,
  "warnings": [
    { "code": "InProgressSoftLimitExceeded", "message": "InProgress batches count is 21 (soft limit is 20)." }
  ]
}
```

- **Errors**:
  - `404`: batch not found
  - `409` / `422`: invalid status transition (business rule)
  - `409`: concurrency conflict (optional, via `xmin`/ETag-like mechanism)

#### Update batch stage
- **Method**: PATCH
- **URL**: `/batches/{id}/stage`
- **Description**: Move stage forward only (1..5). Persists audit log entry.
- **Request**:

```json
{ "newStage": 3 }
```

- **Response (200)**:

```json
{ "batchId": 100, "oldStage": 2, "newStage": 3, "status": "InProgress", "progressPercent": 60, "updatedAt": "2026-01-15T12:20:00Z" }
```

- **Errors**:
  - `404`
  - `422`: newStage out of range (DB constraint is 1..5)
  - `409` / `422`: stage cannot move backwards (business rule)

---

### 2.5 Batch split rules (`batch_split_rules`) — Manager Panel

#### List split rules
- **Method**: GET
- **URL**: `/batch-split-rules`
- **Description**: Read and manage active rules. Applies only to newly created orders.
- **Query params**: `isActive` (optional)
- **Response (200)**:

```json
{
  "items": [
    { "id": 1, "minQty": 1, "maxQty": 200, "percent": 30.0, "minBatchSize": 1, "maxBatchesPerProject": null, "isActive": true, "createdAt": "2026-01-12T10:00:00Z" }
  ]
}
```

#### Create / update split rule
- **Method**: POST / PUT
- **URL**: `/batch-split-rules` / `/batch-split-rules/{id}`
- **Description**: Manage thresholds and percent. Must prevent overlaps among active ranges.
- **Request**:

```json
{ "minQty": 201, "maxQty": 500, "percent": 25.0, "minBatchSize": 1, "maxBatchesPerProject": null, "isActive": true }
```

- **Response (200/201)**:

```json
{ "id": 2, "minQty": 201, "maxQty": 500, "percent": 25.0, "minBatchSize": 1, "maxBatchesPerProject": null, "isActive": true, "createdAt": "2026-01-15T12:00:00Z" }
```

- **Errors**:
  - `422`: invalid numeric values (minQty>=1, percent (0..100], etc.)
  - `409` / `422`: overlaps an existing active rule range (application validation)

#### Deactivate split rule
- **Method**: PATCH
- **URL**: `/batch-split-rules/{id}/deactivate`
- **Description**: Sets `is_active=false`.
- **Response (200)**: updated rule
- **Errors**: `404`

---

### 2.6 Audit log (`batch_audit_log`) — Manager Panel / Project view

#### List audit events for a batch
- **Method**: GET
- **URL**: `/batches/{id}/audit`
- **Description**: Show who changed status/stage and when.
- **Query params**: `page`, `pageSize`
- **Response (200)**:

```json
{
  "items": [
    {
      "id": 1000,
      "batchId": 100,
      "changedAt": "2026-01-15T12:10:00Z",
      "changedByUserId": "identity-user-id",
      "oldStatus": "New",
      "newStatus": "InProgress",
      "oldStage": 1,
      "newStage": 2
    }
  ],
  "page": 1,
  "pageSize": 50,
  "total": 1
}
```

- **Errors**: `404`, `401`, `403` *(optional: allow Operator to view audit for transparency; otherwise Manager-only)*.

---

### 2.7 Dashboard (Manager Panel)

#### Get dashboard metrics
- **Method**: GET
- **URL**: `/dashboard`
- **Description**: Operational metrics:
  - counts by status (New/InProgress/Done)
  - counts by stage (1..5)
  - urgent orders list (due < 7 days)
  - average order/project lead time *(if data available)*
- **Response (200)**:

```json
{
  "countsByStatus": { "New": 10, "InProgress": 21, "Done": 5 },
  "countsByStage": { "1": 3, "2": 8, "3": 12, "4": 9, "5": 4 },
  "urgentOrders": [
    { "orderId": 10, "orderNumber": "ORD-2026-0001", "dueDate": "2026-01-20", "projectId": 10, "projectNumber": "PRJ-2026-0001" }
  ],
  "warnings": [
    { "code": "InProgressSoftLimitExceeded", "message": "InProgress batches count is 21 (soft limit is 20)." }
  ]
}
```

- **Errors**: `401`, `403` (Manager-only).

---

## 3. Authentication and authorization
- **Authentication mechanism**: ASP.NET Core Identity with **cookie authentication** (Blazor Server friendly).
- **Roles**:
  - `Manager`: full access to Manager Panel (CRUD formats, CRUD split rules, dashboard, order creation/history) and project shipping.
  - `Operator`: Kanban read/write on batches (stage/status changes) and project details read.
- **Authorization boundaries**:
  - **Manager-only operations**: create order; manage formats; manage split rules; ship project; dashboard; history views.
  - **Shared operations**: Kanban list; batch status/stage updates; project details.
- **Implementation notes**:
  - Use `[Authorize]` and `[Authorize(Roles="Manager")]`/policies at service entry points (and/or pages/components).
  - Prefer **policy-based checks** for fine-grained rules (e.g., `CanShipProject`).
  - Protect state-changing operations with standard ASP.NET Core antiforgery patterns where applicable.

## 4. Validation and business logic

### 4.1 Resource validation (database constraints + application rules)

#### ProductFormat
- **DB**: unique `name` (`uq_product_formats_name`), `is_active` default true.
- **App**: `name` required, trimmed; enforce reasonable max length.

#### Order
- **DB**:
  - unique `order_number` (`uq_orders_order_number`)
  - `quantity` check 1..100000 (`ck_orders_quantity_range`)
- **App**:
  - `due_date >= today + 7 days` (rule explicitly kept in app)
  - `product_format_id` must exist and be active (recommended)

#### Project
- **DB**:
  - unique `project_number` (`uq_projects_project_number`)
  - completion consistency (`ck_projects_completed_consistency`)
- **App**:
  - created automatically from order
  - shipping action sets `is_completed=true`, `completed_at`, `completed_by_user_id`

#### Batch
- **DB**:
  - unique `(project_id, batch_no)` (`uq_batches_project_batch_no`)
  - `quantity > 0` (`ck_batches_quantity_positive`)
  - `status` in {New, InProgress, Done} (`ck_batches_status_enum`)
  - `stage` in 1..5 (`ck_batches_stage_range`)
- **App**:
  - stage can only move forward (no back)
  - status transitions: **New → InProgress → Done** (assumption: no backward transitions in MVP)
  - **Soft limit**: if global `InProgress` count exceeds 20, return warning (no blocking)

#### BatchSplitRule
- **DB**: numeric constraints (minQty>=1, maxQty>=minQty or null, percent (0..100], etc.)
- **App**:
  - prevent overlaps among active rules (single-tenant; validate in Manager Panel)
  - used to compute batch sizes:
    - `baseSize = ceil(orderQty * percent / 100)`
    - create sequential batches of `baseSize`, last batch = remainder
    - ensure sum of batch quantities = order quantity
    - enforce `min_batch_size` and `max_batches_per_project` if configured

#### BatchAuditEvent
- **DB**:
  - must capture a change (`ck_batch_audit_has_change`)
  - domain constraints for status/stage
- **App**:
  - write audit entries **transactionally** with the batch update

### 4.2 Core business rules (PRD-driven)
- **Kanban scope**: show only projects where `projects.is_completed=false` (active work only).
- **Shipping gate (“Ship to customer”)**:
  - Manager-only action.
  - Allowed only when **all project batches** satisfy:
    - `stage = 5 (Shipping)` **and**
    - `status = Done`
- **No self-registration**: seed initial accounts (Manager + Operator); UI only supports login.
- **N+1 avoidance / performance**:
  - Kanban list should use **projection** (single query join) rather than per-row loads.
  - Use partial indexes for active projects and `InProgress` counting where applicable.
  - `InProgress` count should be computed via aggregate query (and can be cached briefly per request/UI refresh).

