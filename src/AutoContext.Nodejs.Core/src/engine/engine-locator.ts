import { platform } from 'node:os';
import { join } from 'node:path';

const BINARY_NAME = 'autocontext-engine';
const ENGINE_SUBDIRECTORY = 'engine';

/**
 * Where the manager finds the engine binary: either an explicit path,
 * or the bundle root whose `engine/` subdirectory holds the shipped
 * side-car. The union makes "neither supplied" unrepresentable.
 */
export type EngineBinaryLocation =
    | { readonly engineBinaryPath: string; readonly bundleRoot?: undefined }
    | { readonly bundleRoot: string; readonly engineBinaryPath?: undefined };

/**
 * Resolves the absolute path of the engine binary, appending the
 * platform's executable suffix when resolving through the bundle root.
 *
 * Contract counterpart of the C# `EngineLocator.Resolve` in
 * `AutoContext.Client.Core`.
 */
export function resolveEngineBinaryPath(location: EngineBinaryLocation): string {
    if (location.engineBinaryPath !== undefined) {
        if (location.engineBinaryPath === '') {
            throw new Error('engineBinaryPath must not be empty.');
        }
        return location.engineBinaryPath;
    }

    if (location.bundleRoot === '') {
        throw new Error('bundleRoot must not be empty.');
    }

    const fileName = platform() === 'win32' ? `${BINARY_NAME}.exe` : BINARY_NAME;
    return join(location.bundleRoot, ENGINE_SUBDIRECTORY, fileName);
}
