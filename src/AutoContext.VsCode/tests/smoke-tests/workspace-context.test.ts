import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('Workspace Context Detector Smoke Tests', () => {
    test('should detect TypeScript in this workspace', async () => {
        const { exports } = await activatedExtension();
        await exports.workspaceContextDetector.detect();

        assert.ok(exports.workspaceContextDetector.get('hasTypeScript'), 'Should detect TypeScript');
    });

    test('should return empty overridden context keys by default', async () => {
        const { exports } = await activatedExtension();
        await exports.workspaceContextDetector.detect();
        const overridden = exports.workspaceContextDetector.getOverriddenContextKeys();

        assert.strictEqual(overridden.size, 0, 'No overridden context keys expected in extension workspace');
    });
});
