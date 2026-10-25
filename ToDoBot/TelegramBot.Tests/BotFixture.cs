using System.Net;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Scenarios;
using TelegramBot.Services;

namespace TelegramBot.Tests;

internal sealed class RecordingTransport : HttpMessageHandler
{
    public List<JsonElement> Requests { get; } = new();
    public bool FailNext { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (FailNext)
        {
            FailNext = false;
            throw new HttpRequestException("Test transport failure");
        }
        if (!request.RequestUri!.AbsolutePath.EndsWith("/sendMessage", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected API method in test.");

        var payload = await request.Content!.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(payload);
        var data = json.RootElement.Clone();
        Requests.Add(data);
        var response = JsonSerializer.Serialize(new
        {
            ok = true,
            result = new
            {
                message_id = Requests.Count,
                date = 1700000000,
                chat = new { id = data.GetProperty("chat_id").ValueKind == JsonValueKind.String
                    ? long.Parse(data.GetProperty("chat_id").GetString()!) : data.GetProperty("chat_id").GetInt64(), type = "private" },
                text = data.GetProperty("text").GetString()
            }
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        };
    }
}

internal sealed class BotFixture : IDisposable
{
    public RecordingTransport Transport { get; } = new();
    private readonly HttpClient _http;
    public TelegramBotClient Bot { get; }
    public UserService Users { get; } = new();
    public ToDoService Tasks { get; } = new();
    public InMemoryScenarioContextRepository Contexts { get; } = new();
    public UpdateHandler Handler { get; }

    public BotFixture()
    {
        _http = new HttpClient(Transport);
        // Синтаксически допустимое тестовое значение. HTTP целиком перехвачен RecordingTransport.
        Bot = new TelegramBotClient("123456789:TEST_TOKEN_FOR_LOCAL_TESTS_ONLY", _http);
        Handler = new UpdateHandler(Bot, Users, Tasks,
            new IScenario[] { new AddTaskScenario(Users, Tasks) }, Contexts);
    }

    public static Update Update(string? text, long userId = 1, ChatType chatType = ChatType.Private) => new()
    {
        Id = 1,
        Message = new Message
        {
            Id = 1,
            Date = DateTime.UtcNow,
            From = new User { Id = userId, FirstName = "Test", IsBot = false },
            Chat = new Chat { Id = userId, Type = chatType },
            Text = text
        }
    };

    public Task Send(string? text, long userId = 1, CancellationToken ct = default) =>
        Handler.HandleUpdateAsync(Bot, Update(text, userId), ct);

    public async Task Begin(long userId = 1)
    {
        await Send("/start", userId);
        await Send("/addtask", userId);
    }

    public Task<ScenarioContext?> Context(long userId = 1) => Contexts.GetContext(userId, default);

    public async Task<IReadOnlyList<Models.ToDoItem>> Items(long userId = 1)
    {
        var user = await Users.GetUser(userId, default);
        return user is null ? Array.Empty<Models.ToDoItem>() : await Tasks.GetAllByUserId(user.UserId, default);
    }

    public string LastText => Transport.Requests.Last().GetProperty("text").GetString()!;

    public string[] LastButtons => Transport.Requests.Last().GetProperty("reply_markup")
        .GetProperty("keyboard").EnumerateArray().SelectMany(row => row.EnumerateArray())
        .Select(button => button.GetProperty("text").GetString()!).ToArray();

    public void Dispose() => _http.Dispose();
}
