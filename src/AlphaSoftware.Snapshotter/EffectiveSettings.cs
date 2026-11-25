using System.Reflection;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// A verification's settings combined with the global ones.
/// </summary>
sealed class EffectiveSettings
{
    EffectiveSettings(VerifySettings instance)
    {
        Instance = instance;
    }

    public VerifySettings Instance { get; }
    public bool Strict { get; private set; }
    public bool IgnoreEmptyCollections { get; private set; }
    public bool SortDictionaries { get; private set; }
    public bool IncludeObsoletes { get; private set; }
    public bool ScrubGuids { get; private set; }
    public bool ScrubDates { get; private set; }
    public bool ScrubNumericIds { get; private set; }
    public bool DateCounting { get; private set; }
    public bool ScrubbersDisabled { get; private set; }
    public bool RequireUniquePrefix { get; private set; }
    public DefaultValueHandling? DefaultValues { get; private set; }
    public NullValueHandling Nulls { get; private set; }
    public IReadOnlyList<WriteOnlyJsonConverter> Converters { get; private set; } = [];
    public MemberRuleView Rules { get; private set; } = null!;
    public NamedValues Named { get; private set; } = null!;
    public Uniqueness Unique { get; private set; }
    public HashSet<string>? IgnoredParameters { get; private set; }
    public IReadOnlyDictionary<string, object> Context => Instance.Context;

    public static EffectiveSettings Create(VerifySettings instance, Assembly? assembly)
    {
        var global = VerifierSettings.Global;
        var serializer = new SerializerSettings();
        foreach (var action in global.ExtraSettings.Concat(instance.ExtraSettings))
        {
            action(serializer);
        }

        var named = instance.Named.Clone();
        named.Fallback = global.Named;

        HashSet<string>? ignored = null;
        if (instance.IgnoredParameters != null || global.IgnoredParameters != null)
        {
            ignored = new(System.StringComparer.Ordinal);
            foreach (var name in (instance.IgnoredParameters ?? []).Concat(global.IgnoredParameters ?? []))
            {
                ignored.Add(name);
            }
        }

        var disabled = instance.ScrubbersDisabled;
        return new(instance)
        {
            Strict = instance.StrictJson ?? global.StrictJson ?? false,
            IgnoreEmptyCollections = instance.IgnoreEmptyCollections ?? global.IgnoreEmptyCollections ?? true,
            SortDictionaries = instance.SortDictionaries ?? global.SortDictionaries ?? true,
            IncludeObsoletes = instance.IncludeObsoletesValue ?? global.IncludeObsoletesValue ?? false,
            ScrubGuids = !disabled && (instance.ScrubGuidsValue ?? global.ScrubGuidsValue ?? true),
            ScrubDates = !disabled && (instance.ScrubDateTimesValue ?? global.ScrubDateTimesValue ?? true),
            ScrubNumericIds = !disabled && (instance.ScrubNumericIdsValue ?? global.ScrubNumericIdsValue ?? false),
            DateCounting = instance.DateCounting ?? global.DateCounting ?? true,
            ScrubbersDisabled = disabled,
            RequireUniquePrefix = instance.RequireUniquePrefix ?? global.RequireUniquePrefix ?? true,
            DefaultValues = serializer.DefaultValueHandling,
            Nulls = serializer.NullValueHandling,
            Converters = serializer.Converters.ToList(),
            Rules = new([instance.Members, global.Members]),
            Named = named,
            Unique = instance.Unique | global.Unique,
            IgnoredParameters = ignored
        };
    }

    /// <summary>
    /// The scrubber levels for a target with this extension, in the order they run when tied.
    /// </summary>
    public IReadOnlyList<ScrubberSet> ScrubberLevels(string extension)
    {
        var global = VerifierSettings.Global;
        var levels = new List<ScrubberSet> { Instance.Scrubbers };
        if (Instance.ExtensionScrubbers.TryGetValue(extension, out var instanceExtension))
        {
            levels.Add(instanceExtension);
        }

        if (global.ExtensionScrubbers.TryGetValue(extension, out var globalExtension))
        {
            levels.Add(globalExtension);
        }

        levels.Add(global.Scrubbers);
        return levels;
    }

    public IEnumerable<FirstVerify> FirstVerifyHandlers => Instance.FirstVerifyHandlers.Concat(VerifierSettings.Global.FirstVerifyHandlers);
    public IEnumerable<VerifyMismatch> MismatchHandlers => Instance.MismatchHandlers.Concat(VerifierSettings.Global.MismatchHandlers);
    public IEnumerable<VerifyDelete> DeleteHandlers => Instance.DeleteHandlers.Concat(VerifierSettings.Global.DeleteHandlers);
    public IEnumerable<(Action Before, Action After)> VerifyHandlers => Instance.VerifyHandlers.Concat(VerifierSettings.Global.VerifyHandlers);

    public StringCompare? StringComparer(string extension)
    {
        foreach (var (candidate, compare) in Instance.StringComparers)
        {
            if (candidate != null && string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
            {
                return compare;
            }
        }

        foreach (var (candidate, compare) in Instance.StringComparers)
        {
            if (candidate == null)
            {
                return compare;
            }
        }

        return VerifierSettings.StringComparers.TryGetValue(extension, out var global) ? global : VerifierSettings.DefaultStringComparer;
    }

    public StreamCompare? StreamComparer(string extension)
    {
        foreach (var (candidate, compare) in Instance.StreamComparers)
        {
            if (candidate != null && string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
            {
                return compare;
            }
        }

        foreach (var (candidate, compare) in Instance.StreamComparers)
        {
            if (candidate == null)
            {
                return compare;
            }
        }

        return VerifierSettings.StreamComparers.TryGetValue(extension, out var global) ? global : null;
    }
}
