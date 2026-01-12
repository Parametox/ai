# Aplikacja - KanbanLite MVP

## Główny problem
Głównym problemem jest konieczność ręcznego zarządzania zadaniami związanymi z produkcją kartek świątecznych w fabryce. Obecny proces jest czasochłonny, podatny na błędy oraz utrudnia monitorowanie postępów prac na każdym etapie. Docelowo system ma umożliwić zarządzanie produkcją obejmującą 5 kluczowych etapów: projektowanie, druk, cięcie, pakowanie oraz wysyłkę, automatyzując przepływ zadań i zwiększając kontrolę nad realizacją zleceń.


## Najmniejszy zestaw funkcjonalności
Co wchodzi w skład MVP?
- Zarządzanie produkcją zleceń klienta w sposób uporządkowany i skalowalny:
  - Rejestracja nowego zlecenia za pomocą dedykowanego formularza, obejmującego:
    - Liczbę zamawianych sztuk
    - Wybór formatu produktu (z listy rozwijanej/lookupu)
    - Termin realizacji (wymagane zawężenie, umożliwiające automatyczną kontrolę harmonogramu)
  - Automatyczny podział zlecenia na partie produkcyjne ("batch'e"), umożliwiający równoległe przetwarzanie poszczególnych etapów:
    - Możliwość przekazywania do kolejnego etapu (np. cięcie, pakowanie) już częściowo zrealizowanych batchy (np. po wydrukowaniu 10% zamówienia), co usprawnia przepływ produkcji i skraca czas realizacji
    - Etapy produkcji objęte automatyzacją:
      - Projektowanie
      - Druk
      - Cięcie
      - Pakowanie
      - Wysyłka
  - Wydajny monitoring statusu każdej partii – każdy batch posiada indywidualny status postępu
  - Status całego zlecenia/projektu jest dynamicznie wyliczany na podstawie statusów wszystkich powiązanych batchy
  - Zapewnienie, że wysyłka realizowana jest dopiero po zakończeniu wszystkich operacji na batchach przypisanych do danego projektu
- Przycisk "Wyślij do klienta" na projekcie będzie dostępny dopiero wtedy, gdy wszystkie batch'e przypisane do tego projektu zostaną zamknięte i cały druk będzie gotowy do wysyłki. System automatycznie monitoruje statusy poszczególnych batchy oraz etap "druk", a w momencie spełnienia tych warunków odblokowuje możliwość wysyłki do klienta.
- Prosty mechanizm rejestracji i logowania użytkownika, umożliwiający dostęp do systemu wyłącznie uprawnionym osobom.

## Co NIE wchodzi w zakres MVP
MVP nie obejmuje żadnych funkcjonalności wykraczających poza powyżej opisany zakres.

## Kryteria sukcesu
Moim celem jest, aby użytkownik mógł w prosty sposób zadeklarować podczas wypełniania formularza na przykład zamówienie 10 tysięcy kartek, a system automatycznie podzielił to zamówienie na mniejsze partie (batch'e). Chcę mieć możliwość monitorowania statusu i etapu realizacji każdego batcha osobno – na każdym batchu powinien być widoczny liczbowy stan partii, pole wyboru z aktualnym etapem produkcji (stage) oraz pole wskazujące stopień realizacji. Zależy mi, aby przycisk „Wyślij do klienta” na głównym projekcie był dostępny dopiero wtedy, gdy wszystkie batch'e zostaną przesunięte do etapu wysyłki, co zapewni, że żaden etap produkcji nie zostanie pominięty przed realizacją wysyłki.