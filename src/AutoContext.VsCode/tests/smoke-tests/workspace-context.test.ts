import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('Workspace Context Detector Smoke Tests', () => {
    test('should detect TypeScript in this workspace', async () => {
        const { exports } = await activatedExtension();
        await exports.workspaceContextDetector.detect();

        assert.ok(exports.workspaceContextDetector.get('hasTypeScript'), 'Should detect TypeScript');
    });

    test('should return empty overridden file names by default', async () => {
        const { exports } = await activatedExtension();
        await exports.instructionsOverrideWatcher.watch();
        const overridden = exports.instructionsOverrideWatcher.getOverriddenFileNames();

        assert.strictEqual(overridden.size, 0, 'No overridden files expected in extension workspace');
    });
});
