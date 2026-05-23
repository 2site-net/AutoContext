namespace AutoContext.Framework.Tests.Support.Pipes;

using System.Buffers.Binary;

/// <summary>
/// Builds the 4-byte little-endian length header used by
/// <c>LengthPrefixedFrameCodec</c>. Lets framing tests construct
/// malformed payloads (oversized, negative, etc.) without going
/// through the production codec.
/// </summary>
public static class FrameHeaderTestEncoder
{
    public static byte[] Encode(int length)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        return header;
    }
}
