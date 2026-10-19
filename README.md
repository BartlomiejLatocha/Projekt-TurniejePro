# Projekt-TurniejePro

Projekt zespołowy aplikacji internetowych

Aplikacja webowa do organizacji turniejów sportowych: tworzenie turniejów, zgłaszanie drużyn, generowanie terminarza (liga i puchar), wprowadzanie wyników oraz automatyczna tabela.

> Status: projekt w trakcie realizacji (zajęcia projektowe).

## Główne funkcje

- Konta użytkowników i role (organizator, uczestnik, kibic z dostępem tylko do podglądu)
- Tworzenie turniejów: nazwa, dyscyplina, system rozgrywek (liga/puchar), termin, limit drużyn
- Zgłaszanie drużyn i zawodników, akceptacja zgłoszeń przez organizatora
- Generowanie terminarza: round-robin (liga) i drabinka pucharowa
- Wyniki meczów, tabela ligowa, awans w pucharze
- Eksport wyników do PDF/CSV
- Planowane (opcjonalnie): powiadomienia w czasie rzeczywistym (SignalR), statystyki drużyn, tokeny dla kibiców

## Technologie

- .NET 10 / C#
- ASP.NET Core Web API (dokumentacja: Swagger/OpenAPI)
- Blazor (wariant: `TODO`)
- Entity Framework Core, ASP.NET Core Identity
- Baza danych: `TODO`
- xUnit (testy), coverlet + ReportGenerator (pokrycie kodu), GitHub Actions (CI)

## Struktura repozytorium

```
Projekt-TurniejePro/           
├── docs/                      
│   ├── api-contracts.md       # konwencje i katalog kontraktów
├── src/
│   ├── TurniejePro.Domain/    # encje i logika domenowa
│   ├── TurniejePro.Data/      # EF Core, migracje, repozytoria
│   ├── TurniejePro.Contracts/ # wspólne DTO (API i Blazor)
│   ├── TurniejePro.Api/       # REST API
│   └── TurniejePro.Blazor/    # interfejs użytkownika
├── tests/
│   └── TurniejePro.Tests/     # testy jednostkowe i integracyjne
├── .config/                   # manifest narzędzi dotnet (ReportGenerator)
├── .github/
│   ├── workflows/             # pipeline CI (build, testy, pokrycie)
│   ├── CODEOWNERS             # właściciele kodu i wymagane review
│   └── pull_request_template.md
├── .editorconfig              # jednolity styl kodu w zespole
├── .gitignore                 # pliki wyłączone z repozytorium
├── CONTRIBUTING.md            # zasady pracy z repozytorium
├── Directory.Build.props      # wspólne ustawienia (nullable, analizatory)
├── coverage.runsettings       # konfiguracja pomiaru pokrycia kodu
├── TurniejePro.slnx           # solution
└── README.md
```

## Uruchomienie lokalne

### Wymagania

- [.NET SDK](https://dotnet.microsoft.com/download) 10
- Visual Studio w wersji obsługującej .NET 10 (albo VS Code / Rider)
- Git

### Kroki

```bash
git clone https://github.com/<organizacja-lub-konto>/Projekt-TurniejePro.git
cd Projekt-TurniejePro
dotnet restore
dotnet build
dotnet test
```

Uruchomienie API:

```bash
dotnet run --project src/TurniejePro.Api
```

Uruchomienie interfejsu:

```bash
dotnet run --project src/TurniejePro.Blazor
```

### Konfiguracja

Sekretów (connection string, klucze) nie przechowujemy w repozytorium. Lokalnie używamy User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "<twój connection string>" --project src/TurniejePro.Api
```

## Zasady pracy zespołu

- Gałąź `main` jest chroniona: zmiany wyłącznie przez Pull Request z min. 1 zatwierdzeniem (code review).
- Nazwy gałęzi: klucz zadania z Jiry, np. `KAN-49`.
- Komunikaty commitów i tytuły PR zaczynają się od klucza zadania, np. `KAN-49: opis zmiany`.
- Scalanie metodą *Squash and merge*.
- Pull Request musi przejść w CI build, testy i próg pokrycia kodu (≥ 80%) przed scaleniem.

Szczegóły: [CONTRIBUTING.md](CONTRIBUTING.md).

## Zespół

| Osoba | Rola |
|---|---|
| Radosław Kawa | `TODO` |
| Paweł Szczypior | `TODO` |
| Adam Bednarski | `TODO` |
| Bartłomiej Latocha | `TODO` |

## Dokumentacja

- Backlog i zadania: Jira (`TODO: link`)
- API: [Swagger / OpenAPI](#dokumentacja-api-swagger--openapi) (po lokalnym uruchomieniu API)
- Wdrożenie: instrukcja zostanie dodana w ramach zadania FE-10.2

## Dokumentacja API (Swagger / OpenAPI)

API ma interaktywną dokumentację i maszynowo czytelną specyfikację. Obie są dostępne tylko w środowisku **Development**, czyli po lokalnym uruchomieniu API.

### Jak otworzyć

1. Uruchom API: F5 w Visual Studio (projekt startowy `TurniejePro.Api`, profil `https`) albo w terminalu:

   ```bash
   dotnet run --project src/TurniejePro.Api
   ```

2. Port odczytasz z konsoli (linia `Now listening on: https://localhost:XXXX`) lub z `src/TurniejePro.Api/Properties/launchSettings.json`.
3. Otwórz w przeglądarce:

| Co | Adres |
|---|---|
| Swagger UI (interaktywna dokumentacja) | `https://localhost:<port>/swagger` |
| Specyfikacja OpenAPI (JSON) | `https://localhost:<port>/openapi/v1.json` |

W Visual Studio przeglądarka powinna otworzyć Swagger UI automatycznie po F5.

### Wypróbowanie endpointu

W Swagger UI rozwiń endpoint, kliknij **Try it out**, uzupełnij parametry lub treść żądania i wybierz **Execute**. Odpowiedź, kod statusu i przykładowe dane pojawią się poniżej.

### Pobranie specyfikacji

Specyfikację możesz zapisać do pliku, np. do importu w Postmanie lub generowania klienta:

```bash
curl -k https://localhost:<port>/openapi/v1.json -o openapi.json
```

Opcja `-k` pomija weryfikację certyfikatu deweloperskiego. Plik możesz też zapisać z przeglądarki (Ctrl+S na stronie `/openapi/v1.json`).

## Licencja

`TODO`
