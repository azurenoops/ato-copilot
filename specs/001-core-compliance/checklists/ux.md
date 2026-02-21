# UX Checklist: Core Compliance Capabilities

**Purpose**: Validate completeness, clarity, and consistency of user-facing messages, error suggestions, progress feedback, and interaction patterns
**Created**: 2026-02-21
**Feature**: [spec.md](../spec.md), [quickstart.md](../quickstart.md)

## Error Experience

- [x] CHK001 Are all 14 error codes accompanied by specific, actionable `suggestion` text that a non-technical compliance officer can act on? [Clarity, Spec §Error Codes]
- [x] CHK002 Is the error message tone/voice defined (technical, conversational, formal)? [Consistency, Gap]
- [x] CHK003 Are error messages defined for Azure Government-specific failures (wrong cloud, government endpoint unavailable)? [Completeness, Gap]
- [x] CHK004 Is the behavior defined when multiple errors occur in a single operation (show first, show all, prioritize)? [Clarity, Gap]
- [x] CHK005 Are error messages defined for partial success scenarios (e.g., 8 of 10 remediations succeeded)? [Completeness, Gap]

## Progress & Feedback

- [x] CHK006 Is "streaming progress updates" (FR-015) defined — per-family updates, percentage, elapsed time, or all three? [Clarity, Spec §FR-015]
- [x] CHK007 Are progress update intervals specified for long-running assessments (every family? every 10 seconds? every N resources)? [Clarity, Gap]
- [x] CHK008 Is the completion summary format defined consistently across all operations (assessment, remediation, evidence, document)? [Consistency, Gap]
- [x] CHK009 Are loading/processing indicators specified for stdio mode (which lacks visual progress bars)? [Completeness, Gap]

## Confirmation & Safety

- [x] CHK010 Is the confirmation prompt format for remediation defined (exact wording, expected responses)? [Clarity, Spec §US3]
- [x] CHK011 Is the high-risk warning format for AC/IA/SC families defined — just an emoji warning or a structured risk assessment? [Clarity, Spec §US3]
- [x] CHK012 Is the behavior defined when a user responds ambiguously to a confirmation prompt (e.g., "maybe", "I think so")? [Coverage, Gap]
- [x] CHK013 Are requirements defined for batch remediation confirmation — confirm once for all or per-step? [Clarity, Spec §US3]

## Information Presentation

- [x] CHK014 Are findings display formats defined for each scan type (resource view vs policy view vs combined)? [Completeness, Gap]
- [x] CHK015 Is the compliance score format specified (percentage, fraction, letter grade, all three)? [Clarity, Spec §Quickstart]
- [x] CHK016 Are severity labels and their visual indicators defined consistently (Critical/High/Medium/Low — colors? emojis? icons)? [Consistency, Gap]
- [x] CHK017 Is the control family table format (quickstart Step 2) formally specified or just illustrative? [Ambiguity, Spec §Quickstart]
- [x] CHK018 Are date/time formats specified for user-facing output (ISO 8601, locale-aware, relative like "2 hours ago")? [Clarity, Gap]

## Conversation & Context

- [x] CHK019 Is the behavior defined when a user references a control discussed earlier without repeating the ID (e.g., "fix the one we just talked about")? [Clarity, Spec §FR-013]
- [x] CHK020 Is the "no configuration" prompt UX defined — does the agent offer to help configure or just show an error? [Completeness, Spec §US1-4]
- [x] CHK021 Is the "no assessment data" experience defined — does the agent offer to run one or just report the gap? [Completeness, Spec §US6]
- [x] CHK022 Are requirements defined for how the agent handles ambiguous intents that could match multiple tools? [Coverage, Gap]

## Accessibility & Inclusivity

- [x] CHK023 Are requirements defined for screen reader compatibility in MCP tool responses (alt text for tables, structured headings)? [Completeness, Gap]
- [x] CHK024 Are requirements defined for output length limits to avoid overwhelming users with large result sets? [Completeness, Gap]
- [x] CHK025 Is language complexity level specified for compliance jargon (should the agent explain NIST terms to non-experts)? [Clarity, Gap]

## Notes

- Check items off as completed: `[x]`
- Items tagged `[Gap]` indicate missing UX requirements
- Items tagged `[Ambiguity]` indicate unclear interaction patterns
- UX gaps should be resolved before Phase 3 (US1) to establish consistent patterns early
