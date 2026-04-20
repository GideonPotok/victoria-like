using System;
using System.Collections.Generic;

namespace VictoriaLike.Server.Api.Dtos;

public class SubmitCommandRequest
{
    public string ActorId { get; set; } = string.Empty;
    public string? CommandId { get; set; }
    public string? IdempotencyKey { get; set; }
    public long? ExpectedWorldTick { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = new();
}

public class CommandResponse
{
    public string CommandId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public long SubmittedTick { get; set; }
    public long? ExpectedWorldTick { get; set; }
    public string? IdempotencyKey { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? RejectionReason { get; set; }
    public bool SoftLimited { get; set; }
    public int? RemainingInWindow { get; set; }
    public long? RetryAfterTicks { get; set; }
    public double? RetryAfterSeconds { get; set; }
}

public class CommandHistoryDto
{
    public string CommandId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public long SubmittedTick { get; set; }
    public long? ExpectedWorldTick { get; set; }
    public string? IdempotencyKey { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OutcomeStatus { get; set; }
    public string? OutcomeReason { get; set; }
    public long? AppliedTick { get; set; }
    public DateTime? AppliedAt { get; set; }
}
