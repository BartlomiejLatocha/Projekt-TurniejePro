# Kontrakty API (DTO)

> Status: **obowiązuje od zatwierdzenia PR, w którym dodano ten dokument**. Zmiany w konwencjach przechodzą przez PR i są zapisywane w sekcji „Historia decyzji".

Dokument opisuje, jak backend (`TurniejePro.Api`) i frontend (`TurniejePro.Blazor`) wymieniają dane. Jego celem jest to, żeby każdy zasób (turnieje, drużyny, mecze, konta) był opisany w ten sam sposób, niezależnie od tego, kto go implementuje.

## Źródło prawdy

- **Typy kontraktów (DTO)** żyją w projekcie `src/TurniejePro.Contracts` i są współdzielone przez `TurniejePro.Api` i `TurniejePro.Blazor`. Zmiana kontraktu, która rozjeżdża obie strony, kończy się błędem kompilacji.
- **Dokumentacja endpointów:** OpenAPI (`/openapi/v1.json`) i Swagger UI (`/swagger`), generowane z kodu API.
- **Encje domenowe** (`TurniejePro.Domain`) nigdy nie są zwracane ani przyjmowane przez API. Zawsze mapujemy je na DTO (mapowanie w `TurniejePro.Api/Mapping/`).
- Projekty `Domain` i `Data` **nie** referencjonują `Contracts`.

## Struktura projektu Contracts

Każdy zasób ma własny folder i własną przestrzeń nazw. Typy wspólne trafiają do `Common/`.

```
src/TurniejePro.Contracts/
├── Common/
│   └── PagedResponse.cs              # koperta dla każdej listy
├── Tournaments/
│   ├── TournamentFormat.cs           # enum
│   ├── TournamentResponse.cs
│   └── CreateTournamentRequest.cs
├── Profile/                          # zasoby zalogowanego użytkownika (GET /api/v1/me/...)
│   ├── MyTournamentResponse.cs
│   └── MyTeamResponse.cs
├── Validation/
│   └── FutureDateAttribute.cs        # własne atrybuty walidacji
├── Teams/                            # kolejne zasoby według tego samego wzoru
└── ...
```

Przestrzeń nazw odpowiada folderowi: `TurniejePro.Contracts.<Zasób>`, np. `TurniejePro.Contracts.Tournaments`.

## Konwencje

| Obszar | Ustalenie |
|---|---|
| Nazwy typów | `<Zasób>Response` (odpowiedź), `Create<Zasób>Request`, `Update<Zasób>Request`, `<Zasób>ListItemResponse` (skrócona wersja do list, jeśli potrzebna) |
| Typ kontraktu | `sealed record`, niemutowalny, jeden typ w jednym pliku |
| Nazwy pól w JSON | camelCase (domyślne w ASP.NET Core) |
| Identyfikatory | `Guid` |
| Daty i czas | `DateTimeOffset`, zawsze UTC, format ISO 8601 |
| Enumy | serializowane jako tekst (`"League"`, nie `1`). Atrybut `[JsonConverter(typeof(JsonStringEnumConverter))]` na samym enumie. Wartości numerowane jawnie, od 1. |
| Nullowalność | jawna: `string?` oznacza pole opcjonalne, pozostałe są wymagane |
| Walidacja | atrybuty DataAnnotations na parametrach rekordów `*Request`, **bez** prefiksu `property:` (MVC rzuca wyjątek dla rekordu z walidacją na właściwościach, a API kończy żądanie błędem 500). Pole z enumem ma `[EnumDataType(typeof(...))]` (bez tego API przyjmie liczbę spoza enuma), a termin w przyszłości `[FutureDate]` z `Contracts/Validation` |
| Listy | zawsze `PagedResponse<T>`, nigdy goła tablica |
| Błędy | `ProblemDetails` (RFC 9457), bez stack trace'ów i danych wewnętrznych |
| Ścieżki | `/api/v1/<zasób-w-liczbie-mnogiej>`, np. `/api/v1/tournaments` |
| Dane wrażliwe | hasła, hashe i tokeny wewnętrzne nigdy nie trafiają do DTO odpowiedzi |
| Język | nazwy typów i pól po angielsku, opisy (komentarze XML) po polsku |

