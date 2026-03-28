namespace SharpPilot.Mcp.Tests.Tools.DotNet;

using SharpPilot.Mcp.Tools.Checkers.DotNet;

public sealed class CSharpAsyncPatternCheckerTests
{
    [Fact]
    public void Should_pass_correct_async_code()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadAsync(CancellationToken ct = default)
                {
                    await Task.Delay(100, ct).ConfigureAwait(false);
                }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.StartsWith("✅", result);
    }

    [Fact]
    public void Should_reject_async_void()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async void LoadData() { }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("async void", result);
            Assert.Contains("LoadData", result);
        });
    }

    [Fact]
    public void Should_pass_event_handler_async_void()
    {
        // Arrange
        var source = """
            public class MyForm
            {
                public async void OnButtonClicked(object sender, EventArgs e)
                {
                    await Task.Delay(0).ConfigureAwait(false);
                }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.DoesNotContain("async void", result);
    }

    [Fact]
    public void Should_reject_public_async_without_cancellation_token()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadAsync() { }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("CancellationToken", result);
            Assert.Contains("LoadAsync", result);
        });
    }

    [Fact]
    public void Should_pass_public_async_with_cancellation_token()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadAsync(CancellationToken ct = default) { }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.DoesNotContain("CancellationToken", result);
    }

    [Fact]
    public void Should_pass_public_async_with_fully_qualified_cancellation_token()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadAsync(System.Threading.CancellationToken ct = default) { }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.DoesNotContain("CancellationToken", result);
    }

    [Fact]
    public void Should_skip_override_for_cancellation_token_check()
    {
        // Arrange
        var source = """
            public abstract class Base
            {
                public abstract Task LoadAsync();
            }

            public class Derived : Base
            {
                public override async Task LoadAsync() { }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.DoesNotContain("CancellationToken", result);
    }

    [Fact]
    public void Should_skip_async_void_for_cancellation_token_check()
    {
        // Arrange
        var source = """
            public class MyForm
            {
                public async void OnButtonClicked(object sender, EventArgs e)
                {
                    await Task.Delay(0).ConfigureAwait(false);
                }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.DoesNotContain("CancellationToken", result);
    }

    [Fact]
    public void Should_skip_private_async_for_cancellation_token_check()
    {
        // Arrange
        var source = """
            public class MyService
            {
                private async Task InternalLoadAsync() { }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.DoesNotContain("CancellationToken", result);
    }

    [Fact]
    public void Should_reject_await_without_configure_await()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadAsync(CancellationToken ct = default)
                {
                    await Task.Delay(100);
                }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("ConfigureAwait(false)", result);
        });
    }

    [Fact]
    public void Should_pass_await_with_configure_await_false()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadAsync(CancellationToken ct = default)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.DoesNotContain("ConfigureAwait", result);
    }

    [Fact]
    public void Should_reject_await_with_configure_await_true()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadAsync(CancellationToken ct = default)
                {
                    await Task.Delay(100).ConfigureAwait(true);
                }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("ConfigureAwait(false)", result);
        });
    }

    [Fact]
    public void Should_skip_configure_await_check_in_test_class()
    {
        // Arrange
        var source = """
            public class MyServiceTests
            {
                [Fact]
                public async Task Should_load_data()
                {
                    await Task.Delay(0);
                }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.DoesNotContain("ConfigureAwait", result);
    }

    [Fact]
    public void Should_flag_multiple_configure_await_violations()
    {
        // Arrange
        var source = """
            public class MyService
            {
                public async Task LoadAsync(CancellationToken ct = default)
                {
                    await Task.Delay(100);
                    await Task.Delay(200);
                }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.StartsWith("❌", result);
            Assert.Contains("2 async pattern violation(s)", result);
        });
    }

    [Fact]
    public void Should_pass_on_named_async_void_as_event_handler()
    {
        // Arrange — OnXxx naming convention covers Blazor lifecycle, WPF overrides, etc.
        var source = """
            public class MyComponent
            {
                protected async void OnInitialized()
                {
                    await Task.Delay(0).ConfigureAwait(false);
                }

                protected async void OnAfterRender(bool firstRender)
                {
                    await Task.Delay(0).ConfigureAwait(false);
                }
            }
            """;

        // Act
        var result = new CSharpAsyncPatternChecker().Check(source);

        // Assert
        Assert.DoesNotContain("async void", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_throw_on_empty_or_whitespace_input(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new CSharpAsyncPatternChecker().Check(input));
    }

    [Fact]
    public void Should_throw_on_null_input()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CSharpAsyncPatternChecker().Check(null!));
    }
}
