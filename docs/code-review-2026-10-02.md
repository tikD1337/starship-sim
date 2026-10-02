# Code review: bug findings (2 October 2026)

Reviewed commit: [`8bf7107`](https://github.com/tikD1337/starship-sim/tree/8bf7107a02637f47b8861a02467293d7a1971781). All line links below point to that commit.

Scope: `game/src/**` (about 9,900 non-empty lines of C#), `game/tests/**`, `.github/workflows/bench.yml`.

How the findings were verified:

- Every finding was confirmed by reading the code paths involved.
- Finding 1 was also checked numerically (see below).
- The project was **not** built or run during the review (no .NET SDK on the reviewing machine), so nothing here is confirmed at runtime.
- A first pass with local LLMs (qwen3-coder:30b and others) was used only to generate candidates. None of their candidates survived verification; the findings below come from manual reading.

Each finding is also filed as an issue (#23–#31). Each issue has a "🤖 Для ИИ" section saying whether the fix is suitable for an AI model.

## Summary

| # | Finding | Severity | Issue |
|---|---|---|---|
| 1 | File replays are not step-exact: event times are rounded to 0.01 s | Medium | [#23](https://github.com/tikD1337/starship-sim/issues/23) |
| 2 | Ship dry-mass dispersion is lost/corrupted after satellite release | Low–medium | [#24](https://github.com/tikD1337/starship-sim/issues/24) |
| 3 | "случайно" (random) scenario does not enable anomalies when they are off | Low (check intent) | [#25](https://github.com/tikD1337/starship-sim/issues/25) |
| 4 | `Sim.Reset` does not reset `LastStep` | Low | [#26](https://github.com/tikD1337/starship-sim/issues/26) |
| 5 | Goal time note can read "T+01:60.0" | Low (cosmetic) | [#27](https://github.com/tikD1337/starship-sim/issues/27) |
| 6 | Auto-released satellites all spawn at the same bay position | Low (cosmetic) | [#28](https://github.com/tikD1337/starship-sim/issues/28) |
| 7 | `Look.Binds` grows for the whole session | Low | [#29](https://github.com/tikD1337/starship-sim/issues/29) |
| 8 | `bench.yml` interpolates the branch name into bash | Low (hardening) | [#30](https://github.com/tikD1337/starship-sim/issues/30) |
| 9 | Mixture ratio `3.6` duplicated in `Tank.cs` instead of `Pump.MR` | Low (maintainability) | [#31](https://github.com/tikD1337/starship-sim/issues/31) |

---

## 1. File replays are not step-exact: event times are rounded to 0.01 s

**Where:** [`Replay.Text()`, Replay.cs:62](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/UiLogic/Replay.cs#L62) and [`Replay.Apply()`, Replay.cs:93](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/UiLogic/Replay.cs#L93)

**What is wrong:** `Text()` writes event times with `{0:F2}`. `sim.T` is accumulated as `T = -10; T += 0.01` and is almost never an exact two-decimal number. For example, after 1,001 steps it is `0.009999999999831111`.

`F2` writes `0.01`, which is *greater* than the `sim.T` at which the event was recorded. On playback, `Apply()` uses `Ev[_at].T <= sim.T`, so the event fires one step later than in the live flight.

In-memory replays are bit-exact; replays that go through the file (F9 → F10, and the automatic save after a flight) are not. This contradicts the bug-report template, which says a replay "воспроизводит его до шага" (reproduces the flight to the step). Affected events include manual axes, `sep`, and `warp`; a late `warp` also changes the step size.

**Evidence:** over 29,000 accumulated steps from −10, `double.Parse(T.ToString("F2")) > T` holds for 62 % of the steps.

The test "положение до бита" ([UiTests.cs:353](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/tests/SimTests/UiTests.cs#L353)) replays from memory, without `Text()`/`FromText()`, so it cannot catch this.

**Suggested fix:**

- Write times round-trippably: `"{0:F2} {1} {2:R}"` → `"{0:R} {1} {2:R}"`. `ReplayText.Parse` already accepts any double. Storing the integer step index is an alternative.
- Add a variant of the existing replay test that goes through `Replay.FromText(rec.Text().Split('\n'), keys)`.

## 2. Ship dry-mass dispersion is lost or corrupted after satellite release

**Where:**

- [Vehicle.cs:68](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Vehicle.cs#L68): `Dry = Spec.Dry + payload` for the ship.
- [Dispersion.cs:30](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Dispersion.cs#L30): `v.Dry *= DryK[i]` scales the payload too.
- [Sim.cs:83](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Sim.cs#L83): `v.Dry = Math.Max(v.Dry - b.SatM, Spec.Of(Kind.Ship).Dry)` uses an undispersed floor.

**What is wrong:** after all satellites are released (`orbital`/`high` missions with `--disp`):

- **`DryK < 1`:** `Dry` hits the floor `Spec.Dry`. The ship lands heavier than its dispersed dry mass by `Spec.Dry·(1−DryK)`, so the dispersion is erased.
- **`DryK > 1`:** a phantom payload of `payload·(DryK−1)` remains. `Cm` and `OwnInertia` compute `pay = Dry − Spec.Dry` ([Vehicle.cs:120](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Vehicle.cs#L120), [:157](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Vehicle.cs#L157)) and place that mass in the bay, so the centre of mass shifts as well.

**Suggested fix:** disperse only the structure. Keep the structural mass separately (e.g. `DryBase = Spec.Dry * DryK`), and compute the payload as `Dry − DryBase` in `DeploySat`, `Cm` and `OwnInertia`.

Acceptance check: after all satellites are released, `Dry == Spec.Dry * DryK` and the payload is 0 for any `DryK`. Per CONTRIBUTING, attach a `SimCheck` summary before and after.

## 3. "случайно" (random) scenario does not enable anomalies when they are off

**Where:** [Main.cs:85](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Game/Main.cs#L85) and [MissionView.cs:159](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Game/Ui/MissionView.cs#L159)

**What is wrong:** the scenario callback starts the flight with `anom = key != null || _anomOn`. For "случайно" the key is `null`, so with anomalies off a clean flight starts. The records screen describes this entry as «набор неполадок наугад» (a random set of failures), and every other scenario entry does enable anomalies.

Related UI inconsistencies:

- The "случайно" tab is highlighted whenever no script is selected, even with anomalies off.
- After F7 turns anomalies off, a previously selected scenario stays highlighted.

**Suggested fix:** if "random" is meant to enable random anomalies, pass `true` instead of `key != null || _anomOn`. Otherwise, fix the highlighting.

## 4. `Sim.Reset` does not reset `LastStep`

**Where:** [Sim.cs:180](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Sim.cs#L180) (`Reset`) and [Sim.cs:314-319](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Sim.cs#L314-L319) (`Step`)

**What is wrong:** when the step size changes, `Step` corrects velocities by `g·(LastStep − h)/2`. `Reset` leaves `LastStep` from the previous flight. If that flight ended on the coarse orbital step (0.1 s), the first step of the new flight pushes the booster on the pad downward by about 0.44 m/s.

Today the pad clamp ([Flight.cs:165-172](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Flight.cs#L165-L172)) cancels the effect, but it is an avoidable source of last-bit divergence. `SeekTo` calls `Reset` in the middle of a flight when rewinding.

**Suggested fix:** add `sim.LastStep = 0;` in `Reset` next to `sim.T = -10;`.

## 5. Goal time note can read "T+01:60.0"

**Where:** [Mission.cs:120](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Game/Mission.cs#L120)

**What is wrong:** the note is built as `$"T+{(int)(sim.T / 60):00}:{sim.T % 60:00.0}"`. For `sim.T = 119.97` the seconds round to `60.0` without carrying into the minutes. The decimal separator also depends on the OS culture, while the rest of the console always uses a comma.

**Suggested fix:** use `g.Note = NumFmt.Clock(sim.T);`, which already rounds to tenths with a carry.

## 6. Auto-released satellites all spawn at the same bay position

**Where:** [Sim.cs:74-80](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Sim.cs#L74-L80)

**What is wrong:** `DeploySat` spreads satellites along the bay and varies their spin using the loop index `i` (`i % 5`, `i % 3`). `DeployStep` releases them one at a time (`DeploySat(sim, v, 1, true)`), so `i` is always 0: every satellite gets the same spot and the same spin. Satellites are spread only when the `M` key releases 10 at once.

**Suggested fix:** use the satellite's overall index `b.Out.Count` instead of `i`.

## 7. `Look.Binds` grows for the whole session

**Where:** [`Look.Bind`, Look.cs:62-66](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Game/Ui/Look.cs#L62-L66); dead entries are pruned only in `SetTheme` ([Look.cs:56-59](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Game/Ui/Look.cs#L56-L59))

**What is wrong:** every coloured caption adds an entry. `MissionView.Reset`, `ToggleRecords` and `ShowFinal` create tens to hundreds of captions per flight or per opening of the records screen. The colour closures hold strong references (e.g. the `Mission` in `ShowFinal`), so memory grows slowly over a long session.

**Suggested fix:** prune dead entries in `Bind` as well (e.g. every N additions), or remove the entry on the node's `TreeExiting`.

## 8. `bench.yml` interpolates the branch name into bash

**Where:** [bench.yml:84](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/.github/workflows/bench.yml#L84)

**What is wrong:** `--head "${{ github.ref_name }}"` is expanded inside a `run:` script. Git allows `$(…)` in branch names, and such a name would execute in a step that has `GH_TOKEN` with `pull-requests: write`. The workflow runs only on `push`, so an attacker would already need write access. This is hardening rather than an open hole.

**Suggested fix:**

```yaml
      - env:
          BRANCH: ${{ github.ref_name }}
        run: |
          pr=$(gh pr list -R "$REPO" --head "$BRANCH" --state open --json number -q '.[0].number')
```

## 9. Mixture ratio `3.6` duplicated in `Tank.cs` instead of `Pump.MR`

**Where:** [Tank.cs:12-13](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Tank.cs#L12-L13) and [:35](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Sim/Tank.cs#L35)

**What is wrong:** the literal `3.6` is hard-coded although [`Pump.MR = 3.6`](https://github.com/tikD1337/starship-sim/blob/8bf7107a02637f47b8861a02467293d7a1971781/game/src/Engines/Pump.cs#L7) exists. The values agree today, but changing `Pump.MR` would leave the tank volumes and the pressurant split on the old ratio.

**Suggested fix:** replace the literal with `Pump.MR`.

---

## Reviewed and found correct

These were read closely, and suspicions raised during the review (several by local LLMs) turned out to be wrong:

- **`Vehicle`:** shell, propellant-column and payload inertia formulas (`m(R²/2 + L²/12)` for a thin shell, `m(R²/4 + L²/12)` for a solid cylinder), the centre of mass, and `AngDiff` with C#'s signed `%`.
- **`Sim.Step`:** the sign of the half-step velocity correction is consistent with the symplectic-Euler update in `Flight.StepVehicle`.
- **`Guidance.DeorbitDv`:** the bisection direction is correct (periapsis above target → need more Δv → `lo = mid`).
- **`Guide`:** `LatePoll`/`Divert` do not log repeatedly, because `Catch` turns false once `Site == "sea"`.
- **`Sim.Shift` and the separation logic:** sign conventions and the booster/ship indices are correct.
- **`Anomalies`:** engine indices are never negative (`k ∈ [0, 9]`).
- **`ReplayFiles` and `Mission`:** the `FileAccess.Open` null checks are present.
- **`ParamDefs`:** the zones and bands for `LowIsBad`, and `Split` (every group has ≥ 2 sections, so the `Math.Clamp` bounds are valid).
- **`TeleLog.Clear`:** resets `_next`/`_last`.
- **`bench.py`:** the return code is honoured and the regression direction in `compare()` is correct.

## Not covered

- `Socp.cs` and `Gfold.cs` were not reviewed in depth; they are covered by `GfoldTests`.
- Godot view code (`StackView`, `CamRig`, `SkyEarth`, effects) got only spot checks.
- No runtime testing was done.
