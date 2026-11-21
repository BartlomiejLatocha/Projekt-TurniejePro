# Autoryzacja i role

> Status: **obowiązuje** (decyzja z zadania KAN-278). Część zasad nie jest jeszcze zaimplementowana: stan wdrożenia opisuje sekcja [Stan wdrożenia](#stan-wdrożenia).

Dokument opisuje, kto może wykonywać które operacje w API, jak to jest egzekwowane i jak zabezpieczać nowe endpointy. Korzystają z niego: backend (polityki, handlery i kontrolery), frontend (widoczność akcji w UI) oraz osoby piszące testy autoryzacji.

## Najważniejsza zasada

**Uprawnienia wynikają z tego, czyj jest zasób, a nie z globalnej roli.**

- Każdy zalogowany użytkownik może założyć turniej i staje się jego **organizatorem** (`Tournament.OrganizerId`).
- Każdy zalogowany użytkownik może zgłosić drużynę i staje się jej **kapitanem** (`Team.CaptainId`).
- **Kibic** to nie rola, tylko każdy, kto ogląda turniej: także osoba niezalogowana.
- Ta sama osoba może być organizatorem jednego turnieju, kapitanem drużyny w innym i kibicem w trzecim.

Globalne role zostają tylko dwie: zwykły użytkownik i administrator.

## Mechanizm

- **Uwierzytelnianie:** token JWT (Bearer) wydawany przez `POST /api/v1/auth/login` i `POST /api/v1/auth/register`. Klient wysyła go w nagłówku `Authorization: Bearer <token>`.
- **Autoryzacja:** polityki ASP.NET Core nazwane od czynności. Kontrolery odwołują się do **polityk**, a nie bezpośrednio do ról.
  - Polityki bez zasobu (np. „zalogowany”) działają przez atrybut `[Authorize(Policy = ...)]`.
  - Polityki zależne od zasobu (np. „organizator tego turnieju”) sprawdza kontroler po wczytaniu zasobu, przez `IAuthorizationService` i handler wymagania (szczegóły w sekcji [Reguły oparte o zasób](#reguły-oparte-o-zasób)).
- **Claimy w tokenie:**

| Claim | Zawartość |
|---|---|
| `sub` | identyfikator użytkownika (`Guid`); porównujemy go z `OrganizerId` i `CaptainId` |
| `email` | adres e-mail |
| `name` | nazwa użytkownika |
| `role` | jedna wartość na każdą globalną rolę użytkownika |
| `jti` | unikalny identyfikator tokenu |

- **Konfiguracja:** sekcja `Jwt` w `appsettings.json` (`Issuer`, `Audience`, `ExpiresMinutes`). Klucz `Jwt:Key` jest przechowywany w User Secrets i nigdy nie trafia do repozytorium.

## Role globalne

| Rola | Opis | Jak użytkownik ją dostaje |
|---|---|---|
| `User` | zwykły użytkownik: zakłada turnieje, zgłasza drużyny, kibicuje | automatycznie po rejestracji |
| `Admin` | administrator systemu: może wszystko, co organizator i kapitan, w każdym turnieju | tylko ręcznie (seed w środowisku deweloperskim albo wpis w bazie); API nie pozwala nadać tej roli |

Dotychczasowe role `Organizer`, `Participant` i `Spectator` zostają usunięte (zadanie TA-14.2.2). Nazwy `Captain` i `Fan` z kodu API (`Roles.cs`) również znikają: kapitan i kibic nie są rolami.

## Role przypisane do zasobu

| Rola | Kto nią jest | Źródło |
|---|---|---|
| Organizator turnieju | użytkownik, który utworzył turniej | `Tournament.OrganizerId == sub` |
| Kapitan drużyny | użytkownik, który zgłosił drużynę | `Team.CaptainId == sub` |
| Kibic | każdy, w tym osoba niezalogowana | brak, to domyślny poziom dostępu |

Turnieje i drużyny utworzone przed wprowadzeniem tych zasad mogą mieć `OrganizerId` lub `CaptainId` równe `null`. Takimi zasobami zarządza wyłącznie `Admin`.

## Macierz uprawnień

Legenda: **tak** (dozwolone), **nie** (zabronione). „Zalogowany” oznacza dowolnego zalogowanego użytkownika, który nie jest organizatorem ani kapitanem danego zasobu.

| Akcja | Polityka | Niezalogowany | Zalogowany | Kapitan tej drużyny | Organizator tego turnieju | Admin |
|---|---|---|---|---|---|---|
| Rejestracja i logowanie | brak (`AllowAnonymous`) | tak | tak | tak | tak | tak |
| Podgląd turniejów, drużyn, terminarza, tabeli i drabinki | brak (`AllowAnonymous`) | tak | tak | tak | tak | tak |
| Eksport wyników (PDF/CSV) | brak (`AllowAnonymous`) | tak | tak | tak | tak | tak |
| Statystyki i ranking drużyn | brak (`AllowAnonymous`) | tak | tak | tak | tak | tak |
| Utworzenie turnieju | `CreateTournament` | nie | tak | tak | tak | tak |
| Edycja i usunięcie turnieju | `ManageTournament` | nie | nie | nie | tak | tak |
| Akceptacja i odrzucenie zgłoszeń | `ManageTournament` | nie | nie | nie | tak | tak |
| Generowanie i usunięcie terminarza | `ManageTournament` | nie | nie | nie | tak | tak |
| Wpisanie i poprawa wyniku meczu | `EnterResults` | nie | nie | nie | tak | tak |
| Zgłoszenie drużyny do turnieju | `RegisterTeam` | nie | tak | tak | tak | tak |
| Edycja drużyny i zawodników | `ManageTeam` | nie | nie | tak | tak | tak |
| Opłacenie wpisowego (PayPal) | `PayEntryFee` | nie | nie | tak | nie | nie |
| Kibicowanie meczowi tokenami | `Authenticated` | nie | tak | tak | tak | tak |

Uwagi do macierzy:

- **Wyniki wpisuje tylko organizator.** Kapitan nie może wpisać wyniku, nawet meczu własnej drużyny. Ewentualne „zgłoszenie wyniku przez kapitana do zatwierdzenia przez organizatora” to osobna funkcja na przyszłość.
- **Organizator może zgłosić drużynę także do własnego turnieju**, np. gdy wpisuje drużyny w imieniu uczestników. Staje się wtedy jej kapitanem.
- **Wpisowe płaci tylko kapitan.** Organizator nie płaci za cudzą drużynę, a admin nie ma powodu, żeby płacić.
- Uprawnienie nie zastępuje **reguł biznesowych**. Przykład: organizator ma prawo zmienić format turnieju, ale domena odrzuci zmianę, gdy turniej już się rozpoczął. Takie odmowy zwracają `400` lub `409`, a nie `403`. Co wolno zmienić w turnieju w każdym statusie i jak omija to administrator, opisuje [scheduling.md](scheduling.md#edycja-turnieju-zależnie-od-statusu).
- Szczegółowe zasady kibicowania tokenami (np. czy kapitan może kibicować własnej drużynie, limity) ustala zadanie TA-13.1.1.

## Polityki

Nazwy polityk są stałymi w `TurniejePro.Api/Auth/Policies.cs`. Rejestracja polityk znajduje się w jednym miejscu (`JwtAuthenticationExtensions.AddJwtAuthentication`).

| Polityka | Wymaganie | Sposób sprawdzenia |
|---|---|---|
| `Authenticated` | zalogowany użytkownik | atrybut (to także polityka domyślna, patrz niżej) |
| `CreateTournament` | zalogowany użytkownik | atrybut |
| `RegisterTeam` | zalogowany użytkownik | atrybut |
| `ManageTournament` | organizator turnieju albo `Admin` | `IAuthorizationService` + zasób `Tournament` |
| `EnterResults` | organizator turnieju meczu albo `Admin` | `IAuthorizationService` + zasób `Tournament` |
| `ManageTeam` | kapitan drużyny, organizator jej turnieju albo `Admin` | `IAuthorizationService` + zasób `Team` |
| `PayEntryFee` | kapitan drużyny | `IAuthorizationService` + zasób `Team` |

Politykę nazywamy od **czynności**, nie od roli. `EnterResults` ma dziś to samo wymaganie co `ManageTournament`, ale jest osobną polityką: jeśli kiedyś kapitan będzie mógł zgłaszać wyniki, zmienia się jedno miejsce.

Polityki `ManageTournaments`, `AcceptApplications` (dziś: organizator globalny) zastępuje `ManageTournament`. Dotychczasowe nazwy znikają razem z rolami globalnymi.

## Polityka domyślna i wyjątki

**Domyślnie wszystko wymaga zalogowania** (`FallbackPolicy` z `RequireAuthenticatedUser`). Publiczne endpointy otwieramy jawnie. Zapomniany atrybut nie otwiera wtedy luki w zabezpieczeniach.

Endpointy z `[AllowAnonymous]` (lub `.AllowAnonymous()`):

- `POST /api/v1/auth/register` i `POST /api/v1/auth/login`,
- `GET /api/v1/health`,
- `/openapi/v1.json` (`MapOpenApi().AllowAnonymous()`),
- wszystkie endpointy podglądu (`GET`): turnieje, drużyny, terminarz, tabela, a w przyszłości eksport i statystyki.

## Endpointy i polityki

Stan docelowy dla istniejących endpointów. Nowy endpoint dopisujemy do tej tabeli.

| Endpoint | Polityka |
|---|---|
| `POST /api/v1/auth/register`, `POST /api/v1/auth/login` | `AllowAnonymous` |
| `GET /api/v1/auth/me` | `Authenticated` |
| `GET /api/v1/me/tournaments`, `GET /api/v1/me/teams` | `Authenticated` (każdy widzi tylko swoje: `OrganizerId` / `CaptainId` równe `sub`, także admin) |
| `GET /api/v1/health` | `AllowAnonymous` |
| `GET /api/v1/tournaments`, `GET /api/v1/tournaments/{id}` | `AllowAnonymous` |
| `POST /api/v1/tournaments` | `CreateTournament` |
| `PUT /api/v1/tournaments/{id}`, `DELETE /api/v1/tournaments/{id}` | `ManageTournament` |
| `POST /api/v1/teams` | `RegisterTeam` |
| `PATCH /api/v1/teams/{id}/accept`, `PATCH /api/v1/teams/{id}/reject` | `ManageTournament` (turniej drużyny) |
| `GET /api/v1/teams/{id}`, `GET /api/v1/teams/tournament/{tournamentId}` | `AllowAnonymous` |
| `GET /api/v1/tournaments/{id}/schedule` | `AllowAnonymous` |
| `POST /api/v1/tournaments/{id}/schedule`, `DELETE /api/v1/tournaments/{id}/schedule` | `ManageTournament` |
| `PUT /api/v1/tournaments/{id}/matches/{matchId}/result` | `EnterResults` |
| `GET /api/v1/tournaments/{id}/standings` | `AllowAnonymous` |

## Jak zabezpieczyć nowy endpoint

Każdy endpoint ma politykę albo jawne `[AllowAnonymous]`.

**Endpoint bez zasobu** (wystarczy, że użytkownik jest zalogowany):

```csharp
[HttpPost]
[Authorize(Policy = Policies.CreateTournament)]
[ProducesResponseType(typeof(TournamentDetailsResponse), StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> Create(CreateTournamentRequest request, CancellationToken cancellationToken) { ... }
```

**Endpoint działający na zasobie** (trzeba być jego organizatorem lub kapitanem):

```csharp
[HttpDelete("{id:guid}")]
[Authorize] // tylko zalogowani; własność sprawdzamy po wczytaniu turnieju
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
{
    var tournament = await _unitOfWork.Tournaments.GetByIdAsync(id, cancellationToken);
    if (tournament is null)
    {
        return NotFound(...);
    }

    var authorization = await _authorizationService.AuthorizeAsync(User, tournament, Policies.ManageTournament);
    if (!authorization.Succeeded)
    {
        return Forbid();
    }

    ...
}
```

Zasady:

- Kolejność sprawdzeń: **401** (brak logowania, robi to atrybut) → **404** (zasób nie istnieje) → **403** (zasób nie należy do użytkownika) → reguły biznesowe (**400/409**).
- Dodaj `[ProducesResponseType]` dla `401` i `403`, żeby były widoczne w Swaggerze.
- Nie porównuj `OrganizerId` ani `CaptainId` ręcznie w kontrolerze i nie sprawdzaj ról przez `User.IsInRole(...)`. Zawsze używaj polityki.
- Nowa polityka wymaga wpisu w tym dokumencie (macierz, tabela polityk i tabela endpointów).

## Kody odpowiedzi

| Sytuacja | Kod |
|---|---|
| Brak tokenu, token nieprawidłowy lub wygasły | `401 Unauthorized` |
| Poprawny token, ale zasób nie należy do użytkownika (np. cudzy turniej) | `403 Forbidden` |
| Zasób nie istnieje | `404 Not Found` |
| Użytkownik ma uprawnienie, ale operacja łamie regułę biznesową | `400 Bad Request` lub `409 Conflict` |

Turnieje i drużyny są publiczne, więc dla cudzego zasobu zwracamy `403`, a nie `404`: ukrywanie jego istnienia nic nie daje.

Przy logowaniu „nie ma takiego użytkownika” i „złe hasło” zwracają **ten sam** `401`, żeby nie ujawniać, które adresy e-mail mają konta.

## Reguły oparte o zasób

Reguły typu „tylko organizator tego turnieju” realizujemy przez autoryzację opartą o zasób:

- wymaganie (np. `TournamentOrganizerRequirement`) i handler `AuthorizationHandler<TRequirement, Tournament>`, który porównuje claim `sub` z `Tournament.OrganizerId` i przepuszcza rolę `Admin`;
- analogicznie dla drużyny: handler dla `Team` sprawdza `CaptainId` (i dla `ManageTeam` także organizatora turnieju drużyny);
- handlery rejestrujemy w DI razem z politykami, w tym samym miejscu co polityki;
- kontroler wczytuje zasób i woła `IAuthorizationService.AuthorizeAsync(User, zasób, polityka)`.

Dzięki temu logika własności jest w jednym miejscu i da się ją przetestować jednostkowo, bez HTTP.

## Dane osobowe w odpowiedziach

Endpointy podglądu są publiczne, więc nie mogą zwracać danych kontaktowych. Dotyczy to dziś pola `ContactEmail` w `TeamResponse`: widzi je każdy, także osoba niezalogowana. Docelowo `ContactEmail` zwracamy tylko kapitanowi drużyny, organizatorowi turnieju i adminowi; pozostali dostają `null`.

## Dla frontendu (Blazor)

- `AuthResponse.UserId` to identyfikator zalogowanego użytkownika. Przyciski zarządzania turniejem pokazujemy, gdy `TournamentDetailsResponse.OrganizerId == UserId` (lub użytkownik ma rolę `Admin`), a przyciski drużyny, gdy `TeamResponse.CaptainId == UserId`.
- Przycisków „Nowy turniej” i „Zgłoś drużynę” nie ukrywamy według roli: widzi je każdy zalogowany.
- Ukrywanie przycisków to **wygoda użytkownika, a nie zabezpieczenie**. Prawdziwą kontrolę zawsze wykonuje API (`401` i `403`).
- UI obsługuje `401` (przekierowanie do logowania) i `403` (komunikat o braku uprawnień).

## Testowanie

- Pełna macierz „endpoint × użytkownik × oczekiwany kod” jest w `tests/TurniejePro.Tests/AuthorizationMatrixTests.cs`. Zmiana macierzy w tym dokumencie oznacza zmianę testów.
- Po wprowadzeniu własności zasobów kolumny macierzy testów to: brak tokenu / zalogowany obcy / kapitan tej drużyny / organizator tego turnieju / `Admin` (zadanie TA-14.2.4). Każdy test zasobowy potrzebuje turnieju z ustawionym `OrganizerId` i drużyny z ustawionym `CaptainId`.
- Macierz dla endpointów zasobowych jest w `tests/TurniejePro.Tests/ResourceAuthorizationMatrixTests.cs`: 7 endpointów (edycja i usunięcie turnieju, generowanie i usunięcie terminarza, akceptacja i odrzucenie zgłoszenia, wpis wyniku) × 6 wywołujących (brak tokenu, obcy użytkownik, organizator innego turnieju, kapitan drużyny, organizator turnieju, `Admin`). Oczekiwane kody: brak tokenu `401`, obcy, organizator innego turnieju i kapitan `403` (przy odmowie dane się nie zmieniają), organizator i `Admin` kod sukcesu. Dla nieistniejącego zasobu brak tokenu daje `401` (autoryzacja przed szukaniem zasobu), a zalogowany `404`. Nowy endpoint zasobowy to jeden wpis w słowniku `Endpoints`.
- Handlery wymagań testujemy także jednostkowo: właściciel, obcy, `Admin`, zasób z `OrganizerId == null`.
- Dodatkowo sprawdzamy `401` dla tokenu nieprawidłowego, wygasłego, podpisanego innym kluczem oraz ze złym `issuer` i `audience`.
- Tokeny w testach generuje wspólny pomocnik `tests/TurniejePro.Tests/TestTokens.cs` (bez logowania przez HTTP).

## Stan wdrożenia

Na dzień przyjęcia dokumentu kod realizuje jeszcze stary model ról globalnych. Zmiany wprowadzają zadania z epiku „Uprawnienia i własność zasobów”:

| Element | Stan w kodzie | Zadanie |
|---|---|---|
| Polityka domyślna (wymagane logowanie) i publiczny podgląd | zrobione | — |
| Role `User` i `Admin` zamiast `Organizer`/`Participant`/`Spectator`; tworzenie turnieju i zgłaszanie drużyny dla każdego zalogowanego | zrobione | TA-14.2.2 (KAN-284) |
| Polityka `ManageTournament` (`TournamentOrganizerHandler`) w edycji i usuwaniu turnieju, akceptacji i odrzucaniu zgłoszeń, generowaniu i usuwaniu terminarza oraz wpisywaniu wyników | zrobione | TA-14.2.1 (KAN-283) |
| Polityka `ManageTeam` dla drużyn | do zrobienia, razem z edycją drużyny | — |
| `EnterResults` jako osobna polityka zasobowa: wynik wpisuje tylko organizator turnieju lub admin, kapitan nie | zrobione | TA-14.2.3 (KAN-285) |
| Testy 401/403 dla cudzych zasobów | zrobione | TA-14.2.4 (KAN-286) |
| Widoczność akcji w UI według `OrganizerId`/`CaptainId` | do zrobienia | TA-14.2.5 |
| `ContactEmail` tylko dla uprawnionych | do zrobienia | do dopisania w Jirze |
| `PayEntryFee` | do zrobienia | epik „Opłata wpisowa przez PayPal” |

## Historia decyzji

| Data | Decyzja | Zadanie |
|---|---|---|
| 26.10.2026 | Autoryzacja przez polityki nazwane od czynności | KAN-65 |
| 26.10.2026 | Polityka domyślna: wymagane logowanie, publiczne endpointy otwierane jawnie | KAN-65 |
| 14.11.2026 | Role przypisane do zasobu zamiast globalnych ról organizatora, kapitana i kibica; globalnie tylko `User` i `Admin` | KAN-278 |
| 14.11.2026 | Wyniki wpisuje tylko organizator turnieju (kapitan nie) | KAN-278 |
| 14.11.2026 | Podgląd, eksport i statystyki są publiczne, także bez logowania | KAN-278 |
| 14.11.2026 | Dla cudzego zasobu zwracamy `403`, a nie `404` | KAN-278 |
