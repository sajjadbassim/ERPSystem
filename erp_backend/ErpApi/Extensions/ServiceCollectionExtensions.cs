using System.Text;
using ErpApi.Core.Constants;
using ErpApi.Core.Interfaces;
using ErpApi.Core.Models;
using ErpApi.Core.Options;
using ErpApi.Data;
using ErpApi.Data.Interceptors;
using ErpApi.Repositories;
using ErpApi.Services.Accounting;
using ErpApi.Services.Audit;
using ErpApi.Services.Identity;
using ErpApi.Services.MasterData;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ErpApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Scoped لأنه يحتاج ICurrentUserService: الفاعل خاصية طلب لا خاصية تطبيق
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options
                .UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
                .AddInterceptors(serviceProvider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    // لم تُستدعَ من Program.cs بعد: التسجيل والمصادقة ونقاط النهاية من نصيب الخطوة الثامنة
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddHttpContextAccessor();

        // نفس الصنف الذي يبذر به الاختبار كلمات المرور. أي بديل يجعل E01 يرسب أبداً
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ICurrencyRepository, CurrencyRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IFxRateRepository, FxRateRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IFiscalCalendarRepository, FiscalCalendarRepository>();
        services.AddScoped<INumberSequenceRepository, NumberSequenceRepository>();

        // مستودعا القيد والسطر للقراءة فقط، والكتابة عبر البوابة التي تستدعي الإجراء
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IJournalLineRepository, JournalLineRepository>();
        services.AddScoped<IJournalPostingGateway, JournalPostingGateway>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoleService, RoleService>();

        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IFxRateService, FxRateService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IFiscalCalendarService, FiscalCalendarService>();

        services.AddScoped<IJournalEntryService, JournalEntryService>();

        return services;
    }

    public static IServiceCollection AddAppAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                      ?? new JwtOptions();

        // الفشل الصريح عند الإقلاع لا عند أول تسجيل دخول: مفتاح فارغ يعني رموزاً
        // يمكن تزويرها، والسقوط الصامت هنا ثغرة لا إزعاج
        if (string.IsNullOrWhiteSpace(options.Key) || options.Key.Length < 32)
        {
            throw new InvalidOperationException(
                "مفتاح JWT غائب أو أقصر من 32 محرفاً. اضبط Jwt:Key في User Secrets أو متغيرات البيئة.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                // بلا تحويل أسماء المطالبات: erp:uid يبقى كما هو ولا يُترجم sub إلى اسم آخر
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),

                    // بدل الافتراضي خمس دقائق (بند 13): الرمز المنتهي مرفوض بلا سماح
                    ClockSkew = TimeSpan.Zero
                };

                jwt.Events = new JwtBearerEvents
                {
                    // التوقيع سليم لا يعني أن الجلسة قائمة. التعطيل وتغيير الختم يجب
                    // أن يُسقطا الرمز فوراً لا عند انتهائه (الحرّاس F03, F09, F10)
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var userIdRaw = principal?.FindFirst(AppClaimTypes.UserId)?.Value;
                        var stampRaw = principal?.FindFirst(AppClaimTypes.SecurityStamp)?.Value;

                        if (!Guid.TryParse(userIdRaw, out var userId)
                            || !Guid.TryParse(stampRaw, out var stamp))
                        {
                            context.Fail("Token is missing identity claims.");
                            return;
                        }

                        var userService = context.HttpContext.RequestServices
                            .GetRequiredService<IUserService>();

                        if (!await userService.IsSessionValidAsync(
                                userId, stamp, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Session is no longer valid.");
                        }
                    }
                };
            });

        // كل نقطة نهاية محمية افتراضياً، والعام يُعلَن بـ [AllowAnonymous] صراحةً (بند 13)
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
