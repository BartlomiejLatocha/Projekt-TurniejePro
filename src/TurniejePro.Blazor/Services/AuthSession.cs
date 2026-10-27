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

    public void Start(AuthResponse response) => Current = response;

    public void Clear() => Current = null;
}
