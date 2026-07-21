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

1. **Ollama server** — a URL textbox with **Save** and **Refresh models**
   buttons, plus a status/meta line and a transient "Saved" confirmation.
2. **Prompt** — a model dropdown, a message textarea, and a **Send** button.
3. **Response** — the rendered response, a stats line (elapsed time, token
   count when available), and the copy/close actions.

## Key Interactions

- **Model dropdown** is populated from `GET /api/tags`. A custom SVG chevron is
  drawn on the right because native appearance is suppressed for styling.
- **Remember last model:** on change the selection is saved to `localStorage`;
  after models load, the last choice is reselected if still available.
- **Send:** validates URL/model/prompt, shows a spinner and "Thinking…", then
  renders the response. **Ctrl+Enter** in the message box also sends.
- **Connection status dot:** neutral / green (ok) / red (error), reflecting the
  last server interaction.

## Copy / Clear / Close Actions

Bottom of the Response card, three buttons:
- **Copy** — copies the response text (clipboard API with an `execCommand`
  fallback) and flashes "Copied".
- **Copy & Clear** — copies, then clears the prompt and response and refocuses
  the message box for the next turn.
- **Copy & Close** — copies, then asks the host to close the app (`closeApp`).

All three guard against copying an empty/placeholder response.

## Feedback and Error Handling

Errors from the host (unreachable server, bad status, etc.) are shown inline in
the relevant meta line in red, never as a crash. The status dot mirrors success
or failure.

## Related

- [[2026-0721-1030 host proxy and cors avoidance]]
- [[2026-0721-1033 build run and distribution]]
