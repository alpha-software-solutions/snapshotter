using System.Text;

namespace AlphaSoftware.Snapshotter;

/// <summary>
/// The document a serialized target becomes before it is written out as text.
/// </summary>
abstract class Node;

sealed class ObjectNode : Node
{
    public List<KeyValuePair<string, Node>> Members { get; } = [];

    public void Add(string name, Node value) =>
        Members.Add(new(name, value));
}

sealed class ArrayNode : Node
{
    public List<Node> Items { get; } = [];
}

enum ValueKind
{
    /// <summary>
    /// Text: bare in Verify's format, quoted in strict JSON.
    /// </summary>
    String,

    /// <summary>
    /// Numbers, booleans and null: never quoted.
    /// </summary>
    Literal,

    /// <summary>
    /// Always double quoted (NaN and infinities, as Json.NET writes them).
    /// </summary>
    Quoted
}

sealed class ValueNode(string text, ValueKind kind) : Node
{
    public static readonly ValueNode Null = new("null", ValueKind.Literal);
    public static readonly ValueNode Scrubbed = new("{Scrubbed}", ValueKind.String);

    public string Text { get; } = text;
    public ValueKind Kind { get; } = kind;

    public static ValueNode String(string text) => new(Newlines.Normalize(text), ValueKind.String);
}

/// <summary>
/// Writes nodes in Verify's format (two space indents, nothing quoted, multiline strings unindented) or as strict
/// JSON.
/// </summary>
static class NodeWriter
{
    public static string Write(Node node, bool strict)
    {
        var builder = new StringBuilder();
        Write(builder, node, 0, strict);
        return builder.ToString();
    }

    static void Write(StringBuilder builder, Node node, int indent, bool strict)
    {
        switch (node)
        {
            case ObjectNode { Members.Count: 0 }:
                builder.Append("{}");
                break;
            case ObjectNode obj:
                builder.Append('{');
                for (var index = 0; index < obj.Members.Count; index++)
                {
                    var member = obj.Members[index];
                    builder.Append('\n');
                    Indent(builder, indent + 1);
                    builder.Append(strict ? Quote(member.Key) : member.Key);
                    builder.Append(':');
                    if (!strict && member.Value is ValueNode { Kind: ValueKind.String } text && text.Text.IndexOf('\n') >= 0)
                    {
                        // Multiline values start on the next line, at column 0.
                        if (!text.Text.StartsWith("\n", StringComparison.Ordinal))
                        {
                            builder.Append('\n');
                        }

                        builder.Append(text.Text);
                    }
                    else
                    {
                        builder.Append(' ');
                        Write(builder, member.Value, indent + 1, strict);
                    }

                    if (index < obj.Members.Count - 1)
                    {
                        builder.Append(',');
                    }
                }

                builder.Append('\n');
                Indent(builder, indent);
                builder.Append('}');
                break;
            case ArrayNode { Items.Count: 0 }:
                builder.Append("[]");
                break;
            case ArrayNode array:
                builder.Append('[');
                for (var index = 0; index < array.Items.Count; index++)
                {
                    builder.Append('\n');
                    Indent(builder, indent + 1);
                    Write(builder, array.Items[index], indent + 1, strict);
                    if (index < array.Items.Count - 1)
                    {
                        builder.Append(',');
                    }
                }

                builder.Append('\n');
                Indent(builder, indent);
                builder.Append(']');
                break;
            case ValueNode { Kind: ValueKind.Quoted } quoted:
                builder.Append('"').Append(quoted.Text).Append('"');
                break;
            case ValueNode { Kind: ValueKind.Literal } literal:
                builder.Append(literal.Text);
                break;
            case ValueNode value:
                builder.Append(strict ? Quote(value.Text) : value.Text);
                break;
        }
    }

    static void Indent(StringBuilder builder, int level) =>
        builder.Append(' ', level * 2);

    internal static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                default:
                    if (character < ' ')
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}
