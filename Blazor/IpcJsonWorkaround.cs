using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace AvaloniaBlazorHybridDemo.Blazor;

/// <summary>
/// NativeAOT workaround for Blazor Hybrid's IPC JSON pipeline.
///
/// IpcCommon (Microsoft.AspNetCore.Components.WebView 10.0.0) serializes and
/// parses every "__bwv:" message through the shared internal
/// JsonSerializerOptionsProvider.Options with reflection-based System.Text.Json.
/// That path resolves some generic converters at runtime, e.g.
///   deserialize JsonElement[]  -> ArrayConverter&lt;JsonElement[], JsonElement&gt;
///   serialize   JSCallResultType -> EnumConverter&lt;JSCallResultType&gt;
/// NativeAOT cannot materialize those closed generics:
///   "'...' is missing native code or metadata."
///
/// Registering explicit converters on the same options instance lets the lookup
/// succeed before the reflection converter factories are expanded. Every type
/// below is a closed generic created in code, so ILC compiles it statically.
/// </summary>
internal static class IpcJsonWorkaround
{
    private const string ProviderTypeName =
        "Microsoft.AspNetCore.Components.JsonSerializerOptionsProvider";

    public static void Apply()
    {
        try
        {
            var options = TryGetIpcJsonOptions();
            if (options is null)
            {
                AotDiagnostics.Log("IpcJsonWorkaround: IPC JsonSerializerOptions not found");
                return;
            }

            options.Converters.Add(new JsonElementArrayConverter());
            options.Converters.Add(new EnumAsInt32Converter<JSCallResultType>());
            options.Converters.Add(new EnumAsInt32Converter<JSCallType>());

            AotDiagnostics.Log("IpcJsonWorkaround: IPC converters registered");
        }
        catch (Exception ex)
        {
            // Never block startup because of a compatibility workaround.
            AotDiagnostics.Log("IpcJsonWorkaround FAILED: " + ex);
        }
    }

    private static JsonSerializerOptions? TryGetIpcJsonOptions()
    {
        var provider = typeof(Microsoft.AspNetCore.Components.WebView.WebViewManager).Assembly
                           .GetType(ProviderTypeName, throwOnError: false)
                       ?? AppDomain.CurrentDomain.GetAssemblies()
                           .Select(a => a.GetType(ProviderTypeName, throwOnError: false))
                           .FirstOrDefault(t => t is not null);

        var field = provider?.GetField("Options",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        return field?.GetValue(null) as JsonSerializerOptions;
    }

    /// <summary>JsonElement[] without the reflection-based ArrayConverter.</summary>
    private sealed class JsonElementArrayConverter : JsonConverter<JsonElement[]>
    {
        public override JsonElement[] Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
                throw new JsonException("Expected a JSON array of IPC arguments.");

            var result = new JsonElement[root.GetArrayLength()];
            var i = 0;
            foreach (var element in root.EnumerateArray())
                result[i++] = element.Clone();   // detach from the disposed document
            return result;
        }

        public override void Write(
            Utf8JsonWriter writer, JsonElement[] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var element in value)
                element.WriteTo(writer);
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// Writes an enum as its underlying number, matching System.Text.Json's default
    /// behaviour, but without EnumConverter&lt;TEnum&gt; being built by reflection.
    /// </summary>
    private sealed class EnumAsInt32Converter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => (TEnum)Enum.ToObject(typeof(TEnum), reader.GetInt32());

        public override void Write(
            Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
            => writer.WriteNumberValue(Convert.ToInt32(value));
    }
}
