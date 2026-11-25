using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace AlphaSoftware.Snapshotter;

enum ValueContext
{
    Root,
    Member,
    Item
}

/// <summary>
/// Turns objects into <see cref="Node" />s the way Verify does: public fields then properties, defaults and empty
/// collections left out (but bools always written), dictionaries sorted, reference loops cut, Guids and dates
/// replaced with numbered placeholders.
/// </summary>
sealed class Serializer
{
    static readonly ConcurrentDictionary<Type, IReadOnlyList<SerializableMember>> membersCache = new();
    static readonly ConcurrentDictionary<Type, object?> defaults = new();

    readonly Counter counter;
    readonly HashSet<object> path = new(ReferenceComparer.Instance);

    public Serializer(EffectiveSettings settings, Counter counter)
    {
        Settings = settings;
        this.counter = counter;
        Rules = settings.Rules;
    }

    public EffectiveSettings Settings { get; }

    public MemberRuleView Rules { get; }

    public Node Root(object? value) =>
        Value(value, value?.GetType() ?? typeof(object), ValueContext.Root) ?? ValueNode.Null;

    public Node? Value(object? value, Type declaredType, ValueContext context, bool nestedException = false)
    {
        if (value == null)
        {
            return ValueNode.Null;
        }

        var type = value.GetType();
        foreach (var converter in Settings.Converters)
        {
            if (converter.CanConvert(type))
            {
                return Guarded(value, () =>
                {
                    var writer = new VerifyJsonWriter(this, counter, Settings.Context);
                    converter.Write(writer, value);
                    return writer.Result ?? new ObjectNode();
                });
            }
        }

        switch (value)
        {
            case string text:
                return String(text);
            case bool boolean:
                return new ValueNode(boolean ? "true" : "false", ValueKind.Literal);
            case char character:
                return new ValueNode(character.ToString(), ValueKind.String);
            case Enum:
                return new ValueNode(value.ToString()!, ValueKind.String);
            case Guid guid:
                return ValueNode.String(Settings.ScrubGuids ? counter.NextString(guid) : guid.ToString("D"));
            case DateTime dateTime:
                return ValueNode.String(Settings.ScrubDates ? counter.NextString(dateTime) : Formatting.DateTime(dateTime));
            case DateTimeOffset dateTimeOffset:
                return ValueNode.String(Settings.ScrubDates ? counter.NextString(dateTimeOffset) : Formatting.DateTimeOffset(dateTimeOffset));
#if NET6_0_OR_GREATER
            case DateOnly date:
                return ValueNode.String(Settings.ScrubDates ? counter.NextString(date) : Formatting.Date(date));
            case TimeOnly time:
                return ValueNode.String(Settings.ScrubDates ? counter.NextString(time) : Formatting.Time(time));
#endif
            case TimeSpan timeSpan:
                return ValueNode.String(timeSpan.ToString("c"));
            case Uri uri:
                return String(uri.OriginalString);
            case Version version:
                return ValueNode.String(version.ToString());
            case Type typeValue:
                return ValueNode.String(Formatting.TypeName(typeValue));
            case Encoding encoding:
                return ValueNode.String(encoding.WebName);
            case FileInfo file:
                return String(file.FullName.Replace('\\', '/'));
            case DirectoryInfo directory:
                return String(directory.FullName.Replace('\\', '/').TrimEnd('/') + "/");
            case Expression expression:
                return ValueNode.String(expression.ToString());
            case MemberInfo member:
                return ValueNode.String(Formatting.Member(member));
            case byte[] bytes:
                return bytes.Length == 0 && context != ValueContext.Root && Settings.IgnoreEmptyCollections
                    ? null
                    : ValueNode.String(Convert.ToBase64String(bytes));
            case Delegate @delegate:
                return Delegate(@delegate);
            case JsonElement element:
                return Json(element, context);
            case JsonDocument document:
                return Json(document.RootElement, context);
            case XNode or XmlNode:
                return String(value.ToString()!);
            case Exception exception:
                return Guarded(value, () => Exception(exception, nestedException));
            case Task task:
                return Guarded(value, () => Task(task));
        }

        var number = Formatting.Number(value);
        if (number != null)
        {
            return new ValueNode(number, Formatting.IsNonFinite(value) ? ValueKind.Quoted : ValueKind.Literal);
        }

        if (IsKeyValuePair(type))
        {
            return Guarded(value, () => KeyValuePair(value, type));
        }

        if (value is NameValueCollection collection)
        {
            return NameValues(collection, context);
        }

        if (DictionaryEntries(value) is { } entries)
        {
            return Guarded(value, () => Dictionary(value, entries, context));
        }

        if (value is IEnumerable enumerable)
        {
            return Guarded(value, () => Array(enumerable, context));
        }

        return Guarded(value, () => Object(value, type));
    }

