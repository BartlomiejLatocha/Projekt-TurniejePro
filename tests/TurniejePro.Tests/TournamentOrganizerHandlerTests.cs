using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TurniejePro.Api.Auth;
using TurniejePro.Domain;
using TurniejePro.Domain.Enums;

namespace TurniejePro.Tests;

/// <summary>
/// Reguła własności turnieju (KAN-283): organizator turnieju i administrator tak, wszyscy inni nie.
/// </summary>
public class TournamentOrganizerHandlerTests
{
    private static readonly Guid OrganizerId = Guid.NewGuid();

    [Fact]
    public async Task Organizer_OfTournament_Succeeds()
    {
        var succeeded = await AuthorizeAsync(User(OrganizerId), Tournament(OrganizerId));

        Assert.True(succeeded);
    }

    [Fact]
    public async Task OtherUser_WithOrganizerRole_Fails()
    {
        var succeeded = await AuthorizeAsync(User(Guid.NewGuid(), Roles.Organizer), Tournament(OrganizerId));

        Assert.False(succeeded);
    }

    [Fact]
    public async Task Admin_OfSomeoneElsesTournament_Succeeds()
    {
        var succeeded = await AuthorizeAsync(User(Guid.NewGuid(), Roles.Admin), Tournament(OrganizerId));

        Assert.True(succeeded);
    }

    [Fact]
    public async Task TournamentWithoutOrganizer_OnlyAdminSucceeds()
    {
        var tournament = Tournament(organizerId: null);

        Assert.False(await AuthorizeAsync(User(Guid.NewGuid(), Roles.Organizer), tournament));
        Assert.True(await AuthorizeAsync(User(Guid.NewGuid(), Roles.Admin), tournament));
    }

    [Fact]
    public async Task UserWithoutSubjectClaim_Fails()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", Roles.Organizer)], "Test"));

        var succeeded = await AuthorizeAsync(user, Tournament(OrganizerId));

        Assert.False(succeeded);
    }

    [Fact]
    public async Task SubjectThatIsNotGuid_Fails()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, "nie-guid")], "Test"));

        var succeeded = await AuthorizeAsync(user, Tournament(OrganizerId));

        Assert.False(succeeded);
    }

    [Fact]
    public async Task NameIdentifierClaim_IsAcceptedWhenSubIsMissing()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, OrganizerId.ToString())], "Test"));

        var succeeded = await AuthorizeAsync(user, Tournament(OrganizerId));

        Assert.True(succeeded);
    }

    [Fact]
    public async Task AnonymousUser_Fails()
    {
        var succeeded = await AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), Tournament(OrganizerId));

        Assert.False(succeeded);
    }

    private static ClaimsPrincipal User(Guid userId, params string[] roles)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId.ToString()) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", "name", "role"));
    }

    private static Tournament Tournament(Guid? organizerId) =>
        new("Turniej", "Piłka nożna", TournamentFormat.League, DateTimeOffset.UtcNow.AddDays(7), 8, organizerId);

    private static async Task<bool> AuthorizeAsync(ClaimsPrincipal user, Tournament tournament)
    {
        var requirement = new TournamentOrganizerRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, tournament);

        await new TournamentOrganizerHandler().HandleAsync(context);

        return context.HasSucceeded;
    }
}
