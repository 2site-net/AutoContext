import type { WorkspaceContextDetector } from '../workspace-context-detector.js';
import type { InstructionsFilesOverrideWatcher } from '../instructions-files-override-watcher.js';
import type { AutoContextConfigManager } from '../autocontext-config-manager.js';

/**
 * Bundle of long-lived services that manifest entries consult to
 * resolve their current activation/configuration state. Grouped into a
 * single typed context to keep entry constructors focused on data and
 * to remove positional-argument fragility when wiring fakes in tests.
 */
export interface RuntimeContext {
    readonly detector: WorkspaceContextDetector;
    readonly configManager: AutoContextConfigManager;
}

/** Runtime context for instructions entries (adds the override watcher). */
export interface InstructionsRuntimeContext extends RuntimeContext {
    readonly overrideWatcher: InstructionsFilesOverrideWatcher;
}

/** Runtime context for MCP tool/task entries. */
export type McpRuntimeContext = RuntimeContext;