    // Reference loops: a value already being written further up is skipped.
    Node? Guarded(object value, Func<Node?> write)
    {
        if (value.GetType().IsValueType)
        {
            return write();
        }

        if (!path.Add(value))
        {
            return null;
        }

        try
        {
            return write();
        }
        finally
        {
            path.Remove(value);
        }
    }

    Node String(string text)
    {
        if (!Settings.ScrubbersDisabled && counter.TryConvert(text, Settings.ScrubGuids, Settings.ScrubDates, out var replaced))
        {
            return ValueNode.String(replaced);
        }

        return ValueNode.String(text);
    }

    string Key(object key)
    {
        switch (key)
        {
            case string text:
                return !Settings.ScrubbersDisabled && counter.TryConvert(text, Settings.ScrubGuids, Settings.ScrubDates, out var replaced) ? replaced : text;
            case Guid or DateTime or DateTimeOffset
#if NET6_0_OR_GREATER
                or DateOnly or TimeOnly
#endif
                :
                return ((ValueNode)Value(key, key.GetType(), ValueContext.Item)!).Text;
            case Type type:
                return Formatting.TypeName(type);
            default:
                return Convert.ToString(key, CultureInfo.InvariantCulture) ?? "";
        }
    }

    Node? Array(IEnumerable enumerable, ValueContext context)
    {
        var array = new ArrayNode();
        var any = false;
        foreach (var item in enumerable)
        {
            any = true;
            if (item != null)
            {
                switch (Rules.ForInstance(item))
                {
                    case MemberDecision.Ignore:
                        continue;
                    case MemberDecision.Scrub:
                        array.Items.Add(ValueNode.Scrubbed);
                        continue;
                }

                if (Settings.IgnoreEmptyCollections && IsEmptyCollection(item))
                {
                    continue;
                }
            }

            var node = Value(item, item?.GetType() ?? typeof(object), ValueContext.Item);
            if (node != null)
            {
                array.Items.Add(node);
            }
        }

        if (!any && context != ValueContext.Root && Settings.IgnoreEmptyCollections)
        {
            return null;
        }

        return array;
    }

    Node? Dictionary(object dictionary, List<(object Key, object? Value)> entries, ValueContext context)
    {
        if (entries.Count == 0 && context != ValueContext.Root && Settings.IgnoreEmptyCollections)
        {
            return null;
        }

        if (Settings.SortDictionaries && !IsSorted(dictionary.GetType()))
        {
            if (entries.All(entry => entry.Key is string))
            {
                entries = entries.OrderBy(entry => (string)entry.Key, StringComparer.OrdinalIgnoreCase).ToList();
            }
            else if (entries.Count > 0 && entries.All(entry => entry.Key is IComparable && entry.Key.GetType() == entries[0].Key.GetType()))
            {
                entries = entries.OrderBy(entry => entry.Key, Comparer<object>.Default).ToList();
            }
        }

        var obj = new ObjectNode();
        foreach (var (key, value) in entries)
        {
            if (key is string name)
            {
                switch (Rules.ForName(name))
                {
                    case MemberDecision.Ignore:
                        continue;
                    case MemberDecision.Scrub:
                        obj.Add(Key(key), ValueNode.Scrubbed);
                        continue;
                }
            }

            if (value != null)
            {
                switch (Rules.ForInstance(value))
                {
                    case MemberDecision.Ignore:
                        continue;
                    case MemberDecision.Scrub:
                        obj.Add(Key(key), ValueNode.Scrubbed);
                        continue;
                }

                if (Settings.IgnoreEmptyCollections && IsEmptyCollection(value))
                {
                    continue;
                }
            }

            var node = Value(value, value?.GetType() ?? typeof(object), ValueContext.Item);
            if (node != null)
            {
                obj.Add(Key(key), node);
            }
        }

        return obj;
    }

