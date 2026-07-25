namespace AutoContext.Engine.Protocol.Tests;

public sealed class WorkspaceHashTests
{
    [Fact]
    public void Compute_should_return_sixteen_uppercase_hex_characters()
    {
        // Arrange
        var path = OperatingSystem.IsWindows() ? @"C:\workspace" : "/workspace";

        // Act
        var hash = WorkspaceHash.Compute(path);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(Endpoint.WorkspaceHashLength, hash.Value.Length),
            () => Assert.All(hash.Value, c => Assert.True(
                c is (>= '0' and <= '9') or (>= 'A' and <= 'F'),
                $"Unexpected character '{c}' in hash '{hash.Value}'.")));
    }

    [Fact]
    public void Compute_should_be_deterministic_for_the_same_input()
    {
        // Arrange
        var path = OperatingSystem.IsWindows() ? @"C:\workspace" : "/workspace";

        // Act
        var first = WorkspaceHash.Compute(path);
        var second = WorkspaceHash.Compute(path);

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_should_treat_trailing_separator_as_equivalent_to_no_separator()
    {
        // Arrange
        var bare = OperatingSystem.IsWindows() ? @"C:\workspace" : "/workspace";
        var trailing = bare + Path.DirectorySeparatorChar;

        // Act + Assert
        Assert.Equal(
            WorkspaceHash.Compute(bare),
            WorkspaceHash.Compute(trailing));
    }

    [Fact]
    public void Compute_should_produce_different_hashes_for_different_paths()
    {
        // Arrange
        var first = OperatingSystem.IsWindows() ? @"C:\workspace-a" : "/workspace-a";
        var second = OperatingSystem.IsWindows() ? @"C:\workspace-b" : "/workspace-b";

        // Act + Assert
        Assert.NotEqual(
            WorkspaceHash.Compute(first),
            WorkspaceHash.Compute(second));
    }

    [Fact]
    public void Compute_should_be_case_insensitive_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange + Act + Assert
        Assert.Equal(
            WorkspaceHash.Compute(@"c:\workspace"),
            WorkspaceHash.Compute(@"C:\WORKSPACE"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compute_should_reject_null_empty_or_whitespace(string? path)
    {
        Assert.ThrowsAny<ArgumentException>(() => WorkspaceHash.Compute(path!));
    }

    [Fact]
    public void Default_instance_is_empty()
    {
        // Arrange
        WorkspaceHash hash = default;

        // Act + Assert
        Assert.Multiple(
            () => Assert.True(hash.IsEmpty),
            () => Assert.Equal(string.Empty, hash.Value),
            () => Assert.Equal(string.Empty, hash.ToString()));
    }

    [Fact]
    public void ToString_returns_Value()
    {
        // Arrange
        var hash = WorkspaceHash.Parse("0123456789ABCDEF", provider: null);

        // Act + Assert
        Assert.Equal("0123456789ABCDEF", hash.ToString());
    }

    [Fact]
    public void Parse_round_trips_a_valid_hash()
    {
        // Arrange
        const string raw = "0123456789ABCDEF";

        // Act
        var parsed = WorkspaceHash.Parse(raw, provider: null);

        // Assert
        Assert.Equal(raw, parsed.Value);
    }

    [Theory]
    [InlineData("0123456789abcdef")]      // lowercase rejected
    [InlineData("0123456789ABCDE")]        // too short
    [InlineData("0123456789ABCDEF0")]      // too long
    [InlineData("0123456789ABCDEG")]       // non-hex
    [InlineData("")]
    public void Parse_should_throw_FormatException_for_invalid_input(string raw)
    {
        Assert.Throws<FormatException>(() => WorkspaceHash.Parse(raw, provider: null));
    }

    [Fact]
    public void Parse_should_throw_ArgumentNullException_for_null()
    {
        Assert.Throws<ArgumentNullException>(() => WorkspaceHash.Parse(null!, provider: null));
    }
}
