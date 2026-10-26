# Diagram Związków Entencji (ERD) - TurniejePro

> Zadanie: **KAN-55: Zaprojektować ERD (Turniej, Drużyna, Zawodnik, Mecz, Token)**  
> Autor: Adam Bednarski  
> Status: Zaakceptowany

Dokument opisuje model danych bazy aplikacji `TurniejePro` oraz relacje pomiędzy encjami.

---

## Diagram ERD (Mermaid)

```mermaid
erDiagram
    TOURNAMENTS ||--o{ TEAMS : "ma zgłoszone"
    TOURNAMENTS ||--o{ MATCHES : "posiada w terminarzu"
    TOURNAMENTS ||--o{ COLLECTIBLE_TOKENS : "udostępnia"
    TEAMS ||--o{ PLAYERS : "składa się z"
    TEAMS ||--o{ MATCHES : "gra jako gospodarz"
    TEAMS ||--o{ MATCHES : "gra jako gość"

    TOURNAMENTS {
        Guid Id PK
        string Name "Wymagana, maks. 100 znaków"
        string Discipline "Wymagana, maks. 50 znaków"
        int Format "1: League, 2: Cup"
        DateTimeOffset StartsAt "Data i godzina UTC"
        int MaxTeams "Limit drużyn (2-128)"
        bool IsStarted "Status rozpoczęcia"
        Guid OrganizerId "Opcjonalny klucz organizatora"
    }

    TEAMS {
        Guid Id PK
        Guid TournamentId FK "Klucz turnieju (Cascade Delete)"
        string Name "Wymagana, maks. 100 znaków"
        Guid CaptainId "Opcjonalny klucz kapitana"
        string ContactEmail "Opcjonalny e-mail, maks. 100 znaków"
        bool IsAccepted "Czy zatwierdzona przez organizatora"
        DateTimeOffset RegisteredAt "Data zgłoszenia UTC"
    }

    PLAYERS {
        Guid Id PK
        Guid TeamId FK "Klucz drużyny (Cascade Delete)"
        string FullName "Imię i nazwisko / nick, maks. 100 znaków"
        int JerseyNumber "Opcjonalny numer na koszulce"
    }

    MATCHES {
        Guid Id PK
        Guid TournamentId FK "Klucz turnieju (Cascade Delete)"
        int Round "Numer rundy / kolejki (od 1)"
        Guid HomeTeamId FK "Drużyna gospodarzy (opcjonalna)"
        Guid AwayTeamId FK "Drużyna gości (opcjonalna)"
        int HomeScore "Bramki / punkty gospodarzy"
        int AwayScore "Bramki / punkty gości"
        int Status "1: Scheduled, 2: InProgress, 3: Finished, 4: Cancelled"
        DateTimeOffset ScheduledAt "Termin meczu UTC"
    }

    COLLECTIBLE_TOKENS {
        Guid Id PK
        Guid TournamentId FK "Klucz turnieju (Cascade Delete)"
        string Code "Unikalny kod tokenu w turnieju, maks. 50 znaków"
        string Name "Nazwa odznaki / tokenu, maks. 100 znaków"
        string Description "Opcjonalny opis, maks. 500 znaków"
        DateTimeOffset CreatedAt "Data wygenerowania UTC"
    }
```

---

## Opis relacji i reguł integralności

1. **Turniej ↔ Drużyny (`1 : N`)**
   * Każda drużyna jest przypisana do dokładnie jednego turnieju (`TournamentId`).
   * Usunięcie turnieju usuwa powiązane drużyny (`Cascade Delete`).
2. **Drużyna ↔ Zawodnicy (`1 : N`)**
   * Usunięcie drużyny usuwa jej zawodników (`Cascade Delete`).
3. **Turniej ↔ Mecze (`1 : N`)**
   * Turniej posiada listę meczów w terminarzu. Usunięcie turnieju usuwa mecze.
4. **Drużyna ↔ Mecze (`1 : N` jako gospodarz / gość)**
   * Usunięcie drużyny nie usuwa meczu (`Restrict`), aby zachować spójność historii rozgrywek.
5. **Turniej ↔ Zbieralne tokeny (`1 : N`)**
   * Każdy token należy do turnieju.
   * Nałożony jest unikalny indeks złożony na `(TournamentId, Code)` – w ramach jednego turnieju nie mogą istnieć dwa tokeny o tym samym kodzie.
