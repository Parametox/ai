# Instrukcja uruchomienia testów E2E i CI/CD

## 📋 Podsumowanie utworzonych plików

### Struktura projektu testów E2E
```
tests/KanbanLite.E2E.Tests/
├── KanbanLite.E2E.Tests.csproj
├── Infrastructure/
│   ├── E2ETestBase.cs           # Bazowa klasa testów
│   ├── PlaywrightAssertions.cs  # Helper dla Expect()
│   ├── PlaywrightCollection.cs  # Kolekcja xUnit
│   ├── PlaywrightFixture.cs     # Fixture Playwright
│   └── TestConfig.cs            # Konfiguracja (URL, credentials)
├── CriticalPaths/
│   ├── AuthenticationTests.cs   # Testy logowania
│   ├── AuthorizationTests.cs    # Testy autoryzacji (RBAC)
│   ├── CreateOrderTests.cs      # Testy tworzenia zleceń
│   ├── FullWorkflowE2ETests.cs  # ⭐ Pełny test E2E (wymagany do zaliczenia)
│   └── KanbanWorkflowTests.cs   # Testy Kanban
└── AdditionalPaths/
    ├── ManagerDashboardTests.cs # Testy Dashboard
    └── ProjectDetailsTests.cs   # Testy szczegółów projektu
```

### Dodane atrybuty `data-testid` do komponentów
- `Login.razor` - pola logowania
- `NavMenu.razor` - nawigacja
- `CreateOrder.razor` - formularz zlecenia
- `KanbanTable.razor` - tabela batchy
- `BatchStatusSelect.razor` - select statusu
- `BatchStageSelect.razor` - select etapu
- `Project.razor` - przycisk wysyłki

---

## 🚀 Uruchomienie testów lokalnie

### Krok 1: Zainstaluj przeglądarki Playwright (jednorazowo)

```powershell
cd D:\ProjZaliczeniowyAI
dotnet build tests\KanbanLite.E2E.Tests
pwsh tests\KanbanLite.E2E.Tests\bin\Debug\net9.0\playwright.ps1 install chromium
```

### Krok 2: Uruchom aplikację (w pierwszym terminalu)

```powershell
cd D:\ProjZaliczeniowyAI
dotnet run --project src\Web
```

Poczekaj aż pojawi się: `Now listening on: http://localhost:5145`

### Krok 3: Uruchom testy E2E (w drugim terminalu)

```powershell
cd D:\ProjZaliczeniowyAI
dotnet test tests\KanbanLite.E2E.Tests --verbosity normal
```

### Opcje uruchomienia

```powershell
# Uruchom z widoczną przeglądarką (debugging)
$env:PLAYWRIGHT_HEADLESS = "false"
dotnet test tests\KanbanLite.E2E.Tests

# Uruchom z opóźnieniem (slow motion)
$env:PLAYWRIGHT_SLOWMO = "500"
dotnet test tests\KanbanLite.E2E.Tests

# Uruchom tylko jeden test
dotnet test tests\KanbanLite.E2E.Tests --filter "FullName~FullWorkflow"
```

---

## 🔧 Konfiguracja GitHub Actions CI/CD

### Krok 1: Utwórz repozytorium na GitHub

1. Idź na https://github.com/new
2. Nazwa: `KanbanLite` (lub dowolna)
3. Widoczność: **Private** (dla projektu zaliczeniowego)
4. Nie inicjalizuj README (masz już swoje)

### Krok 2: Połącz lokalne repo z GitHub

```powershell
cd D:\ProjZaliczeniowyAI

# Jeśli nie masz jeszcze git init
git init

# Dodaj remote
git remote add origin https://github.com/TWOJ_USERNAME/KanbanLite.git

# Dodaj wszystkie pliki
git add .

# Commit
git commit -m "Add E2E tests with Playwright and CI/CD pipeline"

# Push do main/master
git push -u origin main
# lub
git push -u origin master
```

### Krok 3: Sprawdź Actions

1. Idź na GitHub → Twoje repo → zakładka **Actions**
2. Pipeline powinien się automatycznie uruchomić
3. Kliknij w workflow aby zobaczyć logi

### Krok 4: Naprawa problemów z CI

#### Problem: Brak użytkowników testowych w bazie CI

Workflow zawiera SQL seed, ale wymaga prawidłowego hasha haseł. 
Alternatywnie możesz użyć migracji seedującej użytkowników.

W pliku `.github/workflows/ci.yml` sekcja "Seed test users" może wymagać dostosowania do Twojego schematu Identity.

#### Problem: Aplikacja nie startuje

Sprawdź logi w sekcji "Start application in background" i "Wait for application to be ready".

---

## 📝 Uwagi dotyczące testów

### Test wymagany do zaliczenia

Najważniejszy test to `Manager_FullWorkflow_CreateOrder_ProcessBatches_ShipToClient` 
w pliku `FullWorkflowE2ETests.cs`. Testuje on:

1. ✅ Logowanie (mechanizm kontroli dostępu)
2. ✅ Tworzenie zlecenia (Create)
3. ✅ Przeglądanie Kanban (Read)
4. ✅ Zmiana statusu/etapu (Update)
5. ✅ Wysyłka projektu (logika biznesowa)

### Dane testowe

Testy używają użytkowników:
- **Manager**: `menago` / `menago`
- **Operator**: `operator` / `operator`

Upewnij się, że ci użytkownicy istnieją w Twojej lokalnej bazie.

---

## 🐛 Troubleshooting

### "Playwright browsers not installed"
```powershell
pwsh tests\KanbanLite.E2E.Tests\bin\Debug\net9.0\playwright.ps1 install
```

### "Connection refused" na localhost:5145
- Upewnij się, że aplikacja jest uruchomiona
- Sprawdź czy port 5145 nie jest zajęty

### Testy timeout
- Zwiększ timeout w `TestConfig.cs`
- Uruchom z `PLAYWRIGHT_HEADLESS=false` aby zobaczyć co się dzieje

### GitHub Actions - "Service postgres not healthy"
- To czasami się zdarza, workflow się automatycznie ponowi

---

## 📚 Dokumentacja

- [Playwright for .NET](https://playwright.dev/dotnet/docs/intro)
- [GitHub Actions](https://docs.github.com/en/actions)
- [xUnit](https://xunit.net/docs/getting-started/netcore/cmdline)
