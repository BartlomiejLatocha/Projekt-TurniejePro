using TurniejePro.Contracts.Auth;

namespace TurniejePro.Blazor.Services;

/// <summary>
/// Dane zalogowanego użytkownika w bieżącym obwodzie (circuit) Blazor Server.
/// Po odświeżeniu strony sesja jest pusta.
/// </summary>
public sealed class AuthSession
{
    public AuthResponse? Current { get; private set; }

    public bool IsLoggedIn => Current is not null;

    /// <summary>
    /// Zgłaszane po zalogowaniu lub wylogowaniu, żeby komponenty mogły się odświeżyć.
    /// </summary>
    public event Action? Changed;

    public void Start(AuthResponse response)
    {
        Current = response;
        Changed?.Invoke();
    }

    public void Clear()
    {
        Current = null;
        Changed?.Invoke();
    }

    public bool IsInRole(string role) => Current?.Roles.Contains(role) ?? false;

    public bool IsInAnyRole(IEnumerable<string> roles) => roles.Any(IsInRole);
}
