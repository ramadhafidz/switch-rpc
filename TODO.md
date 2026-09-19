# TODO

This file tracks granular, actionable tasks for **SWITCH RPC**.
For the high-level project vision, phase plan, and long-term milestones, see `docs/ROADMAP.md`.

## 🏃 Active Sprint

Post-migration hardening on `main`, following the Phase 2 .NET merge described in `docs/ROADMAP.md`.

### Discord RPC
- [ ] Finalize the Discord RPC UI layout.
- [ ] Refine Pokédex progress presentation.
- [ ] Display human-readable location data in RPC.
- [ ] Ensure RPC is reliably cleared when Eden/game closes.
- [ ] Decide whether party/box information should be exposed in RPC.
- [ ] Avoid unnecessary RPC updates when GameState has not meaningfully changed.

### GameState
- [ ] Define the final GameState fields required by Discord RPC.
- [ ] Add missing state fields needed by supported games.
- [ ] Handle optional and unavailable GameState fields consistently.

### Pokédex
- [ ] Implement correct regional dex totals for supported games.
- [ ] Handle DLC/local dex accurately.
- [ ] Prevent counting species that do not belong to the selected Pokédex.
- [ ] Verify Pokédex counts against known save files.
- [ ] Decide how multiple regional dexes should rotate in RPC.

---

## 🎮 Game Support

### Pokémon Violet
- [ ] Obtain and test with a valid Violet save file.
- [ ] Verify Eden save path and title ID.
- [ ] Verify trainer data and playtime extraction.
- [ ] Verify Pokédex extraction.
- [ ] Verify location extraction.
- [ ] Verify party and box extraction.
- [ ] Test end-to-end GameState and RPC behavior.

### Pokémon Scarlet
- [ ] Refine base and DLC Pokédex representation.
- [ ] Integrate party data into RPC if required.
- [ ] Integrate rich location names into RPC.
- [ ] Verify behavior with multiple save states.

### Pokémon Legends: Arceus
- [ ] Extract current location/map.
- [ ] Extract research level and perfect Pokédex entries.
- [ ] Extract party Pokémon.
- [ ] Extract active mission/progression.
- [ ] Verify extracted values against known save data.

### Pokémon Legends: Z-A
- [ ] Verify Eden support and title ID upon release.
- [ ] Verify the actual save format.
- [ ] Verify PKHeX support.
- [ ] Implement the C# save reader.
- [ ] Define supported GameState fields.
- [ ] Test end-to-end behavior.

---

## 🧱 Save Reader

- [ ] Improve save candidate resolution.
- [ ] Handle locked/inaccessible save files gracefully.
- [ ] Handle unsupported save formats explicitly.
- [ ] Add regression fixtures for known save files.
- [ ] Keep game-specific extraction isolated from shared infrastructure.

---

## 🏗️ Architecture & Tech Debt

### Game Registry
*Groundwork for the universal emulator/game architecture (ROADMAP Phases 3–4).*

- [ ] Move game detection rules into a centralized `GameDefinition` registry.
- [ ] Centralize artwork and region configurations per game.
- [ ] Define game-specific capabilities in `GameDefinition`.

### Performance
- [ ] Optimize the main application loop while Eden is not running.
- [ ] Avoid unnecessary save reads.
- [ ] Avoid unnecessary Discord RPC updates.
- [ ] Review save refresh and Pokédex rotation intervals.

### Logging
- [ ] Introduce structured application logging.
- [ ] Separate debug logs from user-facing information.
- [ ] Improve error messages for save-reader failures.
- [ ] Remove temporary debug output from production code.

---

## 🧪 Testing

- [x] Reader and formatter unit tests (xUnit) for blank PKHeX saves.
- [x] Save locator tests with an injected root.
- [x] Facade behavior for unidentifiable and unsupported files.
- [ ] Add regression tests with more real save files (Violet, Z-A).
- [ ] Test Eden start/stop behavior.
- [x] Test Discord RPC connection failure/recovery (manual).
- [ ] Test unavailable/locked save files.

---

## 🛠️ Developer Tooling

- [ ] Add CI checks running `dotnet build` and `dotnet test`.
- [ ] Packaging: single-file executable build.
- [x] Document the recommended development workflow.

---

## ⚙️ Configuration

- [ ] Validate configuration values at startup.
- [ ] Handle missing or invalid configuration gracefully.
- [ ] Document configurable RPC behavior.
- [ ] Define sensible limits for refresh intervals.