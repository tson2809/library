using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Server;
using Server.DataAccess;
using Server.Interface;
using Server.Models;
using Server.Options;
using Server.Services;

DotEnvLoader.LoadFromRepositoryRoot();
var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var rawConnectionString = configuration["ConnectStrings:DBConnection"]
    ?? configuration.GetConnectionString("DBConnection");
if (string.IsNullOrWhiteSpace(rawConnectionString))
{
    throw new InvalidOperationException("Thiếu cấu hình ConnectStrings:DBConnection hoặc ConnectionStrings:DBConnection.");
}

var connectTimeout = int.TryParse(configuration["Sql:ConnectTimeoutSeconds"], out var configuredConnectTimeout)
    ? configuredConnectTimeout
    : 15;
var commandTimeout = int.TryParse(configuration["Sql:CommandTimeoutSeconds"], out var configuredCommandTimeout)
    ? configuredCommandTimeout
    : 60;
var maxPoolSize = int.TryParse(configuration["Sql:MaxPoolSize"], out var configuredMaxPoolSize)
    ? configuredMaxPoolSize
    : 150;
var retryCount = int.TryParse(configuration["Sql:RetryCount"], out var configuredRetryCount)
    ? configuredRetryCount
    : 5;
var retryDelaySeconds = int.TryParse(configuration["Sql:RetryDelaySeconds"], out var configuredRetryDelaySeconds)
    ? configuredRetryDelaySeconds
    : 5;
var connectRetryCount = int.TryParse(configuration["Sql:ConnectRetryCount"], out var configuredConnectRetryCount)
    ? configuredConnectRetryCount
    : 3;
var connectRetryInterval = int.TryParse(configuration["Sql:ConnectRetryIntervalSeconds"], out var configuredConnectRetryInterval)
    ? configuredConnectRetryInterval
    : 2;

var connectionBuilder = new SqlConnectionStringBuilder(rawConnectionString)
{
    ConnectTimeout = connectTimeout,
    MaxPoolSize = maxPoolSize,
    ConnectRetryCount = connectRetryCount,
    ConnectRetryInterval = connectRetryInterval,
    TrustServerCertificate = true
};
var connectionString = connectionBuilder.ConnectionString;

var apiKey = configuration["Api:Key"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Thiếu cấu hình Api:Key.");
}

var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    throw new InvalidOperationException("Thiếu cấu hình Jwt:SigningKey hoặc khóa ngắn hơn 32 bytes.");
}

var httpUrl = configuration["Http:Url"] ?? "http://localhost:5099";

LibraryContext CreateDbContext()
{
    var options = new DbContextOptionsBuilder<LibraryContext>()
        .UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.CommandTimeout(commandTimeout);
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: retryCount,
                maxRetryDelay: TimeSpan.FromSeconds(retryDelaySeconds),
                errorNumbersToAdd: null);
        })
        .Options;
    return new LibraryContext(options);
}

builder.Services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
builder.Services.Configure<GoogleAuthOptions>(options =>
{
    options.ClientId = configuration["GoogleAuth:ClientId"];
    if (string.IsNullOrWhiteSpace(options.ClientId))
    {
        options.ClientId = configuration["Authentication:Google:ClientId"];
    }
});
builder.Services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
builder.Services.Configure<PasswordResetOptions>(configuration.GetSection("PasswordReset"));
builder.Services.AddControllers().AddOData(options =>
{
    options.Select().Filter().OrderBy().Expand().Count().SetMaxTop(200);
    options.AddRouteComponents("odata", ODataEdmModelBuilder.GetEdmModel());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Library Management API",
        Version = "v1",
        Description = "RESTful Web API chuẩn PRN232 cho hệ thống quản lý thư viện."
    });

    options.CustomSchemaIds(type =>
    {
        static string Clean(string value) => value
            .Replace(".", "_")
            .Replace("+", "_")
            .Replace("`", "_");

        if (!type.IsGenericType)
        {
            return Clean(type.FullName ?? type.Name);
        }

        var genericName = Clean((type.GetGenericTypeDefinition().FullName ?? type.Name).Split('`')[0]);
        var genericArguments = string.Join("_", type.GetGenericArguments().Select(argument => Clean(argument.FullName ?? argument.Name)));
        return $"{genericName}_{genericArguments}";
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT access token theo dạng: Bearer {token}"
    });

    options.AddSecurityDefinition("X-Api-Key", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API key nội bộ để Client_web gọi Server."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "X-Api-Key" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<LibraryContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(commandTimeout);
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: retryCount,
            maxRetryDelay: TimeSpan.FromSeconds(retryDelaySeconds),
            errorNumbersToAdd: null);
    });
});
builder.Services.AddSingleton<Func<LibraryContext>>(_ => CreateDbContext);
builder.Services.AddSingleton<ILibraryDataAccess>(sp => new LibraryDataAccess(sp.GetRequiredService<Func<LibraryContext>>()));
builder.Services.AddSingleton<IsbnLookupService>();
builder.Services.AddSingleton<PasswordHashService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<GoogleIdentityService>();
builder.Services.AddSingleton<SmtpEmailService>();
builder.Services.AddSingleton<PasswordResetTokenService>();
builder.Services.AddScoped<CurrentUserService>();

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("manager"));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("staff"));
    options.AddPolicy("ManagerOrStaff", policy => policy.RequireRole("manager", "staff"));
});

