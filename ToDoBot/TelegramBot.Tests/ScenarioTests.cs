using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Models;
using TelegramBot.Scenarios;
using Xunit;

namespace TelegramBot.Tests;

public sealed class ScenarioTests
{
    [Fact]
    public async Task FullDialogueCreatesTaskWithExactDeadlineAndRestoresMenu()
    {
        using var f = new BotFixture();
        await f.Begin();
        Assert.Equal("Введите название задачи:", f.LastText);
        Assert.Equal(new[] { "/cancel" }, f.LastButtons);
        var context = Assert.IsType<ScenarioContext>(await f.Context());
        Assert.Equal(1, context.UserId);
        Assert.Equal(ScenarioType.AddTask, context.CurrentScenario);
        Assert.Equal("Name", context.CurrentStep);
        Assert.IsType<ToDoUser>(context.Data["User"]);
        await f.Send("  Подготовить домашнюю работу  ");
        Assert.Equal("Deadline", (await f.Context())!.CurrentStep);
        Assert.Empty(await f.Items());
        Assert.Equal(new[] { "/cancel" }, f.LastButtons);
        await f.Send("25.05.2025");
        var item = Assert.Single(await f.Items());
        Assert.Equal("Подготовить домашнюю работу", item.Name);
        Assert.Equal(new DateTime(2025, 5, 25), item.Deadline);
        Assert.Equal(1, item.User.TelegramUserId);
        Assert.Null(await f.Context());
        Assert.Contains("/addtask", f.LastButtons);
        Assert.Contains("/showtasks", f.LastButtons);
        Assert.DoesNotContain("/cancel", f.LastButtons);
        Assert.Contains(f.Transport.Requests, r => r.GetProperty("text").GetString()!.Contains("успешно добавлена"));
    }

