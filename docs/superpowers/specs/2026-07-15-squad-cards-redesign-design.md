# Squad Cards Redesign — Design Spec

**Date:** 2026-07-15
**Scope:** `/squads` card grid only (not the squad detail page `/squads/$squadId`)

## Problem

The squad card list (`_auth.squads.index.tsx`) currently shows each member as
their Discord `username` (primary) with `gameNickname` as a small secondary
label. Two things need to change:

1. Only the in-game nickname should be shown per member (Discord identity is
   noise here — this is a STALCRAFT clan roster view).
2. Officers and above need a way to rename a squad directly from the card,
   without leaving the list.

## Member nickname display

Replace the two-line "username · gameNickname" pattern with a single label:

```
member.gameNickname ?? member.username
```

If a member hasn't linked a game nickname yet (`gameNickname: null`), fall
back to their Discord username so they don't disappear from the roster.
Applies to both the leader row and the regular member rows on the card.

No change to `_auth.squads.$squadId.tsx` (the member table there keeps
showing Discord name + game nickname as today) — out of scope per this spec.

## Squad rename (Officer+)

**Backend**

- New command `RenameSquadCommand(Guid SquadId, string Name)` in
  `Awake.Application/Features/Squads/Commands/RenameSquad/`, following the
  existing `SetSquadLeaderCommand`/`Handler` pattern:
  - Handler loads the squad via `ISquadRepository.GetByIdAsync`; `Result.Failure`
    if not found.
  - Trims the input name, assigns `squad.Name`, persists via a new
    `ISquadRepository.UpdateAsync(Squad squad, CancellationToken ct)` method
    (mirrors `UpdateMemberAsync`: `context.Squads.Update(squad); await
    context.SaveChangesAsync(ct);`).
- `RenameSquadCommandValidator` (FluentValidation): `SquadId` not empty;
  `Name` not empty, max length 100 (matches `SquadConfiguration`'s
  `HasMaxLength(100)`).
- New endpoint on `SquadsController`:
  `PUT /api/squads/{id:guid}/name`, `[RankAuthorize(UserRank.Officer)]`,
  request body `record RenameSquadRequest(string Name)`. Same
  `result.IsSuccess ? NoContent() : Problem(...)` response pattern as the
  other mutation endpoints in this controller.

**Frontend**

- `squadsApi.rename(squadId, name)` → `apiClient.put<void>(`/squads/${squadId}/name`, { name })`.
- On the squad card (`_auth.squads.index.tsx`), next to the squad name:
  - Visible only when `rank >= UserRank.Officer` (existing `rank` var already
    computed on the page).
  - View mode: squad name text + a small pencil icon button. Clicking the
    pencil calls `e.preventDefault(); e.stopPropagation()` (the card is a
    `<Link>` to the squad detail page — these calls stop the click from
    triggering navigation) and switches that card into edit mode.
  - Edit mode: an `<input>` replaces the name text, pre-filled with the
    current name, autofocused. All interactions with the input
    (`onClick`, `onKeyDown`, `onBlur`) call `stopPropagation` so the
    surrounding `<Link>` never navigates while editing.
    - `Enter` or `onBlur` → save (skip the mutation if the trimmed value is
      unchanged or empty; empty just reverts to view mode without saving).
    - `Escape` → cancel, revert to the original name, no mutation.
  - Save calls a `useMutation` wrapping `squadsApi.rename`, invalidating the
    `['squads']` query on success (same invalidation key the existing
    `removeMember`/`setLeader` mutations on the detail page use).
  - Editing state (`which squad id is currently being edited`) is local
    per-card `useState`, not lifted to the page — only one card can be in
    edit mode at a time in practice since clicking a pencil elsewhere just
    starts editing a different card's own local state independently, which
    is fine (no shared state needed).
- The rest of the card (badge, capacity bar, click-to-navigate) is
  unchanged.

## Out of scope

- Squad detail page member display — stays Discord name + nickname as today.
- Squad renaming from anywhere other than the card (no separate settings
  page).
- Uniqueness validation on squad names (not enforced today, not requested).
