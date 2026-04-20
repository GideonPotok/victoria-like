using VictoriaLike.Server.Services;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class CommandBudgetServiceTests
{
    [Fact]
    public void Evaluate_AllowsSoftLimitedCommandsButRejectsHardLimit()
    {
        var service = new CommandBudgetService();
        var actorId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 10; i++)
        {
            var decision = service.Evaluate(actorId, countryId, "UnknownDevCommand", currentTick: i, receivedAtUtc: now);
            Assert.True(decision.Accepted);
            Assert.False(decision.SoftLimited);
            service.RecordAccepted(actorId, countryId, "UnknownDevCommand", currentTick: i, receivedAtUtc: now);
        }

        var softDecision = service.Evaluate(actorId, countryId, "UnknownDevCommand", currentTick: 10, receivedAtUtc: now);
        Assert.True(softDecision.Accepted);
        Assert.True(softDecision.SoftLimited);

        for (var i = 10; i < 20; i++)
            service.RecordAccepted(actorId, countryId, "UnknownDevCommand", currentTick: i, receivedAtUtc: now);

        var hardDecision = service.Evaluate(actorId, countryId, "UnknownDevCommand", currentTick: 20, receivedAtUtc: now);
        Assert.False(hardDecision.Accepted);
        Assert.Equal("rejected", hardDecision.Status);
        Assert.NotNull(hardDecision.RetryAfter);
    }

    [Fact]
    public void Evaluate_RejectsStrategicCommandDuringCountryCooldown()
    {
        var service = new CommandBudgetService();
        var actorId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var first = service.Evaluate(actorId, countryId, "ChangeTaxRate", currentTick: 12, receivedAtUtc: now);
        Assert.True(first.Accepted);
        service.RecordAccepted(actorId, countryId, "ChangeTaxRate", currentTick: 12, receivedAtUtc: now);

        var second = service.Evaluate(actorId, countryId, "ChangeTaxRate", currentTick: 13, receivedAtUtc: now);
        Assert.False(second.Accepted);
        Assert.Equal(2, second.RetryAfterTicks);

        var afterCooldown = service.Evaluate(actorId, countryId, "ChangeTaxRate", currentTick: 15, receivedAtUtc: now);
        Assert.True(afterCooldown.Accepted);
    }

    [Fact]
    public void GetSnapshots_ExposesWindowAndCooldownState()
    {
        var service = new CommandBudgetService();
        var actorId = Guid.NewGuid();
        var countryId = Guid.NewGuid();
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        service.RecordAccepted(actorId, countryId, "QueueBuilding", currentTick: 4, receivedAtUtc: now);

        var snapshot = Assert.Single(service.GetSnapshots(currentTick: 4, nowUtc: now));
        Assert.Equal(actorId.ToString(), snapshot.ActorId);
        Assert.Equal(countryId.ToString(), snapshot.CountryId);
        Assert.Equal(1, snapshot.UsedInWindow);
        Assert.True(snapshot.CooldownsRemainingTicks.ContainsKey("QueueBuilding"));
    }
}
