namespace AutoContext.Framework.Logging.Tests.Support;

using AutoContext.Framework.Logging;

/// <summary>
/// Builds <see cref="LoggingClient"/> instances with the test
/// project's default wiring. Centralises the constructor so changes
/// to the production signature don't ripple through every test.
/// </summary>
internal static class LoggingClientTestFactory
{
    public static LoggingClient Create(string pipeName, string clientName) =>
        new(pipeName, clientName);
}
