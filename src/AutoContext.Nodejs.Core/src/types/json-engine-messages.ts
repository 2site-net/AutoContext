// Wire shapes of the Engine.* handshake and shutdown surface.
// Property names are the JSON names the engine emits, mirroring the
// `[JsonPropertyName]` attributes on the DTOs in
// `AutoContext.Engine.Protocol/Messages/`. Absent optional properties
// are omitted from the JSON rather than sent as null.

/** Result of the Engine.Hello handshake. */
export interface JsonHandshakeResult {
    readonly protocolVersion: number;
    readonly engineVersion: string;
}

/** Result of Engine.Shutdown. */
export interface JsonShutdownResult {
    readonly accepted: boolean;
}
