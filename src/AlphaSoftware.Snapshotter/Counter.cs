using System.Globalization;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// Hands out the numbered placeholders (<c>Guid_1</c>, <c>DateTime_2</c>, ...) of one verification. Every value
/// of a kind gets the next number the first time it is met, and the same number after that.
/// </summary>
/// <remarks>
/// One counter exists per <c>Verify</c> call and is shared by all of its targets. <see cref="Current" /> gives
/// the active one to converters and scrubbers.
/// </remarks>
public sealed class Counter
{
    static readonly AsyncLocal<Counter?> current = new();

    readonly object gate = new();
    readonly Dictionary<Guid, int> guids = new();
    readonly Dictionary<(long Ticks, DateTimeKind Kind), int> dateTimes = new();
    readonly Dictionary<(long Ticks, TimeSpan Offset), int> dateTimeOffsets = new();
    readonly Dictionary<int, int> dates = new();
    readonly Dictionary<long, int> times = new();
    readonly Dictionary<string, Dictionary<string, int>> ids = new(StringComparer.Ordinal);
    readonly NamedValues named;
    readonly bool dateCounting;

    internal Counter(NamedValues named, bool dateCounting)
    {
        this.named = named;
        this.dateCounting = dateCounting;
    }

    /// <summary>
    /// The counter of the verification running in this async context.
    /// </summary>
    public static Counter Current =>
        current.Value ?? throw new InvalidOperationException("No verification is running in this context.");

    internal static Counter? CurrentOrNull => current.Value;

    internal static IDisposable Start(Counter counter)
    {
        var previous = current.Value;
        current.Value = counter;
        return new Scope(() => current.Value = previous);
    }

    /// <summary>
    /// The placeholder for a Guid: its registered name, <c>Guid_Empty</c> or <c>Guid_N</c>.
    /// </summary>
    public string NextString(Guid value)
    {
        if (named.TryGet(value, out var name))
        {
            return name;
        }

        return value == Guid.Empty ? "Guid_Empty" : $"Guid_{Next(guids, value)}";
    }

    /// <summary>
    /// The placeholder for a DateTime: its registered name, <c>Date_MinValue</c>, <c>Date_MaxValue</c> or
    /// <c>DateTime_N</c>. Values with the same ticks but a different <see cref="DateTimeKind" /> are different.
    /// </summary>
    public string NextString(DateTime value)
    {
        if (named.TryGet(value, out var name))
        {
            return name;
        }

        if (value.Date == DateTime.MinValue.Date)
        {
            return "Date_MinValue";
        }

        if (value.Date == DateTime.MaxValue.Date)
        {
            return "Date_MaxValue";
        }

        return dateCounting ? $"DateTime_{Next(dateTimes, (value.Ticks, value.Kind))}" : "{Scrubbed}";
    }

