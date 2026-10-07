using System.Text.Json.Serialization;

namespace DTA.Shared.Protocol;

public sealed class RequestEnvelope<T>
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public T? Payload { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public sealed class ResponseEnvelope<T>
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok"; // "ok" or "error"

    [JsonPropertyName("server_version")]
    public string ServerVersion { get; set; } = "2.0.0";

    [JsonPropertyName("data_version")]
    public int DataVersion { get; set; } = 1;

    [JsonPropertyName("payload")]
    public T? Payload { get; set; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    public static ResponseEnvelope<T> Ok(T payload, int dataVersion = 1, string requestId = "") =>
        new()
        {
            Status = "ok",
            Payload = payload,
            DataVersion = dataVersion,
            RequestId = requestId
        };

    public static ResponseEnvelope<T> Error(string code, string message, string requestId = "") =>
        new()
        {
            Status = "error",
            ErrorCode = code,
            ErrorMessage = message,
            RequestId = requestId
        };
}
