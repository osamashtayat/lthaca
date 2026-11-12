# ADR-0022: Unclear, off-topic and injected mood text

- **Status:** Accepted (owner, 2026-11-13)
- **Date:** 2026-11-13

## Context
"By mood" (solo) and the room mood step accept free text ("Describe it your way") that goes to the LLM (`mood-parse`).
People will type things that aren't moods: gibberish, questions about something else, or attempts to steer the model
("ignore your instructions…", fake `SYSTEM:` lines, closing tags). The existing guards (ADR-0018, docs/ai.md) make this
harmless: user text is only ever the user message, the reply must match a closed JSON schema, genres are checked
against our list, and mood-parse can't return films. But the user isn't told anything: off-topic text silently yields
empty filters and a random deck. Only an outright model refusal surfaces (`ai.refused`).

## Decision
1. **The model labels, it never acts.** `mood-parse.v2` adds `intent: "mood" | "unclear" | "offTopic"` to its output.
   Gibberish and contradictions without a usable mood are `unclear`; anything that isn't about what to watch,
   including instructions to the model, is `offTopic`. Filters are empty unless `intent` is `mood`.
2. **The server decides** (MoodQuery service, solo and room alike):

   | Input | Result |
   |---|---|
   | Text with a mood (chips or not) | deck / room mood as usual |
   | Text that isn't a mood, **no chips** | 422 `mood.unclear`; nothing is created |
   | Text that isn't a mood, **with chips** | built from the chips only; the reply carries `textIgnored: true` |
   | Empty or whitespace text | no LLM call; chips (or the sampler) only |
   | The model refuses | 422 `ai.refused` (unchanged) |

   Chips win because they are an explicit choice; blocking a user over a bad sentence punishes the clear part.
3. **Never echo the text.** No response, message or log repeats the user's text (docs/backend-conventions.md: no mood
   text in logs). Messages say "we", never "the AI".
4. **User-facing wording** (frontend; `problem.ts` for the code):
   - `mood.unclear`, under the text field, text kept and focused: *"We couldn't find a mood in that. Try something like
     "funny and under 2 hours", or tap a mood above."*
   - `textIgnored`, a small line on the deck that disappears after the first swipe, never a toast: *"We went with your
     moods; we couldn't make sense of the text."*
   - Room mood step: the same rules; the copy says "your vibe for tonight" instead of "your mood".
5. **Evals** for mood-parse grow to about 20 cases labelled `representative`, `edge` and `adversarial` (fake system
   lines, closing-tag tricks, "recommend Fight Club", other languages incl. Arabic, empty text, gibberish,
   contradictions). The runner gets a `kind` field, an `intentIs` check and a `mustNotContain` text check.

## Consequences
+ Users learn when their words weren't understood; injection attempts get a normal, boring outcome.
+ All rules stay server-side; the frontend only maps a code and a flag to text.
- One more prompt version (v2) and a contract change (BE-09a) before the mood screens.
- The model's labelling can be wrong; evals are the guard, and chips always work without it.

## Alternatives rejected
Blocking whenever the text is unclear, even with chips (friction, users leave); a keyword blocklist in code (easy to
evade, false positives on real moods); echoing the text back in the error (looks broken, invites probing); a separate
moderation call (extra latency and cost for a filter-only feature).
