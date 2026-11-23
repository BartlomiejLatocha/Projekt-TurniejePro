using TurniejePro.Contracts.Auth;

namespace TurniejePro.Blazor.Services;

/// <summary>
/// Dane zalogowanego użytkownika. Żyje w obwodzie (circuit) Blazor Server, a po odświeżeniu strony jest odtwarzana
/// z pamięci przeglądarki (<see cref="SessionPersistence"/>). Sesja wygasa razem z tokenem: po tym czasie
/// <see cref="Current"/> zwraca null, a komponenty dostają zdarzenie <see cref="Changed"/>.
/// </summary>
public sealed class AuthSession : IDisposable
{
    private AuthResponse? session;
    private Timer? expiryTimer;

    /// <summary>Bieżąca sesja albo null, gdy nikt nie jest zalogowany lub token wygasł.</summary>
    public AuthResponse? Current => session is { } active && active.ExpiresAt > DateTimeOffset.UtcNow ? active : null;

    public bool IsLoggedIn => Current is not null;

    /// <summary>Prawda, gdy ostatnia sesja wygasła (także po odświeżeniu strony). Login pokazuje wtedy komunikat.</summary>
    public bool WasExpired { get; private set; }

    /// <summary>
    /// Zgłaszane po zalogowaniu, wylogowaniu i wygaśnięciu, żeby komponenty mogły się odświeżyć.
    /// Komponenty obsługują je przez InvokeAsync, bo wygaśnięcie przychodzi z wątku timera.
    /// </summary>
    public event Action? Changed;

    public void Start(AuthResponse response)
    {
        WasExpired = false;
        SetSession(response);
    }

    /// <summary>
    /// Odtwarza sesję zapisaną w przeglądarce po odświeżeniu. Zwraca false, gdy token już wygasł.
    /// </summary>
    public bool Restore(AuthResponse response)
    {
        if (response.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            WasExpired = true;
            Changed?.Invoke();
            return false;
        }

        SetSession(response);
        return true;
    }

    public void Clear()
    {
        expiryTimer?.Dispose();
        expiryTimer = null;
        session = null;
        Changed?.Invoke();
    }

    public bool IsInRole(string role) => Current?.Roles.Contains(role) ?? false;

    public bool IsInAnyRole(IEnumerable<string> roles) => roles.Any(IsInRole);

    /// <summary>
    /// Czy zalogowany użytkownik może zarządzać zasobem należącym do <paramref name="ownerId"/>
    /// (np. organizator turnieju). Administrator może zarządzać każdym zasobem.
    /// </summary>
    public bool IsOwner(Guid? ownerId) =>
        Current is not null
        && (IsInRole(RoleNames.Admin) || (ownerId is { } id && id == Current.UserId));

    public void Dispose() => expiryTimer?.Dispose();

    private void SetSession(AuthResponse response)
    {
        session = response;
        ScheduleExpiry(response.ExpiresAt);
        Changed?.Invoke();
    }

    private void ScheduleExpiry(DateTimeOffset expiresAt)
    {
        expiryTimer?.Dispose();

        var due = expiresAt - DateTimeOffset.UtcNow;
        if (due < TimeSpan.Zero)
        {
            due = TimeSpan.Zero;
        }

        expiryTimer = new Timer(_ => Expire(), null, due, Timeout.InfiniteTimeSpan);
    }

    private void Expire()
    {
        if (session is null)
        {
            return;
        }

        session = null;
        WasExpired = true;
        Changed?.Invoke();
    }
}
