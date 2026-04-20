using System.Collections.Generic;
using System.Text.Json;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class ChangeTaxRateCommandHandlerTests
{
    [Fact]
    public void Handle_AcceptsNumericJsonPayload()
    {
        var countryId = Guid.NewGuid();
        var handler = new ChangeTaxRateCommandHandler();
        var world = new WorldState
        {
            Seed = 0,
            Date = new GameDate(new DateOnly(1800, 1, 1)),
            Countries = new Dictionary<string, CountryState>
            {
                [countryId.ToString()] = new()
                {
                    Id = countryId.ToString(),
                    DisplayName = "England",
                    ProvinceIds = new List<string>(),
                    Treasury = 0m,
                    TaxRate = 10m,
                    TariffRate = 0m,
                    IsPlayable = true
                }
            },
            PlayerAccounts = new Dictionary<string, PlayerAccount>(),
            Provinces = new Dictionary<string, ProvinceState>(),
            Pops = new Dictionary<string, VictoriaLike.Core.Core.Pops.PopState>(),
            Goods = new Dictionary<string, VictoriaLike.Core.Core.Economy.GoodDefinition>(),
            Market = new VictoriaLike.Core.Core.Economy.MarketState(),
            Metrics = new SimulationMetrics(),
            EventLog = new List<string>()
        };

        var actorId = ActorId.New();
        world.PlayerAccounts[actorId.ToString()] = new PlayerAccount(actorId, "england-player", new CountryId(countryId));

        var command = new CommandEnvelope(
            actorId,
            "ChangeTaxRate",
            new Dictionary<string, object>
            {
                ["countryId"] = JsonSerializer.Deserialize<JsonElement>($"\"{countryId}\""),
                ["newTaxRate"] = JsonSerializer.Deserialize<JsonElement>("25")
            });

        var result = handler.Handle(command, world, command.ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(25m, world.Countries[countryId.ToString()].TaxRate);
    }

    [Fact]
    public void Handle_RejectsActorWhoDoesNotControlCountry()
    {
        var countryId = Guid.NewGuid();
        var otherCountryId = Guid.NewGuid();
        var actorId = ActorId.New();
        var handler = new ChangeTaxRateCommandHandler();
        var world = new WorldState
        {
            Seed = 0,
            Date = new GameDate(new DateOnly(1800, 1, 1)),
            Countries = new Dictionary<string, CountryState>
            {
                [countryId.ToString()] = new()
                {
                    Id = countryId.ToString(),
                    DisplayName = "England",
                    ProvinceIds = new List<string>(),
                    Treasury = 0m,
                    TaxRate = 10m,
                    TariffRate = 0m,
                    IsPlayable = true
                },
                [otherCountryId.ToString()] = new()
                {
                    Id = otherCountryId.ToString(),
                    DisplayName = "France",
                    ProvinceIds = new List<string>(),
                    Treasury = 0m,
                    TaxRate = 12m,
                    TariffRate = 0m,
                    IsPlayable = true
                }
            },
            PlayerAccounts = new Dictionary<string, PlayerAccount>
            {
                [actorId.ToString()] = new PlayerAccount(actorId, "france-player", new CountryId(otherCountryId))
            },
            Provinces = new Dictionary<string, ProvinceState>(),
            Pops = new Dictionary<string, VictoriaLike.Core.Core.Pops.PopState>(),
            Goods = new Dictionary<string, VictoriaLike.Core.Core.Economy.GoodDefinition>(),
            Market = new VictoriaLike.Core.Core.Economy.MarketState(),
            Metrics = new SimulationMetrics(),
            EventLog = new List<string>()
        };

        var command = new CommandEnvelope(
            actorId,
            "ChangeTaxRate",
            new Dictionary<string, object>
            {
                ["countryId"] = JsonSerializer.Deserialize<JsonElement>($"\"{countryId}\""),
                ["newTaxRate"] = JsonSerializer.Deserialize<JsonElement>("25")
            });

        var result = handler.Handle(command, world, command.ActorId);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not control country", result.ErrorMessage);
        Assert.Equal(10m, world.Countries[countryId.ToString()].TaxRate);
    }
}