### Kody odpowiedzi HTTP

| Operacja | Sukces | Błędy |
|---|---|---|
| Pobranie (GET) | `200 OK` | `404 Not Found` |
| Utworzenie (POST) | `201 Created` + nagłówek `Location` | `400`, `409 Conflict` |
| Zmiana (PUT/PATCH) | `200 OK` lub `204 No Content` | `400`, `404`, `409` |
| Usunięcie (DELETE) | `204 No Content` | `404` |
| Każda operacja | | `401` (brak logowania), `403` (brak uprawnień) |

Kody odpowiedzi opisujemy w kontrolerach atrybutami `[ProducesResponseType]`, żeby były widoczne w Swaggerze.

### Format listy z paginacją

Typ: `PagedResponse<T>` z `TurniejePro.Contracts.Common`. Parametry zapytania: `page` (od 1) i `pageSize`.

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0
}
```

### Format błędu

Każda odpowiedź błędu (400, 401, 403, 404, 409, 500) ma typ `application/problem+json` i polski `title`. Tytuł jest krótki i ogólny, a komunikat dla użytkownika jest w `detail`. Blazor wyświetla `detail`, a gdy go nie ma, listę z `errors`.

Błąd walidacji modelu (atrybuty DataAnnotations):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Błąd walidacji danych",
  "status": 400,
  "errors": {
    "Name": ["The field Name must be a string with a minimum length of 3 and a maximum length of 100."]
  }
}
```

Błąd z logiki domenowej:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Operacja niedozwolona",
  "status": 400,
  "detail": "Do wygenerowania terminarza wymagane są co najmniej 2 zaakceptowane drużyny."
}
```

### Wyjątki domenowe i globalna obsługa błędów

Logika domenowa zgłasza błędy wyjątkami z `TurniejePro.Domain.Exceptions`. Kontrolery ich **nie łapią**: zamienia je na odpowiedź `DomainExceptionHandler` (`TurniejePro.Api/Errors`), zarejestrowany w `Program.cs` przez `AddApiErrorHandling()` i `UseApiErrorHandling()`.

| Wyjątek | Kiedy | Kod | `title` |
|---|---|---|---|
| `NotFoundException` | szukany obiekt nie istnieje (np. mecz w turnieju) | `404` | Nie znaleziono zasobu |
| `ConflictException` (także `ScheduleAlreadyExistsException`, `MatchResultConflictException`) | operacja sprzeczna z bieżącym stanem danych | `409` | Konflikt z bieżącym stanem danych |
| `BusinessRuleException` | żądanie łamie regułę biznesową (np. remis w pucharze, zmiana rozpoczętego turnieju) | `400` | Operacja niedozwolona |
| `ArgumentException` (i pochodne) | encja odrzuca błędne dane (np. pusta nazwa, numer koszulki spoza 1–99) | `400` | Nieprawidłowe dane |
| każdy inny | błąd w kodzie | `500` | Wystąpił nieoczekiwany błąd serwera |

Zasady:

- **Komunikat wyjątku domenowego trafia do użytkownika** (pole `detail`), więc piszemy go po polsku i bez danych technicznych. Dopiski .NET typu „(Parameter 'name')” handler usuwa.
- **`InvalidOperationException` nie jest błędem użytkownika.** Rzuca go także sam .NET i EF Core przy błędach w kodzie, więc kończy się `500` bez szczegółów. Reguły biznesowe zgłaszamy przez `BusinessRuleException`.
- **Kontroler sam zwraca błąd** tylko wtedy, gdy wynika z jego własnego sprawdzenia (np. turniej nie istnieje → `404`, cudzy turniej → `Forbid()`). Tytuły nadane w kontrolerze zostają; domyślne angielskie tytuły frameworka są podmieniane na polskie (`ProblemTitles`).
- `401` i `403` z uwierzytelniania i `Forbid()` też dostają treść ProblemDetails (`UseStatusCodePages`).

### Zgłoszenia drużyn: kiedy API odmawia

`POST /api/v1/teams` zwraca `400` z tytułem „Rejestracja niedozwolona”, gdy turniej nie przyjmuje zgłoszeń. Powód ustala `Tournament.GetRegistrationBlockReason`; gdy zachodzi kilka naraz, obowiązuje kolejność z tabeli.

| Powód | Warunek | Komunikat (`detail`) |
|---|---|---|
| Start turnieju | turniej został rozpoczęty | Turniej został już rozpoczęty. |
| Termin | chwila zgłoszenia nie jest wcześniejsza niż termin rozpoczęcia turnieju (`startsAt`) | Upłynął termin zgłoszeń (termin rozpoczęcia turnieju). |
| Limit | liczba zgłoszeń zajmujących miejsce (oczekujących i zaakceptowanych) osiągnęła `maxTeams` | Osiągnięto maksymalny limit zarejestrowanych drużyn (N). |

Osobnego terminu zgłoszeń nie ma: zgłoszenia przyjmowane są do chwili rozpoczęcia turnieju. Do limitu liczą się zgłoszenia oczekujące i zaakceptowane (`Tournament.ActiveTeamsCount`). **Odrzucone zgłoszenie zwalnia miejsce**, więc po odrzuceniu drużyny w pełnym turnieju można zgłosić kolejną. Tę samą liczbę sprawdza zmiana limitu w `PUT /api/v1/tournaments/{id}`: nowy limit nie może być mniejszy niż liczba zgłoszeń oczekujących i zaakceptowanych.

## Wzorzec: jak wygląda DTO

Pierwszy zasób (`Tournaments`) jest wzorem do kopiowania przy kolejnych. Zasób Turnieje może go zmieniać, ale pozostałe zasoby trzymają się tego samego kształtu.

**Odpowiedź** (to, co API zwraca):

```csharp
namespace TurniejePro.Contracts.Tournaments;

