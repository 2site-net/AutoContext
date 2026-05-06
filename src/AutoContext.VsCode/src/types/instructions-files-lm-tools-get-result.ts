import type { DisabledInstructionsFileEnvelope } from './disabled-instructions-file-envelope.js';

/**
 * Discriminated-union response shape of
 * `get_autocontext_instructions_file`.
 *
 * - `kind: 'ok'` — content was returned. `returnedSections` lists the
 *   anchors actually included; for a whole-file fetch this is the
 *   full anchor list of the body. `notFoundSections` is present iff
 *   the caller requested a `sections` array containing one or more
 *   unknown anchors.
 * - `kind: 'not-found'` — `name` is not in the bundled manifest. No
 *   identity leakage: the platform's tool-call already echoed the
 *   name back to the model.
 * - `disabled: true` — the user has disabled the file (per-file
 *   activation flags unmet, or `autocontext.json` toggle off). Step 3
 *   envelope, identity-only, no content.
 */
export type InstructionsFilesLmToolsGetResult =
    | InstructionsFilesLmToolsGetOk
    | InstructionsFilesLmToolsGetNotFound
    | DisabledInstructionsFileEnvelope;

export interface InstructionsFilesLmToolsGetOk {
    readonly kind: 'ok';
    readonly name: string;
    readonly key: string;
    readonly fileName: string;
    readonly content: string;
    readonly returnedSections: readonly string[];
    readonly notFoundSections?: readonly string[];
}

export interface InstructionsFilesLmToolsGetNotFound {
    readonly kind: 'not-found';
    readonly name: string;
}
