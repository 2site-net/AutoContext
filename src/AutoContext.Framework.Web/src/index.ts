// AutoContext.Framework.Web — shared TypeScript infrastructure for VS
// Code and Node-based AutoContext components. Re-exports the public
// surface of the package so consumers can import directly from
// `autocontext-framework-web`.

export type { ChannelLogger } from './logging/channel-logger.js';
export { ChannelLoggerBase } from './logging/channel-logger-base.js';
export { LogCategory } from './logging/log-category.js';
export { LogLevel } from './logging/log-level.js';
export type { Logger } from './logging/logger.js';
export { LoggerBase } from './logging/logger-base.js';
export type { LoggerFacade } from './logging/logger-facade.js';
export { NullLogger } from './logging/null-logger.js';
export { LengthPrefixedFrameCodec } from './transport/length-prefixed-frame-codec.js';
export { BoundPipeListener, PipeListener } from './transport/pipe-listener.js';
export { PipeKeepAliveClient } from './transport/pipe-keep-alive-client.js';
export { PipeStreamingClient } from './transport/pipe-streaming-client.js';
export type { PipeStreamingClientOptions } from './transport/pipe-streaming-client.js';
export { PipeTransport } from './transport/pipe-transport.js';