public sealed record TournamentResponse(
    Guid Id,
    string Name,
    string Discipline,
    TournamentFormat Format,
    DateTimeOffset StartsAt,
    int MaxTeams);
```

**Żądanie** (to, co klient wysyła, z walidacją):

```csharp
using System.ComponentModel.DataAnnotations;
using TurniejePro.Contracts.Validation;

namespace TurniejePro.Contracts.Tournaments;

public sealed record CreateTournamentRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Name,
    [Required, StringLength(50)] string Discipline,
    [EnumDataType(typeof(TournamentFormat))] TournamentFormat Format,
    [FutureDate] DateTimeOffset StartsAt,
    [Range(2, 128)] int MaxTeams);
```

**Enum:**

```csharp
using System.Text.Json.Serialization;

namespace TurniejePro.Contracts.Tournaments;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TournamentFormat
{
    League = 1,
    Cup = 2
}
```

Zasady do zapamiętania:

- DTO nie zawiera logiki, tylko dane i atrybuty walidacji.
- Atrybuty walidacji stawiamy na parametrach rekordu (bez `property:`). Walidują je MVC i API, ale nie `<DataAnnotationsValidator />` z Blazora.
- Reguły walidacji kontraktów testujemy pomocnikiem `MvcValidation` z projektu testów (wzór: `TournamentRequestValidationTests`), a nie przez `Validator.TryValidateObject`: ten nie widzi atrybutów na parametrach rekordu, więc każdy model wyglądałby na poprawny. Test `RequestContractsTests` sprawdza przy okazji, że żaden nowy `*Request` nie ma atrybutów na właściwościach (to powodowało błąd 500).
- Formularze Blazora **nie wiążą się bezpośrednio z rekordami `*Request`**. Mają własny model formularza (klasa z właściwościami i atrybutami walidacji, np. `LoginModel` w `Login.razor`), a rekord z `Contracts` powstaje dopiero przy wysyłaniu do API. Reguły walidacji z kontraktu powtarzamy w modelu formularza, żeby użytkownik dostał komunikat przed wysłaniem.
- Pole opcjonalne oznaczamy `?` (np. `string? Description`), a nie wartością domyślną.
- Typy `Domain` i `Data` nie pojawiają się w kontraktach.
- Enum w `Contracts` jest osobnym typem od enuma w `Domain`. Mapowanie między nimi leży w `Api/Mapping/`, a wartości trzymamy zgodne.

## Jak dodać nowy zasób

1. Utwórz folder `src/TurniejePro.Contracts/<Zasób>/` i dodaj typy według wzorca powyżej.
2. Dodaj kontroler w `src/TurniejePro.Api/Controllers/` z atrybutami `[ProducesResponseType]` dla wszystkich kodów odpowiedzi.
3. Dodaj mapowanie encja ↔ DTO w `src/TurniejePro.Api/Mapping/`.
4. Sprawdź w `/swagger`, że schematy, pola wymagane i enumy wyglądają zgodnie z konwencjami.
5. Wpisz zasób w „Katalogu zasobów" poniżej.
6. Otwórz PR. Zmiana w `TurniejePro.Contracts` wymaga zatwierdzenia przez osobę od backendu **i** od frontendu (wymusza to `CODEOWNERS`).

## Zmiany kontraktów

- Zmiana **kompatybilna wstecz** (nowe pole opcjonalne, nowy endpoint) jest dozwolona po review.
- Zmiana **łamiąca** (usunięcie lub zmiana typu pola, zmiana ścieżki lub kodu odpowiedzi, zmiana wartości enuma) wymaga wcześniejszego ogłoszenia w zespole i ustalenia, kiedy obie strony wdrażają zmianę.
- Wartości enumów nie zmieniamy ani nie usuwamy, tylko dodajemy nowe.
- Po pierwszym wdrożeniu (FE-10) zmiana łamiąca wymaga jednoczesnego wdrożenia API i Blazora albo wersjonowania (`/api/v2/...`).

## Katalog zasobów

| Zasób | Endpoint | Request | Response | Właściciel | Status |
|---|---|---|---|---|---|
| Turnieje | `/api/v1/tournaments` | `CreateTournamentRequest`, `UpdateTournamentRequest` | `TournamentResponse`, `TournamentDetailsResponse` | Adam Bednarski | gotowe |
| Drużyny | `/api/v1/teams` | `CreateTeamRequest`, `UpdateTeamRequest` | `TeamResponse` | Adam Bednarski | gotowe |
| Mecze | `/api/v1/matches` | `UpdateMatchResultRequest` | `MatchResponse` | `TODO` | do wykonania |
| Tabela | `/api/v1/tournaments/{id}/standings` | brak | `StandingsResponse` | `TODO` | do wykonania |
| Konta | `/api/v1/auth` | `LoginRequest` | `AuthResponse` | Adam Bednarski | gotowe |
| Eksport (plik) | `/api/v1/tournaments/{id}/export/{csv|pdf}` | brak | plik `text/csv` albo `application/pdf` | Paweł Szczypior, Radosław Kawa | gotowe (KAN-310, KAN-313, KAN-311) |
| Moje turnieje i drużyny | `/api/v1/me/tournaments`, `/api/v1/me/teams` | brak | `MyTournamentResponse`, `MyTeamResponse` | Radosław Kawa | gotowe (KAN-304) |

Listy „moich” zasobów (`/api/v1/me/...`) zwracają zwykłą tablicę, bez `PagedResponse`: jeden użytkownik ma kilka, najwyżej kilkadziesiąt turniejów i drużyn, a UI pokazuje je w całości. Kolejność ustala API: najpierw turnieje w trakcie, potem zapisy, szkice i zakończone, w obrębie etapu według terminu rozpoczęcia. Przestrzeń nazw to `Profile`, a nie `Me`, bo `Me` jest słowem kluczowym Visual Basic (reguła CA1716).

Nazwy typów i ścieżki w wierszach „do wykonania" są propozycjami. Właściciel zasobu może je doprecyzować, trzymając się konwencji z tego dokumentu.

## Historia decyzji

| Data | Decyzja | Uczestnicy |
|---|---|---|
| `TODO` | Wspólne typy DTO w projekcie `TurniejePro.Contracts`; konwencje z tego dokumentu | `TODO` |
| 2026-10-27 | Atrybuty walidacji na parametrach rekordów `*Request`, bez prefiksu `property:` (z prefiksem MVC kończy żądanie błędem 500). Formularze Blazora mają własne modele walidacji | `TODO` |
| 2026-11-14 | Wyjątki domenowe (`NotFoundException`, `ConflictException`, `BusinessRuleException`) i jeden globalny `IExceptionHandler` zamiast `try/catch` w kontrolerach; wszystkie błędy jako ProblemDetails z polskim tytułem | KAN-303 |
