using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TurniejePro.Domain;

namespace TurniejePro.Api.Auth;

/// <summary>
/// Przepuszcza organizatora turnieju i administratora. Turniej bez organizatora (<c>OrganizerId == null</c>,
/// np. utworzony przed wprowadzeniem własności) może zmieniać tylko administrator.
/// </summary>
public sealed class TournamentOrganizerHandler : AuthorizationHandler<TournamentOrganizerRequirement, Tournament>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TournamentOrganizerRequirement requirement,
        Tournament resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (IsAdmin(context.User) || IsOrganizer(context.User, resource))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    // Claim "role" sprawdzamy wprost, bo tak wystawia go JwtTokenService (niezależnie od RoleClaimType tożsamości)
    private static bool IsAdmin(ClaimsPrincipal user) => user.HasClaim("role", Roles.Admin);

    private static bool IsOrganizer(ClaimsPrincipal user, Tournament tournament)
    {
        var subject = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return tournament.OrganizerId is { } organizerId
            && Guid.TryParse(subject, out var userId)
            && userId == organizerId;
    }
}
