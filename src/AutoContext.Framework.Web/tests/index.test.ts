import { describe, expect, it } from 'vitest';
import { FRAMEWORK_WEB_PACKAGE } from '#src/index.js';

describe('AutoContext.Framework.Web', () => {
    it('exposes its package sentinel', () => {
        expect(FRAMEWORK_WEB_PACKAGE).toBe('autocontext-framework-web');
    });
});
