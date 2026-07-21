---
created: 7/21/2026 10:53
---

# Markdown Export and Multi-Turn Append

## Purpose

Document the Export capability as built: writing the current turn to a Markdown
file in the "Note Chat" format, with an Ollama-generated title, and the
append-to-existing-file mechanism that lets a single-turn app produce multi-turn
transcripts.

## Motivation

Tools like Claude Code and ChatGPT make it awkward to get clean Markdown out of a
conversation. Export makes capturing a turn to disk a single click. Because the
app is deliberately single-turn (see [[2026-0721-1028 project overview and intent]]),
Export doubles as the mechanism for assembling multi-turn records: run a turn,
export/append, repeat.

## User Flow

1. After a completed turn, the user clicks **Export** (bottom-right of the
   Response card).
2. The app asks the current model for a short title, then opens a **Save As**
   dialog pre-filled with a generated filename.
3. **New filename** → a fresh Note Chat document is written.
4. **Existing file** → the user is asked **Append / Overwrite / Cancel**:
   - **Append** adds the new turn's sections to the end and bumps the
     frontmatter `updated:` value, leaving `created:` intact.
   - **Overwrite** replaces the file with a fresh single-turn document.
   - **Cancel** does nothing.

The dialog remembers the last-used directory across exports.

## Filename Template

```text
YYYY-MMdd-HHmm Ollama Turn export - <Generated Title>.md
```

- The timestamp prefix comes from the cached turn's creation time.
- The fixed `Ollama Turn export - ` label meshes with a sibling utility that
  uses `ChatGPT export - `, so exports from both sort and read consistently.
- `<Generated Title>` is produced by the model (see below) and sanitized for the
  filesystem.

## Title Generation

- A second `POST /api/generate` call (via the shared host proxy) asks the current
  model for a 3–7 word Title Case title, no punctuation, reply-with-title-only.
- The result is trimmed of quotes/periods, whitespace-collapsed, and capped at
  70 characters.
- Filesystem-invalid characters are stripped.
- If titling fails or returns empty, the title falls back to the first ~6 words
  of the prompt, or "Untitled Turn".

## File Format (Note Chat)

```markdown
---
created: M/D/YYYY, h:mm:ss AM/PM
updated: M/D/YYYY, h:mm:ss AM/PM
model: "Ollama — <model tag>"
---

## User prompt
*M/D/YYYY, h:mm:ss AM/PM*

<prompt>

## Ollama response
*M/D/YYYY, h:mm:ss AM/PM*

<response>
```

- Timestamps use en-US format (`M/d/yyyy, h:mm:ss tt`).
- The assistant heading is `## Ollama response` (the honest label for this app);
  the actual model tag is recorded in frontmatter `model:` as `Ollama — <tag>`.
- Files are written UTF-8 without BOM.
- Multi-turn files are simply additional `## User prompt` / `## Ollama response`
  section pairs appended under the single frontmatter block.

## Implementation Notes

- Each completed turn is cached host-side in `GenerateAsync` (prompt, response,
  model, base URL, and accurate `created`/`updated` timestamps), so Export needs
  no data round-trip from the page.
- Export runs entirely in the C# host: titling, the Save As and append/overwrite
  dialogs, and file I/O. This is consistent with the host-proxy design in
  [[2026-0721-1030 host proxy and cors avoidance]].
- The append path updates the frontmatter `updated:` line via a small regex and
  leaves the rest of the document untouched.

## Out of Scope

- In-app rendering of the assembled multi-turn file.
- Re-importing exported Markdown back into the app.
- Automatic continuous appending without the Save As dialog.

## Related

- [[2026-0721-1028 project overview and intent]]
- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1032 user interface and interactions]]
