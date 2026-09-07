using ErpApi.Core.Constants;
using ErpApi.Core.DTO.Auth;
using ErpApi.Core.Exceptions;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Repositories;
using Microsoft.AspNetCore.Identity;

namespace ErpApi.Services.Identity;

public class AuthService : IAuthService
{
    // تجزئة صورية بالشكل نفسه، تُتحقَّق عند غياب المستخدم ليبقى زمن الرد ثابتاً.
    // بدونها يكشف فارق الزمن وجود الاسم من عدمه، فتنهار غاية E03 من خلف الرسالة الموحّدة
    private const string DummyHash =
        "AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        IUserRepository userRepository,
        IUserService userService,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher)
    {
        _userRepository = userRepository;
        _userService = userService;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<TokenPairDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var candidates = await _userRepository.GetActiveByUserNameAsync(
            request.UserName, request.CompanyCode, ct);

        // اسم واحد في شركتين حالة مسموحة بنيوياً (الحارس E07). رمز الشركة يرفع الالتباس
        // حين يُرسَل؛ وحين يُغيَّب ويتعدّد المطابقون يبقى الرفض كما هو — التخمين بين
        // هويتين ممنوع، والالتباس يُرفض كما يُرفض الاسم المجهول تماماً
        if (candidates.Count != 1)
        {
            _passwordHasher.VerifyHashedPassword(new User(), DummyHash, request.Password);
            throw new UnauthorizedException(AuthMessages.InvalidCredentials);
        }

        var user = candidates[0];
        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedException(AuthMessages.InvalidCredentials);
        }

        return await IssuePairAsync(user, ct);
    }

    public async Task<TokenPairDto> RefreshAsync(RefreshRequestDto request, CancellationToken ct = default)
    {
        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _userRepository.GetRefreshTokenByHashAsync(hash, ct);

        if (stored is null)
        {
            throw new UnauthorizedException(AuthMessages.SessionNoLongerValid);
        }

        // رمز مُبطَل يُقدَّم من جديد: إمّا سرقة وإمّا إعادة إرسال. الحالتان تُعامَلان
        // كسرقة فتُبطَل السلسلة كلها لا الرمز وحده — الفشل مغلق (الحارس F02)
        if (stored.RevokedAt is not null)
        {
            await RevokeAllAsync(stored.UserId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            throw new UnauthorizedException(AuthMessages.SessionNoLongerValid);
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedException(AuthMessages.SessionNoLongerValid);
        }

        var user = await _userRepository.GetByIdAsync(stored.UserId, ct);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException(AuthMessages.SessionNoLongerValid);
        }

        var pair = await IssuePairAsync(user, ct, replacing: stored);
        return pair;
    }

    public async Task ChangePasswordAsync(ChangePasswordRequestDto request, CancellationToken ct = default)
    {
        var context = await _userService.GetValidatedContextAsync(ct);
        var user = context.User;

        var verification = _passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.CurrentPassword);

        if (verification == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedException(AuthMessages.InvalidCredentials);
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);

        // الختم الجديد يُسقط كل رمز وصول قائم عند أول طلب، وإبطال رموز التجديد
        // يُغلق الباب الآخر. الاثنان معاً لا أحدهما (الحارس F03)
        user.SecurityStamp = Guid.NewGuid();
        _userRepository.Update(user);

        await RevokeAllAsync(user.Id, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<TokenPairDto> IssuePairAsync(
        User user, CancellationToken ct, RefreshToken? replacing = null)
    {
        var accessToken = _tokenService.CreateAccessToken(user, out var expiresAt);
        var (rawRefreshToken, tokenHash) = _tokenService.CreateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = _tokenService.RefreshTokenExpiryUtc()
        };

        await _userRepository.AddRefreshTokenAsync(refreshToken, ct);

        // التدوير: القديم يُبطَل ويشير إلى خلفه، فتُعرف السلسلة عند اكتشاف إعادة استعمال
        if (replacing is not null)
        {
            replacing.RevokedAt = DateTime.UtcNow;
            replacing.ReplacedByTokenId = refreshToken.Id;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new TokenPairDto
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            AccessTokenExpiresAt = expiresAt
        };
    }

    private async Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        var active = await _userRepository.GetActiveRefreshTokensAsync(userId, ct);
        var now = DateTime.UtcNow;

        foreach (var token in active)
        {
            token.RevokedAt = now;
        }
    }
}
