# Terminarz i wyniki: ustalenia

> Status: **propozycja, obowiązuje po zatwierdzeniu PR z tym dokumentem.**
> Zakres: KAN-83 (generowanie i pobieranie terminarza), KAN-86 (wpis i edycja wyniku).
> Poza zakresem: tabela ligowa i statystyki (epik KAN-10), widoki w Blazorze (KAN-84, KAN-87).

Dokument opisuje, jak API zamienia wynik algorytmów z `Domain/Scheduling` na mecze w bazie i jak obsługuje wyniki. Ma dawać wszystkim (backend, frontend, testy) te same założenia.

## Elementy, na których bazujemy

- **Algorytmy ligi:** `RoundRobinScheduler`, `RoundRobinRound`, `RoundRobinPairing`.
- **Algorytmy pucharu:** `KnockoutBracketGenerator`, `KnockoutBracketAdvancer`, `BracketRound`, `BracketMatch`.
- **Encja `Match`:** turniej, numer rundy, drużyny (opcjonalne), wynik (opcjonalny), status, termin (opcjonalny).
- **Polityki:** `ManageTournaments` (generowanie i usuwanie terminarza), `EnterResults` (wyniki).

`BracketMatch` opisuje mecz drabinki polami: `Position` (od 1, od góry drabinki), `HomeTeamId`, `AwayTeamId`, `IsBye` i `WinnerId`. Encja `Match` nie ma dziś `Position` ani `IsBye`, więc bez ich dodania nie da się zapisać drabinki w sposób, który pozwala awansować zwycięzców.

## Zmiany w modelu danych

| Zmiana | Po co |
|---|---|
| `Match.Position` (`int`, od 1) | Miejsce meczu w rundzie. Konieczne do awansu w pucharze i stałej kolejności wyświetlania. |
| `Match.IsBye` (`bool`) | Odróżnia wolny los od meczu, w którym rywal jest jeszcze nieznany. |
| Metoda `Match.MarkAsBye()` | Ustawia `IsBye` i status `Finished`, wymaga braku drużyny gości. |
| Unikalny indeks `(TournamentId, Round, Position)` | Chroni przed podwójnym wygenerowaniem terminarza przy równoległych żądaniach. |
| `IMatchRepository` rozszerzające `IRepository<Match>` | Pobranie meczów turnieju z drużynami (bez dodatkowych zapytań) i sprawdzenie, czy terminarz istnieje. |

Zasady wdrożenia:

- **Jedna migracja** na te zmiany, przygotowana razem z Adamem, żeby uniknąć konfliktów w `Migrations/`.
- `IUnitOfWork.Matches` zmienia typ z `IRepository<Match>` na `IMatchRepository`. Ponieważ nowy interfejs rozszerza stary, istniejący kod nadal się kompiluje.
- Repozytorium tworzy `UnitOfWork` (tak jak `Teams` i `Tournaments`), więc osobny wpis w `DependencyInjection.cs` jest potrzebny tylko wtedy, gdy repozytoria są tam rejestrowane.

## Generowanie terminarza

**Endpoint:** `POST /api/v1/tournaments/{tournamentId}/schedule` (polityka `ManageTournaments`).

Warunki:

- turniej istnieje (`404`),
- są co najmniej **2 zaakceptowane zgłoszenia** (inaczej `409`),
- terminarz nie został jeszcze wygenerowany (inaczej `409`),
- zapis wszystkich meczów odbywa się **w jednej transakcji** (`UnitOfWork`). Naruszenie unikalnego indeksu przy równoległym żądaniu kończy się `409`.

Sukces: `201 Created` z `ScheduleResponse`.

### Liga (round-robin)

- Każda para gra **raz** (bez rewanżów).
- Wolny los (pauza przy nieparzystej liczbie drużyn) **nie jest zapisywany jako mecz**: zapisujemy tylko pary, w których obie drużyny są znane.
- `Round` to numer rundy z algorytmu, `Position` to kolejność meczu w rundzie od 1.
- `ScheduledAt` zostaje puste (terminarz jest rundowy, bez godzin).

### Puchar (drabinka)

