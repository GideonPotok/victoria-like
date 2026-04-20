using VictoriaLike.Core.Core.World;

namespace VictoriaLike.Core.Data.Validation;

public sealed class WorldValidator
{
    public void Validate(WorldState world)
    {
        foreach (var country in world.Countries.Values)
        {
            foreach (var provinceId in country.ProvinceIds)
            {
                if (!world.Provinces.ContainsKey(provinceId))
                {
                    throw new InvalidOperationException($"Country {country.Id} references missing province {provinceId}.");
                }
            }
        }

        foreach (var province in world.Provinces.Values)
        {
            if (!world.Countries.ContainsKey(province.OwnerId))
            {
                throw new InvalidOperationException($"Province {province.Id} references missing owner {province.OwnerId}.");
            }

            if (string.IsNullOrWhiteSpace(province.RgoType))
            {
                throw new InvalidOperationException($"Province {province.Id} has no RGO type.");
            }

            foreach (var (goodId, quantity) in province.OutputsPerTick)
            {
                if (string.IsNullOrWhiteSpace(goodId))
                    throw new InvalidOperationException($"Province {province.Id} has an empty output good id.");
                if (!world.Goods.ContainsKey(goodId))
                    throw new InvalidOperationException($"Province {province.Id} outputs unknown good {goodId}.");
                if (quantity < 0m)
                    throw new InvalidOperationException($"Province {province.Id} output {goodId} is negative.");
            }

            foreach (var popId in province.PopulationIds)
            {
                if (!world.Pops.TryGetValue(popId, out var pop))
                {
                    throw new InvalidOperationException($"Province {province.Id} references missing pop {popId}.");
                }

                if (!string.Equals(pop.ProvinceId, province.Id, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Pop {pop.Id} belongs to {pop.ProvinceId} but is listed in {province.Id}.");
                }
            }
        }

        foreach (var factory in world.Factories.Values)
        {
            if (!world.Countries.ContainsKey(factory.CountryId))
                throw new InvalidOperationException($"Factory {factory.Id} references missing country {factory.CountryId}.");

            if (!string.IsNullOrWhiteSpace(factory.ProvinceId) && !world.Provinces.ContainsKey(factory.ProvinceId))
                throw new InvalidOperationException($"Factory {factory.Id} references missing province {factory.ProvinceId}.");

            if (string.IsNullOrWhiteSpace(factory.OutputGood))
                throw new InvalidOperationException($"Factory {factory.Id} has no output good.");

            if (!world.Goods.ContainsKey(factory.OutputGood))
                throw new InvalidOperationException($"Factory {factory.Id} outputs unknown good {factory.OutputGood}.");

            foreach (var goodId in factory.InputGoods.Keys)
            {
                if (string.IsNullOrWhiteSpace(goodId))
                    throw new InvalidOperationException($"Factory {factory.Id} has an empty input good id.");
                if (!world.Goods.ContainsKey(goodId))
                    throw new InvalidOperationException($"Factory {factory.Id} consumes unknown good {goodId}.");
            }
        }

        foreach (var army in world.Armies.Values)
        {
            if (!world.Countries.ContainsKey(army.CountryId))
                throw new InvalidOperationException($"Army {army.Id} references missing country {army.CountryId}.");

            if (!world.Provinces.ContainsKey(army.LocationProvinceId))
                throw new InvalidOperationException($"Army {army.Id} references missing location {army.LocationProvinceId}.");

            if (!string.IsNullOrWhiteSpace(army.DestinationProvinceId) &&
                !world.Provinces.ContainsKey(army.DestinationProvinceId))
            {
                throw new InvalidOperationException($"Army {army.Id} references missing destination {army.DestinationProvinceId}.");
            }
        }

        foreach (var war in world.Wars.Values)
        {
            if (!world.Countries.ContainsKey(war.AttackerCountryId))
                throw new InvalidOperationException($"War {war.Id} references missing attacker {war.AttackerCountryId}.");

            if (!world.Countries.ContainsKey(war.DefenderCountryId))
                throw new InvalidOperationException($"War {war.Id} references missing defender {war.DefenderCountryId}.");

            if (string.Equals(war.AttackerCountryId, war.DefenderCountryId, StringComparison.Ordinal))
                throw new InvalidOperationException($"War {war.Id} has the same attacker and defender.");
        }

        foreach (var battle in world.BattleReports.Values)
        {
            if (!world.Wars.ContainsKey(battle.WarId))
                throw new InvalidOperationException($"Battle {battle.Id} references missing war {battle.WarId}.");

            if (!world.Provinces.ContainsKey(battle.ProvinceId))
                throw new InvalidOperationException($"Battle {battle.Id} references missing province {battle.ProvinceId}.");
        }
    }
}
