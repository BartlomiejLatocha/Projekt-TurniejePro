using System;
using System.Collections.Generic;
using System.Text;
namespace TurniejePro.Contracts.Auth;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email,
    List<string> Roles
);
