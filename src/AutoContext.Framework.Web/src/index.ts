// AutoContext.Framework.Web — shared TypeScript infrastructure for VS
// Code and Node-based AutoContext components. Re-exports the public
// surface of the package so consumers can import directly from
// `autocontext-framework-web`.

export type { Logger } from './logging/logger.js';
export { NullLogger } from './logging/null-logger.js';
export { LengthPrefixedFrameCodec } from './transport/length-prefixed-frame-codec.js';
export { BoundPipeListener, PipeListener } from './transport/pipe-listener.js';
export { PipeKeepAliveClient } from './transport/pipe-keep-alive-client.js';
export { PipeStreamingClient } from './transport/pipe-streaming-client.js';
export type { PipeStreamingClientOptions } from './transport/pipe-streaming-client.js';
export { PipeTransport } from './transport/pipe-transport.js';
