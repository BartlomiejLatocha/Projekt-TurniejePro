# Drabinka pucharowa

Drabinkę (eliminacja po jednej porażce) generuje `KnockoutBracketGenerator.Generate` z przestrzeni nazw `TurniejePro.Domain.Scheduling`. Metoda przyjmuje identyfikatory drużyn w kolejności rozstawienia i zwraca listę rund (`BracketRound`), a w każdej mecze (`BracketMatch`).

## Zasady

- **Rozstawienie:** kolejność podanych drużyn to rozstawienie, pierwsza jest najwyżej rozstawiona. Wynik zależy tylko od tej kolejności, bez losowania. Jeśli rozstawienie ma być losowe, wołający tasuje listę przed wywołaniem.
- **Pary pierwszej rundy** powstają według standardowej drabinki, np. dla 8 drużyn: 1–8, 4–5, 2–7, 3–6. Dwie najwyżej rozstawione drużyny mogą spotkać się najwcześniej w finale. W parze pierwsza (wyżej rozstawiona) jest gospodarzem.
- **Wolne losy:** gdy liczba drużyn nie jest potęgą dwójki, drabinka jest uzupełniana do najbliższej potęgi (np. 5 drużyn do 8), a brakujące miejsca to wolne losy. Dostają je najwyżej rozstawione drużyny. Mecz z wolnym losem (`IsBye`) ma tylko gospodarza, który jest od razu zwycięzcą (`WinnerId`) i jest wpisany do drugiej rundy.
- **Liczby:** liczba rund to `ceil(log2 n)`, a rozegranych meczów (bez wolnych losów) jest `n - 1`.
- Miejsca w kolejnych rundach, na które jeszcze nikt nie awansował, mają `null` w `HomeTeamId` lub `AwayTeamId`. Zwycięzca meczu z pozycji nieparzystej trafia do następnej rundy jako gospodarz, z parzystej jako gość.

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

## Model

- `BracketRound`: numer rundy (od 1, ostatnia to finał) i mecze od góry drabinki.
- `BracketMatch`: pozycja w rundzie (od 1), gospodarz, gość, flaga wolnego losu i zwycięzca.

Metoda zwraca sam układ drabinki. Zamiana na encje `Match`, zapis do bazy i endpoint nie są jeszcze podpięte do API.
