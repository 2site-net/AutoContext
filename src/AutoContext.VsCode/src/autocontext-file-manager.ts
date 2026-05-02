import { readFile, unlink, writeFile } from 'node:fs/promises';
import { AutoContextConfig } from './autocontext-config.js';
import type { InstructionsFileConfigEntry } from '#types/instructions-file-config-entry.js';
import type { McpToolConfigEntry } from '#types/mcp-tool-config-entry.js';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Owns async I/O and disk-shape mapping for `.autocontext.json`.
 *
 * Errors (including ENOENT) are propagated to the caller so the manager
 * can apply its own logging/dedup policy. Use `AutoContextFileManager.isNotFound`
 * to identify missing-file errors.
 */
export class AutoContextFileManager {
    constructor(private readonly logger: ChannelLogger) {}

    /** Reads and parses the config file. Throws on any I/O or JSON error. */
    async load(path: string): Promise<AutoContextConfig> {
        const raw = await readFile(path, 'utf-8');
        const parsed: Record<string, unknown> = JSON.parse(raw);
        return AutoContextFileManager.fromDisk(parsed);
    }

    /** Writes the config to disk as JSON (4-space indent, trailing newline). */
    async save(path: string, config: AutoContextConfig): Promise<void> {
        const serialised = AutoContextFileManager.toDisk(config);
        await writeFile(path, JSON.stringify(serialised, null, 4) + '\n', 'utf-8');
    }

    /** Deletes the config file. Tolerates all errors (logs them at debug). */
    async delete(path: string): Promise<void> {
        try {
            await unlink(path);
        } catch (err) {
            // File didn't exist — nothing to delete.
            this.logger.debug(`No config file to delete: ${path}`, err);
        }
    }

    /** Returns true if the given error is a Node.js ENOENT (file not found) error. */
    static isNotFound(err: unknown): boolean {
        return err instanceof Error && 'code' in err && err.code === 'ENOENT';
    }

    /** Converts on-disk JSON to the internal model. */
    private static fromDisk(parsed: Record<string, unknown>): AutoContextConfig {
        const config = new AutoContextConfig();

        if (typeof parsed.version === 'string') {
            config.version = parsed.version;
        }

        if (parsed.diagnostic) {
            config.diagnostic = parsed.diagnostic as AutoContextConfig['diagnostic'];
        }

        const rawInstructions = parsed.instructions as Record<string, Record<string, unknown>> | undefined;
        if (rawInstructions && typeof rawInstructions === 'object') {
            const instructions: Record<string, InstructionsFileConfigEntry> = {};
            for (const [fileName, raw] of Object.entries(rawInstructions)) {
                const entry: InstructionsFileConfigEntry = {};
                if (typeof raw.version === 'string') {
                    entry.version = raw.version;
                }
                if (raw.enabled === false) {
                    entry.enabled = false;
                }
                const disabledIds = raw.disabledInstructions as string[] | undefined;
                if (disabledIds && disabledIds.length > 0) {
                    entry.disabledInstructions = disabledIds;
                }
                instructions[fileName] = entry;
            }
            if (Object.keys(instructions).length > 0) {
                config.instructions = instructions;
            }
        }

        const rawTools = parsed.mcpTools as Record<string, Record<string, unknown> | false> | undefined;
        if (rawTools && typeof rawTools === 'object') {
            const mcpTools: Record<string, McpToolConfigEntry | false> = {};
            for (const [toolName, raw] of Object.entries(rawTools)) {
                if (raw === false) {
                    mcpTools[toolName] = false;
                } else {
                    const entry: McpToolConfigEntry = {};
                    if (raw.enabled === false) {
                        entry.enabled = false;
                    }
                    if (typeof raw.version === 'string') {
                        entry.version = raw.version;
                    }
                    const disabledTasks = raw.disabledTasks as string[] | undefined;
                    if (disabledTasks && disabledTasks.length > 0) {
                        entry.disabledTasks = disabledTasks;
                    }
                    mcpTools[toolName] = entry;
                }
            }
            if (Object.keys(mcpTools).length > 0) {
                config.mcpTools = mcpTools;
            }
        }

        return config;
    }

    /** Converts the internal model to on-disk JSON. Keys are camelCase. */
    private static toDisk(config: AutoContextConfig): Record<string, unknown> {
        const output: Record<string, unknown> = {};

        if (config.version) {
            output.version = config.version;
        }
        if (config.diagnostic) {
            output.diagnostic = config.diagnostic;
        }

        if (config.instructions) {
            const instructions: Record<string, Record<string, unknown>> = {};
            for (const [fileName, entry] of Object.entries(config.instructions)) {
                const raw: Record<string, unknown> = {};
                if (entry.version) {
                    raw.version = entry.version;
                }
                if (entry.enabled === false) {
                    raw.enabled = false;
                }
                if (entry.disabledInstructions && entry.disabledInstructions.length > 0) {
                    raw.disabledInstructions = entry.disabledInstructions;
                }
                instructions[fileName] = raw;
            }
            output.instructions = instructions;
        }

        if (config.mcpTools) {
            const tools: Record<string, Record<string, unknown> | false> = {};
            for (const [toolName, entry] of Object.entries(config.mcpTools)) {
                if (entry === false) {
                    tools[toolName] = false;
                } else {
                    const raw: Record<string, unknown> = {};
                    if (entry.enabled === false) {
                        raw.enabled = false;
                    }
                    if (entry.version) {
                        raw.version = entry.version;
                    }
                    if (entry.disabledTasks && entry.disabledTasks.length > 0) {
                        raw.disabledTasks = entry.disabledTasks;
                    }
                    tools[toolName] = raw;
                }
            }
            output.mcpTools = tools;
        }

        return output;
    }
}
