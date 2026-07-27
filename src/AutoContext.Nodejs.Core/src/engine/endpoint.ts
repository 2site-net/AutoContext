import { randomUUID } from 'node:crypto';

import { WORKSPACE_HASH_LENGTH } from './workspace-hash.js';

/**
 * The four logical channels an engine binds per
 * (workspace, launcher instance).
 */
export const EndpointKind = {
    /** Request/response RPC. Requires the Engine.Hello handshake. */
    Rpc: 'rpc',

    /** Engine-broadcast events. Requires the Engine.Hello handshake. */
    Events: 'events',

    /** Passive health/readiness probe. No handshake. */
    Health: 'health',

    /** Server-streaming log tail. No handshake. */
    Logs: 'logs',
} as const;

export type EndpointKind = typeof EndpointKind[keyof typeof EndpointKind];

const ENDPOINT_PREFIX = 'autocontext-engine:';
const KIND_WORKSPACE_SEPARATOR = '@';
const WORKSPACE_INSTANCE_SEPARATOR = '#';
const INSTANCE_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const WORKSPACE_HASH_PATTERN = new RegExp(`^[0-9A-F]{${WORKSPACE_HASH_LENGTH}}$`);

/**
 * Renders the canonical endpoint address
 * `autocontext-engine:<kind>@<workspaceHash>#<instanceId>`, which is
 * also the OS pipe name both sides use verbatim.
 *
 * Contract counterpart of the C# `Endpoint.ToString()` in
 * `AutoContext.Engine.Protocol`.
 *
 * @throws When the hash is not 16 uppercase hex characters or the
 * instance id is not a lowercase hyphenated UUID.
 */
export function formatEndpoint(
    kind: EndpointKind,
    workspaceHash: string,
    instanceId: string,
): string {
    if (!WORKSPACE_HASH_PATTERN.test(workspaceHash)) {
        throw new Error(
            `workspaceHash must be ${WORKSPACE_HASH_LENGTH} uppercase hex characters.`);
    }
    if (!INSTANCE_ID_PATTERN.test(instanceId)) {
        throw new Error('instanceId must be a lowercase hyphenated UUID.');
    }

    return ENDPOINT_PREFIX
        + kind
        + KIND_WORKSPACE_SEPARATOR
        + workspaceHash
        + WORKSPACE_INSTANCE_SEPARATOR
        + instanceId;
}

/**
 * Mints the per-launch instance id in the form the endpoint segment
 * and the engine's `--instance-id` switch both expect.
 */
export function createInstanceId(): string {
    return randomUUID();
}
