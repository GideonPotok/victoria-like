using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VictoriaLike.Client.Api
{
    public enum ScheduledCommandState
    {
        Idle,
        Drafting,
        Debouncing,
        Submitting,
        RetryScheduled,
        Settled
    }

    public sealed class ScheduledCommandStatus
    {
        public string FieldKey;
        public ScheduledCommandState State;
        public float DesiredValue;
        public float LastSubmittedValue;
        public CommandResponseData LastResponse;
    }

    public interface IClientCommandScheduler
    {
        event Action<ScheduledCommandStatus> StatusChanged;

        void SetDesiredValue(string fieldKey, float value, Func<float, Task<CommandResponseData>> submitAsync);
        void Clear();
    }

    public sealed class ClientCommandScheduler : IClientCommandScheduler
    {
        private const int DebounceMilliseconds = 400;
        private const int RetryTickMilliseconds = 1000;

        private readonly object _gate = new();
        private readonly Dictionary<string, ScheduledFieldSlot> _slots = new();

        public event Action<ScheduledCommandStatus> StatusChanged;

        public void SetDesiredValue(string fieldKey, float value, Func<float, Task<CommandResponseData>> submitAsync)
        {
            if (string.IsNullOrEmpty(fieldKey))
                throw new ArgumentException("Field key is required.", nameof(fieldKey));
            if (submitAsync == null)
                throw new ArgumentNullException(nameof(submitAsync));

            ScheduledFieldSlot slot;
            lock (_gate)
            {
                if (!_slots.TryGetValue(fieldKey, out slot))
                {
                    slot = new ScheduledFieldSlot(fieldKey, submitAsync);
                    _slots[fieldKey] = slot;
                }
                else
                {
                    slot.SubmitAsync = submitAsync;
                }

                slot.DesiredValue = Mathf.Clamp01(value);
                slot.HasDesiredValue = true;
                slot.LastEditAtUtc = DateTime.UtcNow;
                slot.State = ScheduledCommandState.Drafting;

                Publish(slot, null);

                if (slot.RunnerTask == null || slot.RunnerTask.IsCompleted)
                    slot.RunnerTask = RunSlotAsync(slot);
            }
        }

        public void Clear()
        {
            List<ScheduledFieldSlot> snapshot;
            lock (_gate)
            {
                snapshot = new List<ScheduledFieldSlot>(_slots.Values);
                _slots.Clear();
            }

            foreach (var slot in snapshot)
            {
                slot.HasDesiredValue = false;
                slot.State = ScheduledCommandState.Idle;
                Publish(slot, slot.LastResponse);
                slot.Cancellation.Cancel();
                slot.Cancellation.Dispose();
            }
        }

        private async Task RunSlotAsync(ScheduledFieldSlot slot)
        {
            try
            {
                while (!slot.Cancellation.IsCancellationRequested)
                {
                    float desiredValue;

                    lock (_gate)
                    {
                        if (!slot.HasDesiredValue)
                        {
                            slot.State = ScheduledCommandState.Idle;
                            Publish(slot, slot.LastResponse);
                            break;
                        }

                        desiredValue = slot.DesiredValue;
                        slot.State = ScheduledCommandState.Debouncing;
                        Publish(slot, slot.LastResponse);
                    }

                    await DelayUntilQuietAsync(slot, slot.Cancellation.Token);
                    slot.Cancellation.Token.ThrowIfCancellationRequested();

                    lock (_gate)
                    {
                        if (!slot.HasDesiredValue || Math.Abs(slot.DesiredValue - desiredValue) > 0.0001f)
                            continue;

                        slot.State = ScheduledCommandState.Submitting;
                        slot.LastSubmittedValue = desiredValue;
                        Publish(slot, slot.LastResponse);
                    }

                    CommandResponseData response;
                    try
                    {
                        response = await slot.SubmitAsync(desiredValue);
                    }
                    catch (Exception ex)
                    {
                        response = new CommandResponseData
                        {
                            commandType = slot.FieldKey,
                            status = "failed",
                            message = ex.Message,
                            rejectionReason = "TransportFailure"
                        };
                    }

                    var retryDelayMs = 0;
                    lock (_gate)
                    {
                        slot.LastResponse = response;

                        if (response != null &&
                            response.status == "rejected" &&
                            response.retryAfterTicks > 0)
                        {
                            retryDelayMs = Math.Max(1, response.retryAfterTicks) * RetryTickMilliseconds;
                            slot.State = ScheduledCommandState.RetryScheduled;
                            Publish(slot, response);
                        }
                        else
                        {
                            if (slot.HasDesiredValue &&
                                Math.Abs(slot.DesiredValue - desiredValue) < 0.0001f)
                            {
                                slot.HasDesiredValue = false;
                            }

                            slot.State = ScheduledCommandState.Settled;
                            Publish(slot, response);
                        }
                    }

                    if (retryDelayMs > 0)
                    {
                        await Task.Delay(retryDelayMs, slot.Cancellation.Token);
                        continue;
                    }

                    lock (_gate)
                    {
                        if (slot.HasDesiredValue)
                            continue;

                        slot.State = ScheduledCommandState.Idle;
                        Publish(slot, slot.LastResponse);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                lock (_gate)
                {
                    if (_slots.TryGetValue(slot.FieldKey, out var current) && ReferenceEquals(current, slot))
                    {
                        if (!slot.HasDesiredValue)
                            _slots.Remove(slot.FieldKey);
                    }
                }
            }
        }

        private async Task DelayUntilQuietAsync(ScheduledFieldSlot slot, CancellationToken cancellationToken)
        {
            while (true)
            {
                var waitMs = 0;
                lock (_gate)
                {
                    var elapsed = DateTime.UtcNow - slot.LastEditAtUtc;
                    waitMs = Math.Max(0, DebounceMilliseconds - (int)elapsed.TotalMilliseconds);
                }

                if (waitMs <= 0)
                    return;

                await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
            }
        }

        private void Publish(ScheduledFieldSlot slot, CommandResponseData response)
        {
            var handler = StatusChanged;
            if (handler == null)
                return;

            handler(new ScheduledCommandStatus
            {
                FieldKey = slot.FieldKey,
                State = slot.State,
                DesiredValue = slot.DesiredValue,
                LastSubmittedValue = slot.LastSubmittedValue,
                LastResponse = response
            });
        }

        private sealed class ScheduledFieldSlot
        {
            public ScheduledFieldSlot(string fieldKey, Func<float, Task<CommandResponseData>> submitAsync)
            {
                FieldKey = fieldKey;
                SubmitAsync = submitAsync;
            }

            public string FieldKey { get; }
            public CancellationTokenSource Cancellation { get; } = new();
            public Func<float, Task<CommandResponseData>> SubmitAsync { get; set; }
            public Task RunnerTask { get; set; }
            public DateTime LastEditAtUtc { get; set; }
            public float DesiredValue { get; set; }
            public float LastSubmittedValue { get; set; }
            public bool HasDesiredValue { get; set; }
            public ScheduledCommandState State { get; set; }
            public CommandResponseData LastResponse { get; set; }
        }
    }
}
