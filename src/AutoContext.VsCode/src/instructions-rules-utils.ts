/**
 * Utilities for normalizing AutoContext instruction-rule markdown.
 */
export class InstructionsRulesUtils {
    private static readonly instructionIdTag = /\[INST\d{4}\]\s*/g;

    /** Strip `[INSTxxxx]` rule-id tags from instruction markdown. */
    static stripAllRulesIds(content: string): string {
        return content.replace(InstructionsRulesUtils.instructionIdTag, '');
    }
}
