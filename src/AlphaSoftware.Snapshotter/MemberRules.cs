using System.Linq.Expressions;
using System.Reflection;

namespace AlphaSoftware.Snapshotter;

enum MemberDecision
{
    Include,
    Ignore,
    Scrub
}

/// <summary>
/// Which members, types and instances are ignored or scrubbed, at one level.
/// </summary>
sealed class MemberRules
{
    public HashSet<string> IgnoredNames { get; private set; } = new(StringComparer.Ordinal);
    public HashSet<string> ScrubbedNames { get; private set; } = new(StringComparer.Ordinal);
    public List<(Type Type, string Name)> IgnoredTypeMembers { get; private set; } = [];
    public List<(Type Type, string Name)> ScrubbedTypeMembers { get; private set; } = [];
    public List<Func<string, bool>> IgnoredNamePredicates { get; private set; } = [];
    public List<Func<string, bool>> ScrubbedNamePredicates { get; private set; } = [];
    public List<Func<MemberInfo, bool>> IgnoredMemberPredicates { get; private set; } = [];
    public List<Func<MemberInfo, bool>> ScrubbedMemberPredicates { get; private set; } = [];
    public List<Type> IgnoredTypes { get; private set; } = [];
    public List<Type> ScrubbedTypes { get; private set; } = [];
    public List<(Type Type, Func<object, bool> Match)> IgnoredInstances { get; private set; } = [];
    public List<(Type Type, Func<object, bool> Match)> ScrubbedInstances { get; private set; } = [];
    public List<Func<Exception, bool>> IgnoredThrows { get; private set; } = [];

    public MemberRules Clone() =>
        new()
        {
            IgnoredNames = new(IgnoredNames, StringComparer.Ordinal),
            ScrubbedNames = new(ScrubbedNames, StringComparer.Ordinal),
            IgnoredTypeMembers = [.. IgnoredTypeMembers],
            ScrubbedTypeMembers = [.. ScrubbedTypeMembers],
            IgnoredNamePredicates = [.. IgnoredNamePredicates],
            ScrubbedNamePredicates = [.. ScrubbedNamePredicates],
            IgnoredMemberPredicates = [.. IgnoredMemberPredicates],
            ScrubbedMemberPredicates = [.. ScrubbedMemberPredicates],
            IgnoredTypes = [.. IgnoredTypes],
            ScrubbedTypes = [.. ScrubbedTypes],
            IgnoredInstances = [.. IgnoredInstances],
            ScrubbedInstances = [.. ScrubbedInstances],
            IgnoredThrows = [.. IgnoredThrows]
        };

    internal static string MemberName(LambdaExpression expression)
    {
        var body = expression.Body;
        // Value type members are boxed to object, which adds a conversion around the member access.
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression { Member: PropertyInfo or FieldInfo } member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException($"Expected a property or field access, such as `_ => _.Name`, but got `{expression}`.", nameof(expression));
    }
}

/// <summary>
/// The member rules of a verification and the global ones, applied together.
/// </summary>
sealed class MemberRuleView(IReadOnlyList<MemberRules> levels)
{
    public MemberDecision ForMember(MemberInfo member, Type memberType)
    {
        // By member type, then by name on any type, then by declaring type and name, then by predicate.
        if (levels.Any(level => level.IgnoredTypes.Any(type => Matches(type, memberType))))
        {
            return MemberDecision.Ignore;
        }

        if (levels.Any(level => level.ScrubbedTypes.Any(type => Matches(type, memberType))))
        {
            return MemberDecision.Scrub;
        }

        var name = member.Name;
        if (levels.Any(level => level.IgnoredNames.Contains(name)))
        {
            return MemberDecision.Ignore;
        }

        if (levels.Any(level => level.ScrubbedNames.Contains(name)))
        {
            return MemberDecision.Scrub;
        }

        var declaring = member.ReflectedType ?? member.DeclaringType;
        if (declaring != null)
        {
            if (levels.Any(level => level.IgnoredTypeMembers.Any(rule => rule.Name == name && rule.Type.IsAssignableFrom(declaring))))
            {
                return MemberDecision.Ignore;
            }

            if (levels.Any(level => level.ScrubbedTypeMembers.Any(rule => rule.Name == name && rule.Type.IsAssignableFrom(declaring))))
            {
                return MemberDecision.Scrub;
            }
        }

        if (levels.Any(level => level.IgnoredNamePredicates.Any(match => match(name)) || level.IgnoredMemberPredicates.Any(match => match(member))))
        {
            return MemberDecision.Ignore;
        }

        if (levels.Any(level => level.ScrubbedNamePredicates.Any(match => match(name)) || level.ScrubbedMemberPredicates.Any(match => match(member))))
        {
            return MemberDecision.Scrub;
        }

        return MemberDecision.Include;
    }

    /// <summary>
    /// For dictionary keys and JSON property names: only the rules that work by name.
    /// </summary>
    public MemberDecision ForName(string name)
    {
        if (levels.Any(level => level.IgnoredNames.Contains(name) || level.IgnoredNamePredicates.Any(match => match(name))))
        {
            return MemberDecision.Ignore;
        }

        if (levels.Any(level => level.ScrubbedNames.Contains(name) || level.ScrubbedNamePredicates.Any(match => match(name))))
        {
            return MemberDecision.Scrub;
        }

        return MemberDecision.Include;
    }

    public MemberDecision ForInstance(object value)
    {
        var type = value.GetType();
        if (levels.Any(level => level.IgnoredInstances.Any(rule => rule.Type.IsAssignableFrom(type) && rule.Match(value))))
        {
            return MemberDecision.Ignore;
        }

        if (levels.Any(level => level.ScrubbedInstances.Any(rule => rule.Type.IsAssignableFrom(type) && rule.Match(value))))
        {
            return MemberDecision.Scrub;
        }

        return MemberDecision.Include;
    }

    public bool IgnoresThrow(Exception exception) =>
        exception is NotImplementedException or NotSupportedException ||
        levels.Any(level => level.IgnoredThrows.Any(match => match(exception)));

    static bool Matches(Type rule, Type memberType)
    {
        memberType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (rule.IsAssignableFrom(memberType))
        {
            return true;
        }

        if (!rule.IsGenericTypeDefinition)
        {
            return false;
        }

        for (var type = memberType; type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == rule)
            {
                return true;
            }
        }

        return memberType.GetInterfaces().Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == rule);
    }
}
