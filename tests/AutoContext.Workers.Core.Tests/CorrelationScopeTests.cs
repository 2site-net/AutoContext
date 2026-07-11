namespace AutoContext.Workers.Core.Tests;

using AutoContext.Workers.Core;

public sealed class CorrelationScopeTests
{
    [Fact]
    public void Should_expose_no_correlation_id_by_default()
    {
        // Assert
        Assert.Null(CorrelationScope.Current);
    }

    [Fact]
    public void Should_expose_the_pushed_id_as_current()
    {
        // Act
        using (CorrelationScope.Push("abc123"))
        {
            // Assert
            Assert.Equal("abc123", CorrelationScope.Current);
        }
    }

    [Fact]
    public void Should_restore_the_previous_id_when_the_scope_is_disposed()
    {
        // Arrange
        Assert.Null(CorrelationScope.Current);

        // Act
        using (CorrelationScope.Push("outer"))
        {
            Assert.Equal("outer", CorrelationScope.Current);
        }

        // Assert
        Assert.Null(CorrelationScope.Current);
    }

    [Fact]
    public void Should_restore_the_outer_id_when_a_nested_scope_is_disposed()
    {
        // Act + Assert
        using (CorrelationScope.Push("outer"))
        {
            using (CorrelationScope.Push("inner"))
            {
                Assert.Equal("inner", CorrelationScope.Current);
            }

            Assert.Equal("outer", CorrelationScope.Current);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Should_reject_a_null_or_empty_correlation_id(string? correlationId)
    {
        // Act + Assert
        Assert.ThrowsAny<ArgumentException>(() => CorrelationScope.Push(correlationId!));
    }
}
