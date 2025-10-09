using System;

namespace VelloSharp.Composition.Telemetry;

public readonly record struct CommandResult(
    CommandStatus Status,
    string Message,
    DateTime TimestampUtc)
{
    public static CommandResult Accepted(string message = "", DateTime? timestampUtc = null) =>
        new(CommandStatus.Accepted, message, timestampUtc ?? DateTime.UtcNow);

    public static CommandResult Rejected(string message, DateTime? timestampUtc = null) =>
        new(CommandStatus.Rejected, message, timestampUtc ?? DateTime.UtcNow);

    public static CommandResult Failed(string message, DateTime? timestampUtc = null) =>
        new(CommandStatus.Failed, message, timestampUtc ?? DateTime.UtcNow);

    public static CommandResult Pending(string message = "", DateTime? timestampUtc = null) =>
        new(CommandStatus.Pending, message, timestampUtc ?? DateTime.UtcNow);

    public static CommandResult NotFound(string message, DateTime? timestampUtc = null) =>
        new(CommandStatus.NotFound, message, timestampUtc ?? DateTime.UtcNow);
}
