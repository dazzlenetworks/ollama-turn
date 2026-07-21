---
created: 7/21/2026 10:28
---

# Ollama Turn: Project Overview and Intent

## Purpose

Capture what Ollama Turn is, the problem it solves, and the guiding philosophy
behind it, so the intent survives beyond the conversation that produced it.

## What It Is

Ollama Turn is a small Windows desktop app for doing a **single request →
single response** turn against a remote Ollama server. It presents a modern,
browser-grade UI in a native window, with no separate web server to run and no
CORS configuration to fight.

## The Problem

Querying a networked Ollama instance ad hoc usually means either raw `curl`
against the REST API or standing up something heavier (OpenWebUI on an LXC or
Coolify). There was a gap: nothing lightweight that gives a nice UI, remembers
the server URL, and just does one clean turn without ceremony.

## Guiding Philosophy

- **Single turn, done well.** The app deliberately does one prompt/response at a
  time rather than trying to be a full chat client. The name reflects the scope.
- **No install, no server product.** It must run on a stock Windows machine with
  nothing to deploy. See [[2026-0721-1029 server-free architecture decisions]].
- **Get out of the user's way on CORS and config.** These are the two things
  that normally make a browser-to-Ollama tool annoying; the design removes both
  as concerns. See [[2026-0721-1030 host proxy and cors avoidance]] and
  [[2026-0721-1031 configuration storage model]].
- **Pretty matters.** A native window with a clean, responsive UI, not a dated
  shell. See [[2026-0721-1032 user interface and interactions]].

## Non-Goals

- Not a multi-turn conversation manager (single-turn by design).
- Not a model-management or fine-tuning tool.
- Not a hosted or multi-user service.

## Endpoints Used

The whole app is built on two Ollama REST endpoints:
- `GET /api/tags` — list installed models (populates the dropdown).
- `POST /api/generate` with `stream: false` — run the turn.
