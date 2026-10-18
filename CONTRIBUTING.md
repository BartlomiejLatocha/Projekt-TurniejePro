# Zasady pracy z repozytorium

Dokument opisuje, jak wprowadzamy zmiany w projekcie TurniejePro. Obowiązuje wszystkich członków zespołu.

## Zasady ogólne

- Gałąź `main` jest chroniona. Nie wypychamy na nią zmian bezpośrednio.
- Każda zmiana powstaje na osobnej gałęzi, powiązanej z zadaniem w Jirze.
- Do `main` trafia tylko kod, który przeszedł Pull Request (PR) i został zatwierdzony przez co najmniej jedną inną osobę.
- `main` ma być zawsze w stanie, który się buduje i przechodzi testy.
- Kod wprowadzany razem z testami, bo PR bez nich może nie przejść progu.

## Nazewnictwo

### Gałęzie

Gałąź nazywamy kluczem zadania z Jiry:

```
KAN-<numer>
```

Przykład: `KAN-49`

Jedna gałąź odpowiada jednemu zadaniu. Klucz w nazwie gałęzi sprawia, że Jira automatycznie łączy gałąź, commity i PR z zadaniem.

### Commity

Komunikat zaczynamy od klucza zadania, po nim dwukropek i krótki opis zmiany:

```
KAN-49: Utworzenie solution z projektami API, Domain, Data, Blazor i Tests
```

Zasady:

- klucz zadania na początku (dzięki temu commit pojawia się w zadaniu w Jirze),
- opis zwięzły i konkretny, mówiący co zostało zrobione,
- małe, logiczne commity zamiast jednego dużego na koniec pracy,
- bez commitów typu „poprawki", „zmiany", „asdf".

### Pull Requesty

Tytuł PR: klucz zadania i tytuł zadania z Jiry.

```
KAN-49: Utworzyć solution (API, Domain, Data, Blazor, Tests); włączyć nullable i analizatory Roslyn
```

W opisie PR wypełniamy szablon (zadanie, co zmieniono, jak przetestować, checklista). Kod zadania z backlogu (np. `TA-01.1.1`) możemy dopisać w opisie.

## Przebieg pracy

1. **Zaktualizuj `main`:**
   ```bash
   git switch main
   git pull
   ```
2. **Utwórz gałąź zadania** (klucz z Jiry):
   ```bash
   git switch -c KAN-49
   ```
3. **Pracuj i commituj** małymi krokami:
   ```bash
   git add .
   git commit -m "KAN-49: opis zmiany"
   ```
4. **Wypchnij gałąź:**
   ```bash
   git push -u origin KAN-49
   ```
5. **Otwórz Pull Request** do `main` na GitHubie i wypełnij szablon.
6. **Poproś o review** inną osobę z zespołu i zmień status zadania w Jirze.
7. **Reaguj na uwagi:** poprawki dodawaj kolejnymi commitami na tej samej gałęzi, a komentarze oznaczaj jako rozwiązane po wprowadzeniu zmian.
8. **Scal PR** po zatwierdzeniu (patrz niżej).
9. **Wróć na `main` i zaktualizuj go:**
   ```bash
   git switch main
   git pull
   ```

## Code review

- Każdy PR wymaga **minimum jednego zatwierdzenia** od osoby innej niż autor.
- Autor nie zatwierdza własnego PR-a.
- Recenzent sprawdza: zgodność z opisem i DoD zadania, czytelność i poprawność kodu, brak zbędnych plików (`bin/`, `obj/`, sekrety), testy oraz brak nowych ostrzeżeń kompilatora.
- Wszystkie komentarze muszą być rozwiązane przed scaleniem.
- Nowy commit po zatwierdzeniu unieważnia zatwierdzenie i wymaga ponownego przeglądu.
- Recenzję wykonujemy możliwie szybko (cel: do 1 dnia roboczego), aby nie blokować pracy zespołu.

## Scalanie

- Używamy wyłącznie **Squash and merge**. Dzięki temu na `main` jedno zadanie to jeden czytelny commit.
- Tytuł commita po squashu = tytuł PR-a (z kluczem zadania).
- Gałąź po scaleniu jest usuwana automatycznie.
- PR scala autor po otrzymaniu zatwierdzenia.

## Przed otwarciem PR

- [ ] Projekt buduje się bez błędów i ostrzeżeń (`dotnet build`)
- [ ] Testy przechodzą (`dotnet test`)
- [ ] Nowy kod ma testy, pokrycie ≥ 80%
- [ ] W commicie nie ma plików tymczasowych ani sekretów (sprawdź `git status` i `git diff`)
- [ ] Zmiany dotyczą tylko tego zadania
- [ ] Dokumentacja (README, opisy) jest zaktualizowana, jeśli to potrzebne

## Sekrety i konfiguracja

Haseł, kluczy i connection stringów nie commitujemy. Lokalnie używamy User Secrets (`dotnet user-secrets`) lub zmiennych środowiskowych. Jeśli sekret przypadkowo trafi do repozytorium, od razu informujemy zespół, bo trzeba go unieważnić (samo usunięcie z kolejnego commita nie wystarcza).

## Konflikty i aktualizacja gałęzi

Jeśli `main` zmienił się, zanim scaliłeś PR:

```bash
git switch main
git pull
git switch KAN-49
git merge main
```

Rozwiąż konflikty lokalnie, uruchom build i testy, a następnie wypchnij gałąź.
