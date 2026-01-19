Należy rozbudować Product Requirements Document (PRD) oraz user stories o funkcjonalność logowania użytkowników do aplikacji. Rejestracja nowych użytkowników NIE jest przewidziana – zakładamy, że konta już istnieją w systemie.

Użytkownicy po uruchomieniu aplikacji zawsze trafiają na ekran logowania. Widoki dostępne są wyłącznie po poprawnej autoryzacji. Przekierowanie: po zalogowaniu użytkownik przechodzi na stronę główną ("/"). Użytkownicy niezalogowani nie mają dostępu do żadnych stron aplikacji poza logowaniem.

Przewidziane role: <roles><role>manager</role><role>operator</role></roles>. Każda rola ma pełnić inne funkcje określone w PRD.

Zaktualizuj (lub dodaj) poniższe sekcje PRD:
- <auth>
    <login enabled="true"/>
    <register enabled="false"/>
    <roles>
        <role name="manager"/>
        <role name="operator"/>
    </roles>
    <forced-authentication>true</forced-authentication>
    <default-redirect-after-login>/</default-redirect-after-login>
    <access>
        <guests denied="true"/>
    </access>
</auth>

- User stories – dodaj:
    <user_story>
        Korzystający z aplikacji
        po uruchomieniu zobaczyć ekran logowania, jeżeli nie jestem zalogowany
        nieuprawnione osoby nie mają dostępu do funkcji aplikacji
    </user_story>
    <user_story>
        Zalogowany użytkownik
        po wpisaniu poprawnych danych być przekierowany na stronę główną (" / ")
        mogę korzystać z aplikacji zgodnie z uprawnieniami
    </user_story>
    <user_story>
        Zalogowany użytkownik
        mieć różne uprawnienia w zależności od roli ("manager" lub "operator")
        mogę korzystać z funkcji zgodnie z przydzieloną rolą
    </user_story>

<types>
- Typy:
    - Należy dodać/zmodyfikować definicje typów związanych z autoryzacją w pliku `@Types.cs`, m.in.:
        - `User` (z polami: id, username, role, ...),
        - `LoginRequest` (login/username, password),
        - `AuthResponse` (np. identyfikator sesji, payload użytkownika, rola użytkownika, daty ważności, ...)
        - `Role` (typ wyliczeniowy: manager, operator)
</types>
<api>
- API/service layer:
    - Należy dodać do @api-plan.md metody/serwisy związane z logowaniem i autoryzacją użytkownika, jeśli jeszcze ich tam nie ma.
    - Przypominam: nie jest to klasyczne REST API – w projekcie Blazor Server całość komunikacji odbywa się poprzez dependency injection (DI), a serwisy wywoływane są bezpośrednio in-process.
    - Wymagane kontrakty serwisów/metod:
        - `Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)` — przyjmuje creds, zwraca AuthResponse z danymi użytkownika, rolą, identyfikatorem sesji. Obsługuje wyjątki (błędny login/hasło).
        - `Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default)` — zwraca aktualnie zalogowanego użytkownika i jego uprawnienia na podstawie sesji/contextu.
        - `Task LogoutAsync(CancellationToken ct = default)` — wylogowanie/wyczyszczenie sesji.
    - Dodaj odpowiednie DTO do specyfikacji (@Types.cs / C#), takie jak: `UserDto`, `LoginRequest`, `AuthResponse`, `Role` (enum).
    - Zadbaj o odpowiednią prezentację tych metod w @api-plan.md w stylu: opis, kontrakt C#, wyjątki, uwagi (niedostępność rejestracji).
</api>


<references>
- @prd.md – główny Product Requirements Document, sekcja auth
- @user-stories.md – sekcja z user stories związanymi z logowaniem
- @ui-plan.md – projekt UI ekranu logowania
- @api-plan.md – opis endpointów do logowania, autoryzacji
- @Types.cs – definicje typu User, LoginRequest, AuthResponse, Role
</references>

