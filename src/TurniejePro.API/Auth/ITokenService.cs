using TurniejePro.Contracts.Auth;

namespace TurniejePro.Api.Auth;

public interface ITokenService
{
    AuthResponse CreateToken(Guid userId, string email, IEnumerable<string> roles);
}
