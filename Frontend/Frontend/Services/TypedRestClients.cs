using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Client_web.Services;

internal sealed record RestRequest(HttpMethod Method, string RelativeUrl, object? Body, bool IsAuthenticationResponse = false);

public abstract class LibraryApiClientBase
{
    private const string AccessTokenSessionKey = "Auth.AccessToken";
    private const string AccessTokenExpiresAtSessionKey = "Auth.AccessTokenExpiresAt";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _baseUrl;
    private readonly string _apiKey;

    protected LibraryApiClientBase(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;

        var baseUrl = configuration["ServerApi:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Thiếu cấu hình ServerApi:BaseUrl.");
        }

        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = configuration["ServerApi:ApiKey"]
            ?? configuration["Api:Key"]
            ?? throw new InvalidOperationException("Thiếu cấu hình ServerApi:ApiKey hoặc Api:Key.");
    }

    protected static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    protected Task<ApiResponse> GetAsync(string relativeUrl, CancellationToken cancellationToken = default)
    {
        return SendRestAsync(new RestRequest(HttpMethod.Get, relativeUrl, null), cancellationToken);
    }

    protected Task<ApiResponse> PostAsync(string relativeUrl, object? body, CancellationToken cancellationToken = default, bool isAuthenticationResponse = false)
    {
        return SendRestAsync(new RestRequest(HttpMethod.Post, relativeUrl, body, isAuthenticationResponse), cancellationToken);
    }

    protected Task<ApiResponse> PutAsync(string relativeUrl, object? body, CancellationToken cancellationToken = default)
    {
        return SendRestAsync(new RestRequest(HttpMethod.Put, relativeUrl, body), cancellationToken);
    }

    protected Task<ApiResponse> PatchAsync(string relativeUrl, object? body, CancellationToken cancellationToken = default)
    {
        return SendRestAsync(new RestRequest(HttpMethod.Patch, relativeUrl, body), cancellationToken);
    }

    private async Task<ApiResponse> SendRestAsync(RestRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(request.Method, $"{_baseUrl}{request.RelativeUrl}");
            AddCommonHeaders(httpRequest);

            if (request.Body is not null && request.Method != HttpMethod.Get)
            {
                httpRequest.Content = JsonContent.Create(request.Body, options: JsonOptions);
            }

            using var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseText = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            return ParseApiResult(responseText, httpResponse.StatusCode, request.IsAuthenticationResponse);
        }
        catch (HttpRequestException)
        {
            return new ApiResponse { Success = false, Message = "Máy chủ API chưa hoạt động hoặc không thể kết nối." };
        }
        catch (TaskCanceledException)
        {
            return new ApiResponse { Success = false, Message = "Hệ thống phản hồi chậm, vui lòng thử lại." };
        }
        catch (InvalidOperationException ex)
        {
            return new ApiResponse { Success = false, Message = ex.Message };
        }
    }

    private void AddCommonHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("X-Api-Key", _apiKey);

        var token = _httpContextAccessor.HttpContext?.Session.GetString(AccessTokenSessionKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private ApiResponse ParseApiResult(string responseText, HttpStatusCode statusCode, bool isAuthenticationResponse)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new ApiResponse
            {
                Success = false,
                Message = $"Máy chủ API trả về lỗi {(int)statusCode}."
            };
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(responseText);
        }
        catch (JsonException)
        {
            return new ApiResponse
            {
                Success = false,
                Message = statusCode == HttpStatusCode.InternalServerError
                    ? "Máy chủ API gặp lỗi nội bộ. Vui lòng thử lại sau."
                    : $"Máy chủ API trả về lỗi {(int)statusCode}."
            };
        }

        using (document)
        {
            var root = document.RootElement;

            if (!ApiPayload.TryGetProperty(root, "success", out var successElement))
            {
                return ParseProblemDetails(root, statusCode);
            }

            var success = successElement.ValueKind == JsonValueKind.True;
            var message = ApiPayload.GetString(root, "message") ?? (success ? "Thành công." : $"Máy chủ API trả về lỗi {(int)statusCode}.");
            var data = ApiPayload.TryGetProperty(root, "data", out var dataElement)
                ? dataElement.Clone()
                : default;

            if (isAuthenticationResponse && success && data.ValueKind == JsonValueKind.Object)
            {
                StoreLoginToken(data);
                if (ApiPayload.TryGetProperty(data, "user", out var userElement))
                {
                    data = userElement.Clone();
                }
            }

            if (!success && string.IsNullOrWhiteSpace(message))
            {
                message = $"Máy chủ API trả về lỗi {(int)statusCode}.";
            }

            return new ApiResponse
            {
                Success = success,
                Message = message,
                Data = data
            };
        }
    }

    private static ApiResponse ParseProblemDetails(JsonElement root, HttpStatusCode statusCode)
    {
        var message = ApiPayload.GetString(root, "title") ?? $"Máy chủ API trả về lỗi {(int)statusCode}.";
        if (ApiPayload.TryGetProperty(root, "errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Object)
        {
            var messages = new List<string>();
            foreach (var property in errorsElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    messages.AddRange(property.Value.EnumerateArray()
                        .Select(x => x.GetString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))!);
                }
            }

            if (messages.Count > 0)
            {
                message = string.Join(" ", messages);
            }
        }

        return new ApiResponse
        {
            Success = false,
            Message = message
        };
    }

    private void StoreLoginToken(JsonElement loginData)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session is null)
        {
            return;
        }

        var token = ApiPayload.GetString(loginData, "accessToken");
        if (!string.IsNullOrWhiteSpace(token))
        {
            session.SetString(AccessTokenSessionKey, token);
        }

        var expiresAt = ApiPayload.GetString(loginData, "expiresAt");
        if (!string.IsNullOrWhiteSpace(expiresAt))
        {
            session.SetString(AccessTokenExpiresAtSessionKey, expiresAt);
        }
    }
}

