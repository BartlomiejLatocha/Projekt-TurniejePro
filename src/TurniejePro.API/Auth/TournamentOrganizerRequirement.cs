using Microsoft.AspNetCore.Authorization;

namespace TurniejePro.Api.Auth;

/// <summary>
/// Wymaganie: użytkownik jest organizatorem turnieju (<c>Tournament.OrganizerId</c> równe claimowi <c>sub</c>)
/// albo ma rolę <see cref="Roles.Admin"/>. Sprawdza je <see cref="TournamentOrganizerHandler"/>.
/// </summary>
public sealed class TournamentOrganizerRequirement : IAuthorizationRequirement
{
}
