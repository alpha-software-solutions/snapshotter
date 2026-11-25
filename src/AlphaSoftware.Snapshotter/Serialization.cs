using System.Collections;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// What gets compared: text, or raw bytes.
/// </summary>
sealed class Payload
{
    Payload(string extension, string? text, byte[]? bytes)
    {
        Extension = extension;
        Text = text;
        Bytes = bytes;
    }

    public string Extension { get; }
    public string? Text { get; }
    public byte[]? Bytes { get; }

    public static Payload FromText(string text, string extension) =>
        new(extension, text, null);

    public static Payload FromBytes(byte[] bytes, string extension) =>
        new(extension, null, bytes);
}

static class Serialization
{
    /// <summary>
    /// Strings are compared as text. Everything else becomes indented JSON.
    /// </summary>
    public static Payload FromObject(object? target, SnapshotSettings settings)
    {
        if (target is string text)
        {
            return FromText(text, settings);
        }

        var options = CreateOptions(settings);
        var node = target == null
            ? null
            : JsonSerializer.SerializeToNode(target, target.GetType(), options);
        return FromNode(node, settings, options);
    }

    public static Payload FromJson(string json, SnapshotSettings settings)
    {
        var node = JsonNode.Parse(
            json,
            documentOptions: new()
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        return FromNode(node, settings, CreateOptions(settings));
    }

    public static Payload FromText(string text, SnapshotSettings settings)
    {
        var scrubber = new ValueScrubber(settings);
        return Payload.FromText(scrubber.ScrubText(Text.Normalize(text)), settings.Extension ?? "txt");
    }

    static Payload FromNode(JsonNode? node, SnapshotSettings settings, JsonSerializerOptions options)
    {
        var scrubber = new ValueScrubber(settings);
        node = scrubber.ScrubNode(node);
        var json = node?.ToJsonString(options) ?? "null";
        return Payload.FromText(scrubber.ScrubText(Text.Normalize(json)), "json");
    }

    internal static JsonSerializerOptions CreateOptions(SnapshotSettings settings)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo => RemoveIgnoredMembers(typeInfo, settings));

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            // Snapshots are read by people: keep non-ASCII characters, quotes and angle brackets readable.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = settings.IgnoreNullValues
                ? JsonIgnoreCondition.WhenWritingNull
                : JsonIgnoreCondition.Never,
            TypeInfoResolver = resolver
        };

        foreach (var converter in settings.Converters)
        {
            options.Converters.Add(converter);
        }

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new ExceptionConverterFactory());
        options.Converters.Add(new TypeConverter());
        return options;
    }

    static void RemoveIgnoredMembers(JsonTypeInfo typeInfo, SnapshotSettings settings)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            var property = typeInfo.Properties[index];
            var memberName = (property.AttributeProvider as MemberInfo)?.Name ?? property.Name;
            if (IsIgnored(typeInfo.Type, memberName, settings) ||
                // Delegates and reflection objects describe code, not data, and mostly cannot be serialized.
                typeof(Delegate).IsAssignableFrom(property.PropertyType) ||
                typeof(MemberInfo).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(Type))
            {
                typeInfo.Properties.RemoveAt(index);
            }
        }
    }

    static bool IsIgnored(Type declaringType, string memberName, SnapshotSettings settings)
    {
        if (settings.IgnoredMembers.Contains(memberName))
        {
            return true;
        }

        foreach (var ignored in settings.IgnoredTypeMembers)
        {
            if (ignored.Value == memberName && ignored.Key.IsAssignableFrom(declaringType))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Replaces values that change from run to run with stable placeholders. The same value always gets the
/// same placeholder within one snapshot, so the snapshot still shows which values were equal.
/// </summary>
sealed class ValueScrubber(SnapshotSettings settings)
{
    static readonly Regex dateTime = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})?$",
        RegexOptions.CultureInvariant);

    static readonly Regex inlineGuid = new(
        @"(?<![0-9a-fA-F])[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![0-9a-fA-F])",
        RegexOptions.CultureInvariant);

    readonly Dictionary<string, string> guids = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> dates = new(StringComparer.Ordinal);

    public JsonNode? ScrubNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var name in jsonObject.Select(property => property.Key).ToList())
                {
                    if (settings.IgnoredMembers.Contains(name))
                    {
                        jsonObject.Remove(name);
                        continue;
                    }

                    var value = jsonObject[name];
                    var scrubbed = ScrubNode(value);
                    if (!ReferenceEquals(scrubbed, value))
                    {
                        jsonObject[name] = scrubbed;
                    }
                }

                return jsonObject;

            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    var value = array[index];
                    var scrubbed = ScrubNode(value);
                    if (!ReferenceEquals(scrubbed, value))
                    {
                        array[index] = scrubbed;
                    }
                }

                return array;

            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                var text = value.TryGetValue<string>(out var stringValue)
                    ? stringValue
                    : JsonSerializer.Deserialize<string>(value.ToJsonString())!;
                var replaced = ScrubValue(text);
                return ReferenceEquals(replaced, text) ? value : JsonValue.Create(replaced);

            default:
                return node;
        }
    }

    string ScrubValue(string value)
    {
        if (settings.ScrubGuids && value.Length == 36 && Guid.TryParseExact(value, "D", out _))
        {
            return Placeholder(guids, "Guid", value);
        }

        if (settings.ScrubDates && dateTime.IsMatch(value))
        {
            return Placeholder(dates, "DateTime", value);
        }

        return value;
    }

    public string ScrubText(string text)
    {
        if (settings.ScrubGuids && settings.ScrubInlineGuids)
        {
            text = inlineGuid.Replace(text, match => Placeholder(guids, "Guid", match.Value));
        }

        foreach (var scrubber in settings.Scrubbers)
        {
            text = Text.Normalize(scrubber(text));
        }

        return text;
    }

    static string Placeholder(Dictionary<string, string> seen, string kind, string value)
    {
        if (!seen.TryGetValue(value, out var placeholder))
        {
            placeholder = $"{kind}_{seen.Count + 1}";
            seen[value] = placeholder;
        }

        return placeholder;
    }
}

