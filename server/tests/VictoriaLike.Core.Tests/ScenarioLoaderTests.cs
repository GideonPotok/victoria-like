using Xunit;
using VictoriaLike.Core.Scenarios;
using System.Diagnostics;

namespace VictoriaLike.Core.Tests;

public class ScenarioLoaderTests
{
    private readonly ScenarioLoader _loader = new();

    [Fact]
    public async Task LoadsValidScenarioSuccessfully()
    {
        var scenarioPath = FindScenarioFile("tiny-2country.json");
        if (scenarioPath == null)
        {
            // Scenario might not exist in test environment; skip
            return;
        }

        var scenario = await _loader.LoadAsync(scenarioPath);

        Assert.NotNull(scenario);
        Assert.Equal("Tiny Two Country Test", scenario.Name);
        Assert.Equal(2, scenario.Countries.Count);
        Assert.Equal(12, scenario.Provinces.Count);
        Assert.Single(scenario.Markets);
        Assert.All(scenario.Provinces, province =>
            Assert.Equal(province.Population, province.PopGroups.Sum(pop => pop.Size)));
        Assert.All(scenario.Provinces, province =>
            Assert.False(string.IsNullOrWhiteSpace(province.RgoType)));
        Assert.Contains(scenario.Provinces, province => province.PopGroups.Any(pop => pop.PopType == "artisans"));
        Assert.Contains(scenario.Provinces, province => province.PopGroups.Any(pop => pop.PopType == "capitalists"));
    }

    [Fact]
    public async Task LoadsFallbackPopForProvinceWithoutExplicitPops()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
                {
                  "scenario": {
                    "name": "Test",
                    "countries": [
                      { "name": "England", "tag": "ENG", "taxRate": 10 }
                    ],
                    "markets": [
                      { "name": "Market1", "goods": {} }
                    ],
                    "provinces": [
                      { "name": "Prov1", "owner": "ENG", "market": "Market1", "population": 1000 }
                    ]
                  }
                }
                """;

            await File.WriteAllTextAsync(tempFile, json);

            var scenario = await _loader.LoadAsync(tempFile);
            var province = Assert.Single(scenario.Provinces);
            var pop = Assert.Single(province.PopGroups);

            Assert.Equal(province.Population, pop.Size);
            Assert.Equal("farmers", pop.PopType);
            Assert.Equal("poor", pop.Strata);
            Assert.Equal("grain_farm", province.RgoType);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadsExplicitPopGroupsWithInferredAndExplicitStrata()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
                {
                  "scenario": {
                    "name": "Test",
                    "countries": [
                      { "name": "England", "tag": "ENG", "taxRate": 10 }
                    ],
                    "markets": [
                      { "name": "Market1", "goods": { "grain": 1.0 } }
                    ],
                    "provinces": [
                      {
                        "name": "Prov1",
                        "owner": "ENG",
                        "market": "Market1",
                        "population": 1000,
                        "pops": [
                          { "size": 700, "popType": "farmers", "culture": "english", "religion": "protestant" },
                          { "size": 200, "popType": "clerks", "culture": "english", "religion": "protestant" },
                          { "size": 100, "popType": "capitalists", "strata": "rich", "culture": "english", "religion": "protestant" }
                        ]
                      }
                    ]
                  }
                }
                """;

            await File.WriteAllTextAsync(tempFile, json);

            var scenario = await _loader.LoadAsync(tempFile);
            var province = Assert.Single(scenario.Provinces);

