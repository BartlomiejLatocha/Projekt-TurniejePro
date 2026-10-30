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
| Walidacja | atrybuty DataAnnotations na parametrach rekordów `*Request`, **bez** prefiksu `property:` (MVC rzuca wyjątek dla rekordu z walidacją na właściwościach, a API kończy żądanie błędem 500) |
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

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "name": ["The Name field is required."]
  }
}
```

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

namespace TurniejePro.Contracts.Tournaments;

public sealed record CreateTournamentRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Name,
    [Required, StringLength(50)] string Discipline,
    TournamentFormat Format,
    DateTimeOffset StartsAt,
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
| Drużyny | `/api/v1/teams` | `CreateTeamRequest` | `TeamResponse` | `TODO` | do wykonania |
| Mecze | `/api/v1/matches` | `UpdateMatchResultRequest` | `MatchResponse` | `TODO` | do wykonania |
| Tabela | `/api/v1/tournaments/{id}/standings` | brak | `StandingsResponse` | `TODO` | do wykonania |
| Konta | `/api/v1/auth` | `LoginRequest` | `AuthResponse` | Adam Bednarski | gotowe |

Nazwy typów i ścieżki w wierszach „do wykonania" są propozycjami. Właściciel zasobu może je doprecyzować, trzymając się konwencji z tego dokumentu.

## Historia decyzji

| Data | Decyzja | Uczestnicy |
|---|---|---|
| `TODO` | Wspólne typy DTO w projekcie `TurniejePro.Contracts`; konwencje z tego dokumentu | `TODO` |
| 2026-10-27 | Atrybuty walidacji na parametrach rekordów `*Request`, bez prefiksu `property:` (z prefiksem MVC kończy żądanie błędem 500). Formularze Blazora mają własne modele walidacji | `TODO` |
