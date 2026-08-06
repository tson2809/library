namespace Client_web.Services;

public static class UiTextLocalizer
{
    public static string TranslateMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Không có thông báo từ hệ thống.";
        }

        return message.Trim();
    }
}
