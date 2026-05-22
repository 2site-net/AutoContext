namespace AutoContext.Engine.Tests.Support;

/// <summary>
/// xUnit collection marker that serialises tests which mutate the
/// shared <see cref="Console.Out"/> / <see cref="Console.Error"/>
/// writers. <see cref="ProgramTests"/> redirects both writers around
/// every invocation; without this lock, any future test class that
/// joins the collection (or any opt-in intra-class parallelism)
/// would race on the process-wide console.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleRedirection
{
    /// <summary>
    /// Name passed to <see cref="CollectionAttribute"/> on member
    /// test classes.
    /// </summary>
    public const string Name = "ConsoleRedirection";
}