/// <summary>
/// Writes what identifies an exception, and leaves out what changes between runs or machines: the stack
/// trace, source, HResult and target site.
/// </summary>
sealed class ExceptionConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(Exception).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter) Activator.CreateInstance(typeof(ExceptionConverter<>).MakeGenericType(typeToConvert))!;
}

sealed class ExceptionConverter<T> : JsonConverter<T>
    where T : Exception
{
    static readonly HashSet<string> handled = new(
        typeof(Exception).GetProperties().Select(property => property.Name)
            .Concat([nameof(ArgumentException.ParamName), nameof(AggregateException.InnerExceptions)]),
        StringComparer.Ordinal);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Exceptions are only written to snapshots, never read back.");

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        WriteException(writer, value, options);

    static void WriteException(Utf8JsonWriter writer, Exception exception, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Type", exception.GetType().Name);
        writer.WriteString("Message", Message(exception));

        if (exception is ArgumentException { ParamName: { } paramName })
        {
            writer.WriteString("ParamName", paramName);
        }

        // Properties that derived exception types add, such as HttpRequestException.StatusCode.
        foreach (var property in exception.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (handled.Contains(property.Name) || property.GetIndexParameters().Length > 0 || !property.CanRead)
            {
                continue;
            }

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(exception);
            }
            catch
            {
                continue;
            }

            if (propertyValue == null || propertyValue is Delegate or MemberInfo)
            {
                continue;
            }

            writer.WritePropertyName(property.Name);
            JsonSerializer.Serialize(writer, propertyValue, propertyValue.GetType(), options);
        }

        if (exception.Data.Count > 0)
        {
            writer.WritePropertyName("Data");
            writer.WriteStartObject();
            foreach (DictionaryEntry entry in exception.Data)
            {
                writer.WritePropertyName(entry.Key.ToString() ?? "");
                JsonSerializer.Serialize(writer, entry.Value, entry.Value?.GetType() ?? typeof(object), options);
            }

            writer.WriteEndObject();
        }

        if (exception is AggregateException aggregate)
        {
            writer.WritePropertyName("InnerExceptions");
            writer.WriteStartArray();
            foreach (var inner in aggregate.InnerExceptions)
            {
                WriteException(writer, inner, options);
            }

            writer.WriteEndArray();
        }
        else if (exception.InnerException != null)
        {
            writer.WritePropertyName("InnerException");
            WriteException(writer, exception.InnerException, options);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// The message without what the runtime appends from other fields, which differs between .NET Framework
    /// and .NET: the parameter name of an <see cref="ArgumentException" />, and the inner messages of an
    /// <see cref="AggregateException" />. Both are written as fields of their own.
    /// </summary>
    internal static string Message(Exception exception)
    {
        var message = exception.Message;

        if (exception is ArgumentException { ParamName: { } paramName })
        {
            message = RemoveSuffix(message, $" (Parameter '{paramName}')");
            message = RemoveSuffix(message, $"\r\nParameter name: {paramName}");
            message = RemoveSuffix(message, $"\nParameter name: {paramName}");
        }

        if (exception is AggregateException aggregate)
        {
            message = RemoveSuffix(message, string.Concat(aggregate.InnerExceptions.Select(inner => $" ({inner.Message})")));
        }

        return message;
    }

    static string RemoveSuffix(string value, string suffix) =>
        suffix.Length > 0 && value.EndsWith(suffix, StringComparison.Ordinal)
            ? value.Substring(0, value.Length - suffix.Length)
            : value;
}

/// <summary>
/// System.Text.Json refuses to serialize <see cref="Type" />. A snapshot only needs its name.
/// </summary>
sealed class TypeConverter : JsonConverter<Type>
{
    public override bool CanConvert(Type typeToConvert) =>
        typeof(Type).IsAssignableFrom(typeToConvert);

    public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Types are only written to snapshots, never read back.");

    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.FullName ?? value.Name);
}

static class Text
{
    /// <summary>
    /// Line endings become \n and a byte order mark is dropped, so a snapshot compares the same on every OS
    /// and whatever the editor did to the verified file.
    /// </summary>
    public static string Normalize(string text)
    {
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text.Substring(1);
        }

        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }
}
