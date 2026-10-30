using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Teams;

public sealed record CreatePlayerRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Range(1, 99)] int? JerseyNumber);
