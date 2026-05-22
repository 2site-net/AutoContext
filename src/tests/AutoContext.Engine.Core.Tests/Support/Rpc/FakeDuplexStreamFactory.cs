namespace AutoContext.Engine.Core.Tests.Support.Rpc;

using System.IO.Pipelines;

/// <summary>
/// Creates a pair of connected in-memory duplex streams for testing
/// the <c>RpcConnectionProcessor</c> end-to-end without binding a
/// real named pipe. Whatever one side writes the other side reads;
/// disposing one side surfaces as EOF on the other.
/// </summary>
internal static class FakeDuplexStreamFactory
{
    public static (Stream Client, Stream Server) Create()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        return (
            Client: new CompositeStream(
                serverToClient.Reader.AsStream(),
                clientToServer.Writer.AsStream()),
            Server: new CompositeStream(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream()));
    }

    private sealed class CompositeStream(Stream reader, Stream writer) : Stream
    {
        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => writer.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            writer.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) =>
            reader.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            reader.ReadAsync(buffer, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) =>
            writer.Write(buffer, offset, count);

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            writer.WriteAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writer.Dispose();
                reader.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
