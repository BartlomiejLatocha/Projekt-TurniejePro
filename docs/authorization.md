# Autoryzacja i role

> Status: **propozycja do uzgodnienia**. Pola oznaczone `?` w macierzy uprawnień wymagają decyzji zespołu. Po akceptacji zmień status na „obowiązuje" i uzupełnij historię decyzji.

Dokument opisuje, kto może wykonywać które operacje w API, jak to jest egzekwowane i jak zabezpieczać nowe endpointy. Korzystają z niego: backend (polityki i kontrolery), frontend (widoczność akcji w UI) oraz osoby piszące testy autoryzacji.

## Mechanizm

- **Uwierzytelnianie:** token JWT (Bearer) wydawany przez `POST /api/v1/auth/login`. Klient wysyła go w nagłówku `Authorization: Bearer <token>`.
- **Autoryzacja:** polityki ASP.NET Core oparte na rolach. Kontrolery odwołują się do **polityk** (nazwanych od czynności), a nie bezpośrednio do ról.
- **Claimy w tokenie:**

| Claim | Zawartość |
|---|---|
| `sub` | identyfikator użytkownika (`Guid`) |
| `email` | adres e-mail |
| `name` | nazwa użytkownika |
| `role` | jedna wartość na każdą rolę użytkownika |
| `jti` | unikalny identyfikator tokenu |

- **Konfiguracja:** sekcja `Jwt` w `appsettings.json` (`Issuer`, `Audience`, `ExpiresMinutes`). Klucz `Jwt:Key` jest przechowywany w User Secrets i nigdy nie trafia do repozytorium.

## Role

Nazwy ról muszą być **identyczne** z tymi, które tworzy seed (zadanie KAN-64). Poniższa tabela jest szkicem, więc popraw ją według stanu w kodzie.

| Rola | Opis | Jak użytkownik ją dostaje |
|---|---|---|
| `Spectator` (kibic) | podgląd turniejów, drużyn, wyników i tabel | domyślnie po rejestracji |
| `Captain` (kapitan) | zgłasza i prowadzi własną drużynę | `TODO` (nadaje organizator / seed) |
| `Organizer` (organizator) | zarządza turniejami, zgłoszeniami i wynikami | `TODO` (seed / nadaje administrator) |

## Macierz uprawnień

Legenda: **tak** (dozwolone), **nie** (zabronione), **?** (do decyzji zespołu).

| Akcja | Polityka | Niezalogowany | Kibic | Kapitan | Organizator |
|---|---|---|---|---|---|
| Rejestracja i logowanie | brak (`AllowAnonymous`) | tak | tak | tak | tak |
| Podgląd turniejów, tabel i wyników | `ViewTournaments` | tak | tak | tak | tak |
| Zgłoszenie drużyny do turnieju | `RegisterTeam` | nie | nie | tak | tak |
| Edycja własnej drużyny i zawodników | `RegisterTeam` + reguła zasobu | nie | nie | tak (tylko swojej) | tak |
| Tworzenie i edycja turnieju | `ManageTournaments` | nie | nie | nie | tak |
| Akceptacja i odrzucanie zgłoszeń | `ManageTournaments` | nie | nie | nie | tak |
| Generowanie terminarza | `ManageTournaments` | nie | nie | nie | tak |
| Wprowadzanie wyników meczów | `EnterResults` | nie | nie | tak | tak |
| Eksport wyników (PDF/CSV) | `ViewTournaments` | nie | tak | tak | tak |

Decyzje do podjęcia:

## Polityki

Nazwy polityk są stałymi w `TurniejePro.Api/Auth/Policies.cs`. Rejestracja polityk znajduje się w jednym miejscu (metoda rozszerzająca wywoływana z `Program.cs`).

| Polityka | Wymagana rola (propozycja) |
|---|---|
| `ViewTournaments` | zalogowany użytkownik (dowolna rola) |
| `RegisterTeam` | `Captain` lub `Organizer` |
| `ManageTournaments` | `Organizer` |
| `EnterResults` | `Organizer` (do decyzji: także `Captain`) |

