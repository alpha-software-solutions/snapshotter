// Snapshot names: {Type}.{Method}{_Parameters}{.Uniqueness}{#Target}.verified.{ext}, found without a test
// framework.
public class NamingTests
{
    [Fact]
    public Task Simple() => Verify("simple");

    [Fact]
    public async Task AsyncMethod()
    {
        await Task.Yield();
        await Verify("after an await");
    }

    [Fact]
    public Task Lambda()
    {
        Func<Task> run = async () => await Verify("in a lambda");
        return run();
    }

    [Fact]
    public Task LocalFunction()
    {
        return Local();

        static Task Local() => Verify("in a local function");
    }

    [Fact]
    public Task UseMethodName() =>
        Verify("custom method").UseMethodName("CustomMethodName");

    [Fact]
    public Task UseTypeName() =>
        Verify("custom type").UseTypeName("CustomTypeName");

    [Fact]
    public Task UseFileName() =>
        Verify("custom file").UseFileName("CustomFileName");

    [Fact]
    public Task UseDirectory() =>
        Verify("in a directory").UseDirectory("Snapshots");

    [Fact]
    public Task UseUniqueDirectory() =>
        Verify("in a unique directory")
            .UseUniqueDirectory()
            .AppendContentAsFile("second", name: "named");

    [Fact]
    public Task MultipleCallsWithMethodNames()
    {
        return Run();

        async Task Run()
        {
            await Verify("first").UseMethodName("MultipleCalls_1");
            await Verify("second").UseMethodName("MultipleCalls_2");
        }
    }

    [Fact]
    public Task UniqueForRuntime() =>
        Verify("one per runtime").UniqueForRuntime();

    [Fact]
    public Task UniqueForRuntimeAndVersion() =>
        Verify("one per runtime and version").UniqueForRuntimeAndVersion();

    [Fact]
    public Task UniqueForTargetFrameworkAndVersion() =>
        Verify("one per target framework")
            .UniqueForTargetFrameworkAndVersion();

    [Fact]
    public Task MultipleTargets() =>
        Verify(new[] { new Target("txt", "first"), new Target("txt", "second"), new Target("md", "# third") });

    [Fact]
    public Task NamedTargets() =>
        Verify(new[] { new Target("txt", "a", "theNameA"), new Target("txt", "b", "theNameB") });

    [Fact]
    public Task AppendedFiles() =>
        Verify("primary")
            .AppendContentAsFile("extra content")
            .AppendFile(Path.Combine(AttributeReader.ProjectDirectory, "Inputs", "data.bin"));

    [Fact]
    public async Task SecondVerifyWithTheSameNameThrows()
    {
        await Verify("once").UseMethodName("Twice");
        var exception = await Assert.ThrowsAsync<Exception>(async () => await Verify("again").UseMethodName("Twice"));
        Assert.StartsWith("The prefix has already been used:", exception.Message);
    }

    [Fact]
    public void InvalidFileName()
    {
        var exception = Assert.Throws<ArgumentException>(() => new VerifySettings().UseFileName("a#b"));
        Assert.Equal("Invalid character for file name. Value: a#b. Char:#", exception.Message);
    }

    [Fact]
    public void UseFileNameWithMethodNameThrows()
    {
        var settings = new VerifySettings();
        settings.UseMethodName("Method");
        var exception = Assert.Throws<Exception>(() => settings.UseFileName("File"));
        Assert.Equal("UseFileName is not compatible with UseMethodName or UseTypeName.", exception.Message);
    }

    public class Nested
    {
        [Fact]
        public Task NestedClass() => Verify("named Outer.Inner");
    }
}

public class GenericTests<T>
{
    public Task Run() => Verify(typeof(T));
}

public class GenericClassTests
{
    [Fact]
    public Task GenericClass() => new GenericTests<int>().Run();
}
