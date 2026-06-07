namespace AutoContext.Instructions.Parser;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The outcome of a non-throwing asynchronous parse of an instructions file. The
/// asynchronous try-pattern cannot use an <see langword="out"/> parameter, so this
/// type carries what <see cref="InstructionsFile.TryParse"/> exposes through its
/// <see langword="out"/> result instead: a <see cref="Success"/> flag, the parsed
/// <see cref="Value"/> when the read succeeded, and an <see cref="ErrorMessage"/>
/// describing why it did not.
/// </summary>
public sealed record InstructionsFileTryResult
{
    private InstructionsFileTryResult(bool success, InstructionsFileParsedContent? value, string errorMessage)
    {
        (Success, Value, ErrorMessage) = (success, value, errorMessage);
    }

    /// <summary>
    /// The reason the read failed, or <see cref="string.Empty"/> when it succeeded.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// <see langword="true"/> when the file was read and parsed; otherwise
    /// <see langword="false"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool Success { get; }

    /// <summary>
    /// The complete structural parse when <see cref="Success"/> is
    /// <see langword="true"/>; otherwise <see langword="null"/>.
    /// </summary>
    public InstructionsFileParsedContent? Value { get; }

    /// <summary>
    /// Creates a failed result carrying <paramref name="errorMessage"/>.
    /// </summary>
    /// <param name="errorMessage">A non-empty description of why the read failed.</param>
    /// <returns>A failed result.</returns>
    /// <exception cref="ArgumentException"><paramref name="errorMessage"/> is
    /// <see langword="null"/> or empty.</exception>
    public static InstructionsFileTryResult Fail(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrEmpty(errorMessage);

        return new InstructionsFileTryResult(false, null, errorMessage);
    }

    /// <summary>
    /// Creates a successful result carrying <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The complete structural parse.</param>
    /// <returns>A successful result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is
    /// <see langword="null"/>.</exception>
    public static InstructionsFileTryResult Ok(InstructionsFileParsedContent value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new InstructionsFileTryResult(true, value, string.Empty);
    }
}
