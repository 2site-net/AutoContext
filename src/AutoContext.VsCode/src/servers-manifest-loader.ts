import { join } from 'node:path';
import { ServerEntry } from './server-entry.js';
import { ServersManifest } from './servers-manifest.js';
import { JsonFile } from './json-file.js';

interface JsonServer {
    id: string;
    name: string;
    type: string;
}

interface JsonServersFile {
    servers: readonly JsonServer[];
}

/**
 * Loads `resources/servers.json` from the extension folder and projects
 * it into a {@link ServersManifest} instance.
 */
export class ServersManifestLoader {
    constructor(private readonly extensionPath: string) {}

    load(): ServersManifest {
        const json = JsonFile.fromUtf8<JsonServersFile>(
            join(this.extensionPath, 'resources', 'servers.json'),
        );

        const entries = json.servers.map(s => {
            if (s.type !== 'dotnet' && s.type !== 'node') {
                throw new Error(`servers.json: entry '${s.id}' has unsupported type '${s.type}'.`);
            }
            return new ServerEntry(s.id, s.name, s.type);
        });

        return new ServersManifest(entries);
    }
}
