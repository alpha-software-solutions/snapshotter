namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Whether members holding their type's default value are written.
/// </summary>
public enum DefaultValueHandling
{
    /// <summary>
    /// Write every member.
    /// </summary>
    Include,

    /// <summary>
    /// Leave out members holding their default value, <c>false</c> included.
    /// </summary>
    Ignore
}

/// <summary>
/// Whether members holding null are written.
/// </summary>
public enum NullValueHandling
{
    /// <summary>
    /// Write them as <c>null</c>.
    /// </summary>
    Include,

    /// <summary>
    /// Leave them out. The default.
    /// </summary>
    Ignore
}

/// <summary>
/// Serializer options changed with <c>AddExtraSettings</c>.
/// </summary>
/// <remarks>
/// By default members holding their default value are left out, except <c>bool</c> members, which are always
/// written. Assigning <see cref="DefaultValueHandling" /> (to anything) removes that exception.
/// </remarks>
public sealed class SerializerSettings
{
    /// <summary>
    /// Converters that write values of their type, tried before the built in handling.
    /// </summary>
    public IList<WriteOnlyJsonConverter> Converters { get; } = new List<WriteOnlyJsonConverter>();

    /// <summary>
    /// Whether default values are written. Unset, they are left out except for <c>bool</c> members.
    /// </summary>
    public DefaultValueHandling? DefaultValueHandling { get; set; }

    /// <summary>
    /// Whether null members are written. Defaults to <see cref="Snapshotter.NullValueHandling.Ignore" />.
    /// </summary>
    public NullValueHandling NullValueHandling { get; set; } = NullValueHandling.Ignore;
}

/// <summary>
/// Writes values of one type in a custom way. Add it with
/// <c>AddExtraSettings(_ =&gt; _.Converters.Add(new MyConverter()))</c>.
/// </summary>
public abstract class WriteOnlyJsonConverter
{
    /// <summary>
    /// Whether this converter writes values of <paramref name="type" />.
    /// </summary>
    public abstract bool CanConvert(Type type);

    /// <summary>
    /// Writes <paramref name="value" />.
    /// </summary>
    public abstract void Write(VerifyJsonWriter writer, object value);

    /// <summary>
    /// Runs a converter on its own and returns what it wrote, as strict JSON wrapped in braces. Useful for testing
    /// a converter.
    /// </summary>
    public static string Execute<TConverter>(object target, VerifySettings? settings = null)
        where TConverter : WriteOnlyJsonConverter, new() =>
        Execute(new TConverter(), target, settings);

    /// <inheritdoc cref="Execute{TConverter}(object, VerifySettings?)" />
    public static string Execute<TConverter>(TConverter converter, object target, VerifySettings? settings = null)
        where TConverter : WriteOnlyJsonConverter
    {
        settings = new(settings);
        settings.UseStrictJson();
        var effective = EffectiveSettings.Create(settings, null);
        var counter = new Counter(effective.Named, effective.DateCounting);
        var serializer = new Serializer(effective, counter);
        var writer = new VerifyJsonWriter(serializer, counter, settings.Context);
        writer.WriteStartObject();
        converter.Write(writer, target);
        writer.WriteEndObject();
        return NodeWriter.Write(writer.Result!, strict: true);
    }
}

/// <summary>
/// A converter for values of <typeparamref name="T" /> and types derived from it.
/// </summary>
public abstract class WriteOnlyJsonConverter<T> : WriteOnlyJsonConverter
{
    /// <inheritdoc />
    public override bool CanConvert(Type type) =>
        typeof(T).IsAssignableFrom(type);

    /// <inheritdoc />
    public sealed override void Write(VerifyJsonWriter writer, object value) =>
        Write(writer, (T)value);

    /// <summary>
    /// Writes <paramref name="value" />.
    /// </summary>
    public abstract void Write(VerifyJsonWriter writer, T value);
}

/// <summary>
/// Given to converters to write their value. Members written with <see cref="WriteMember" /> follow the same
/// ignore and scrub rules as any other member.
/// </summary>
public sealed class VerifyJsonWriter
{
    readonly Serializer serializer;
    readonly Stack<Node> containers = new();
    string? pendingName;

    internal VerifyJsonWriter(Serializer serializer, Counter counter, IReadOnlyDictionary<string, object> context)
    {
        this.serializer = serializer;
        Counter = counter;
        Context = context;
    }

    internal Node? Result { get; private set; }

    /// <summary>
    /// The counter of this verification, for numbered placeholders.
    /// </summary>
    public Counter Counter { get; }

    /// <summary>
    /// The verification's context values.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; }

    public void WriteStartObject() => Open(new ObjectNode());

    public void WriteEndObject() => Close();

    public void WriteStartArray() => Open(new ArrayNode());

    public void WriteEndArray() => Close();

    /// <summary>
    /// Names the next value.
    /// </summary>
    public void WritePropertyName(string name)
    {
        // A converter that only writes members gets an object around them.
        if (containers.Count == 0 && Result == null)
        {
            Open(new ObjectNode());
        }

        pendingName = name;
    }

    /// <summary>
    /// Writes a value as it is: no Guid or date scrubbing.
    /// </summary>
    public void WriteRawValue(string? value) =>
        Add(value == null ? ValueNode.Null : ValueNode.String(value));

    /// <summary>
    /// Writes a value, serialized and scrubbed like any other.
    /// </summary>
    public void WriteValue(object? value) => Serialize(value);

    /// <summary>
    /// Writes a value, serialized and scrubbed like any other.
    /// </summary>
    public void Serialize(object? value) =>
        Add(serializer.Value(value, value?.GetType() ?? typeof(object), ValueContext.Item) ?? ValueNode.Null);

    /// <summary>
    /// Writes a named member, unless it is null or ignored. A scrubbed member is written as <c>{Scrubbed}</c>.
    /// </summary>
    public void WriteMember(object target, object? value, string name)
    {
        switch (serializer.Rules.ForName(name))
        {
            case MemberDecision.Ignore:
                return;
            case MemberDecision.Scrub:
                WritePropertyName(name);
                Add(ValueNode.Scrubbed);
                return;
        }

        if (value == null && serializer.Settings.Nulls == NullValueHandling.Ignore)
        {
            return;
        }

        var node = serializer.Value(value, value?.GetType() ?? typeof(object), ValueContext.Member);
        if (node == null)
        {
            return;
        }

        WritePropertyName(name);
        Add(node);
    }

    /// <summary>
    /// Writes a named member unless it equals <paramref name="defaultIgnore" />.
    /// </summary>
    public void WriteMember<T>(object target, T value, string name, T defaultIgnore)
    {
        if (EqualityComparer<T>.Default.Equals(value, defaultIgnore))
        {
            return;
        }

        WriteMember(target, value, name);
    }

    void Open(Node node)
    {
        Add(node);
        containers.Push(node);
    }

    void Close()
    {
        if (containers.Count > 0)
        {
            containers.Pop();
        }
    }

    internal void Add(Node node)
    {
        if (containers.Count == 0)
        {
            Result ??= node;
            return;
        }

        switch (containers.Peek())
        {
            case ObjectNode obj:
                obj.Add(pendingName ?? throw new InvalidOperationException("Call WritePropertyName before writing a value inside an object."), node);
                pendingName = null;
                break;
            case ArrayNode array:
                array.Items.Add(node);
                break;
        }
    }
}
