// AutoContext.Framework.Web — shared TypeScript infrastructure for VS Code
// and Node-based AutoContext components. Phase 0: scaffolding only.
//
// Subsequent phases will populate this package with the unified pipe
// transport primitives (PipeTransport, LengthPrefixedFrameCodec, and the
// Layer-3 endpoint kinds). Until then, this package exposes a single
// sentinel constant so consumers can verify the workspace dependency
// edge resolves end-to-end.

export const FRAMEWORK_WEB_PACKAGE = 'autocontext-framework-web';
