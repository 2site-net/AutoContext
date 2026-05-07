import * as assert from 'node:assert/strict';
import { activatedExtension } from './helpers.js';

suite('Diagnostics Runner Smoke Tests', () => {
    test('should collect diagnostics records without throwing', async () => {
        const { exports } = await activatedExtension();

        const records = await exports.diagnosticsRunner.collect();

        assert.ok(Array.isArray(records), 'collect() should return an array');
        for (const r of records) {
            assert.ok(typeof r.entry === 'string' && r.entry.length > 0, `Record missing entry: ${JSON.stringify(r)}`);
            assert.ok(typeof r.kind === 'string' && r.kind.length > 0, `Record missing kind: ${JSON.stringify(r)}`);
            assert.ok(typeof r.message === 'string', `Record missing message: ${JSON.stringify(r)}`);
        }
    });
});
