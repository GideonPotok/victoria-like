using VictoriaLike.Server.Services;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class WorldSnapshotDocumentTests
{
    [Fact]
    public void Validate_AcceptsCompleteSavepointWithBuildingQueue()
    {
        var countryId = Guid.NewGuid();
        var otherCountryId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var provinceId = Guid.NewGuid();
        var queueId = Guid.NewGuid();
        var armyId = Guid.NewGuid();
        var warId = Guid.NewGuid();

        var document = new WorldSnapshotDocument
        {
            TickNumber = 42,
            WorldTimestamp = new DateTime(1800, 2, 12),
            Countries =
            [
                new CountrySnapshotDto { Id = countryId, Name = "Albion", Tag = "ALB", TaxRate = 10 },
                new CountrySnapshotDto { Id = otherCountryId, Name = "Bretoria", Tag = "BRE", TaxRate = 10 }
            ],
            Markets =
            [
                new MarketSnapshotDto { Id = marketId, Name = "Albion Market" }
            ],
            Provinces =
            [
                new ProvinceSnapshotDto
                {
                    Id = provinceId,
                    Name = "Albionshire",
                    OwnerId = countryId,
                    MarketId = marketId,
                    Population = 1_000
                }
            ],
            Players =
            [
                new PlayerSnapshotDto
                {
                    ActorId = Guid.NewGuid(),
                    Username = "albion-player",
                    ControlledCountryId = countryId,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            BuildingQueue =
            [
                new BuildingQueueSnapshotDto
                {
                    Id = queueId,
                    ProvinceId = provinceId,
                    CountryId = countryId,
                    BuildingType = "farm",
                    TicksRemaining = 12,
                    QueuedAt = DateTime.UtcNow
                }
            ],
            Armies =
            [
                new ArmyStackSnapshotDto
                {
                    Id = armyId,
                    CountryId = countryId,
                    LocationProvinceId = provinceId,
                    DestinationProvinceId = provinceId,
                    MovementTicksRemaining = 1,
                    SoldierCount = 1_000,
                    Morale = 0.9m
                }
            ],
            Wars =
            [
                new WarSnapshotDto
                {
                    Id = warId,
                    AttackerCountryId = countryId,
                    DefenderCountryId = otherCountryId,
                    StartedAt = new DateTime(1800, 2, 1),
                    IsActive = false
                }
            ]
        };

        var errors = document.Validate();

        Assert.Empty(errors);
        Assert.DoesNotContain(errors, error => error.Contains("Army", StringComparison.Ordinal));

        var item = Assert.Single(document.ToBuildingQueue());
        Assert.Equal(queueId, item.Id);
        Assert.Equal(provinceId, item.ProvinceId);
        Assert.Equal(countryId, item.CountryId);
        Assert.Equal("farm", item.BuildingType);
        Assert.Equal(12, item.TicksRemaining);

        var army = Assert.Single(document.ToArmies());
        Assert.Equal(armyId, army.Id);
        Assert.Equal(1, army.MovementTicksRemaining);

        var war = Assert.Single(document.ToWars());
        Assert.Equal(warId, war.Id);
        Assert.Equal(countryId, war.AttackerCountryId.Value);
        Assert.Equal(otherCountryId, war.DefenderCountryId.Value);
        Assert.False(war.IsActive);
    }

    [Fact]
    public void Validate_RejectsMissingReferencesAndNegativeQueueProgress()
    {
        var document = new WorldSnapshotDocument
        {
            TickNumber = 1,
            WorldTimestamp = new DateTime(1800, 1, 1),
            Countries =
            [
                new CountrySnapshotDto { Id = Guid.NewGuid(), Name = "Albion", Tag = "ALB", TaxRate = 10 }
            ],
            Markets =
            [
                new MarketSnapshotDto { Id = Guid.NewGuid(), Name = "Albion Market" }
            ],
            BuildingQueue =
            [
                new BuildingQueueSnapshotDto
                {
                    Id = Guid.NewGuid(),
                    ProvinceId = Guid.NewGuid(),
                    CountryId = Guid.NewGuid(),
                    BuildingType = "",
                    TicksRemaining = -1,
                    QueuedAt = DateTime.UtcNow
                }
            ]
        };

        var errors = document.Validate();

        Assert.Contains(errors, error => error.Contains("missing province", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("missing country", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("negative ticks", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("no building type", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsInvalidPopStrataAndTotals()
    {
        var countryId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var provinceId = Guid.NewGuid();
        var document = new WorldSnapshotDocument
        {
            TickNumber = 1,
            WorldTimestamp = new DateTime(1800, 1, 1),
            Countries =
            [
                new CountrySnapshotDto { Id = countryId, Name = "Albion", Tag = "ALB", TaxRate = 10 }
            ],
            Markets =
            [
                new MarketSnapshotDto { Id = marketId, Name = "Albion Market" }
            ],
            Provinces =
            [
                new ProvinceSnapshotDto
                {
                    Id = provinceId,
                    Name = "Albionshire",
                    OwnerId = countryId,
                    MarketId = marketId,
                    Population = 1_000,
                    PopGroups =
                    [
                        new PopGroupSnapshotDto
                        {
                            Id = Guid.NewGuid(),
                            Size = 900,
                            PopType = "farmers",
                            Strata = "worker",
                            Culture = "english",
                            Religion = "protestant",
                            EmployedCount = 900
                        }
                    ]
                }
            ]
        };

        var errors = document.Validate();

        Assert.Contains(errors, error => error.Contains("invalid strata", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("POP sizes sum", StringComparison.Ordinal));
    }
}
