using System.Text;
using System.Xml.Linq;

public class FileTests
{
    static string Input(string name) =>
        Path.Combine(AttributeReader.ProjectDirectory, "Inputs", name);

    [Fact]
    public Task Bytes() =>
        Verify(new byte[] { 0, 1, 2, 3, 255 });

    [Fact]
    public Task BytesWithExtension() =>
        Verify(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "png");

    [Fact]
    public Task BytesForATextExtension() =>
        Verify(Encoding.UTF8.GetBytes("text\r\nfrom bytes"), "txt");

    [Fact]
    public Task Stream()
    {
        var stream = new MemoryStream([1, 2, 3]);
        stream.Position = 2;
        // Read from the start, whatever the position.
        return Verify(stream);
    }

    [Fact]
    public Task StreamWithInfo() =>
        Verify(new MemoryStream([1, 2, 3]), "bin", info: new { Size = 3 });

    [Fact]
    public Task TextFile() =>
        VerifyFile(Input("notes.txt"));

    [Fact]
    public Task BinaryFile() =>
        VerifyFile(Input("data.bin"));

    [Fact]
    public Task FileWithExtension() =>
        VerifyFile(Input("notes.txt"), extension: "csv");

    [Fact]
    public Task Directory() =>
        VerifyDirectory(Path.Combine(AttributeReader.ProjectDirectory, "Inputs"));

    [Fact]
    public Task Json() =>
        VerifyJson("""
                   {
                     "name": "Ann",
                     "id": "ebced679-45d3-4653-8791-3d969c4a986c",
                     "created": "2020-01-02T03:04:05Z",
                     "password": "secret",
                     "tags": [ "a", "b" ],
                     "empty": [],
                     "nested": { "z": 1, "a": 2.50, "flag": false, "nothing": null },
                     // comments and trailing commas are allowed
                     "last": true,
                   }
                   """)
            .IgnoreMember("password");

    [Fact]
    public Task StrictJsonFromJson() =>
        VerifyJson("""{"name": "Ann", "multi": "a\nb"}""")
            .UseStrictJson();

    [Fact]
    public Task Xml() =>
        VerifyXml("""
                  <?xml version="1.0" encoding="utf-8"?>
                  <body><node att="ebced679-45d3-4653-8791-3d969c4a986c">text</node><secret>x</secret><empty /></body>
                  """)
            .ScrubMember("secret");

    [Fact]
    public Task XDocumentAsRoot() =>
        Verify(XDocument.Parse("<a><b>c</b></a>"));
}