Politykę nazywamy od **czynności**, nie od roli. Dzięki temu zmiana uprawnień (np. dopuszczenie kapitana do wyników) to zmiana w jednym miejscu, a nie w każdym kontrolerze.

## Jak zabezpieczyć nowy endpoint

Każdy endpoint ma politykę albo jawne `[AllowAnonymous]`:

```csharp
[ApiController]
[Route("api/v1/tournaments")]
public class TournamentsController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.ManageTournaments)]
    [ProducesResponseType(typeof(TournamentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(CreateTournamentRequest request) { ... }
}
```

Zasady:

- Dodaj `[ProducesResponseType]` dla `401` i `403`, żeby były widoczne w Swaggerze.
- Nie sprawdzaj ról ręcznie w kodzie akcji (`User.IsInRole(...)`), jeśli da się to wyrazić polityką.
- Nowa polityka wymaga wpisu w tym dokumencie (macierz i tabela polityk).

## Polityka domyślna i wyjątki

`TODO: decyzja zespołu.` Propozycja: **domyślnie wszystko wymaga zalogowania** (`FallbackPolicy`), a publiczne endpointy otwieramy jawnie. Zapomniany atrybut nie otwiera wtedy luki w zabezpieczeniach.

Endpointy, które muszą mieć `[AllowAnonymous]` (lub `.AllowAnonymous()`):

- `POST /api/v1/auth/register` i `POST /api/v1/auth/login`,
- endpoint zdrowia aplikacji,
- `/openapi/v1.json` (`MapOpenApi().AllowAnonymous()`),
- publiczny podgląd turniejów, jeśli zespół zdecyduje, że nie wymaga logowania.

## Kody odpowiedzi

| Sytuacja | Kod |
|---|---|
| Brak tokenu, token nieprawidłowy lub wygasły | `401 Unauthorized` |
| Poprawny token, ale rola bez uprawnień | `403 Forbidden` |
| Zasób nie istnieje lub użytkownik nie powinien wiedzieć, że istnieje | `404 Not Found` |

Przy logowaniu „nie ma takiego użytkownika" i „złe hasło" zwracają **ten sam** `401`, żeby nie ujawniać, które adresy e-mail mają konta.

## Reguły oparte o zasób

Same role nie wystarczą dla reguł typu „kapitan edytuje wyłącznie swoją drużynę". Takie reguły realizujemy przez autoryzację opartą o zasób (`IAuthorizationService` i handler wymagania), w ramach zadań dotyczących drużyn. Polityka (`RegisterTeam`) sprawdza rolę, a handler sprawdza własność zasobu.

## Dla frontendu (Blazor)

- Role użytkownika są w odpowiedzi logowania (`AuthResponse.Roles`) i w claimie `role` tokenu.
- Ukrywanie przycisków i widoków według roli to **wygoda użytkownika, a nie zabezpieczenie**. Prawdziwą kontrolę zawsze wykonuje API (`401` i `403`).
- UI powinien obsługiwać `401` (przekierowanie do logowania) i `403` (komunikat o braku uprawnień).

## Testowanie

- Test mechanizmu (zadanie KAN-65): kontroler testowy w projekcie `TurniejePro.Tests` z kilkoma politykami, sprawdzający `401`, `403` i `200` dla tokenów poszczególnych ról.
- Pełna macierz „endpoint × rola × oczekiwany kod" (zadanie KAN-67) opiera się na tabeli w tym dokumencie. Zmiana macierzy oznacza zmianę testów.
- Tokeny w testach generuje wspólny pomocnik z projektu `Tests` (bez logowania przez HTTP).

## Historia decyzji

| Data | Decyzja | Uczestnicy |
|---|---|---|
| `TODO` | Autoryzacja przez polityki oparte na rolach, nazwane od czynności | `TODO` |
| `TODO` | Polityka domyślna (wymagane logowanie) | `TODO` |
| `TODO` | Zakres uprawnień kapitana (wyniki meczów) | `TODO` |
