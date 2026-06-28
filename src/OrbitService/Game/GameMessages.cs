using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrbitService.Game;

public static class GameMessages
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(object message) =>
        JsonSerializer.Serialize(message, JsonOptions);

    public static IncomingMessage? Deserialize(string json) =>
        JsonSerializer.Deserialize<IncomingMessage>(json, JsonOptions);
}

public sealed class IncomingMessage
{
    public string? Type { get; set; }
    public double Angle { get; set; }
    public double Speed { get; set; }
    public bool OnTrail { get; set; }
    public bool Crashed { get; set; }
}

public sealed class JoinedMessage
{
    public string Type { get; init; } = "joined";
    public required string PlayerId { get; init; }
    public required string Status { get; init; }
}

public sealed class MatchedMessage
{
    public string Type { get; init; } = "matched";
    public required string PlayerId { get; init; }
    public required string OpponentId { get; init; }
    public long StartAt { get; init; }
}

public sealed class OpponentStateMessage
{
    public string Type { get; init; } = "opponent_state";
    public double Angle { get; init; }
    public double Speed { get; init; }
    public bool OnTrail { get; init; }
    public bool Crashed { get; init; }
}

public sealed class OpponentDisconnectedMessage
{
    public string Type { get; init; } = "opponent_disconnected";
}

public sealed class ErrorMessage
{
    public string Type { get; init; } = "error";
    public required string Message { get; init; }
}
