using System;
using System.Collections.Generic;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Data;
using VictoriaLike.Server.Services;
using Xunit;

namespace VictoriaLike.Core.Tests;

/// Regression guard for pop_known_issues.md P2: per-strata tax rates and per-category
/// spending levels must round-trip through CommandWorldStateMapper so a server restart
/// preserves the player's budget choices instead of resetting to scenario defaults.
public sealed class BudgetPersistenceRoundTripTests
{
    private static readonly DateTime WorldTimestamp = new(1836, 1, 1);
    private static readonly IReadOnlyList<GoodDefinition> EmptyGoods = Array.Empty<GoodDefinition>();

    [Fact]
    public void ToSimulationWorld_PreservesBudgetFieldsFromCountryEntity()
    {
        var country = NewCountryWithBudget(
            tax: 18, poor: 0.05m, middle: 0.10m, rich: 0.25m,
            education: 0.6m, military: 0.7m, administration: 0.4m);
        var snapshot = new WorldStateSnapshot { Countries = [country] };

        var world = CommandWorldStateMapper.ToSimulationWorld(snapshot, WorldTimestamp, EmptyGoods);

        var state = world.Countries[country.Id.Value.ToString()];
        Assert.Equal(18, state.TaxRate);
        Assert.Equal(0.05m, state.PoorTaxRate);
        Assert.Equal(0.10m, state.MiddleTaxRate);
        Assert.Equal(0.25m, state.RichTaxRate);
        Assert.Equal(0.6m, state.EducationSpending);
        Assert.Equal(0.7m, state.MilitarySpending);
        Assert.Equal(0.4m, state.AdministrationSpending);
    }

    [Fact]
    public void ToPersistedCountries_ProjectsMutatedBudgetFieldsBackOntoCountryEntity()
    {
        var country = NewCountryWithBudget(
            tax: 10, poor: -1m, middle: -1m, rich: -1m,
            education: 0.5m, military: 0.5m, administration: 0.5m);
        var snapshot = new WorldStateSnapshot { Countries = [country] };
        var world = CommandWorldStateMapper.ToSimulationWorld(snapshot, WorldTimestamp, EmptyGoods);

        var state = world.Countries[country.Id.Value.ToString()];
        state.TaxRate = 23;
        state.PoorTaxRate = 0.04m;
        state.MiddleTaxRate = 0.12m;
        state.RichTaxRate = 0.30m;
        state.EducationSpending = 0.85m;
        state.MilitarySpending = 0.20m;
        state.AdministrationSpending = 0.65m;

        var persisted = CommandWorldStateMapper.ToPersistedCountries(snapshot, world);

        var roundTripped = Assert.Single(persisted);
        Assert.Equal(23, roundTripped.TaxRate);
        Assert.Equal(0.04m, roundTripped.PoorTaxRate);
        Assert.Equal(0.12m, roundTripped.MiddleTaxRate);
        Assert.Equal(0.30m, roundTripped.RichTaxRate);
        Assert.Equal(0.85m, roundTripped.EducationSpending);
        Assert.Equal(0.20m, roundTripped.MilitarySpending);
        Assert.Equal(0.65m, roundTripped.AdministrationSpending);
    }

    [Fact]
    public void ToPersistedCountries_LeavesUntouchedCountryAtOriginalValues()
    {
        var changed = NewCountryWithBudget(
            tax: 10, poor: -1m, middle: -1m, rich: -1m,
            education: 0.5m, military: 0.5m, administration: 0.5m);
        var untouched = NewCountryWithBudget(
            tax: 12, poor: 0.06m, middle: 0.11m, rich: 0.22m,
            education: 0.30m, military: 0.40m, administration: 0.50m);
        var snapshot = new WorldStateSnapshot { Countries = [changed, untouched] };
        var world = CommandWorldStateMapper.ToSimulationWorld(snapshot, WorldTimestamp, EmptyGoods);

        world.Countries[changed.Id.Value.ToString()].MilitarySpending = 0.99m;

        var persisted = CommandWorldStateMapper.ToPersistedCountries(snapshot, world);

        var preservedUntouched = persisted.Find(c => c.Id.Value == untouched.Id.Value);
        Assert.NotNull(preservedUntouched);
        Assert.Equal(12, preservedUntouched!.TaxRate);
        Assert.Equal(0.06m, preservedUntouched.PoorTaxRate);
        Assert.Equal(0.30m, preservedUntouched.EducationSpending);
        Assert.Equal(0.40m, preservedUntouched.MilitarySpending);
        Assert.Equal(0.50m, preservedUntouched.AdministrationSpending);
    }

    private static Country NewCountryWithBudget(
        int tax,
        decimal poor, decimal middle, decimal rich,
        decimal education, decimal military, decimal administration) =>
        new(new CountryId(Guid.NewGuid()), $"Country-{Guid.NewGuid():N}", "TST", tax)
        {
            Treasury = 1000m,
            PoorTaxRate = poor,
            MiddleTaxRate = middle,
            RichTaxRate = rich,
            EducationSpending = education,
            MilitarySpending = military,
            AdministrationSpending = administration
        };
}
