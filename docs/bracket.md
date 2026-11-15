# Drabinka pucharowa

Drabinkę (eliminacja po jednej porażce) generuje `KnockoutBracketGenerator.Generate` z przestrzeni nazw `TurniejePro.Domain.Scheduling`. Metoda przyjmuje identyfikatory drużyn w kolejności rozstawienia i zwraca listę rund (`BracketRound`), a w każdej mecze (`BracketMatch`).

## Zasady

- **Rozstawienie:** kolejność podanych drużyn to rozstawienie, pierwsza jest najwyżej rozstawiona. Wynik zależy tylko od tej kolejności, bez losowania. Jeśli rozstawienie ma być losowe, wołający tasuje listę przed wywołaniem.
- **Pary pierwszej rundy** powstają według standardowej drabinki, np. dla 8 drużyn: 1–8, 4–5, 2–7, 3–6. Dwie najwyżej rozstawione drużyny mogą spotkać się najwcześniej w finale. W parze pierwsza (wyżej rozstawiona) jest gospodarzem.
- **Wolne losy:** gdy liczba drużyn nie jest potęgą dwójki, drabinka jest uzupełniana do najbliższej potęgi (np. 5 drużyn do 8), a brakujące miejsca to wolne losy. Dostają je najwyżej rozstawione drużyny. Mecz z wolnym losem (`IsBye`) ma tylko gospodarza, który jest od razu zwycięzcą (`WinnerId`) i jest wpisany do drugiej rundy.
- **Liczby:** liczba rund to `ceil(log2 n)`, a rozegranych meczów (bez wolnych losów) jest `n - 1`.
- Miejsca w kolejnych rundach, na które jeszcze nikt nie awansował, mają `null` w `HomeTeamId` lub `AwayTeamId`. Zwycięzca meczu z pozycji nieparzystej trafia do następnej rundy jako gospodarz, z parzystej jako gość.

## Rozstawienie drużyn przy generowaniu terminarza

Generator drabinki dostaje drużyny już w kolejności rozstawienia. Tę kolejność ustala `TeamSeeder.Seed` (wywoływany przez `ScheduleService`) według wybranego trybu, a nie według tego, w jakiej kolejności baza zwróciła drużyny:

| Tryb (`SeedingMode`) | Kolejność rozstawienia |
|---|---|
| `Registration` (domyślny, **stała kolejność**) | według czasu zgłoszenia drużyny (`RegisteredAt`), a przy identycznym czasie według identyfikatora. Najwcześniej zgłoszona drużyna jest rozstawiona najwyżej. |
| `Random` (**losowanie**) | tasowanie Fishera-Yatesa kryptograficznym generatorem liczb losowych, zaczynając od kolejności zgłoszeń |
| `Manual` (**ręczne**) | kolejność podana przez organizatora: dokładnie wszystkie zaakceptowane drużyny, każda raz |

Rozstawienie dotyczy zaakceptowanych drużyn turnieju. W pucharze wyznacza pary pierwszej rundy i drużyny z wolnym losem, a w lidze (round-robin) układ kolejek.

### API

`POST /api/v1/tournaments/{id}/schedule` ma opcjonalne body (`GenerateScheduleRequest`). Bez body, z pustym obiektem `{}` albo bez pola `seeding` obowiązuje kolejność zgłoszeń, czyli to samo co dotąd, ale teraz z jawnie ustaloną kolejnością.

```json
{ "seeding": "Registration" }
{ "seeding": "Random" }
{ "seeding": "Manual", "teamOrder": ["<id drużyny 1>", "<id drużyny 2>", "..."] }
```

- `teamOrder` jest wymagane tylko przy `Manual` i ignorowane przy pozostałych trybach.
- Błędy ręcznego rozstawienia dają `400` (`BusinessRuleException`): brak `teamOrder`, ta sama drużyna dwa razy, drużyna spoza zaakceptowanych w turnieju, brak którejś zaakceptowanej drużyny.
- Nieznana wartość `seeding` (np. `"Banana"`, `99`, `0`) daje `400` (błąd walidacji).
- Autoryzacja bez zmian: tylko organizator turnieju albo `Admin` (`403` dla pozostałych, `401` bez tokenu), a ponowne generowanie to `409`.