    Node? NameValues(NameValueCollection collection, ValueContext context)
    {
        if (collection.Count == 0 && context != ValueContext.Root && Settings.IgnoreEmptyCollections)
        {
            return null;
        }

        var obj = new ObjectNode();
        foreach (var key in collection.AllKeys)
        {
            obj.Add(key ?? "null", String(string.Join(",", collection.GetValues(key) ?? [])));
        }

        return obj;
    }

    Node? KeyValuePair(object pair, Type type)
    {
        var key = type.GetProperty("Key")!.GetValue(pair);
        var value = type.GetProperty("Value")!.GetValue(pair);
        var obj = new ObjectNode();
        if (key is string name)
        {
            obj.Add(Key(name), Value(value, value?.GetType() ?? typeof(object), ValueContext.Item) ?? ValueNode.Null);
            return obj;
        }

        obj.Add("Key", Value(key, key?.GetType() ?? typeof(object), ValueContext.Member) ?? ValueNode.Null);
        var valueNode = value == null ? null : Value(value, value.GetType(), ValueContext.Member);
        if (valueNode != null)
        {
            obj.Add("Value", valueNode);
        }

        return obj;
    }

    Node Object(object value, Type type)
    {
        var obj = new ObjectNode();
        foreach (var member in Members(type))
        {
            WriteMember(obj, value, member, type);
        }

        return obj;
    }

    void WriteMember(ObjectNode obj, object target, SerializableMember member, Type targetType, string? nameOverride = null)
    {
        var name = nameOverride ?? member.Name;
        switch (Rules.ForMember(member.Member, member.Type))
        {
            case MemberDecision.Ignore:
                return;
            case MemberDecision.Scrub:
                // A scrubbed member is always written, and its getter is never called.
                obj.Add(name, ValueNode.Scrubbed);
                return;
        }

        object? value;
        try
        {
            value = member.Get(target);
        }
        catch (Exception exception)
        {
            var inner = exception is TargetInvocationException { InnerException: { } thrown } ? thrown : exception;
            if (Rules.IgnoresThrow(inner))
            {
                return;
            }

            throw new Exception($"Error getting value from '{member.Name}' on '{Formatting.TypeName(targetType)}'.", inner);
        }

        if (value is string text && name == "Message" && target is Exception exceptionTarget)
        {
            value = ExceptionMessage(exceptionTarget, text);
        }

        if (!ShouldWrite(value, member.Type))
        {
            return;
        }

        if (value != null)
        {
            switch (Rules.ForInstance(value))
            {
                case MemberDecision.Ignore:
                    return;
                case MemberDecision.Scrub:
                    obj.Add(name, ValueNode.Scrubbed);
                    return;
            }

            if (Settings.ScrubNumericIds && Formatting.IsNumeric(member.Type) && IsIdName(name))
            {
                var scope = string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase)
                    ? Formatting.IsAnonymous(targetType) ? name : WithoutArity(targetType.Name)
                    : name;
                obj.Add(name, ValueNode.String(counter.NextId(scope, Convert.ToString(value, CultureInfo.InvariantCulture)!)));
                return;
            }
        }

