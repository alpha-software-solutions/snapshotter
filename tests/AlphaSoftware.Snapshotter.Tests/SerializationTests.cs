public class SerializationTests
{
    public enum Status { Pending, Shipped }

    public record Line(string Product, int Quantity);

    public record Order(int Id, Status Status, List<Line> Lines, string? Note);

    public class Node
    {
        public string Name { get; set; } = "";
        public Node? Parent { get; set; }
        public List<Node> Children { get; } = [];
    }

    public class WithCodeMembers
    {
        public string Name { get; set; } = "kept";
        public Type Kind { get; set; } = typeof(Order);
        public Action OnChange { get; set; } = () => { };
    }

    [Fact]
    public Task Records() =>
        Verify(new Order(7, Status.Shipped, [new("apple", 2), new("pear", 1)], null));

    [Fact]
    public Task NullsLeftOut() =>
        Verify(new Order(7, Status.Pending, [], null))
            .IgnoreNullValues();

    [Fact]
    public Task Dictionary() =>
        Verify(new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 });

    [Fact]
    public Task Cycles()
    {
        var root = new Node { Name = "root" };
        var child = new Node { Name = "child", Parent = root };
        root.Children.Add(child);
        return Verify(root);
    }

    [Fact]
    public Task TypesAndDelegates() =>
        Verify(new WithCodeMembers());

    [Fact]
    public Task Null() =>
        Verify((object?) null);

    [Fact]
    public Task Json() =>
        VerifyJson("""
                   // comments and trailing commas are accepted
                   {"b": 2, "a": [1, 2,], "nested": {"c": true}}
                   """);
}