- Zapisujemy **od razu wszystkie rundy**. W rundach późniejszych drużyny są `null`, dopóki nie awansują zwycięzcy.
- Wolne losy w pierwszej rundzie zapisujemy jako mecz z `IsBye = true` i statusem `Finished` (bez wyniku). Drużyna z wolnym losem jest już wpisana do odpowiedniego meczu następnej rundy.
- Kolejność drużyn w drabince jest losowana przy generowaniu. Losowanie nie jest zapisywane, więc ponowne generowanie daje inną drabinkę.
- Awans do następnej rundy: pozycja w następnej rundzie to `(Position + 1) / 2` (dzielenie całkowite). Zwycięzca meczu o nieparzystej `Position` trafia na miejsce gospodarza, o parzystej na miejsce gościa.

### Pobranie i usunięcie

- `GET /api/v1/tournaments/{tournamentId}/schedule` jest **publiczny** (`[AllowAnonymous]`), bo terminarz ma być udostępniony. Zwraca `200` z `ScheduleResponse`, a `404`, gdy turniej lub terminarz nie istnieje. Mecze są pogrupowane w rundy i posortowane po `Round`, `Position`.
- `DELETE /api/v1/tournaments/{tournamentId}/schedule` (polityka `ManageTournaments`) usuwa terminarz, żeby można go było wygenerować ponownie. Zwraca `204`, `404` albo `409`, gdy którykolwiek mecz ma już wpisany wynik.

### Skutek dla zgłoszeń

Po wygenerowaniu terminarza **nie można** przyjmować nowych zgłoszeń ani akceptować oczekujących (`409`). Do `RegistrationBlockReason` dodajemy wartość oznaczającą wygenerowany terminarz (uzgodnienie z osobą odpowiedzialną za KAN-76).

## Wpisywanie wyniku

**Endpoint:** `PUT /api/v1/tournaments/{tournamentId}/matches/{matchId}/result` (polityka `EnterResults`), ciało: `UpdateMatchResultRequest`. Sukces: `200` z `MatchResponse`.

Reguły:

| Sytuacja | Odpowiedź |
|---|---|
| Wynik ujemny lub poza zakresem 0-999 | `400` |
| Mecz nie istnieje lub należy do innego turnieju | `404` |
| Mecz nie ma jeszcze obu drużyn | `409` |
| Wolny los (`IsBye`) lub mecz anulowany | `409` |
| Remis w pucharze | `400` (w lidze remis jest dozwolony) |
| Edycja wyniku w pucharze, gdy mecz następnej rundy ma już wynik | `409` |

Dodatkowo:

- Wynik ustawia `Match.SetResult`, który zmienia status na `Finished`. Edycja istniejącego wyniku nadpisuje poprzedni.
- W pucharze po zapisie wyniku zwycięzca trafia do właściwego miejsca w następnej rundzie (`KnockoutBracketAdvancer`). Zmiana zwycięzcy przy edycji podmienia drużynę w następnym meczu.
- Walidacje biznesowe są w serwisie, bo `Match.SetResult` sprawdza tylko ujemne wartości.
- Tabela ligowa **nie jest** zapisywana. Liczymy ją na odczyt z meczów (`StandingsCalculator`), w osobnym zadaniu.

## Endpointy

| Metoda i ścieżka | Polityka | Sukces | Błędy |
|---|---|---|---|
| `POST /api/v1/tournaments/{id}/schedule` | `ManageTournaments` | `201` | `401`, `403`, `404`, `409` |
| `GET /api/v1/tournaments/{id}/schedule` | publiczny | `200` | `404` |
| `DELETE /api/v1/tournaments/{id}/schedule` | `ManageTournaments` | `204` | `401`, `403`, `404`, `409` |
| `PUT /api/v1/tournaments/{id}/matches/{matchId}/result` | `EnterResults` | `200` | `400`, `401`, `403`, `404`, `409` |

Kontrakty: `TurniejePro.Contracts.Matches` (`ScheduleResponse`, `ScheduleRoundResponse`, `MatchResponse`, `MatchTeamResponse`, `MatchStatus`, `UpdateMatchResultRequest`).

## Podział pracy

| Obszar | Kto |
|---|---|
| Algorytmy i ich testy | zrobione (algorytmy zrecenzowane) |
| Zmiany w `Match`, migracja, `IMatchRepository` | do uzgodnienia |
| Serwis generowania, kontrolery, testy API | KAN-83, KAN-86 |
| Widok terminarza i formularz wyniku | KAN-84, KAN-87 |
| Blokada zgłoszeń po wygenerowaniu terminarza | uzgodnienie z KAN-76 |

## Historia decyzji

| Data | Decyzja | Uczestnicy |
|---|---|---|
| `TODO` | Zasady generowania terminarza i wyników z tego dokumentu | `TODO` |