        var node = Value(value, member.Type, ValueContext.Member, typeof(Exception).IsAssignableFrom(member.Type));
        if (node != null)
        {
            obj.Add(name, node);
        }
    }

    static string WithoutArity(string name) =>
        name.IndexOf('`') is var tick and > 0 ? name.Substring(0, tick) : name;

    static bool IsIdName(string name) =>
        name.EndsWith("Id", StringComparison.Ordinal) || name.EndsWith("ID", StringComparison.Ordinal);

    bool ShouldWrite(object? value, Type declaredType)
    {
        if (value == null)
        {
            return Settings.Nulls == NullValueHandling.Include;
        }

        if (Settings.IgnoreEmptyCollections && IsEmptyCollection(value))
        {
            return false;
        }

        switch (Settings.DefaultValues)
        {
            case DefaultValueHandling.Include:
                return true;
            case null when declaredType == typeof(bool):
                return true;
        }

        if (!declaredType.IsValueType || Nullable.GetUnderlyingType(declaredType) != null)
        {
            return true;
        }

        var defaultValue = defaults.GetOrAdd(declaredType, Activator.CreateInstance);
        return !Equals(value, defaultValue);
    }

    static bool IsEmptyCollection(object value)
    {
        switch (value)
        {
            case string:
                return false;
            case ICollection collection:
                return collection.Count == 0;
            case IEnumerable enumerable when !IsKeyValuePair(value.GetType()):
                var enumerator = enumerable.GetEnumerator();
                try
                {
                    return !enumerator.MoveNext();
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }

            default:
                return false;
        }
    }

    Node Exception(Exception exception, bool nested)
    {
        var obj = new ObjectNode();
        var type = exception.GetType();
        if (nested)
        {
            obj.Add("$type", ValueNode.String(Formatting.TypeName(type)));
        }

        obj.Add("Type", ValueNode.String(Formatting.TypeName(type)));

        var members = Members(type).ToDictionary(member => member.Name);
        var written = new HashSet<string>(StringComparer.Ordinal) { "Source", "HResult", "TargetSite", "StackTrace" };
        if (exception is AggregateException)
        {
            written.Add("InnerException");
        }

        void Write(string name)
        {
            if (written.Add(name) && members.TryGetValue(name, out var member))
            {
                WriteMember(obj, exception, member, type);
            }
        }

        Write("Message");
        // Members declared by derived types, most derived first, then the base Exception members.
        for (var current = type; current != null && current != typeof(Exception); current = current.BaseType)
        {
            foreach (var member in Members(type).Where(member => member.Member.DeclaringType == current))
            {
                Write(member.Name);
            }
        }

        Write("Data");
        Write("InnerException");
        Write("HelpLink");

        if (Rules.ForMember(members["StackTrace"].Member, typeof(string)) == MemberDecision.Include &&
            exception.StackTrace is { } stackTrace)
        {
            var cleaned = CleanStackTrace(stackTrace);
            if (cleaned.Length > 0)
            {
                obj.Add("StackTrace", ValueNode.String(cleaned));
            }
        }
        else if (Rules.ForMember(members["StackTrace"].Member, typeof(string)) == MemberDecision.Scrub)
        {
            obj.Add("StackTrace", ValueNode.Scrubbed);
        }

        return obj;
    }

    // ArgumentException appends the parameter name to its message; it is written as ParamName instead.
    static string ExceptionMessage(Exception exception, string message)
    {
        if (exception is not ArgumentException { ParamName: { } parameter })
        {
            return message;
        }

        foreach (var suffix in new[] { $" (Parameter '{parameter}')", $"\r\nParameter name: {parameter}", $"\nParameter name: {parameter}" })
        {
            var index = message.IndexOf(suffix, StringComparison.Ordinal);
            if (index >= 0)
            {
                return message.Substring(0, index) + message.Substring(index + suffix.Length);
            }
        }

        return message;
    }

    internal static string CleanStackTrace(string stackTrace)
    {
        var lines = new List<string>();
        foreach (var raw in Newlines.Normalize(stackTrace).Split('\n'))
        {
            var line = raw.Trim();
            // Cut "in C:\src\File.cs:line 10" first: the path must not decide which frames are kept.
            var close = line.IndexOf(')');
            if (close > 0)
            {
                line = line.Substring(0, close + 1);
            }

            if (line.Length == 0 ||
                line.StartsWith("--- End of stack trace", StringComparison.Ordinal) ||
                line.Contains("System.Runtime.CompilerServices.TaskAwaiter") ||
                line.Contains("System.Runtime.ExceptionServices.ExceptionDispatchInfo") ||
                line.StartsWith("at AlphaSoftware.Snapshotter.", StringComparison.Ordinal) ||
                (line.Contains(".MoveNext()") && line.Contains('<')))
            {
                continue;
            }

            lines.Add(line.Replace('+', '.'));
        }

        return string.Join("\n", lines);
    }

    Node Task(Task task)
    {
        var obj = new ObjectNode();
        obj.Add("Status", ValueNode.String(task.Status.ToString()));
        if (task.Status == TaskStatus.RanToCompletion)
        {
            var result = task.GetType().GetProperty("Result");
            if (result != null && task.GetType().IsGenericType && task.GetType().GetGenericArguments()[0].Name != "VoidTaskResult")
            {
                var value = result.GetValue(task);
                var node = value == null ? null : Value(value, result.PropertyType, ValueContext.Member);
                if (node != null)
                {
                    obj.Add("Result", node);
                }
            }
        }
        else if (task.Exception is { } exception)
        {
            obj.Add("Exception", Exception(exception, nested: true));
        }

        return obj;
    }

    Node Delegate(Delegate value)
    {
        var obj = new ObjectNode();
        obj.Add("Type", ValueNode.String(Formatting.TypeName(value.GetType())));
        if (value.Target != null)
        {
            obj.Add("Target", ValueNode.String(Formatting.TypeName(value.Target.GetType())));
        }

        obj.Add("Method", ValueNode.String(value.Method.ToString()!));
        return obj;
    }

    Node? Json(JsonElement element, ValueContext context)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new ObjectNode();
                foreach (var property in element.EnumerateObject())
                {
                    switch (Rules.ForName(property.Name))
                    {
                        case MemberDecision.Ignore:
                            continue;
                        case MemberDecision.Scrub:
                            obj.Add(property.Name, ValueNode.Scrubbed);
                            continue;
                    }

                    var node = Json(property.Value, ValueContext.Member);
                    if (node != null)
                    {
                        obj.Add(Key(property.Name), node);
                    }
                }

                return obj;
            case JsonValueKind.Array:
                var array = new ArrayNode();
                foreach (var item in element.EnumerateArray())
                {
                    var node = Json(item, ValueContext.Item);
                    if (node != null)
                    {
                        array.Items.Add(node);
                    }
                }

                return array.Items.Count == 0 && context == ValueContext.Member && Settings.IgnoreEmptyCollections ? null : array;
            case JsonValueKind.String:
                return String(element.GetString()!);
            case JsonValueKind.Number:
                return new ValueNode(element.GetRawText(), ValueKind.Literal);
            case JsonValueKind.True:
                return new ValueNode("true", ValueKind.Literal);
            case JsonValueKind.False:
                return new ValueNode("false", ValueKind.Literal);
            default:
                return ValueNode.Null;
        }
    }

    static bool IsKeyValuePair(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);

    static bool IsSorted(Type type)
    {
        if (type == typeof(SortedList))
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(SortedDictionary<,>) || definition == typeof(SortedList<,>) ||
               definition.FullName == "System.Collections.Immutable.ImmutableSortedDictionary`2";
    }

    static List<(object Key, object? Value)>? DictionaryEntries(object value)
    {
        if (value is IDictionary dictionary)
        {
            var list = new List<(object, object?)>();
            foreach (DictionaryEntry entry in dictionary)
            {
                list.Add((entry.Key, entry.Value));
            }

            return list;
        }

        var type = value.GetType();
        var isDictionary = type.GetInterfaces().Any(candidate =>
            candidate.IsGenericType &&
            (candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>) || candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
        if (!isDictionary || value is not IEnumerable items)
        {
            return null;
        }

        var entries = new List<(object, object?)>();
        foreach (var item in items)
        {
            var itemType = item!.GetType();
            entries.Add((itemType.GetProperty("Key")!.GetValue(item)!, itemType.GetProperty("Value")!.GetValue(item)));
        }

        return entries;
    }

    IReadOnlyList<SerializableMember> Members(Type type)
    {
        var members = membersCache.GetOrAdd(type, SerializableMember.For);
        IEnumerable<SerializableMember> result = members;
        if (!Settings.IncludeObsoletes)
        {
            result = result.Where(member => !member.Obsolete);
        }

        if (VerifierSettings.SortProperties)
        {
            result = result.OrderBy(member => member.Name, StringComparer.Ordinal);
        }

        return result.ToList();
    }

    sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

sealed class SerializableMember
{
    SerializableMember(MemberInfo member, Type type, Func<object, object?> get)
    {
        Member = member;
        Type = type;
        Get = get;
        Name = member.Name;
        Obsolete = member.IsDefined(typeof(ObsoleteAttribute), true);
    }

    public MemberInfo Member { get; }
    public Type Type { get; }
    public string Name { get; }
    public bool Obsolete { get; }
    public Func<object, object?> Get { get; }

    // Public instance fields, then public readable properties, most derived type first. Streams are never written.
    public static IReadOnlyList<SerializableMember> For(Type type)
    {
        var members = new List<SerializableMember>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (names.Add(field.Name) && !typeof(Stream).IsAssignableFrom(field.FieldType))
            {
                members.Add(new(field, field.FieldType, field.GetValue));
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 ||
                property.GetMethod is not { IsPublic: true } ||
                typeof(Stream).IsAssignableFrom(property.PropertyType) ||
                (property.Name == "EqualityContract" && property.PropertyType == typeof(Type)) ||
                !names.Add(property.Name))
            {
                continue;
            }

            members.Add(new(property, property.PropertyType, target => property.GetValue(target)));
        }

        return members;
    }
}
