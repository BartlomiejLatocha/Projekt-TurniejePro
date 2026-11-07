# Terminarz ligowy (round-robin)

Terminarz "każdy z każdym" generuje `RoundRobinScheduler.Generate` z przestrzeni nazw `TurniejePro.Domain.Scheduling`. Metoda przyjmuje identyfikatory drużyn i zwraca listę kolejek (`RoundRobinRound`), a w każdej parę gospodarz-gość (`RoundRobinPairing`).

## Zasady

- Każda para drużyn spotyka się **dokładnie raz** (jedna runda, bez rewanżów).
- Liczba kolejek: `n - 1` dla parzystej liczby drużyn, `n` dla nieparzystej. W kolejce gra każda drużyna najwyżej raz.
- **Nieparzysta liczba drużyn:** w każdej kolejce jedna drużyna pauzuje (`RestingTeamId`), a w całym terminarzu każda pauzuje dokładnie raz.
- **Gospodarz i gość:** liczba meczów u siebie i na wyjeździe każdej drużyny różni się najwyżej o 1.
- Wynik zależy tylko od kolejności podanych drużyn, bez losowania. Jeśli kolejność ma być losowa, wołający tasuje listę przed wywołaniem.
- Metoda zwraca same pary. Zamiana ich na encje `Match` i zapis do bazy nie jest jeszcze podpięta do API.

## Wymagania wejścia

Co najmniej 2 drużyny, bez powtórzeń (inaczej `ArgumentException`), lista różna od `null`.

## Przykłady

Cztery drużyny A, B, C, D (pierwsza w parze to gospodarz):

| Kolejka | Mecze |
|---|---|
| 1 | A–D, B–C |
| 2 | C–A, D–B |
| 3 | A–B, C–D |

Trzy drużyny A, B, C (nieparzysta liczba, jedna pauzuje):

| Kolejka | Mecz | Pauzuje |
|---|---|---|
| 1 | A–B | C |
| 2 | C–A | B |
| 3 | B–C | A |

## Testy

`RoundRobinScheduleInvariantsTests` sprawdza własności terminarza dla 2–16 i 64 drużyn, osobnym testem dla każdej własności: brak powtórzonych par, każda para dokładnie raz, liczba kolejek i meczów w kolejce, nikt nie gra dwa razy w kolejce ani sam ze sobą, każda drużyna rozgrywa `n - 1` meczów, pauzy (nikt przy parzystej liczbie, każdy dokładnie raz przy nieparzystej), numeracja kolejek od 1 i równowaga gospodarz-gość. Nowy rozmiar turnieju dopisuje się w `AllTeamCounts`. Dokładne przykłady dla 3 i 4 drużyn są w `RoundRobinSchedulerTests`.

## Jak to działa

Metoda okręgu: drużyny stoją na pozycjach, w każdej kolejce pierwsza i ostatnia tworzą parę, druga z przedostatnią i tak dalej. Po kolejce wszystkie pozycje oprócz pierwszej obracają się o jedno miejsce. Przy nieparzystej liczbie drużyn pierwszą (stałą) pozycję zajmuje pauza, więc drużyna naprzeciw niej w danej kolejce nie gra.
