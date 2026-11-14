# ADR-0026: Taste readiness and the film picker

- **Status:** Accepted (owner, 2026-11-14)
- **Date:** 2026-11-14
- **Amends:** api-contract.md §12 (BE-14a); answers A11

## Context
By taste (solo) and group taste ("Blend your histories") need history: at least 10 films liked, rated ≥ 3.5 or
"loved it" (A16). People get there by using Ithaca or by importing their Letterboxd export (ADR-0024). That leaves two
gaps: someone with no Letterboxd account, or who doesn't want to import, hits a dead end; and guests in a taste room have
no library at all (ADR-0012), so BE-14a could only assume they "add no taste" (A11, ⚠). The owner decided both
(2026-11-14).

## Decision
1. **Readiness in rooms.** `justSwipe`: everyone is ready on joining (unchanged). `mood`: ready after the mood step
   (unchanged). `taste`: a member is ready when the server has enough taste for them: a user who meets A16, or a guest
   who picked films in that room (decision 3). `RoomMemberDto.isReady` carries it; the host may still start with
   members not ready ("2 OF 3 READY").
2. **The film picker.** Anyone without enough history can pick **10–20 films they love** instead of importing: search
   or browse our catalog (the existing `GET /api/films?search=…`, open to guests), tap films, submit. 10 is A16's
   threshold, so a full pick makes a user ready.
3. **Where picks go.**
   - **Users:** each pick is a normal `liked` in My Cinema (only adds; other flags and ratings untouched; already liked
     is fine). Permanent, and it counts everywhere taste does. Solo: `POST /api/taste/picks`.
   - **Guests:** picks are stored on their `RoomMember`, used only for that room's blend, and deleted with the room
     (they leave with the membership row's lifetime). A repeat submit replaces them.
   - **Guest upgrade** (ADR-0012): when the guest registers or signs in, their picks become likes in the user's library,
     like their votes move.
   - In a room, one endpoint serves both (`PUT /api/rooms/{code}/taste`): the server routes a user's picks to likes and
     a guest's to the membership, so the lobby needs one call.
4. **The host must be ready** to create a taste room (unchanged from BE-14a: 422 `taste.not_ready`).
5. **The warning.** Members who aren't ready are told their taste isn't in the blend and are offered the picker; the
   host sees who isn't ready before starting. The frontend counts from `isReady` (no new field): a display, not a rule.
6. **Solo By taste empty state** (FE-13) gains the picker as a third way out, next to "Import your Letterboxd" and
   "Just swipe instead".
7. Films the blend skips: any film a member has seen (users: from their library; guests: nothing known).

## Consequences
+ No dead end: anyone reaches By taste in a minute, with or without Letterboxd.
+ Guests count in group taste, and their picks survive a sign-up.
+ Users' picks reuse `UserFilm`, so the profile, receipt and readiness need no new source.
- A small room-scoped store for guests (10–20 film ids per membership), which ADR-0024 Q1 avoided for imports. The
  difference: no file, no background job, no retention rule beyond the room's own; it holds ids only.
- Rooms never expire yet, so guest picks live as long as their room (see the BE-10 TODO on room expiry).
- There is no design board for the picker; FE-21 follows the visual language (frontend-conventions.md).

## Alternatives rejected
A separate "taste seed" store for users (two sources of taste, and picks wouldn't show in My Cinema); no picks for
guests (guests could never count in group taste); a per-room "taste count" field for the warning (the frontend can count
`isReady`).
