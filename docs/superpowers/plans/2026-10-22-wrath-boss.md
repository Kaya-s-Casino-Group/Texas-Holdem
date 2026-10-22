# Wrath Boss Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development. User authorized the recommended choices and implementation; no additional design approval is needed.

**Goal:** Implement the approved Wrath rules and usable Unity/H5 presentation without changing normal combat, elite, poker-hand or item behavior.

**Architecture:** Boss configuration and immutable runtime are opt-in; common engine boundaries delegate to Boss-only helpers. Keep five hand identities and use formal Revive, normal damage, status, reaction and terminal pipelines. Frontends consume a shared pure query of authoritative state.

**Tech Stack:** C#, XML content, NUnit/.NET 10, Unity scene/presentation, JavaScript H5.

## Baseline and ownership

Work on `codex/wrath-boss-20261022` in both repositories, carrying the user's existing uncommitted elite changes. Preserve historical `.20.xml` and the standard adventure map. Root owns this spec, final integration, damage-immunity primitive and adventure scaling. Core worker owns Wrath runtime, engine hooks, content configuration/compiler and rule integration tests. Unity worker owns main-repository presentation/setup/tests. H5 worker owns editor projection/web UI/schema/tests. Workers coordinate type contracts before consuming new APIs and do not stage or commit other workers' files.

## Task 1 — Opt-in damage immunity and fixed adventure health

- [x] Add failing NUnit cases in `External/BattleToolkit/Engine/Tests/Editor/CombatWrathImmunityTests.cs`: two positive damage segments become zero even under incoming additive modifiers; raw zero does not consume; poison/reflection count; Eliminate bypasses; ordinary HitArmor unchanged.
- [x] Add a separate `DamageImmunitySpec` behavior in `Engine/Runtime/Combat/CombatStatusDefinition.cs`, validated and parsed through content status schema. Consume one layer for a positive raw Damage request, force zero after modifiers, leave status application independent.
- [x] Add optional `scaleHealthWithAdventure` on Enemy content (default true); hand definitions set false. Update adventure clone to preserve it; test a multiplier of 2 keeps hands at 1 while body grows to 3000.

## Task 2 — Wrath authority and content

- [x] Add `WrathBossDefinition`, immutable runtime and pure presentation query in focused files under `Engine/Runtime/Composition/Encounters/`.
- [x] First write tests showing 5→6/11→12 scheduling, cancellation at R→R+5 readiness, D formula, charge on successful action only, independent hand due rounds, regeneration before hand queue, heal/control order, single ordinary completion, revival interruption and preview purity.
- [x] Hook only configured Wrath into phase entry, settled combat observation, actor attack resolution and post-ordinary-action extra effects. Preserve ordinary state construction and rollback semantics.
- [x] Parse/validate opt-in `<Wrath ...>` configuration and references. Update only `.21.xml` wrath/rules/five-hand entries and synchronize the standalone Wrath template. Keep historical old-Wrath tests using `.20.xml` valid.
- [x] Expose a pure public query containing body ID, effective attack, charges, warning state, release/cooldown rounds and each hand's role/regen round. Unity/H5 must use this query, not private fields or frontend clocks.

## Task 3 — Unity entry and presentation

- [x] Add shared projection tests for dead-hand countdown, warning cancellation, full body HP vs hand HP and current D.
- [x] Extend `BattleDisplayProjector` with Boss resources/labels, preserving existing elite projection.
- [x] Add a focused HUD partial for five hands and red warning, and correct Boss body-only HP/victory text.
- [x] Add an Editor menu/setup for a separate runnable six-unit Wrath demo based on BattleDemo, with Actor binding before initialization and original demo unchanged. Test roster bindings and target selection where Unity is available.

## Task 4 — H5 editor and playtest

- [x] Add projection and browser tests against the shared Boss query.
- [x] Extend `PlaytestProjection.cs`, `web/playtest.js`, `web/playtest.css`, `web/schema.js` for opt-in Boss configuration, dead hands/countdown, body warning/effective D, preserving normal large-roster behavior and target candidates.
- [x] Validate new schema round-trip and host XML validation, build host, run targeted browser verification when runtime/browser dependencies permit.

## Task 5 — Integration and review

- [x] Run new focused fixtures using the official usable SDK, then run the full existing rule suite once after integration.
- [x] Build the .NET host; validate `.21.xml` and Wrath template; smoke test six-unit startup and a full timed battle.
- [x] Run shared display tests through a generated `Library/WrathValidation/SharedTests.targets` with current absolute paths.
- [x] Independently review spec compliance, then code correctness, including rollback, timeline and death sources. Fix actionable findings and rerun relevant tests.
- [x] Record exact passed tests and unavailable Unity checks in `Tools/Validation/2026-10-22-wrath-boss.md`, with launch instructions and remaining visual limitations.

Verification commands (set `POKER_DOTNET` to the usable official SDK):

```sh
"$POKER_DOTNET" test External/BattleToolkit/Tests/BattleToolkit.Tests.csproj --configuration Release --filter 'FullyQualifiedName~Wrath'
"$POKER_DOTNET" test External/BattleToolkit/Tests/BattleToolkit.Tests.csproj --configuration Release
"$POKER_DOTNET" build External/BattleToolkit/Build/BattleToolkit.Host/BattleToolkit.Host.csproj --configuration Release
```

## Final status — 2026-10-22

Implementation and available automated verification are complete. New rules/adventure tests: 44/44; new shared projection tests: 7/7. Final full rules: 1639/1652 passed, with all 13 failures reproduced on clean submodule HEAD. Shared display/HUD: 27/28, with the pre-existing elite 90-vs-8 assertion mismatch. H5 and XML/host verification passed. Independent spec and correctness reviews found two issues (ultimate attack modifiers and stale Boss timeline snapshots); both were reproduced, fixed, and independently verified.

Unity Editor is unavailable on this machine. Two scene tests were added but could not be run; scene generation, PlayMode and actual presentation remain explicit follow-up verification, not completed checks. See `Tools/Validation/2026-10-22-wrath-boss.md` for exact evidence, commands and limitations.

## User correction — four-step ordinary sequence

On 2026-10-22 the user specified strike → strike → guard → charge. Current content and standalone template now use the distinct IDs strike, strike-2, guard, charge; both frontends show two cycles in the eight-intent forecast. Production runtime is unchanged. Updated rule/replay, shared projection and HTTP/browser assertions pass, including round 6 ordinary 43 plus ultimate 129 and round 12 charge followed by ultimate 189. Full rule failures still match the clean baseline. The existing local Web host serves the updated content; existing browser drafts/battles require loading standard content and starting a new game.
