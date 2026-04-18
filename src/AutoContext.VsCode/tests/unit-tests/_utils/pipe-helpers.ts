import { connect, type Socket } from 'node:net';

export function pipePath(pipeName: string): string {
    return process.platform === 'win32'
        ? `\\\\.\\pipe\\${pipeName}`
        : `/tmp/CoreFxPipe_${pipeName}`;
}

export function connectAndSend(pipe: string, scope: string): Promise<Socket> {
    return new Promise((resolve, reject) => {
        const socket = connect(pipePath(pipe), () => {
            socket.write(scope, () => resolve(socket));
        });
        socket.on('error', reject);
    });
}
