using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Server.Options;

namespace Server.Services;

public sealed class SmtpEmailService
{
    private readonly SmtpOptions _options;

    public SmtpEmailService(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken cancellationToken)
    {
        ValidateOptions();

        using var message = new MailMessage
        {
            From = new MailAddress(
                string.IsNullOrWhiteSpace(_options.FromEmail) ? _options.Username : _options.FromEmail,
                string.IsNullOrWhiteSpace(_options.FromName) ? "Library System" : _options.FromName),
            Subject = "Đặt lại mật khẩu Library System",
            Body = $"""
                  Xin chào,

                  Bạn vừa yêu cầu đặt lại mật khẩu cho hệ thống Library System.

                  Vui lòng mở liên kết sau để đặt mật khẩu mới. Liên kết sẽ hết hạn sau một thời gian ngắn:
                  {resetUrl}

                  Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.
                  """,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = new NetworkCredential(_options.Username, _options.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Host)
            || string.IsNullOrWhiteSpace(_options.Username)
            || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException("SMTP chưa được cấu hình đầy đủ.");
        }
    }
}
