using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Contracts.Auth;
using Server.Contracts.Common;
using Server.Interface;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly ILibraryDataAccess _dataAccess;
    private readonly PasswordHashService _passwordHashService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly GoogleIdentityService _googleIdentityService;
    private readonly SmtpEmailService _smtpEmailService;
    private readonly PasswordResetTokenService _passwordResetTokenService;

    public AuthController(
        ILibraryDataAccess dataAccess,
        PasswordHashService passwordHashService,
        JwtTokenService jwtTokenService,
        GoogleIdentityService googleIdentityService,
        SmtpEmailService smtpEmailService,
        PasswordResetTokenService passwordResetTokenService,
        CurrentUserService currentUser)
        : base(currentUser)
    {
        _dataAccess = dataAccess;
        _passwordHashService = passwordHashService;
        _jwtTokenService = jwtTokenService;
        _googleIdentityService = googleIdentityService;
        _smtpEmailService = smtpEmailService;
        _passwordResetTokenService = passwordResetTokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult<LoginResponse>>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _dataAccess.GetActiveUserForAuthenticationAsync(request.Username, cancellationToken);
        if (authResult.User is null || string.IsNullOrWhiteSpace(authResult.PasswordHash))
        {
            return Failure<LoginResponse>("Sai tên đăng nhập hoặc mật khẩu.", StatusCodes.Status401Unauthorized);
        }

        var passwordResult = _passwordHashService.VerifyPassword(request.Password, authResult.PasswordHash);
        if (passwordResult == PasswordVerificationResult.Invalid)
        {
            return Failure<LoginResponse>("Sai tên đăng nhập hoặc mật khẩu.", StatusCodes.Status401Unauthorized);
        }

        if (passwordResult == PasswordVerificationResult.ValidLegacyHash)
        {
            await _dataAccess.UpdateUserPasswordHashAsync(request.Username, _passwordHashService.HashPassword(request.Password), cancellationToken);
        }

        var rawRoleName = await _dataAccess.GetUserRoleNameAsync(request.Username, cancellationToken);
        var user = AuthUserMapper.FromAuthenticationUser(authResult.User, rawRoleName);
        var token = _jwtTokenService.CreateToken(user);

        var userId = await _dataAccess.GetUserIdByUsernameAsync(user.Username, cancellationToken);
        await _dataAccess.AddSystemLogAsync("LOGIN", "User", null, $"Đăng nhập REST/JWT: {user.Username}", userId, cancellationToken);

        return Success(new LoginResponse
        {
            AccessToken = token.Token,
            ExpiresAt = token.ExpiresAt,
            User = user
        }, "Đăng nhập thành công.");
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult<LoginResponse>>> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var googlePayload = await _googleIdentityService.ValidateIdTokenAsync(request.IdToken, cancellationToken);
        if (googlePayload is null || string.IsNullOrWhiteSpace(googlePayload.Email) || !googlePayload.EmailVerified)
        {
            return Failure<LoginResponse>("Mã đăng nhập Google không hợp lệ hoặc email chưa được xác minh.", StatusCodes.Status401Unauthorized);
        }

        var authUser = await _dataAccess.AuthenticateByEmailAsync(googlePayload.Email, cancellationToken);
        if (authUser is null)
        {
            return Failure<LoginResponse>("Email Google chưa được cấp quyền truy cập hệ thống.", StatusCodes.Status401Unauthorized);
        }

        var username = authUser.GetType().GetProperty("Username")?.GetValue(authUser) as string ?? string.Empty;
        var rawRoleName = await _dataAccess.GetUserRoleNameAsync(username, cancellationToken);
        var user = AuthUserMapper.FromAuthenticationUser(authUser, rawRoleName);
        var token = _jwtTokenService.CreateToken(user);

        var userId = await _dataAccess.GetUserIdByEmailAsync(googlePayload.Email, cancellationToken);
        await _dataAccess.AddSystemLogAsync("LOGIN_GOOGLE", "User", null, $"Đăng nhập Google REST/JWT: {googlePayload.Email}", userId, cancellationToken);

        return Success(new LoginResponse
        {
            AccessToken = token.Token,
            ExpiresAt = token.ExpiresAt,
            User = user
        }, "Đăng nhập Google thành công.");
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var username = await _dataAccess.GetUsernameByEmailAsync(request.Email, cancellationToken);
        if (!string.IsNullOrWhiteSpace(username))
        {
            var token = _passwordResetTokenService.CreateToken(request.Email);
            var resetUrl = $"{request.ResetBaseUrl.TrimEnd('/')}?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";
            await _smtpEmailService.SendPasswordResetEmailAsync(request.Email, resetUrl, cancellationToken);
            var userId = await _dataAccess.GetUserIdByEmailAsync(request.Email, cancellationToken);
            await _dataAccess.AddSystemLogAsync("FORGOT_PASSWORD", "User", userId?.ToString(), $"Gửi email đặt lại mật khẩu: {request.Email}", userId, cancellationToken);
        }

        return SuccessMessage("Nếu email tồn tại trong hệ thống, chúng tôi đã gửi liên kết đặt lại mật khẩu.");
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!_passwordResetTokenService.ValidateAndConsume(request.Email, request.Token))
        {
            return Failure("Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        }

        var username = await _dataAccess.GetUsernameByEmailAsync(request.Email, cancellationToken);
        if (string.IsNullOrWhiteSpace(username))
        {
            return Failure("Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        }

        await _dataAccess.ChangePasswordAsync(username, _passwordHashService.HashPassword(request.NewPassword), cancellationToken);
        var userId = await _dataAccess.GetUserIdByEmailAsync(request.Email, cancellationToken);
        await _dataAccess.AddSystemLogAsync("RESET_PASSWORD_SELF", "User", userId?.ToString(), $"Người dùng đặt lại mật khẩu qua email: {request.Email}", userId, cancellationToken);
        return SuccessMessage("Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResult<object>>> Profile(CancellationToken cancellationToken)
    {
        var profile = await _dataAccess.GetUserProfileAsync(CurrentUsername, cancellationToken);
        return profile is null
            ? Failure<object>("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound)
            : Success(profile);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResult>> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _dataAccess.UpdateUserProfileAsync(CurrentUsername, request.NewUsername, request.Phone, request.AvatarUrl, cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "UPDATE_PROFILE", "User", CurrentUserId?.ToString(), $"Cập nhật hồ sơ: {CurrentUsername}", cancellationToken);
            return SuccessMessage("Cập nhật hồ sơ thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation(ex);
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResult>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var authenticationResult = await _dataAccess.GetActiveUserForAuthenticationAsync(CurrentUsername, cancellationToken);
        if (authenticationResult.User is null || string.IsNullOrWhiteSpace(authenticationResult.PasswordHash))
        {
            return Failure("Không tìm thấy tài khoản.", StatusCodes.Status404NotFound);
        }

        if (_passwordHashService.VerifyPassword(request.OldPassword, authenticationResult.PasswordHash) == PasswordVerificationResult.Invalid)
        {
            return Failure("Mật khẩu cũ không đúng.");
        }

        try
        {
            await _dataAccess.ChangePasswordAsync(CurrentUsername, _passwordHashService.HashPassword(request.NewPassword), cancellationToken);
            await AddActorSystemLogAsync(_dataAccess, "CHANGE_PASSWORD", "User", CurrentUserId?.ToString(), $"Đổi mật khẩu: {CurrentUsername}", cancellationToken);
            return SuccessMessage("Đổi mật khẩu thành công.");
        }
        catch (InvalidOperationException ex)
        {
            return HandleInvalidOperation(ex);
        }
    }
}
