using System.Text.Json;
using System.Net.Sockets;
using Client_web.Models.ViewModels;
using Client_web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace Client_web.Controllers;

public sealed class AuthController : Controller
{
    private readonly IAuthApiClient _authApi;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthApiClient authApi, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _authApi = authApi;
        _environment = environment;
        _configuration = configuration;
    }

    private string? GetGoogleClientId()
    {
        var clientId = _configuration["GoogleAuth:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = _configuration["Authentication:Google:ClientId"];
        }

        return clientId;
    }

    private bool IsGoogleLoginEnabled()
    {
        return !string.IsNullOrWhiteSpace(GetGoogleClientId());
    }

    private void PrepareLoginView(string? returnUrl)
    {
        var googleClientId = GetGoogleClientId();
        ViewBag.ReturnUrl = returnUrl;
        ViewBag.GoogleClientId = googleClientId;
        ViewBag.GoogleEnabled = !string.IsNullOrWhiteSpace(googleClientId);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Auth.Username")))
        {
            return RedirectToAction("Index", "Home");
        }

        if (TempData["Error"] is string tempError && !string.IsNullOrWhiteSpace(tempError))
        {
            ModelState.AddModelError(string.Empty, tempError);
        }

        var googleError = Request.Query["googleError"].ToString();
        if (!string.IsNullOrWhiteSpace(googleError))
        {
            ModelState.AddModelError(string.Empty, $"Đăng nhập Google thất bại: {googleError}");
        }

        PrepareLoginView(returnUrl);
        return View(new LoginVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm model, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            PrepareLoginView(returnUrl);
            return View(model);
        }

        ApiResponse response;
        try
        {
            response = await _authApi.LoginAsync(new
            {
                model.Username,
                model.Password
            }, cancellationToken);
        }
        catch (SocketException)
        {
            PrepareLoginView(returnUrl);
            ModelState.AddModelError(string.Empty, "Máy chủ chưa hoạt động");
            return View(model);
        }
        catch (IOException)
        {
            PrepareLoginView(returnUrl);
            ModelState.AddModelError(string.Empty, "Máy chủ chưa hoạt động");
            return View(model);
        }
        catch (TimeoutException)
        {
            PrepareLoginView(returnUrl);
            ModelState.AddModelError(string.Empty, "Hệ thống phản hồi chậm, vui lòng thử lại.");
            return View(model);
        }

        if (!response.Success)
        {
            PrepareLoginView(returnUrl);
            ModelState.AddModelError(string.Empty, UiTextLocalizer.TranslateMessage(response.Message));
            return View(model);
        }

        var user = response.Data.Deserialize<LoginUserVm>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (user is null)
        {
            PrepareLoginView(returnUrl);
            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác");
            return View(model);
        }

        HttpContext.Session.SetString("Auth.Username", user.Username);
        HttpContext.Session.SetString("Auth.FullName", user.FullName);
        HttpContext.Session.SetString("Auth.Role", user.Role);
        HttpContext.Session.SetString("Auth.AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? "/images/avatar/default.jpg" : user.AvatarUrl);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoogleLogin(string credential, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!IsGoogleLoginEnabled())
        {
            TempData["Error"] = "Đăng nhập Google chưa được cấu hình trên hệ thống.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (string.IsNullOrWhiteSpace(credential))
        {
            TempData["Error"] = "Không nhận được mã đăng nhập Google.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        ApiResponse response;
        try
        {
            response = await _authApi.LoginGoogleAsync(new
            {
                IdToken = credential
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Đăng nhập Google thất bại khi gọi máy chủ: {ex.Message}";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var user = response.Data.Deserialize<LoginUserVm>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (user is null)
        {
            TempData["Error"] = "Không đọc được thông tin tài khoản từ phản hồi đăng nhập Google.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        HttpContext.Session.SetString("Auth.Username", user.Username);
        HttpContext.Session.SetString("Auth.FullName", user.FullName);
        HttpContext.Session.SetString("Auth.Role", user.Role);
        HttpContext.Session.SetString("Auth.AvatarUrl", string.IsNullOrWhiteSpace(user.AvatarUrl) ? "/images/avatar/default.jpg" : user.AvatarUrl);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("Auth.Username")))
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new ForgotPasswordVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resetUrl = Url.Action(nameof(ResetPassword), "Auth", null, Request.Scheme) ?? string.Empty;
        var response = await _authApi.ForgotPasswordAsync(new
        {
            model.Email,
            ResetBaseUrl = resetUrl
        }, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(ForgotPassword));
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "Liên kết đặt lại mật khẩu không hợp lệ.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordVm
        {
            Email = email,
            Token = token
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var response = await _authApi.ResetPasswordAsync(new
        {
            model.Email,
            model.Token,
            model.NewPassword
        }, cancellationToken);

        if (!response.Success)
        {
            ModelState.AddModelError(string.Empty, UiTextLocalizer.TranslateMessage(response.Message));
            return View(model);
        }

        TempData["Success"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> Profile(string tab = "info", CancellationToken cancellationToken = default)
    {
        var username = HttpContext.Session.GetString("Auth.Username");
        if (string.IsNullOrWhiteSpace(username))
        {
            return RedirectToAction(nameof(Login));
        }

        var response = await _authApi.GetProfileAsync(cancellationToken);
        if (!response.Success)
        {
            TempData["Error"] = UiTextLocalizer.TranslateMessage(response.Message);
            return RedirectToAction("Index", "Home");
        }

        var profile = response.Data.Deserialize<ProfileVm>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (profile is null)
        {
            TempData["Error"] = "Không đọc được thông tin hồ sơ.";
            return RedirectToAction("Index", "Home");
        }

        ViewBag.ActiveTab = string.Equals(tab, "password", StringComparison.OrdinalIgnoreCase) ? "password" : "info";
        return View(profile);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string? newUsername, string? phone, IFormFile? avatarFile, CancellationToken cancellationToken)
    {
        var username = HttpContext.Session.GetString("Auth.Username");
        if (string.IsNullOrWhiteSpace(username))
        {
            return RedirectToAction(nameof(Login));
        }

        var normalizedNewUsername = newUsername?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedNewUsername))
        {
            TempData["Error"] = "Tên đăng nhập không được để trống.";
            return RedirectToAction(nameof(Profile), new { tab = "info" });
        }

        var avatarUrl = HttpContext.Session.GetString("Auth.AvatarUrl");
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            avatarUrl = "/images/avatar/default.jpg";
        }

        if (avatarFile is not null && avatarFile.Length > 0)
        {
            var extension = Path.GetExtension(avatarFile.FileName)?.ToLowerInvariant();
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp" };
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Chỉ hỗ trợ ảnh định dạng PNG, JPG, JPEG hoặc WEBP.";
                return RedirectToAction(nameof(Profile), new { tab = "info" });
            }

            const long maxBytes = 5 * 1024 * 1024;
            if (avatarFile.Length > maxBytes)
            {
                TempData["Error"] = "Kích thước ảnh tối đa là 5MB.";
                return RedirectToAction(nameof(Profile), new { tab = "info" });
            }

            var avatarDirectory = Path.Combine(_environment.WebRootPath, "images", "avatar");
            Directory.CreateDirectory(avatarDirectory);

            var fileName = $"user-{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(avatarDirectory, fileName);

            await using (var fileStream = System.IO.File.Create(filePath))
            {
                await avatarFile.CopyToAsync(fileStream, cancellationToken);
            }

            avatarUrl = $"/images/avatar/{fileName}";
        }

        var response = await _authApi.UpdateProfileAsync(new
        {
            Username = username,
            NewUsername = normalizedNewUsername,
            Phone = phone,
            AvatarUrl = avatarUrl
        }, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        if (response.Success)
        {
            HttpContext.Session.SetString("Auth.Username", normalizedNewUsername);
            HttpContext.Session.SetString("Auth.AvatarUrl", avatarUrl);
        }

        return RedirectToAction(nameof(Profile), new { tab = "info" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string? oldPassword, string? newPassword, string? confirmNewPassword, CancellationToken cancellationToken)
    {
        var username = HttpContext.Session.GetString("Auth.Username");
        if (string.IsNullOrWhiteSpace(username))
        {
            return RedirectToAction(nameof(Login));
        }

        if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmNewPassword))
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ thông tin đổi mật khẩu.";
            return RedirectToAction(nameof(Profile), new { tab = "password" });
        }

        if (!string.Equals(newPassword, confirmNewPassword, StringComparison.Ordinal))
        {
            TempData["Error"] = "Mật khẩu mới và xác nhận mật khẩu mới không khớp.";
            return RedirectToAction(nameof(Profile), new { tab = "password" });
        }

        var response = await _authApi.ChangePasswordAsync(new
        {
            Username = username,
            OldPassword = oldPassword,
            NewPassword = newPassword
        }, cancellationToken);

        TempData[response.Success ? "Success" : "Error"] = UiTextLocalizer.TranslateMessage(response.Message);
        return RedirectToAction(nameof(Profile), new { tab = "password" });
    }
}


