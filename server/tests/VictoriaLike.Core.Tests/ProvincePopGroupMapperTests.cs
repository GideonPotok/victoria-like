using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Api;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class ProvincePopGroupMapperTests
{
    [Fact]
    public void ToProvincePopGroups_ExposesDemographicsEmploymentNeedsAndShare()
    {
        var province = new Province(
            new ProvinceId(Guid.NewGuid()),
            "London",
            new CountryId(Guid.NewGuid()),
            new MarketId(Guid.NewGuid()),
            population: 1_000)
        {
            PopGroups =
            [
                new PopGroup(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    new ProvinceId(Guid.NewGuid()),
                    250,
                    "clerks",
                    "middle",
                    "english",
                    "protestant",
                    0.65m)
                {
                    Militancy = 1.2m,
                    Consciousness = 3.4m,
                    Cash = 12.5m,
                    LifeNeedsFulfillment = 0.9m,
                    EverydayNeedsFulfillment = 0.7m,
                    LuxuryNeedsFulfillment = 0.2m,
                    EmployedCount = 200,
                    UnemployedCount = 50
                },
                new PopGroup(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    new ProvinceId(Guid.NewGuid()),
                    750,
                    "farmers",
                    "poor",
                    "english",
                    "protestant",
                    0.25m)
            ]
        };

        var pops = ProvincePopGroupMapper.ToProvincePopGroups(province);

        Assert.Equal(2, pops.Count);
        Assert.Equal("farmers", pops[0].PopType);
        Assert.Equal(0.75m, pops[0].PopulationShare);

        var clerks = pops[1];
        Assert.Equal("11111111-1111-1111-1111-111111111111", clerks.Id);
        Assert.Equal(250, clerks.Size);
        Assert.Equal(0.25m, clerks.PopulationShare);
        Assert.Equal("middle", clerks.Strata);
        Assert.Equal("english", clerks.Culture);
        Assert.Equal("protestant", clerks.Religion);
        Assert.Equal(0.65m, clerks.Literacy);
        Assert.Equal(1.2m, clerks.Militancy);
        Assert.Equal(3.4m, clerks.Consciousness);
        Assert.Equal(12.5m, clerks.Cash);
        Assert.Equal(0.9m, clerks.LifeNeedsFulfillment);
        Assert.Equal(0.7m, clerks.EverydayNeedsFulfillment);
        Assert.Equal(0.2m, clerks.LuxuryNeedsFulfillment);
        Assert.Equal(200, clerks.EmployedCount);
        Assert.Equal(50, clerks.UnemployedCount);
    }

    [Fact]
    public void ToAdminProvincePopGroups_UsesSameInspectionFields()
    {
        var province = new Province(
            new ProvinceId(Guid.NewGuid()),
            "Paris",
            new CountryId(Guid.NewGuid()),
            new MarketId(Guid.NewGuid()),
            population: 400)
        {
            PopGroups =
            [
                new PopGroup(
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    new ProvinceId(Guid.NewGuid()),
                    100,
                    "artisans",
                    "middle",
                    "french",
                    "catholic",
                    0.4m)
            ]
        };

        var pop = Assert.Single(ProvincePopGroupMapper.ToAdminProvincePopGroups(province));

        Assert.Equal("33333333-3333-3333-3333-333333333333", pop.Id);
        Assert.Equal("artisans", pop.PopType);
        Assert.Equal("middle", pop.Strata);
        Assert.Equal("french", pop.Culture);
        Assert.Equal("catholic", pop.Religion);
        Assert.Equal(0.25m, pop.PopulationShare);
    }
}
