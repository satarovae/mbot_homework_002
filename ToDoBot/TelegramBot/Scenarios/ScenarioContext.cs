namespace TelegramBot.Scenarios;

public sealed class ScenarioContext
{
    public long UserId { get; set; }
    public ScenarioType CurrentScenario { get; set; }
    public string? CurrentStep { get; set; }
    public Dictionary<string, object> Data { get; } = new();

    public ScenarioContext(ScenarioType scenario)
    {
        CurrentScenario = scenario;
    }
}
