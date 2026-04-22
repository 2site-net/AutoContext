// Public surface for AutoContext.Worker.Web hosting utilities.
// The worker entry point is wired in Phase 5b; for now this module
// exports only the hosting primitives used by future tasks and tests.
export type { McpTask } from './hosting/mcp-task.js';
export type { WorkerHostOptions } from './hosting/worker-host-options.js';
export { McpToolService } from './hosting/mcp-tool-service.js';
export { readMessage, writeMessage, MAX_MESSAGE_BYTES } from './hosting/pipe-framing.js';
