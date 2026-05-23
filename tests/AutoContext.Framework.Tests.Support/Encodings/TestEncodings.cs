namespace AutoContext.Framework.Tests.Support.Encodings;

using System.Text;

/// <summary>
/// Encoding instances shared across pipe / logging tests so the
/// "no BOM" wire format isn't re-declared in every test class.
/// </summary>
public static class TestEncodings
{
    /// <summary>UTF-8 without a leading byte-order mark — matches the wire format every framework client emits.</summary>
    public static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
}
