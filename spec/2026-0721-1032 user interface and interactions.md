---
created: 7/21/2026 10:32
---

# User Interface and Interactions

## Purpose

Describe the UI structure and interaction behaviors as built, so the intended
experience is recorded independently of the HTML implementation.

## Layout

A single page ([`www/index.html`](../www/index.html)) with three stacked cards,
centered with a max width (1080px) and comfortable padding. The window opens
**maximized**; the content stays centered and reflows responsively rather than
stretching edge to edge.

Visual style: dark theme, system font, subtle borders, an accent blue, and a
connection-status dot in the header.

## Cards

1. **Ollama Server** — a URL textbox with **Save** and **Refresh models**
   buttons, plus a status/meta line and a transient "Saved" confirmation.
2. **Prompt** — a model dropdown, a message textarea, and a **Clear** / **Send**
   button pair. **Clear** sits to the left of **Send** and wears the same
   `secondary` outline style as the copy buttons, so the primary blue stays on
   the one action that costs something.
3. **Response** — the rendered response, a stats line (elapsed time, token
   count when available), and the copy/close actions.

## Key Interactions

- **Model dropdown** is populated from `GET /api/tags`. A custom SVG chevron is
  drawn on the right because native appearance is suppressed for styling.
- **Remember last model:** on change the selection is saved to `config.json`
  through the host; after models load, that choice is reselected if still
  available. See [[2026-0721-1031 configuration storage model]].
- **Send:** validates URL/model/prompt, shows a spinner and "Thinking…", then
  renders the response.
- **Enter sends; Shift+Enter inserts a line break.** This matches what every
  chat client trains the fingers to expect, and prompts here are usually one
  line. **Ctrl+Enter** still sends, so the older habit keeps working. Enter is
  ignored while an IME composition is open, where it commits a candidate rather
  than ending the message.
- **Clear:** empties the message textarea and refocuses it, leaving the response
  on screen to read while the next prompt is typed. It is the mirror of Copy &
  Clear, which wipes the response and leaves the prompt — between them either
  half of a turn can be cleared without touching the other.
- **Follows the config file:** if `config.json` changes while the window is open
  — the CLI's `--set-model`, an editor, a file sync — the URL box and model
  dropdown update themselves and an "Updated from config.json" flash appears
  beside the server row. A changed URL reloads the model list, since a different
  server means a different set of models.
- **Connection status dot:** neutral / green (ok) / red (error), reflecting the
  last server interaction.

## Copy / Clear / Close Actions

Bottom of the Response card, three buttons:
- **Copy** — copies the response text (clipboard API with an `execCommand`
  fallback) and flashes "Copied".
- **Copy & Clear** — copies, then clears **only the response** and refocuses the
  message box. The prompt is deliberately left in place: the common move after
  reading an answer is to adjust the wording and run it again, and wiping the
  message meant retyping it.
- **Copy & Close** — copies, then asks the host to close the app (`closeApp`).

All three guard against copying an empty/placeholder response.

## Feedback and Error Handling

Errors from the host (unreachable server, bad status, etc.) are shown inline in
the relevant meta line in red, never as a crash. The status dot mirrors success
or failure.

## Related

- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1033 build run and distribution]]
