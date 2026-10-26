using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace TurniejePro.Contracts.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password
);
