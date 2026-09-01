using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notificaciones.Api.Serialization;

/// <summary>
/// Shared serialization contract for the gateway. Controllers and the Server-Sent Events stream
/// must agree byte for byte, otherwise the console would receive two different shapes for the
/// same payload depending on the transport.
/// </summary>
public static class ApiJson
{
    /// <summary>
    /// camelCase properties and enums serialized by name, so the browser never has to map
    /// numeric enum values back to labels.
    /// </summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
