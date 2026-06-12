using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroExercise.ApiClient;

/// <summary>
/// Shared JSON options for talking to the REST API. Web defaults (camelCase, case-insensitive)
/// plus a string-enum converter to match the server, which serializes <c>TrackingType</c> as
/// strings ("Reps"/"Seconds").
/// </summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