    /// <summary>
    /// The placeholder for a DateTimeOffset: its registered name, <c>Date_MinValue</c>, <c>Date_MaxValue</c> or
    /// <c>DateTimeOffset_N</c>. The same instant with a different offset is a different value.
    /// </summary>
    public string NextString(DateTimeOffset value)
    {
        if (named.TryGet(value, out var name))
        {
            return name;
        }

        if (value.Date == DateTime.MinValue.Date)
        {
            return "Date_MinValue";
        }

        if (value.Date == DateTime.MaxValue.Date)
        {
            return "Date_MaxValue";
        }

        return dateCounting ? $"DateTimeOffset_{Next(dateTimeOffsets, (value.UtcTicks, value.Offset))}" : "{Scrubbed}";
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// The placeholder for a DateOnly: its registered name, <c>Date_MinValue</c>, <c>Date_MaxValue</c> or
    /// <c>Date_N</c>.
    /// </summary>
    public string NextString(DateOnly value)
    {
        if (named.TryGet(value, out var name))
        {
            return name;
        }

        if (value == DateOnly.MinValue)
        {
            return "Date_MinValue";
        }

        if (value == DateOnly.MaxValue)
        {
            return "Date_MaxValue";
        }

        return dateCounting ? $"Date_{Next(dates, value.DayNumber)}" : "{Scrubbed}";
    }

    /// <summary>
    /// The placeholder for a TimeOnly: its registered name, <c>Time_MinValue</c>, <c>Time_MaxValue</c> or
    /// <c>Time_N</c>.
    /// </summary>
    public string NextString(TimeOnly value)
    {
        if (named.TryGet(value, out var name))
        {
            return name;
        }

        if (value == TimeOnly.MinValue)
        {
            return "Time_MinValue";
        }

        if (value == TimeOnly.MaxValue)
        {
            return "Time_MaxValue";
        }

        return $"Time_{Next(times, value.Ticks)}";
    }
#endif

    /// <summary>
    /// Replaces <paramref name="value" /> when the whole of it is a Guid or a date, as member values are.
    /// </summary>
    public bool TryConvert(string value, out string result) =>
        TryConvert(value, scrubGuids: true, scrubDates: true, out result);

    internal bool TryConvert(string value, bool scrubGuids, bool scrubDates, out string result)
    {
        if (scrubGuids && value.Length >= 32 && Guid.TryParse(value, out var guid))
        {
            result = NextString(guid);
            return true;
        }

        if (scrubDates && TryParseDate(value, out result))
        {
            return true;
        }

        result = value;
        return false;
    }

    bool TryParseDate(string value, out string result)
    {
        var culture = CultureInfo.CurrentCulture;
        if (value.Length >= 19 && char.IsDigit(value[0]) && value[4] == '-' &&
            DateTimeOffset.TryParseExact(value, "yyyy-MM-ddTHH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            result = NextString(iso);
            return true;
        }

        foreach (var format in VerifierSettings.ExtraDateTimeOffsetFormats)
        {
            if (DateTimeOffset.TryParseExact(value, format, culture, DateTimeStyles.None, out var offset))
            {
                result = NextString(offset);
                return true;
            }
        }

        foreach (var format in VerifierSettings.ExtraDateTimeFormats)
        {
            if (DateTime.TryParseExact(value, format, culture, DateTimeStyles.None, out var dateTime))
            {
                result = NextString(dateTime);
                return true;
            }
        }

#if NET6_0_OR_GREATER
        foreach (var format in VerifierSettings.ExtraDateFormats.Prepend("d"))
        {
            if (DateOnly.TryParseExact(value, format, culture, DateTimeStyles.None, out var date))
            {
                result = NextString(date);
                return true;
            }
        }

        foreach (var format in VerifierSettings.ExtraTimeFormats.Prepend("h:mm tt"))
        {
            if (TimeOnly.TryParseExact(value, format, culture, DateTimeStyles.None, out var time))
            {
                result = NextString(time);
                return true;
            }
        }
#else
        if (DateTime.TryParseExact(value, "d", culture, DateTimeStyles.None, out var shortDate))
        {
            result = $"Date_{Next(dates, (int)(shortDate.Ticks / TimeSpan.TicksPerDay))}";
            return true;
        }
#endif

        result = value;
        return false;
    }

    internal string NextId(string scope, string value)
    {
        lock (gate)
        {
            if (!ids.TryGetValue(scope, out var values))
            {
                ids[scope] = values = new(StringComparer.Ordinal);
            }

            if (!values.TryGetValue(value, out var number))
            {
                values[value] = number = values.Count + 1;
            }

            return $"{scope}_{number}";
        }
    }

    int Next<T>(Dictionary<T, int> numbers, T value)
        where T : notnull
    {
        lock (gate)
        {
            if (!numbers.TryGetValue(value, out var number))
            {
                numbers[value] = number = numbers.Count + 1;
            }

            return number;
        }
    }

    sealed class Scope(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}

/// <summary>
/// Values registered with <c>AddNamedGuid</c>, <c>AddNamedDate</c> and so on, written as their name.
/// </summary>
sealed class NamedValues
{
    readonly Dictionary<object, string> values = new();

    public NamedValues? Fallback { get; set; }

    public void Add(object value, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A name is required.", nameof(name));
        }

        values[value] = name;
    }

    public bool Contains(object value) => values.ContainsKey(value);

    public bool TryGet(object value, out string name)
    {
        if (values.TryGetValue(value, out name!))
        {
            return true;
        }

        if (Fallback != null)
        {
            return Fallback.TryGet(value, out name);
        }

        name = "";
        return false;
    }

    public NamedValues Clone()
    {
        var clone = new NamedValues();
        foreach (var pair in values)
        {
            clone.values[pair.Key] = pair.Value;
        }

        return clone;
    }
}
