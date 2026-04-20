using System.Collections.Generic;
using System.Text.Json;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class BudgetCommandHandlerTests
{
    [Fact]
    public void ChangeStrataTax_SetsFractionalTaxRate()
    {
        var countryId = Guid.NewGuid();
        var actorId = ActorId.New();
        var world = CreateWorld(countryId, actorId);
        var command = CreateCommand(
            actorId,
            "ChangeStrataTax",
            ("countryId", $"\"{countryId}\""),
            ("strata", "\"middle\""),
            ("rate", "0.35"));

        var result = new ChangeStrataTaxCommandHandler().Handle(command, world, actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.35m, world.Countries[countryId.ToString()].MiddleTaxRate);
    }

    [Fact]
    public void ChangeStrataTax_NormalizesPercentRateAndCanClearOverride()
    {
        var countryId = Guid.NewGuid();
        var actorId = ActorId.New();
        var world = CreateWorld(countryId, actorId);
        var handler = new ChangeStrataTaxCommandHandler();

        var setResult = handler.Handle(
            CreateCommand(actorId, "ChangeStrataTax", ("countryId", $"\"{countryId}\""), ("strata", "\"rich\""), ("rate", "40")),
            world,
            actorId);
        var clearResult = handler.Handle(
            CreateCommand(actorId, "ChangeStrataTax", ("countryId", $"\"{countryId}\""), ("strata", "\"rich\""), ("rate", "-1")),
            world,
            actorId);

        Assert.True(setResult.IsSuccess);
        Assert.True(clearResult.IsSuccess);
        Assert.Equal(-1m, world.Countries[countryId.ToString()].RichTaxRate);
    }

    [Fact]
    public void ChangeSpending_SetsRequestedCategory()
    {
        var countryId = Guid.NewGuid();
        var actorId = ActorId.New();
        var world = CreateWorld(countryId, actorId);
        var command = CreateCommand(
            actorId,
            "ChangeSpending",
            ("countryId", $"\"{countryId}\""),
            ("category", "\"administration\""),
            ("level", "75"));

        var result = new ChangeSpendingCommandHandler().Handle(command, world, actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.75m, world.Countries[countryId.ToString()].AdministrationSpending);
        Assert.Equal(0.5m, world.Countries[countryId.ToString()].EducationSpending);
    }

    [Fact]
    public void ChangeSpending_RejectsOutOfRangeLevel()
    {
        var countryId = Guid.NewGuid();
        var actorId = ActorId.New();
        var world = CreateWorld(countryId, actorId);
        var command = CreateCommand(
            actorId,
            "ChangeSpending",
            ("countryId", $"\"{countryId}\""),
            ("category", "\"education\""),
            ("level", "125"));

        var result = new ChangeSpendingCommandHandler().Handle(command, world, actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(CommandRejectionReason.InvalidParameterRange, result.RejectionReason);
        Assert.Equal(0.5m, world.Countries[countryId.ToString()].EducationSpending);
    }

    [Fact]
    public void ChangeSpending_RejectsActorWhoDoesNotControlCountry()
    {
        var countryId = Guid.NewGuid();
        var actorId = ActorId.New();
        var otherCountryId = Guid.NewGuid();
        var world = CreateWorld(countryId, actorId, otherCountryId);
        var command = CreateCommand(
            actorId,
            "ChangeSpending",
            ("countryId", $"\"{countryId}\""),
            ("category", "\"military\""),
            ("level", "0.25"));

        var result = new ChangeSpendingCommandHandler().Handle(command, world, actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(CommandRejectionReason.NotCountryOwner, result.RejectionReason);
        Assert.Equal(0.5m, world.Countries[countryId.ToString()].MilitarySpending);
    }

    private static CommandEnvelope CreateCommand(ActorId actorId, string commandType, params (string Key, string Json)[] values)
    {
        var payload = new Dictionary<string, object>();
        foreach (var (key, json) in values)
        {
            payload[key] = JsonSerializer.Deserialize<JsonElement>(json);
        }

        return new CommandEnvelope(actorId, commandType, payload);
    }

    private static WorldState CreateWorld(Guid countryId, ActorId actorId, Guid? controlledCountryId = null)
    {
        var controlled = controlledCountryId ?? countryId;
        var world = new WorldState
        {
            Seed = 0,
            Date = new GameDate(new DateOnly(1800, 1, 1)),
            Countries = new Dictionary<string, CountryState>
            {
                [countryId.ToString()] = new()
                {
                    Id = countryId.ToString(),
                    DisplayName = "Albion",
                    ProvinceIds = new List<string>(),
                    Treasury = 0m,
                    TaxRate = 10m,
                    TariffRate = 0m,
                    EducationSpending = 0.5m,
                    MilitarySpending = 0.5m,
                    AdministrationSpending = 0.5m,
                    IsPlayable = true
                }
            },
            PlayerAccounts = new Dictionary<string, PlayerAccount>
            {
                [actorId.ToString()] = new(actorId, "player", new CountryId(controlled))
            },
            Provinces = new Dictionary<string, ProvinceState>(),
            Pops = new Dictionary<string, VictoriaLike.Core.Core.Pops.PopState>(),
            Goods = new Dictionary<string, VictoriaLike.Core.Core.Economy.GoodDefinition>(),
            Market = new VictoriaLike.Core.Core.Economy.MarketState(),
            Metrics = new SimulationMetrics(),
            EventLog = new List<string>()
        };

        if (controlled != countryId)
        {
            world.Countries[controlled.ToString()] = new CountryState
            {
                Id = controlled.ToString(),
                DisplayName = "Bretoria",
                ProvinceIds = new List<string>(),
                Treasury = 0m,
                TaxRate = 10m,
                TariffRate = 0m,
                EducationSpending = 0.5m,
                MilitarySpending = 0.5m,
                AdministrationSpending = 0.5m,
                IsPlayable = true
            };
        }

        return world;
    }
}
