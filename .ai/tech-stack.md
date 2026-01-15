Frontend/UI: Blazor Server (SSR) + MudBlazor (komponenty) + Bootstrap (layout/utilities – opcjonalnie, jeśli MudBlazor wystarczy, można zrezygnować z Bootstrap).
Backend: .NET 8 (ASP.NET Core) w tym samym hostcie co UI (jeden projekt/aplikacja na MVP).
Auth/RBAC: ASP.NET Core Identity (cookie auth) + role Manager/Operator + autoryzacja widoków/akcji (np. [Authorize(Roles="Manager")] dla Panelu i “Wyślij do klienta”).
Database/ORM: PostgreSQL + EF Core Code‑First + migracje; model pod: Orders/Projects, Batches, ProductFormats, BatchSplitRules, AuditLog.
Audyt/Historia: tabela zdarzeń (kto/kiedy/co) dla zmian statusu/etapu; zapisywana transakcyjnie razem ze zmianą.
Real-time: Blazor Server już działa na SignalR (utrzymuje “circuit”); dodatkowe hube’y tylko jeśli potrzebujesz broadcastu zmian między użytkownikami/zakładkami ponad standardową synchronizację UI.
Observability: logowanie (np. Serilog) + podstawowe metryki/healthchecks.
CI/CD: GitHub Actions: dotnet restore/build/test + publikacja artefaktu + deploy do Azure App Service; migracje uruchamiane kontrolowanie (np. step “apply migrations” lub start-up migration w środowiskach nie-produkcyjnych).
Hosting: Azure App Service + Azure Database for PostgreSQL (lub Postgres w innym dostawcy na MVP).