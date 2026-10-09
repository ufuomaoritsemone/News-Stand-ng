using System.Text.Json;
using System.Text.Json.Serialization;

namespace NigerianNewGrid;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> used consistently across all services
/// to eliminate per-class duplication and ensure a single point of change.
/// </summary>
internal static class SharedJsonOptions
{
    /// <summary>
    /// camelCase property naming, case-insensitive deserialization, null properties omitted on serialization.
    /// Suitable for all internal Preferences / SQLite JSON payloads.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };
}
