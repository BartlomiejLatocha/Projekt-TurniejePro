# Tabela ligowa

Tabelę liczy `StandingsCalculator.Calculate(teamIds, matches)` z przestrzeni nazw `TurniejePro.Domain.Standings`. Przyjmuje drużyny tabeli i mecze turnieju (encje `Match`), a zwraca wiersze `StandingsRow` w kolejności tabeli.

## Punktacja

| Wynik | Punkty |
|---|---|
| wygrana | 3 |
| remis | 1 |
| porażka | 0 |

Wiersz zawiera: miejsce, drużynę, mecze rozegrane, wygrane, remisy, porażki, bramki (punkty) zdobyte i stracone, bilans (`GoalDifference`, zdobyte minus stracone) oraz punkty.

## Które mecze się liczą

Tylko mecze o statusie `Finished` z wynikiem, w których grają dwie drużyny z tabeli. Mecze zaplanowane, w toku, odwołane, bez jednej z drużyn albo z drużyną spoza tabeli są pomijane. Dzięki temu do tabeli można podać np. tylko zaakceptowane drużyny.

## Zasady rozstrzygania remisów

O kolejności drużyn decydują kolejno:

1. punkty,
2. bilans bramek,
3. bramki zdobyte,
4. punkty z meczów bezpośrednich między drużynami, które remisują do tego miejsca (liczą się tylko mecze pomiędzy nimi),
5. kolejność drużyn na liście wejściowej (tylko do uporządkowania wierszy, patrz niżej).

Drużyny, których nie rozstrzyga żadna z zasad 1–4, dostają **to samo miejsce**, a następne miejsce jest przesunięte (np. 1, 1, 1, 4). Wiersze o tym samym miejscu są ułożone według kolejności na liście wejściowej.

## Korekta wyniku

Tabela jest funkcją meczów, więc nie przechowuje stanu: po zmianie wyniku meczu (`Match.SetResult` wywołane ponownie) wystarczy policzyć ją od nowa.

## Wymagania wejścia

Listy drużyn i meczów różne od `null`, drużyny bez powtórzeń (inaczej `ArgumentException`).

## Uwagi

Reguły rozstrzygania remisów są przyjętym domyślnym ustawieniem (punkty, bilans, bramki, mecze bezpośrednie). Dla 3 i więcej remisujących drużyn mecze bezpośrednie liczą się wyłącznie punktami, bez osobnej mini-tabeli z bilansem. Zmiana kolejności kryteriów to zmiana w jednym miejscu, w `StandingsCalculator`.

Metoda liczy tabelę w pamięci. Endpoint (`/api/v1/tournaments/{id}/standings`) i widok tabeli nie są jeszcze podpięte.
