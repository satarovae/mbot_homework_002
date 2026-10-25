using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot;

public static class BotKeyboards
{
    public static ReplyKeyboardMarkup Cancel => new(new[]
    {
        new KeyboardButton[] { new("/cancel") }
    }) { ResizeKeyboard = true };

    public static ReplyKeyboardMarkup Main => new(new[]
    {
        new KeyboardButton[] { new("/addtask"), new("/showtasks") },
        new KeyboardButton[] { new("/showalltasks"), new("/report") },
        new KeyboardButton[] { new("/help") }
    }) { ResizeKeyboard = true };
}
