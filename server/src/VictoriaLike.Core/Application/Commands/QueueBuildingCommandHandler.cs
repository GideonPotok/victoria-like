using System;
using System.Linq;
using System.Text.Json;
using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

public class QueueBuildingCommandHandler : ICommandHandler
{
    public string CommandType => "QueueBuilding";

    public CommandResult Handle(CommandEnvelope envelope, WorldState world, ActorId actor)
    {
        if (!envelope.Payload.TryGetValue("provinceId", out var provinceIdObj) ||
            !envelope.Payload.TryGetValue("buildingType", out var buildingTypeObj))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                "Missing provinceId or buildingType in payload");

        if (provinceIdObj is not JsonElement provinceIdEl || buildingTypeObj is not JsonElement buildingTypeEl)
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid payload format");

        var buildingType = buildingTypeEl.GetString()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(buildingType) || !BuildingTemplates.All.TryGetValue(buildingType, out var template))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                $"Unknown building type: '{buildingType}'. Valid: {string.Join(", ", BuildingTemplates.All.Keys)}");

        var provinceIdStr = provinceIdEl.GetString();
        if (!Guid.TryParse(provinceIdStr, out var provinceGuid))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload, "Invalid provinceId format");

        var provinceKey = provinceGuid.ToString();
        if (!world.Provinces.TryGetValue(provinceKey, out var province))
            return CommandResult.Reject(CommandRejectionReason.MalformedPayload,
                $"Province {provinceKey} not found");

        if (!CommandAuthorizer.TryResolveActorAccount(actor, world, out var account, out var authFailure))
            return authFailure!;

        if (!CommandAuthorizer.TryCheckProvinceOwnership(account!, province, out authFailure))
            return authFailure!;

        var controlledCountryId = account!.ControlledCountry.Value.ToString();
        if (!world.Countries.TryGetValue(controlledCountryId, out var country))
            return CommandResult.Fail($"Country {controlledCountryId} not found in world state");

        if (!CommandAuthorizer.TryCheckTreasury(country, template.Cost, out authFailure))
            return authFailure!;

        if (world.BuildingQueue.Any(entry => entry.ProvinceId == provinceKey))
            return CommandResult.Reject(
                CommandRejectionReason.ActiveConstructionConflict,
                $"Province {provinceKey} already has active construction queued");

        country.Treasury -= template.Cost;

        world.BuildingQueue.Add(new BuildingQueueEntry
        {
            Id = Guid.NewGuid().ToString(),
            ProvinceId = provinceKey,
            CountryId = controlledCountryId,
            BuildingType = buildingType,
            TicksRemaining = template.BuildTicks,
            QueuedAt = DateTime.UtcNow
        });

        return CommandResult.Success();
    }
}