    [Theory]
    [InlineData("25.05.20254")]
    [InlineData("2025-05-25")]
    [InlineData("25/05/2025")]
    [InlineData("1.05.2025")]
    [InlineData("01.5.2025")]
    [InlineData("31.04.2025")]
    [InlineData("29.02.2025")]
    [InlineData("00.01.2025")]
    [InlineData("01.13.2025")]
    [InlineData("25.05.2025 ")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task InvalidDateKeepsNameAndStepThenAcceptsValidDate(string? invalid)
    {
        using var f = new BotFixture();
        await f.Begin();
        await f.Send("Test");
        await f.Send(invalid);
        Assert.Equal("Deadline", (await f.Context())!.CurrentStep);
        Assert.Equal("Test", (await f.Context())!.Data["Name"]);
        Assert.Empty(await f.Items());
        Assert.Contains("Неверный формат даты", f.LastText);
        Assert.Equal(new[] { "/cancel" }, f.LastButtons);
        await f.Send("29.02.2024");
        Assert.Equal(new DateTime(2024, 2, 29), Assert.Single(await f.Items()).Deadline);
        Assert.Null(await f.Context());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelClearsEitherStepAndDoesNotCreateTask(bool afterName)
    {
        using var f = new BotFixture();
        await f.Begin();
        if (afterName) await f.Send("Abandoned task");
        await f.Send("/cancel");
        Assert.Null(await f.Context());
        Assert.Empty(await f.Items());
        Assert.Contains("отменён", f.LastText);
        Assert.Contains("/addtask", f.LastButtons);
        await f.Send("/addtask");
        Assert.Equal("Name", (await f.Context())!.CurrentStep);
        Assert.False((await f.Context())!.Data.ContainsKey("Name"));
    }

    [Fact]
    public async Task CancelWithoutScenarioIsHarmless()
    {
        using var f = new BotFixture();
        await f.Send("/cancel");
        Assert.Null(await f.Context());
        Assert.Equal("Нет активного сценария.", f.LastText);
        Assert.Contains("/addtask", f.LastButtons);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/showtasks")]
    public async Task InvalidNameDoesNotAdvanceOrDispatchCommand(string? name)
    {
        using var f = new BotFixture();
        await f.Begin();
        await f.Send(name);
        Assert.Equal("Name", (await f.Context())!.CurrentStep);
        Assert.Empty(await f.Items());
        Assert.Equal(new[] { "/cancel" }, f.LastButtons);
        Assert.Contains("название задачи", f.LastText);
    }

    [Fact]
    public async Task LongNameIsRejectedButMaximumLengthIsAccepted()
    {
        using var f = new BotFixture();
        await f.Begin();
        await f.Send(new string('Я', 129));
        Assert.Equal("Name", (await f.Context())!.CurrentStep);
        await f.Send(new string('Я', 128));
        await f.Send("01.01.2030");
        Assert.Equal(128, Assert.Single(await f.Items()).Name.Length);
    }

    [Fact]
    public async Task DifferentUsersHaveIndependentContextsAndTasks()
    {
        using var f = new BotFixture();
        await f.Begin(1);
        await f.Begin(2);
        Assert.NotSame(await f.Context(1), await f.Context(2));
        await f.Send("First", 1);
        await f.Send("Second", 2);
        await f.Send("01.01.2030", 1);
        Assert.Null(await f.Context(1));
        Assert.Equal("Deadline", (await f.Context(2))!.CurrentStep);
        await f.Send("/cancel", 2);
        Assert.Equal("First", Assert.Single(await f.Items(1)).Name);
        Assert.Empty(await f.Items(2));
    }

    [Fact]
    public async Task TwoCompletedScenariosProduceTwoDifferentTasks()
    {
        using var f = new BotFixture();
        await f.Send("/start");
        foreach (var name in new[] { "First", "Second" })
        {
            await f.Send("/addtask");
            await f.Send(name);
            await f.Send("01.01.2030");
        }
        Assert.Equal(2, (await f.Items()).Count);
        Assert.Equal(2, (await f.Items()).Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public async Task HelpListsDeadlineAndCancelAndShowTaskAliasWorks()
    {
        using var f = new BotFixture();
        await f.Send("/help");
        Assert.Contains("/cancel", f.LastText);
        Assert.Contains("dd.MM.yyyy", f.LastText);
        await f.Begin();
        await f.Send("Test");
        await f.Send("25.05.2025");
        await f.Send("/showtask");
        Assert.Contains("Test", f.LastText);
        Assert.Contains("25.05.2025", f.LastText);
    }

    [Fact]
    public async Task RegistrationRequiredAndRepeatedStartKeepsUser()
    {
        using var f = new BotFixture();
        await f.Send("/addtask");
        Assert.Contains("/start", f.LastText);
        Assert.Null(await f.Context());
        await f.Send("/start");
        var first = await f.Users.GetUser(1, default);
        await f.Send("/start");
        Assert.Same(first, await f.Users.GetUser(1, default));
    }

    [Fact]
    public async Task CommandsWithBotSuffixAndMixedCaseAreAccepted()
    {
        using var f = new BotFixture();
        await f.Send("/START@MyBot");
        await f.Send("/AddTask@MyBot");
        Assert.Equal("Name", (await f.Context())!.CurrentStep);
        await f.Send("/CANCEL@MyBot");
        Assert.Null(await f.Context());
    }

    [Fact]
    public async Task NonMessageAndGroupUpdateAreIgnored()
    {
        using var f = new BotFixture();
        await f.Handler.HandleUpdateAsync(f.Bot, new Update { Id = 1 }, default);
        await f.Handler.HandleUpdateAsync(f.Bot, BotFixture.Update("/start", chatType: ChatType.Group), default);
        Assert.Empty(f.Transport.Requests);
    }

    [Fact]
    public async Task FailedConfirmationCanBeRetriedWithoutDuplicatingTask()
    {
        using var f = new BotFixture();
        await f.Begin();
        await f.Send("Test");
        f.Transport.FailNext = true;
        await Assert.ThrowsAnyAsync<Exception>(() => f.Send("25.05.2025"));
        Assert.Single(await f.Items());
        Assert.NotNull(await f.Context());
        await f.Send("25.05.2025");
        Assert.Single(await f.Items());
        Assert.Null(await f.Context());
    }

    [Fact]
    public void MissingScenarioThrows()
    {
        using var f = new BotFixture();
        Assert.IsType<AddTaskScenario>(f.Handler.GetScenario(ScenarioType.AddTask));
        Assert.Throws<InvalidOperationException>(() => f.Handler.GetScenario(ScenarioType.None));
    }
}
