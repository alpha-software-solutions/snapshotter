using System.Collections.Specialized;

// The snapshot format: Verify's pseudo-JSON, with names and strings unquoted.
public class SerializationTests
{
    [Fact]
    public Task Strings() =>
        Verify(new
        {
            Plain = "abc",
            Empty = "",
            Space = " ",
            Lead = "  lead",
            Trail = "trail  ",
            Colon = "a: b",
            Comma = "a, b",
            Braces = "{x} [y]",
            Quotes = "say \"hi\" 'yo'",
            Backslash = @"c:\temp\x",
            Null = (string?)null,
            NullWord = "null",
            Number = "123"
        });

    [Fact]
    public Task MultilineStrings() =>
        Verify(new
        {
            Before = 1,
            Text = "line1\nline2\r\nline3",
            After = 2,
            StartsWithNewline = "\nfoo",
            EndsWithNewline = "bar\n",
            Nested = new { Inner = "x\ny" },
            Items = new[] { "single", "multi\nline" }
        });

    [Fact]
    public Task Numbers() =>
        Verify(new
        {
            Int = 1,
            Long = long.MaxValue,
            ULong = ulong.MaxValue,
            Decimal = 1.10m,
            WholeDecimal = 2m,
            Double = 1.1d,
            WholeDouble = 2d,
            Pi = Math.PI,
            Sum = 0.1 + 0.2,
            Large = 1e20,
            Small = 1e-7,
            NaN = double.NaN,
            Infinity = double.PositiveInfinity,
            Float = 1.1f,
            WholeFloat = 3f,
            Negative = -5
        });

    [Fact]
    public Task Scalars() =>
        Verify(new
        {
            True = true,
            False = false,
            Char = 'a',
            Enum = Color.Blue,
            UndefinedEnum = (Color)7,
            Flags = Access.Read | Access.Execute,
            TimeSpan = new TimeSpan(1, 2, 3, 4, 5),
            NegativeTimeSpan = TimeSpan.FromMinutes(-90),
            Uri = new Uri("https://example.com/a?b=c"),
            RelativeUri = new Uri("/rel/path", UriKind.Relative),
            Version = new Version(1, 2, 3),
            Type = typeof(Dictionary<string, int?>),
            ArrayType = typeof(string[]),
            NestedType = typeof(Environment.SpecialFolder),
            Bytes = new byte[] { 1, 2, 3, 255 }
        });

    [Fact]
    public Task DatesWhenNotScrubbed() =>
        Verify(new
        {
            Midnight = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Minutes = new DateTime(2000, 1, 1, 1, 1, 0, DateTimeKind.Utc),
            Seconds = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            Fraction = new DateTime(2000, 1, 1, 1, 1, 1, 999, DateTimeKind.Utc),
            Unspecified = new DateTime(2020, 10, 10, 1, 2, 3).AddTicks(1234567),
            Offset = new DateTimeOffset(2000, 1, 1, 0, 0, 0, new TimeSpan(1, 30, 0)),
            WholeHourOffset = new DateTimeOffset(2000, 1, 1, 1, 1, 1, TimeSpan.FromHours(-5)),
            ZeroOffset = new DateTimeOffset(2020, 10, 10, 0, 0, 0, TimeSpan.Zero)
        }).DontScrubDateTimes();

    [Fact]
    public Task DefaultsAreLeftOutButBoolsAreKept() =>
        Verify(new Defaults());

    [Fact]
    public Task DefaultsIncluded() =>
        Verify(new Defaults())
            .AddExtraSettings(_ =>
            {
                _.DefaultValueHandling = DefaultValueHandling.Include;
                _.NullValueHandling = NullValueHandling.Include;
            });

    [Fact]
    public Task Collections() =>
        Verify(new
        {
            List = new List<int> { 1, 2, 3 },
            Empty = new List<int>(),
            EmptyArray = Array.Empty<string>(),
            Nested = new[] { new[] { 1, 2 }, [], new[] { 3 } },
            Mixed = new object?[] { 1, "two", null, true, new { X = 1 } },
            Objects = new[] { new { Name = "a" }, new { Name = "b" } },
            EmptyObjects = new[] { new { } }
        });

    [Fact]
    public Task EmptyCollectionsKept() =>
        Verify(new { List = new List<int>(), Dictionary = new Dictionary<string, int>() })
            .DontIgnoreEmptyCollections();

    [Fact]
    public Task RootEmptyCollection() =>
        Verify(new List<string>());

    [Fact]
    public Task Dictionaries() =>
        Verify(new
        {
            StringKeys = new Dictionary<string, object?> { ["b"] = 2, ["10"] = 10, ["a"] = 1, ["B"] = 3, ["9"] = 9, ["_"] = 0, ["null"] = null },
            IntKeys = new Dictionary<int, string> { [10] = "ten", [-1] = "minus one", [9] = "nine" },
            EnumKeys = new Dictionary<Color, int> { [Color.Blue] = 2, [Color.Red] = 1 },
            Sorted = new SortedDictionary<string, int>(StringComparer.Ordinal) { ["b"] = 2, ["a"] = 1 },
            TypeKeys = new Dictionary<Type, int> { [typeof(int)] = 1, [typeof(List<string>)] = 2 },
            NameValues = new NameValueCollection { { "key", "value1" }, { "key", "value2" } }
        });

