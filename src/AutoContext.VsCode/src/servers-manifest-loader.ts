import { ServerEntry } from './server-entry.js';
import { ServersManifest } from './servers-manifest.js';
import { ResourceManifestLoader } from './resource-manifest-loader.js';

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
export class ServersManifestLoader extends ResourceManifestLoader<JsonServersFile, ServersManifest> {
    constructor(extensionPath: string) {
        super(extensionPath, 'servers.json');
    }

    protected project(json: JsonServersFile): ServersManifest {
        const entries = json.servers.map(s => {
            if (s.type !== 'dotnet' && s.type !== 'node') {
                this.fail(`entry '${s.id}' has unsupported type '${s.type}'.`);
            }
            return new ServerEntry(s.id, s.name, s.type);
        });

        return new ServersManifest(entries);
    }
}
