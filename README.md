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

- .NET (wersja: 10) / C#
- ASP.NET Core Web API (dokumentacja: Swagger/OpenAPI)
- Blazor (wariant: `TODO`)
- Entity Framework Core, ASP.NET Core Identity
- Baza danych: `TODO`
- xUnit (testy), GitHub Actions (CI)

## Struktura repozytorium

TurniejePro/
├── src/
│   ├── TurniejePro.Domain/    # encje i logika domenowa
│   ├── TurniejePro.Data/      # EF Core, migracje, repozytoria
│   ├── TurniejePro.Api/       # REST API
│   └── TurniejePro.Blazor/    # interfejs użytkownika
├── tests/
│   └── TurniejePro.Tests/     # testy jednostkowe i integracyjne
├── .github/                   # CI, szablon PR, CODEOWNERS
├── .editorconfig              # jednolity styl kodu w zespole
├── .gitignore                 # pliki wyłączone z repozytorium
├── Directory.Build.props      # wspólne ustawienia (nullable, analizatory)
├── TurniejePro.slnx           # solution
└── README.md

## Uruchomienie lokalne

### Wymagania

- [.NET SDK](https://dotnet.microsoft.com/download) (wersja jak wyżej)
- Visual Studio 2022 lub nowsze (albo VS Code / Rider)
- Git

### Kroki

```bash
git clone https://github.com/<organizacja-lub-konto>/TurniejePro.git
cd TurniejePro
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
- Nazwy gałęzi: `feature/<klucz-zadania>-krótki-opis`, np. `feature/TA-01.1.1-szkielet-solution`.
- Klucz zadania z Jiry umieszczamy w nazwie gałęzi i w opisie PR.
- Scalanie metodą *Squash and merge*.
- Pull Request musi przejść build i testy w CI przed scaleniem.

Szczegóły: `CONTRIBUTING.md` (do uzupełnienia).

## Zespół

| Osoba | Rola |
|---|---|
| Radosław Kawa | `TODO` |
| Paweł Szczypior | `TODO` |
| Adam Bednarski | `TODO` |
| Bartłomiej Latocha | `TODO` |

## Dokumentacja

- Backlog i zadania: Jira (`TODO: link`)
- API: Swagger UI pod `/swagger` po uruchomieniu API
- Wdrożenie: instrukcja zostanie dodana w ramach zadania FE-10.2

## Licencja

`TODO`