var app = builder.Build();
app.Urls.Add(httpUrl);

await EnsureDatabaseCompatibilityAsync(CreateDbContext, app.Logger);

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Library Management API v1");
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/odata", StringComparison.OrdinalIgnoreCase))
    {
        var providedApiKey = context.Request.Headers["X-Api-Key"].ToString();
        if (!string.Equals(providedApiKey, apiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "API key không hợp lệ." });
            return;
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok", service = "library-server" }));
app.MapGet("/health/ready", async (Func<LibraryContext> dbFactory, CancellationToken cancellationToken) =>
{
    try
    {
        await using var db = dbFactory();
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

static async Task EnsureDatabaseCompatibilityAsync(Func<LibraryContext> dbFactory, ILogger logger)
{
    try
    {
        await using var db = dbFactory();
        await db.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH('dbo.Loans', 'RenewalCount') IS NULL
BEGIN
    ALTER TABLE dbo.Loans
    ADD RenewalCount INT NOT NULL CONSTRAINT DF_Loans_RenewalCount DEFAULT 0 WITH VALUES;
END
""");

        await db.Database.ExecuteSqlRawAsync("""
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.BookReservations')
      AND name = 'CHK_BookReservations_Status'
)
BEGIN
    ALTER TABLE dbo.BookReservations DROP CONSTRAINT CHK_BookReservations_Status;
END
""");

        await db.Database.ExecuteSqlRawAsync("""
IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.BookReservations')
      AND name = 'CHK_BookReservations_Flow'
)
BEGIN
    ALTER TABLE dbo.BookReservations DROP CONSTRAINT CHK_BookReservations_Flow;
END
""");

        await db.Database.ExecuteSqlRawAsync("""
UPDATE dbo.BookReservations
SET Status = 'Ready'
WHERE Status = 'Pending'
  AND ReservedCopyId IS NOT NULL
  AND FulfilledAt IS NULL
  AND CancelledAt IS NULL;
""");

        await db.Database.ExecuteSqlRawAsync("""
ALTER TABLE dbo.BookReservations
ADD CONSTRAINT CHK_BookReservations_Status
CHECK (Status IN ('Pending', 'Ready', 'Fulfilled', 'Cancelled', 'Expired'));
""");

        await db.Database.ExecuteSqlRawAsync("""
ALTER TABLE dbo.BookReservations
ADD CONSTRAINT CHK_BookReservations_Flow CHECK (
    (Status = 'Pending' AND FulfilledAt IS NULL AND CancelledAt IS NULL)
    OR (Status = 'Ready' AND ReservedCopyId IS NOT NULL AND FulfilledAt IS NULL AND CancelledAt IS NULL)
    OR (Status = 'Fulfilled' AND FulfilledAt IS NOT NULL)
    OR (Status = 'Cancelled' AND CancelledAt IS NOT NULL)
    OR (Status = 'Expired')
);
""");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not apply database compatibility updates.");
    }
}