internal static class ApiPayload
{
    public static string Escape(string value) => Uri.EscapeDataString(value);

    public static string? GetString(JsonElement payload, string propertyName)
    {
        if (!TryGetProperty(payload, propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    public static bool TryGetProperty(JsonElement payload, string propertyName, out JsonElement property)
    {
        property = default;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var item in payload.EnumerateObject())
        {
            if (string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        }

        return false;
    }

    public static void AddQuery(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{Escape(key)}={Escape(value)}");
        }
    }
}

public interface IAuthApiClient
{
    Task<ApiResponse> LoginAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> LoginGoogleAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetProfileAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> UpdateProfileAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> ChangePasswordAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> ForgotPasswordAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> ResetPasswordAsync(object payload, CancellationToken cancellationToken = default);
}

public sealed class AuthApiClient : LibraryApiClientBase, IAuthApiClient
{
    public AuthApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> LoginAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/auth/login", payload, cancellationToken, true);
    public Task<ApiResponse> LoginGoogleAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/auth/google", payload, cancellationToken, true);
    public Task<ApiResponse> GetProfileAsync(CancellationToken cancellationToken = default) => GetAsync("/api/auth/profile", cancellationToken);
    public Task<ApiResponse> UpdateProfileAsync(object payload, CancellationToken cancellationToken = default) => PutAsync("/api/auth/profile", payload, cancellationToken);
    public Task<ApiResponse> ChangePasswordAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/auth/change-password", payload, cancellationToken);
    public Task<ApiResponse> ForgotPasswordAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/auth/forgot-password", payload, cancellationToken);
    public Task<ApiResponse> ResetPasswordAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/auth/reset-password", payload, cancellationToken);
}

public interface IUsersApiClient
{
    Task<ApiResponse> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> UpdateAsync(int userId, object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> ResetPasswordAsync(int userId, object payload, CancellationToken cancellationToken = default);
}

public sealed class UsersApiClient : LibraryApiClientBase, IUsersApiClient
{
    public UsersApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetUsersAsync(CancellationToken cancellationToken = default) => GetAsync("/api/users", cancellationToken);
    public Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/users", payload, cancellationToken);
    public Task<ApiResponse> UpdateAsync(int userId, object payload, CancellationToken cancellationToken = default) => PutAsync($"/api/users/{userId}", payload, cancellationToken);
    public Task<ApiResponse> ResetPasswordAsync(int userId, object payload, CancellationToken cancellationToken = default) => PostAsync($"/api/users/{userId}/reset-password", payload, cancellationToken);
}

public interface IBooksApiClient
{
    Task<ApiResponse> GetBooksAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> GetMaxBarcodeAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> GetDetailAsync(int bookId, CancellationToken cancellationToken = default);
    Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> UpdateAsync(int bookId, object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> DeactivateAsync(int bookId, CancellationToken cancellationToken = default);
    Task<ApiResponse> LookupIsbnAsync(string isbn, CancellationToken cancellationToken = default);
}

public sealed class BooksApiClient : LibraryApiClientBase, IBooksApiClient
{
    public BooksApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetBooksAsync(CancellationToken cancellationToken = default) => GetAsync("/api/books", cancellationToken);
    public Task<ApiResponse> GetMaxBarcodeAsync(CancellationToken cancellationToken = default) => GetAsync("/api/books/max-barcode", cancellationToken);
    public Task<ApiResponse> GetDetailAsync(int bookId, CancellationToken cancellationToken = default) => GetAsync($"/api/books/{bookId}", cancellationToken);
    public Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/books", payload, cancellationToken);
    public Task<ApiResponse> UpdateAsync(int bookId, object payload, CancellationToken cancellationToken = default) => PutAsync($"/api/books/{bookId}", payload, cancellationToken);
    public Task<ApiResponse> DeactivateAsync(int bookId, CancellationToken cancellationToken = default) => PostAsync($"/api/books/{bookId}/deactivate", null, cancellationToken);
    public Task<ApiResponse> LookupIsbnAsync(string isbn, CancellationToken cancellationToken = default) => GetAsync($"/api/books/isbn-lookup?isbn={ApiPayload.Escape(isbn)}", cancellationToken);
}

public interface IBookCopiesApiClient
{
    Task<ApiResponse> LookupAsync(string? query, CancellationToken cancellationToken = default);
    Task<ApiResponse> UpdateStatusAsync(string barcode, object payload, CancellationToken cancellationToken = default);
}

public sealed class BookCopiesApiClient : LibraryApiClientBase, IBookCopiesApiClient
{
    public BookCopiesApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> LookupAsync(string? query, CancellationToken cancellationToken = default) => GetAsync($"/api/book-copies?query={ApiPayload.Escape(query ?? string.Empty)}", cancellationToken);
    public Task<ApiResponse> UpdateStatusAsync(string barcode, object payload, CancellationToken cancellationToken = default) => PatchAsync($"/api/book-copies/{ApiPayload.Escape(barcode)}/status", payload, cancellationToken);
}

public interface ICategoriesApiClient
{
    Task<ApiResponse> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default);
}

public sealed class CategoriesApiClient : LibraryApiClientBase, ICategoriesApiClient
{
    public CategoriesApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetCategoriesAsync(CancellationToken cancellationToken = default) => GetAsync("/api/categories", cancellationToken);
    public Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/categories", payload, cancellationToken);
}

public interface IMembersApiClient
{
    Task<ApiResponse> GetMembersAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> UpdateAsync(int memberId, object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> GetStatusAsync(string memberCode, CancellationToken cancellationToken = default);
}

public sealed class MembersApiClient : LibraryApiClientBase, IMembersApiClient
{
    public MembersApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetMembersAsync(CancellationToken cancellationToken = default) => GetAsync("/api/members", cancellationToken);
    public Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/members", payload, cancellationToken);
    public Task<ApiResponse> UpdateAsync(int memberId, object payload, CancellationToken cancellationToken = default) => PutAsync($"/api/members/{memberId}", payload, cancellationToken);
    public Task<ApiResponse> GetStatusAsync(string memberCode, CancellationToken cancellationToken = default) => GetAsync($"/api/members/{ApiPayload.Escape(memberCode)}/status", cancellationToken);
}

public interface ILoansApiClient
{
    Task<ApiResponse> GetLoansAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> GetDetailAsync(int loanId, CancellationToken cancellationToken = default);
    Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> ReturnByBarcodeAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> RenewAsync(int loanId, object payload, CancellationToken cancellationToken = default);
}

public sealed class LoansApiClient : LibraryApiClientBase, ILoansApiClient
{
    public LoansApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetLoansAsync(CancellationToken cancellationToken = default) => GetAsync("/api/loans", cancellationToken);
    public Task<ApiResponse> GetDetailAsync(int loanId, CancellationToken cancellationToken = default) => GetAsync($"/api/loans/{loanId}", cancellationToken);
    public Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/loans", payload, cancellationToken);
    public Task<ApiResponse> ReturnByBarcodeAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/loans/return-by-barcode", payload, cancellationToken);
    public Task<ApiResponse> RenewAsync(int loanId, object payload, CancellationToken cancellationToken = default) => PostAsync($"/api/loans/{loanId}/renew", payload, cancellationToken);
}

public interface IReservationsApiClient
{
    Task<ApiResponse> GetOpenReservationsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default);
    Task<ApiResponse> CancelAsync(int reservationId, CancellationToken cancellationToken = default);
}

public sealed class ReservationsApiClient : LibraryApiClientBase, IReservationsApiClient
{
    public ReservationsApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetOpenReservationsAsync(CancellationToken cancellationToken = default) => GetAsync("/api/reservations?status=open", cancellationToken);
    public Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/reservations", payload, cancellationToken);
    public Task<ApiResponse> CancelAsync(int reservationId, CancellationToken cancellationToken = default) => PostAsync($"/api/reservations/{reservationId}/cancel", null, cancellationToken);
}

public interface IManagerApiClient
{
    Task<ApiResponse> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> GetRevenueAsync(int year, CancellationToken cancellationToken = default);
}

public sealed class ManagerApiClient : LibraryApiClientBase, IManagerApiClient
{
    public ManagerApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetDashboardAsync(CancellationToken cancellationToken = default) => GetAsync("/api/manager/dashboard", cancellationToken);
    public Task<ApiResponse> GetRevenueAsync(int year, CancellationToken cancellationToken = default) => GetAsync($"/api/manager/revenue?year={year}", cancellationToken);
}

public interface IFinePaymentsApiClient
{
    Task<ApiResponse> GetHistoryAsync(string? memberKeyword, string? receivedByKeyword, string? fromDate, string? toDate, int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<ApiResponse> CollectAsync(object payload, CancellationToken cancellationToken = default);
}

public sealed class FinePaymentsApiClient : LibraryApiClientBase, IFinePaymentsApiClient
{
    public FinePaymentsApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetHistoryAsync(string? memberKeyword, string? receivedByKeyword, string? fromDate, string? toDate, int? page, int? pageSize, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        ApiPayload.AddQuery(query, "memberKeyword", memberKeyword);
        ApiPayload.AddQuery(query, "receivedByKeyword", receivedByKeyword);
        ApiPayload.AddQuery(query, "fromDate", fromDate);
        ApiPayload.AddQuery(query, "toDate", toDate);
        if (page.HasValue)
        {
            ApiPayload.AddQuery(query, "page", page.Value.ToString());
        }

        if (pageSize.HasValue)
        {
            ApiPayload.AddQuery(query, "pageSize", pageSize.Value.ToString());
        }

        var path = query.Count == 0 ? "/api/fine-payments" : $"/api/fine-payments?{string.Join('&', query)}";
        return GetAsync(path, cancellationToken);
    }

    public Task<ApiResponse> CollectAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/fine-payments", payload, cancellationToken);
}

public interface INotificationsApiClient
{
    Task<ApiResponse> GetNotificationsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse> GetDetailAsync(int notificationId, CancellationToken cancellationToken = default);
    Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default);
}

public sealed class NotificationsApiClient : LibraryApiClientBase, INotificationsApiClient
{
    public NotificationsApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetNotificationsAsync(CancellationToken cancellationToken = default) => GetAsync("/api/notifications", cancellationToken);
    public Task<ApiResponse> GetDetailAsync(int notificationId, CancellationToken cancellationToken = default) => GetAsync($"/api/notifications/{notificationId}", cancellationToken);
    public Task<ApiResponse> CreateAsync(object payload, CancellationToken cancellationToken = default) => PostAsync("/api/notifications", payload, cancellationToken);
}

public interface IReportsApiClient
{
    Task<ApiResponse> GetOverdueLoansAsync(CancellationToken cancellationToken = default);
}

public sealed class ReportsApiClient : LibraryApiClientBase, IReportsApiClient
{
    public ReportsApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetOverdueLoansAsync(CancellationToken cancellationToken = default) => GetAsync("/api/reports/overdue-loans", cancellationToken);
}

public interface ISystemLogsApiClient
{
    Task<ApiResponse> GetSystemLogsAsync(CancellationToken cancellationToken = default);
}

public sealed class SystemLogsApiClient : LibraryApiClientBase, ISystemLogsApiClient
{
    public SystemLogsApiClient(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(httpClient, configuration, httpContextAccessor) { }

    public Task<ApiResponse> GetSystemLogsAsync(CancellationToken cancellationToken = default) => GetAsync("/api/system-logs", cancellationToken);
}
