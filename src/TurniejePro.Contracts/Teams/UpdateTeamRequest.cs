using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Teams;

public sealed record UpdateTeamRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    [EmailAddress, StringLength(100)] string? ContactEmail);
