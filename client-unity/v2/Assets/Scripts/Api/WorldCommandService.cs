using System;
using System.Threading.Tasks;

namespace VictoriaLike.Client.Api
{
    public enum CommandOutcomeKind
    {
        Unknown,
        Accepted,
        Applied,
        Rejected,
        Failed
    }

    public enum CommandOutcomeSource
    {
        HttpResponse,
        WebSocketEvent,
        ClientFallback
    }

    public sealed class CommandOutcomeData
    {
        public string CommandId;
        public string CommandType;
        public string RawStatus;
        public string Message;
        public string RejectionReason;
        public int RetryAfterTicks;
        public CommandOutcomeKind Kind;
        public CommandOutcomeSource Source;
    }

    public static class CommandOutcomeMapper
    {
        public static CommandOutcomeData FromResponse(CommandResponseData response)
        {
            if (response == null)
                return new CommandOutcomeData { Kind = CommandOutcomeKind.Unknown, Source = CommandOutcomeSource.ClientFallback };

            return new CommandOutcomeData
            {
                CommandId = response.commandId,
                CommandType = response.commandType,
                RawStatus = response.status,
                Message = response.message,
                RejectionReason = response.rejectionReason,
                RetryAfterTicks = response.retryAfterTicks,
                Kind = MapKind(response.status),
                Source = CommandOutcomeSource.HttpResponse
            };
        }

        public static CommandOutcomeData FromResult(CommandResultData result)
        {
            if (result == null)
                return new CommandOutcomeData { Kind = CommandOutcomeKind.Unknown, Source = CommandOutcomeSource.ClientFallback };

            return new CommandOutcomeData
            {
                CommandId = result.CommandId,
                CommandType = result.CommandType,
                RawStatus = result.Status,
                Message = string.IsNullOrEmpty(result.Message) ? result.Reason : result.Message,
                RejectionReason = result.RejectionReason,
                RetryAfterTicks = result.RetryAfterTicks,
                Kind = MapKind(result.Status),
                Source = CommandOutcomeSource.WebSocketEvent
            };
        }

        public static string Format(CommandOutcomeData outcome)
        {
            if (outcome == null)
                return string.Empty;

            var commandType = string.IsNullOrEmpty(outcome.CommandType) ? "command" : outcome.CommandType;

            if (outcome.Kind == CommandOutcomeKind.Rejected)
            {
                if (outcome.RetryAfterTicks > 0)
                    return $"{commandType}: cooldown, retry in {outcome.RetryAfterTicks} tick(s)";

                if (!string.IsNullOrEmpty(outcome.Message))
                    return $"{commandType}: {outcome.Message}";
            }

            if (!string.IsNullOrEmpty(outcome.Message) &&
                (outcome.Kind == CommandOutcomeKind.Failed || outcome.Kind == CommandOutcomeKind.Unknown))
                return $"{commandType}: {outcome.Message}";

            var statusText = string.IsNullOrEmpty(outcome.RawStatus)
                ? outcome.Kind.ToString().ToLowerInvariant()
                : outcome.RawStatus;

            return $"{commandType}: {statusText}";
        }

        private static CommandOutcomeKind MapKind(string status)
        {
            return (status ?? string.Empty).ToLowerInvariant() switch
            {
                "accepted" => CommandOutcomeKind.Accepted,
                "applied" => CommandOutcomeKind.Applied,
                "rejected" => CommandOutcomeKind.Rejected,
                "failed" => CommandOutcomeKind.Failed,
                _ => CommandOutcomeKind.Unknown
            };
        }
    }

    public interface IWorldCommandService
    {
        event Action<CommandOutcomeData> CommandOutcomeReceived;

        void BindRealtime();
        void UnbindRealtime();
        CommandOutcomeData ToOutcome(CommandResponseData response);
        Task<CommandResponseData> QueueBuildingAsync(string provinceId, string buildingType);
        Task<CommandResponseData> ChangeTaxRateAsync(string countryId, int taxRate);
        Task<CommandResponseData> ChangeStrataTaxAsync(string countryId, string strata, float rate);
        Task<CommandResponseData> ChangeSpendingAsync(string countryId, string category, float level);
        Task<CommandResponseData> MoveArmyAsync(string armyId, string destinationProvinceId);
        Task<CommandResponseData> DeclareWarAsync(string targetCountryId);
        Task<CommandResponseData> MakePeaceAsync(string targetCountryId);
    }

    public sealed class WorldCommandService : IWorldCommandService
    {
        private readonly IWorldApiClient _apiClient;
        private readonly WorldWebSocketClient _webSocketClient;
        private bool _isBound;

        public event Action<CommandOutcomeData> CommandOutcomeReceived;

        public WorldCommandService(IWorldApiClient apiClient, WorldWebSocketClient webSocketClient)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _webSocketClient = webSocketClient;
        }

        public void BindRealtime()
        {
            if (_isBound || _webSocketClient == null)
                return;

            _webSocketClient.OnCommandResult += HandleCommandResult;
            _isBound = true;
        }

        public void UnbindRealtime()
        {
            if (!_isBound || _webSocketClient == null)
                return;

            _webSocketClient.OnCommandResult -= HandleCommandResult;
            _isBound = false;
        }

        public CommandOutcomeData ToOutcome(CommandResponseData response)
            => CommandOutcomeMapper.FromResponse(response);

        public Task<CommandResponseData> QueueBuildingAsync(string provinceId, string buildingType)
            => SubmitAsync(() => _apiClient.QueueBuildingAsync(provinceId, buildingType));

        public Task<CommandResponseData> ChangeTaxRateAsync(string countryId, int taxRate)
            => SubmitAsync(() => _apiClient.ChangeTaxRateAsync(countryId, taxRate));

        public Task<CommandResponseData> ChangeStrataTaxAsync(string countryId, string strata, float rate)
            => SubmitAsync(() => _apiClient.ChangeStrataTaxAsync(countryId, strata, rate));

        public Task<CommandResponseData> ChangeSpendingAsync(string countryId, string category, float level)
            => SubmitAsync(() => _apiClient.ChangeSpendingAsync(countryId, category, level));

        public Task<CommandResponseData> MoveArmyAsync(string armyId, string destinationProvinceId)
            => SubmitAsync(() => _apiClient.MoveArmyAsync(armyId, destinationProvinceId));

        public Task<CommandResponseData> DeclareWarAsync(string targetCountryId)
            => SubmitAsync(() => _apiClient.DeclareWarAsync(targetCountryId));

        public Task<CommandResponseData> MakePeaceAsync(string targetCountryId)
            => SubmitAsync(() => _apiClient.MakePeaceAsync(targetCountryId));

        private void HandleCommandResult(CommandResultData result)
        {
            CommandOutcomeReceived?.Invoke(CommandOutcomeMapper.FromResult(result));
        }

        private async Task<CommandResponseData> SubmitAsync(Func<Task<CommandResponseData>> submitAsync)
        {
            var response = await submitAsync();
            CommandOutcomeReceived?.Invoke(CommandOutcomeMapper.FromResponse(response));
            return response;
        }
    }
}
