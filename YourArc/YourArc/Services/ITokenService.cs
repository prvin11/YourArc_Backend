using YourArc.Data;

namespace YourArc.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}
