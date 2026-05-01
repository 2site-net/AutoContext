// AutoContext.Framework.Web — shared TypeScript infrastructure for VS
// Code and Node-based AutoContext components. Re-exports the public
// surface of the package so consumers can import directly from
// `autocontext-framework-web`.

export type { Logger } from './logging/logger.js';
export { NullLogger } from './logging/null-logger.js';
export { LengthPrefixedFrameCodec } from './transport/length-prefixed-frame-codec.js';
export { PipeTransport } from './transport/pipe-transport.js';
