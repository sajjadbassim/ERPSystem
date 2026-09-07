using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ErpApi.Core.Constants;
using ErpApi.Core.Models;
using ErpApi.Core.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ErpApi.Services.Identity;

public class TokenService : ITokenService
{
    private const int RefreshTokenBytes = 32;

    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public string CreateAccessToken(User user, out DateTime expiresAtUtc)
    {
        expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        // الختم داخل الرمز ليُقارن بما في القاعدة عند كل طلب. الصلاحيات والفروع خارجه عمداً
        // (راجع AppClaimTypes): وضعها فيه يجعل السحب لا يسري حتى انتهاء الرمز
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(AppClaimTypes.UserId, user.Id.ToString()),
            new(AppClaimTypes.SecurityStamp, user.SecurityStamp.ToString())
        };

        if (user.CompanyId is { } companyId)
        {
            claims.Add(new Claim(AppClaimTypes.CompanyId, companyId.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string RawToken, byte[] TokenHash) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        return (raw, HashRefreshToken(raw));
    }

    // SHA-256 بلا ملح: الرمز نفسه عشوائي بـ 256 بت، فلا قاموس يُبنى عليه ولا حاجة لإبطاء متعمَّد
    public byte[] HashRefreshToken(string rawToken) =>
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));

    public DateTime RefreshTokenExpiryUtc() => DateTime.UtcNow.AddDays(_options.RefreshTokenDays);
}
