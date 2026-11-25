using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// How values are written when they are not scrubbed.
/// </summary>
static class Formatting
{
    static readonly CultureInfo invariant = CultureInfo.InvariantCulture;

    // The shortest of four precisions that loses nothing: date, minutes, seconds, fractions.
    public static string Date(DateTime value, string separator = " ", string timeSeparator = ":")
    {
        var time = value.TimeOfDay;
        string text;
        if (time == TimeSpan.Zero)
        {
            text = value.ToString("yyyy-MM-dd", invariant);
        }
        else if (time.Ticks % TimeSpan.TicksPerMinute == 0)
        {
            text = value.ToString($"yyyy-MM-dd'{separator}'HH'{timeSeparator}'mm", invariant);
        }
        else if (time.Ticks % TimeSpan.TicksPerSecond == 0)
        {
            text = value.ToString($"yyyy-MM-dd'{separator}'HH'{timeSeparator}'mm'{timeSeparator}'ss", invariant);
        }
        else
        {
            text = value.ToString($"yyyy-MM-dd'{separator}'HH'{timeSeparator}'mm'{timeSeparator}'ss.FFFFFFF", invariant);
        }

        return text;
    }

    public static string DateTime(DateTime value) =>
        Date(value) + value.Kind switch
        {
            DateTimeKind.Utc => " Utc",
            DateTimeKind.Local => " Local",
            _ => ""
        };

    public static string DateTimeOffset(DateTimeOffset value) =>
        Date(value.DateTime) + " " + Offset(value.Offset);

    public static string Offset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        return absolute.Minutes == 0
            ? $"{sign}{absolute.Hours}"
            : $"{sign}{absolute.Hours}-{absolute.Minutes:00}";
    }

#if NET6_0_OR_GREATER
    public static string Date(DateOnly value) =>
        value.ToString("yyyy-MM-dd", invariant);

    public static string Time(TimeOnly value) =>
        value.ToString("h:mm tt", invariant);
#endif

    public static string Double(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(invariant);
        }

        return WithFraction(Shortest(value));
    }

    // The shortest text that reads back as the same value. .NET Core's "R" already is; .NET Framework's is not
    // (it gives 3.1415926535897931 for Math.PI).
    static string Shortest(double value)
    {
        for (var digits = 15; digits < 17; digits++)
        {
            var text = value.ToString("G" + digits, invariant);
            if (double.Parse(text, invariant) == value)
            {
                return text;
            }
        }

        return value.ToString("R", invariant);
    }

    static string Shortest(float value)
    {
        for (var digits = 6; digits < 9; digits++)
        {
            var text = value.ToString("G" + digits, invariant);
            if (float.Parse(text, invariant) == value)
            {
                return text;
            }
        }

        return value.ToString("R", invariant);
    }

    public static string Float(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return value.ToString(invariant);
        }

        return WithFraction(Shortest(value));
    }

    public static string Decimal(decimal value) =>
        WithFraction(value.ToString(invariant));

    static string WithFraction(string number) =>
        number.IndexOfAny(['.', 'E', 'e']) >= 0 ? number : number + ".0";

    public static bool IsNonFinite(object value) =>
        value is double d && (double.IsNaN(d) || double.IsInfinity(d)) ||
        value is float f && (float.IsNaN(f) || float.IsInfinity(f));

    public static string? Number(object value) =>
        value switch
        {
            double d => Double(d),
            float f => Float(f),
            decimal m => Decimal(m),
#if NET6_0_OR_GREATER
            Half h => WithFraction(h.ToString(invariant)),
#endif
            int or long or short or byte or sbyte or uint or ulong or ushort or System.Numerics.BigInteger =>
                ((IFormattable)value).ToString(null, invariant),
            _ => null
        };

    public static bool IsNumeric(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
               type == typeof(sbyte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) ||
               type == typeof(decimal) || type == typeof(double) || type == typeof(float);
    }

    /// <summary>
    /// A C#-style type name without namespaces: <c>List&lt;int&gt;</c>, <c>Dictionary&lt;string, int?&gt;</c>,
    /// <c>Outer.Inner</c>.
    /// </summary>
    public static string TypeName(Type type)
    {
        if (IsAnonymous(type))
        {
            return "dynamic";
        }

        if (type.IsArray)
        {
            return TypeName(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return TypeName(underlying) + "?";
        }

        var keyword = Keyword(type);
        if (keyword != null)
        {
            return keyword;
        }

        var builder = new StringBuilder();
        if (type.IsNested && !type.IsGenericParameter)
        {
            builder.Append(TypeName(type.DeclaringType!)).Append('.');
        }

        var name = type.Name;
        var tick = name.IndexOf('`');
        builder.Append(tick > 0 ? name.Substring(0, tick) : name);

        if (type.IsGenericType)
        {
            var arguments = type.GetGenericArguments();
            if (type.IsNested && type.DeclaringType!.IsGenericType)
            {
                arguments = arguments.Skip(type.DeclaringType.GetGenericArguments().Length).ToArray();
            }

            if (arguments.Length > 0)
            {
                builder.Append('<');
                builder.Append(string.Join(", ", arguments.Select(argument => argument.IsGenericParameter ? argument.Name : TypeName(argument))));
                builder.Append('>');
            }
        }

        return builder.ToString();
    }

    public static bool IsAnonymous(Type type) =>
        type.Name.Contains("AnonymousType") &&
        type.IsDefined(typeof(CompilerGeneratedAttribute), false);

    static string? Keyword(Type type)
    {
        if (type == typeof(bool)) return "bool";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(sbyte)) return "sbyte";
        if (type == typeof(char)) return "char";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(int)) return "int";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(long)) return "long";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(short)) return "short";
        if (type == typeof(ushort)) return "ushort";
        if (type == typeof(object)) return "object";
        if (type == typeof(string)) return "string";
        if (type == typeof(void)) return "void";
        return null;
    }

    public static string Member(MemberInfo member) =>
        member switch
        {
            MethodBase method => $"{TypeName(method.DeclaringType!)}.{method.Name}({string.Join(", ", method.GetParameters().Select(parameter => $"{TypeName(parameter.ParameterType)} {parameter.Name}"))})",
            _ => $"{TypeName(member.DeclaringType!)}.{member.Name}"
        };
}
