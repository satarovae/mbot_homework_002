using TelegramBot.Models;
using TelegramBot.Scenarios;
using TelegramBot.Services;
using Xunit;

namespace TelegramBot.Tests;

public sealed class RepositoryAndServiceTests
{
    [Fact]
    public async Task ContextRepositoryStoresReplacesAndResetsOnlySelectedUser()
    {
        var repo = new InMemoryScenarioContextRepository();
        Assert.Null(await repo.GetContext(1, default));
        var first = new ScenarioContext(ScenarioType.AddTask) { UserId = 1 };
        var other = new ScenarioContext(ScenarioType.AddTask) { UserId = 2 };
        await repo.SetContext(1, first, default);
        await repo.SetContext(2, other, default);
        var replacement = new ScenarioContext(ScenarioType.None) { UserId = 1 };
        await repo.SetContext(1, replacement, default);
        Assert.Same(replacement, await repo.GetContext(1, default));
        await repo.ResetContext(1, default);
        await repo.ResetContext(1, default);
        Assert.Null(await repo.GetContext(1, default));
        Assert.Same(other, await repo.GetContext(2, default));
    }

    [Fact]
    public async Task MismatchedUserContextIsRejected()
    {
        var repo = new InMemoryScenarioContextRepository();
        await Assert.ThrowsAsync<ArgumentException>(() => repo.SetContext(1,
            new ScenarioContext(ScenarioType.AddTask) { UserId = 2 }, default));
    }

    [Fact]
    public async Task CancellationTokenStopsRepositoriesServicesAndHandler()
    {
        using var f = new BotFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ct = cts.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Contexts.GetContext(1, ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Contexts.SetContext(1,
            new ScenarioContext(ScenarioType.AddTask) { UserId = 1 }, ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Contexts.ResetContext(1, ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Users.RegisterUser(1, "Test", ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Users.GetUser(1, ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Tasks.Add(new ToDoUser(), "Task", DateTime.Today, ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Tasks.GetAllByUserId(Guid.NewGuid(), ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Send("/start", ct: ct));
        Assert.Empty(f.Transport.Requests);
    }

    [Fact]
    public async Task TaskServiceRejectsInvalidName()
    {
        var service = new ToDoService();
        var user = new ToDoUser();
        await Assert.ThrowsAsync<ArgumentException>(() => service.Add(user, " ", DateTime.Today, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.Add(user, new string('a', 129), DateTime.Today, default));
        Assert.Empty(await service.GetAllByUserId(user.UserId, default));
    }

    [Fact]
    public async Task CommandsCompleteAndDeleteOnlyOwnedTasks()
    {
        using var f = new BotFixture();
        await f.Begin();
        await f.Send("Task");
        await f.Send("01.01.2030");
        var item = Assert.Single(await f.Items());
        await f.Send("/start", 2);
        await f.Send($"/completetask {item.Id}", 2);
        Assert.Equal(ToDoItemState.Active, item.State);
        await f.Send($"/removetask {item.Id}", 2);
        Assert.Single(await f.Items(1));
        await f.Send($"/completetask {item.Id}");
        await f.Send("/showtasks");
        Assert.Equal("Список задач пуст.", f.LastText);
        await f.Send("/showalltasks");
        Assert.Contains("Завершена", f.LastText);
        await f.Send("/report");
        Assert.Contains("Завершённых: 1", f.LastText);
        await f.Send($"/removetask {item.Id}");
        Assert.Empty(await f.Items());
    }
}
