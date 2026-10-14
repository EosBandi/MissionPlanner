using System.Text.Json;
using System.Text.Json.Serialization;

namespace MPBackend.Server;

public static class Json
{
    /// <summary>
    /// Shared by REST and WebSocket so both speak the same JSON.
    /// Fields are included because MAVLink message structs expose public fields, and NaN/Infinity
    /// are allowed because CurrentState uses them for "no data".
    /// </summary>
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.IncludeFields = true;
        options.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals | JsonNumberHandling.AllowReadingFromString;
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        if (!options.Converters.OfType<JsonStringEnumConverter>().Any())
            options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
