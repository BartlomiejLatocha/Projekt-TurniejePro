using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Auth;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);