            Assert.Equal(3, province.PopGroups.Count);
            Assert.Equal("poor", province.PopGroups[0].Strata);
            Assert.Equal("middle", province.PopGroups[1].Strata);
            Assert.Equal("rich", province.PopGroups[2].Strata);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadsFactories()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
                {
                  "scenario": {
                    "name": "Test",
                    "countries": [
                      { "name": "England", "tag": "ENG", "taxRate": 10 }
                    ],
                    "markets": [
                      { "name": "Market1", "goods": { "coal": 2.0, "iron": 3.0, "steel": 10.0 } }
                    ],
                    "provinces": [
                      { "name": "Prov1", "owner": "ENG", "market": "Market1", "population": 1000 }
                    ],
                    "factories": [
                      {
                        "country": "ENG",
                        "province": "Prov1",
                        "type": "steel_mill",
                        "level": 1,
                        "employedCraftsmen": 600,
                        "employedClerks": 100,
                        "inputGoods": { "coal": 0.5, "iron": 0.5 },
                        "outputGood": "steel",
                        "outputPerTick": 8.0,
                        "cashReserve": 12.0
                      }
                    ]
                  }
                }
                """;

            await File.WriteAllTextAsync(tempFile, json);

            var scenario = await _loader.LoadAsync(tempFile);
            var factory = Assert.Single(scenario.Factories);

            Assert.Equal("steel_mill", factory.Type);
            Assert.Equal("steel", factory.OutputGood);
            Assert.Equal(600, factory.EmployedCraftsmen);
            Assert.Equal(100, factory.EmployedClerks);
            Assert.Equal(0.5m, factory.InputGoods["coal"]);
            Assert.Equal(12m, factory.CashReserve);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task MediumScenarioLoadsWithExpectedScaleAndFastEnough()
    {
        var scenarioPath = FindScenarioFile("medium-8country.json");
        if (scenarioPath == null)
            return;

        var sw = Stopwatch.StartNew();
        var scenario = await _loader.LoadAsync(scenarioPath);
        sw.Stop();

        Assert.Equal("Medium North Sea Test", scenario.Name);
        Assert.InRange(scenario.Countries.Count, 6, 10);
        Assert.InRange(scenario.Provinces.Count, 50, 100);
        Assert.Single(scenario.Markets);
        Assert.NotEmpty(scenario.Factories);
        Assert.NotEmpty(scenario.Armies);
        Assert.Contains(scenario.Wars, war => war.IsActive);
        Assert.All(scenario.Provinces, province =>
            Assert.Equal(province.Population, province.PopGroups.Sum(pop => pop.Size)));
        Assert.True(sw.ElapsedMilliseconds < 1_000, $"Medium scenario load took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Phase1AlbionServerScenarioLoadsForPublicDemo()
    {
        var scenarioPath = FindScenarioFile("phase1-albion-server.json");
        if (scenarioPath == null)
            return;

        var scenario = await _loader.LoadAsync(scenarioPath);

        Assert.Equal("Phase 1 Albion Demo", scenario.Name);
        Assert.Single(scenario.Countries);
        Assert.Equal("ALB", scenario.Countries[0].Tag);
        Assert.Equal(2, scenario.Provinces.Count);
        Assert.Single(scenario.Markets);
        Assert.Single(scenario.Players);
        Assert.NotEmpty(scenario.Factories);
        Assert.NotEmpty(scenario.Armies);
        Assert.All(scenario.Provinces, province =>
            Assert.Equal(province.Population, province.PopGroups.Sum(pop => pop.Size)));
    }

    [Fact]
    public async Task RejectsUnknownFactoryAndRgoGoods()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
                {
                  "scenario": {
                    "name": "Bad Goods",
                    "countries": [
                      { "name": "England", "tag": "ENG", "taxRate": 10 }
                    ],
                    "markets": [
                      { "name": "Market1", "goods": { "grain": 1.0 } }
                    ],
                    "provinces": [
                      { "name": "Prov1", "owner": "ENG", "market": "Market1", "population": 1000, "outputsPerTick": { "unobtainium": 1.0 } }
                    ],
                    "factories": [
                      {
                        "country": "ENG",
                        "province": "Prov1",
                        "type": "bad_factory",
                        "inputGoods": { "coal": 1.0 },
                        "outputGood": "steel",
                        "outputPerTick": 1.0
                      }
                    ]
                  }
                }
                """;

            await File.WriteAllTextAsync(tempFile, json);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _loader.LoadAsync(tempFile));
            Assert.Contains("unknown good", ex.Message);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ThrowsOnMissingFile()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _loader.LoadAsync("/nonexistent/path.json"));
    }

    [Fact]
    public async Task ThrowsOnInvalidJson()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "{ invalid json ");
            var ex = await Record.ExceptionAsync(() => _loader.LoadAsync(tempFile));
            Assert.NotNull(ex);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ThrowsOnDuplicateCountryTag()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
                {
                  "scenario": {
                    "name": "Test",
                    "countries": [
                      { "name": "Country1", "tag": "ENG", "taxRate": 10 },
                      { "name": "Country2", "tag": "ENG", "taxRate": 10 }
                    ],
                    "markets": [
                      { "name": "Market1", "goods": {} }
                    ],
                    "provinces": [
                      { "name": "Prov1", "owner": "ENG", "market": "Market1", "population": 1000 }
                    ]
                  }
                }
                """;

            await File.WriteAllTextAsync(tempFile, json);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadAsync(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ThrowsOnInvalidCountryReference()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
                {
                  "scenario": {
                    "name": "Test",
                    "countries": [
                      { "name": "England", "tag": "ENG", "taxRate": 10 }
                    ],
                    "markets": [
                      { "name": "Market1", "goods": {} }
                    ],
                    "provinces": [
                      { "name": "Prov1", "owner": "FRA", "market": "Market1", "population": 1000 }
                    ]
                  }
                }
                """;

            await File.WriteAllTextAsync(tempFile, json);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadAsync(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private string? FindScenarioFile(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "content", "scenarios", fileName),
            Path.Combine(AppContext.BaseDirectory, "scenarios", fileName),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}
