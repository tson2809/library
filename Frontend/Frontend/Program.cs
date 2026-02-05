namespace Client_web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Services.DotEnvLoader.LoadFromRepositoryRoot();
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHttpClient<Services.IAuthApiClient, Services.AuthApiClient>();
            builder.Services.AddHttpClient<Services.IUsersApiClient, Services.UsersApiClient>();
            builder.Services.AddHttpClient<Services.IBooksApiClient, Services.BooksApiClient>();
            builder.Services.AddHttpClient<Services.IBookCopiesApiClient, Services.BookCopiesApiClient>();
            builder.Services.AddHttpClient<Services.ICategoriesApiClient, Services.CategoriesApiClient>();
            builder.Services.AddHttpClient<Services.IMembersApiClient, Services.MembersApiClient>();
            builder.Services.AddHttpClient<Services.ILoansApiClient, Services.LoansApiClient>();
            builder.Services.AddHttpClient<Services.IReservationsApiClient, Services.ReservationsApiClient>();
            builder.Services.AddHttpClient<Services.IManagerApiClient, Services.ManagerApiClient>();
            builder.Services.AddHttpClient<Services.IFinePaymentsApiClient, Services.FinePaymentsApiClient>();
            builder.Services.AddHttpClient<Services.INotificationsApiClient, Services.NotificationsApiClient>();
            builder.Services.AddHttpClient<Services.IReportsApiClient, Services.ReportsApiClient>();
            builder.Services.AddHttpClient<Services.ISystemLogsApiClient, Services.SystemLogsApiClient>();
            if (string.IsNullOrWhiteSpace(configuration["ServerApi:BaseUrl"]))
            {
                throw new InvalidOperationException("Thiếu cấu hình ServerApi:BaseUrl.");
            }

            if (string.IsNullOrWhiteSpace(configuration["ServerApi:ApiKey"])
                && string.IsNullOrWhiteSpace(configuration["Api:Key"]))
            {
                throw new InvalidOperationException("Thiếu cấu hình ServerApi:ApiKey hoặc Api:Key.");
            }

            builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(8);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseSession();

            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var isLoginPath = path.StartsWith("/Auth/Login", StringComparison.OrdinalIgnoreCase);
                var isGoogleAuthPath = path.StartsWith("/Auth/GoogleLogin", StringComparison.OrdinalIgnoreCase);
                var isPasswordResetPath = path.StartsWith("/Auth/ForgotPassword", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith("/Auth/ResetPassword", StringComparison.OrdinalIgnoreCase);
                var isPublicPath = path.StartsWith("/Home/Error", StringComparison.OrdinalIgnoreCase);

                if (!isLoginPath && !isGoogleAuthPath && !isPasswordResetPath && !isPublicPath)
                {
                    var username = context.Session.GetString("Auth.Username");
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        context.Response.Redirect("/Auth/Login");
                        return;
                    }
                }

                await next();
            });

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