## Wymagania wejścia

Co najmniej 2 drużyny, bez powtórzeń (inaczej `ArgumentException`), lista różna od `null`.

## Przykłady

Pięć drużyn A–E (rozstawionych w tej kolejności), `?` to miejsce czekające na zwycięzcę:

| Runda | Mecze |
|---|---|
| 1 | A (wolny los), D–E, B (wolny los), C (wolny los) |
| 2 | A–?, B–C |
| 3 (finał) | ?–? |

Trzy drużyny A–C: runda 1: A (wolny los), B–C, a finał A–? czeka na zwycięzcę meczu B–C.

## Awans zwycięzców

`KnockoutBracketAdvancer.AdvanceWinner(rounds, roundNumber, position, winnerId)` zapisuje zwycięzcę meczu i wpisuje go do następnej rundy. Drabinka jest niemutowalna: metoda zwraca nową drabinkę, a przekazana zostaje bez zmian.

- Zwycięzca z meczu na pozycji **nieparzystej** trafia do następnego meczu jako gospodarz, z **parzystej** jako gość (mecz `p` w rundzie `r` zasila mecz `ceil(p / 2)` w rundzie `r + 1`).
- Zwycięzca finału zostaje mistrzem: `KnockoutBracketAdvancer.GetChampion(rounds)` zwraca go albo `null`, dopóki finał nie jest rozstrzygnięty.
- Zwycięzcą może być tylko jedna z dwóch drużyn meczu (inaczej `ArgumentException`). Mecz z wolnym losem nie ma wyniku, a mecz, w którym nie znamy jeszcze obu drużyn, nie może być rozstrzygnięty (`InvalidOperationException`). Nieistniejąca runda lub pozycja daje `ArgumentOutOfRangeException`.
- Ponowne wskazanie tego samego zwycięzcy nic nie zmienia.
- **Korekta wyniku:** zwycięzcę można zmienić, dopóki następny mecz nie został rozstrzygnięty. Wtedy nowy zwycięzca zastępuje starego w następnym meczu. Po rozstrzygnięciu następnego meczu korekta kończy się `InvalidOperationException`, bo zmieniłaby przebieg dalszych meczów.

## Testy

`TeamSeederTests` sprawdza każdy tryb rozstawienia (kolejność zgłoszeń niezależna od kolejności wejścia, losowanie z podmienionym generatorem, błędy ręcznego rozstawienia), `ScheduleSeedingTests` wpływ rozstawienia na pary pierwszej rundy i wolne losy, a `ScheduleSeedingIntegrationTests` to samo przez API razem z opcjonalnym body i błędnymi danymi.

`KnockoutBracketAdvancerTests` sprawdza awans (gospodarz i gość, wolne losy, korekty, błędy) oraz rozegranie całej drabinki dla 2–34, 64, 100 i 128 drużyn: przy wygrywającym zawsze wyżej rozstawionym mistrzem jest pierwsza drużyna, a przy naprzemiennych zwycięzcach każda drużyna oprócz mistrza przegrywa dokładnie raz.

`KnockoutBracketGeneratorTests` pokrywa dokładny układ dla 2, 3, 4, 5 i 8 drużyn. `KnockoutBracketInvariantsTests` sprawdza własności drabinki dla 2–34 oraz 64, 100 i 128 drużyn (128 to limit turnieju), osobnym testem dla każdej: liczba rund i meczów, numeracja, liczba i adresaci wolnych losów, wpisanie drużyn z wolnym losem do drugiej rundy, `n - 1` meczów do rozegrania, wyżej rozstawiony jako gospodarz, rozdzielenie dwóch najwyżej rozstawionych oraz brak powtórzeń drużyn w rundzie. Nowy rozmiar dopisuje się w `AllTeamCounts`.

## Model

- `BracketRound`: numer rundy (od 1, ostatnia to finał) i mecze od góry drabinki.
- `BracketMatch`: pozycja w rundzie (od 1), gospodarz, gość, flaga wolnego losu i zwycięzca.

Metoda zwraca sam układ drabinki. Zamiana na encje `Match`, zapis do bazy i endpoint nie są jeszcze podpięte do API.
