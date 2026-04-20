using System;
using System.Collections.Generic;
using System.Linq;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Api.Dtos;

namespace VictoriaLike.Server.Api;

public static class ProvincePopGroupMapper
{
    public static List<ProvincePopGroupDto> ToProvincePopGroups(Province province) =>
        OrderedPopGroups(province)
            .Select(pop => new ProvincePopGroupDto
            {
                Id = pop.Id.ToString(),
                Size = pop.Size,
                PopulationShare = PopulationShare(province, pop),
                PopType = pop.PopType,
                Strata = pop.Strata,
                Culture = pop.Culture,
                Religion = pop.Religion,
                Literacy = pop.Literacy,
                Militancy = pop.Militancy,
                Consciousness = pop.Consciousness,
                Cash = pop.Cash,
                LifeNeedsFulfillment = pop.LifeNeedsFulfillment,
                EverydayNeedsFulfillment = pop.EverydayNeedsFulfillment,
                LuxuryNeedsFulfillment = pop.LuxuryNeedsFulfillment,
                EmployedCount = pop.EmployedCount,
                UnemployedCount = pop.UnemployedCount
            })
            .ToList();

    public static List<AdminProvincePopGroupDto> ToAdminProvincePopGroups(Province province) =>
        OrderedPopGroups(province)
            .Select(pop => new AdminProvincePopGroupDto
            {
                Id = pop.Id.ToString(),
                Size = pop.Size,
                PopulationShare = PopulationShare(province, pop),
                PopType = pop.PopType,
                Strata = pop.Strata,
                Culture = pop.Culture,
                Religion = pop.Religion,
                Literacy = pop.Literacy,
                Militancy = pop.Militancy,
                Consciousness = pop.Consciousness,
                Cash = pop.Cash,
                LifeNeedsFulfillment = pop.LifeNeedsFulfillment,
                EverydayNeedsFulfillment = pop.EverydayNeedsFulfillment,
                LuxuryNeedsFulfillment = pop.LuxuryNeedsFulfillment,
                EmployedCount = pop.EmployedCount,
                UnemployedCount = pop.UnemployedCount
            })
            .ToList();

    private static IEnumerable<PopGroup> OrderedPopGroups(Province province) =>
        province.PopGroups
            .OrderByDescending(pop => pop.Size)
            .ThenBy(pop => pop.PopType);

    private static decimal PopulationShare(Province province, PopGroup pop) =>
        province.Population > 0 ? Math.Round(pop.Size / (decimal)province.Population, 4) : 0m;
}