    [Fact]
    public Task DictionariesUnsorted() =>
        Verify(new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 })
            .DontSortDictionaries();

    [Fact]
    public Task KeyValuePairs() =>
        Verify(new
        {
            StringKey = new KeyValuePair<string, int>("k", 1),
            IntKey = new KeyValuePair<int, string>(3, "v"),
            List = new[] { new KeyValuePair<string, int>("a", 1), new KeyValuePair<string, int>("b", 2) }
        });

    [Fact]
    public Task RootKeyValuePair() =>
        Verify(new KeyValuePair<string, int>("theKey", 10));

    [Fact]
    public Task Tuples() =>
        Verify(new
        {
            Value = (1, "a"),
            Named = (Id: 1, Name: "x"),
            Reference = Tuple.Create(true, 2)
        });

    [Fact]
    public Task RecordsAndStructs() =>
        Verify(new
        {
            Record = new Person("Ann", 30),
            Struct = new Point { X = 1, Label = "l" },
            DefaultStruct = default(Point)
        });

    [Fact]
    public Task FieldsBeforePropertiesAndDerivedBeforeBase() =>
        Verify(new
        {
            Order = new PropertiesAndFields(),
            Inheritance = new Derived()
        });

    [Fact]
    public Task ReferenceLoops()
    {
        var root = new Node { Name = "root" };
        root.Self = root;
        root.Children = [root, new Node { Name = "kid" }];
        return Verify(root);
    }

    [Fact]
    public Task ObsoleteMembersAreLeftOut() =>
        Verify(new WithObsolete());

    [Fact]
    public Task ObsoleteMembersIncluded() =>
        Verify(new WithObsolete())
            .IncludeObsoletes();

    [Fact]
    public Task MembersThatThrowNotImplemented() =>
        Verify(new Throwing());

    [Fact]
    public async Task MembersThatThrowOtherwiseFail()
    {
        var exception = await Assert.ThrowsAsync<Exception>(async () => await Verify(new ThrowingInvalid()));
        Assert.Equal("Error getting value from 'Bad' on 'SerializationTests.ThrowingInvalid'.", exception.Message);
    }

    [Fact]
    public Task IgnoreMembersThatThrow() =>
        Verify(new ThrowingInvalid())
            .IgnoreMembersThatThrow<InvalidOperationException>();

    [Fact]
    public Task StrictJson() =>
        Verify(new
        {
            Name = "x",
            Multi = "a\r\nb",
            Number = 1.5,
            Quote = "q\"",
            Date = new DateTime(2020, 1, 1),
            Char = 'c',
            Flags = Access.Read | Access.Write,
            Dictionary = new Dictionary<string, int> { ["k"] = 1 },
            Bool = false
        }).UseStrictJson();

    [Fact]
    public Task Task() =>
        Verify(new { Done = System.Threading.Tasks.Task.FromResult(5) });

    public enum Color
    {
        Red,
        Blue
    }

    [Flags]
    public enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4
    }

    public class Defaults
    {
        public int Int { get; set; }
        public string? String { get; set; }
        public bool Bool { get; set; }
        public bool? NullableBool { get; set; } = false;
        public int? NullableZero { get; set; } = 0;
        public double Double { get; set; }
        public Color Enum { get; set; }
        public Guid Guid { get; set; }
        public Guid? NullableEmptyGuid { get; set; } = Guid.Empty;
        public DateTime DateTime { get; set; }
        public DateTime? NullableMinDate { get; set; } = System.DateTime.MinValue;
    }

    public record Person(string Name, int Age);

    public struct Point
    {
        public int X { get; set; }
        public string? Label { get; set; }
    }

    public class PropertiesAndFields
    {
        public string P1 { get; set; } = "p1";
        public string F1 = "f1";
        public string P2 { get; set; } = "p2";
        public string F2 = "f2";
    }

    public class Base
    {
        public string BaseProperty { get; set; } = "b";
    }

    public class Derived : Base
    {
        public string DerivedProperty { get; set; } = "d";
    }

    public class Node
    {
        public string? Name { get; set; }
        public Node? Self { get; set; }
        public List<Node>? Children { get; set; }
    }

    public class WithObsolete
    {
        public string Current { get; set; } = "current";

        [Obsolete("Use Current")]
        public string Old { get; set; } = "old";
    }

    public class Throwing
    {
        public string Fine => "fine";
        public string NotImplemented => throw new NotImplementedException();
        public string NotSupported => throw new NotSupportedException();
    }

    public class ThrowingInvalid
    {
        public string Fine => "fine";
        public string Bad => throw new InvalidOperationException("bad");
    }
}
