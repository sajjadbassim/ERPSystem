using ErpApi.Core.Models;

namespace ErpApi.Services.Identity;

public interface ITokenService
{
    string CreateAccessToken(User user, out DateTime expiresAtUtc);

    // يُرجع الرمز الخام مرة واحدة لحامله، والتجزئة وحدها هي ما يُخزَّن (الحارس F07)
    (string RawToken, byte[] TokenHash) CreateRefreshToken();

    byte[] HashRefreshToken(string rawToken);

    DateTime RefreshTokenExpiryUtc();
}
