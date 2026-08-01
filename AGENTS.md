Include ..\AGENTS.md

# Bot Storage - Powered — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `botstorage`
- **Namespace:** `Calloatti.BotStorage`
- **ModId:** `Calloatti.BotStoragePowered`
- **Framework:** Harmony, Bindito DI, SimpleConfig
- **Publicizer:** removes `Timberborn.BlueprintSystem` and `Timberborn.Buildings` (see csproj — `PausableBuilding.PausedChanged` becomes ambiguous if publicized)
- **Min Game Version:** 1.0.12.5 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Variant of Bot Storage that requires power. Adds a powered bot storage building where bots are parked and protected from deterioration. Same core as Bot Storage but with power requirements. Note: this file is documentation for AI agents, not a public API reference.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `BotStorage.cs` | `IModStarter` entry point, `BotStorageBuilding` component, `BotStorageBannerSetter`, `BotStorageConfigurator`, `PreventUnstaffedStatusPatch`, `DeteriorableTickPatch` |

## Classes in `BotStorage.cs`

### `BotStorageModStarter : IModStarter`
Entry point. Loads `SimpleConfig` from the mod path into `static Config`, runs `new Harmony("calloatti.botstorage").PatchAll()`.

### `BotStorageBuilding : BaseComponent, IAwakableComponent, IInitializableEntity, IDeletableEntity`
Core component. Purely event-driven — no `IUpdatableComponent`/`TickableComponent`, so no per-frame or per-tick work.
- `public static ConcurrentDictionary<Deteriorable, BotStorageBuilding> ProtectedBots` — O(1) map of protected bots to their storage building (the building value is needed for its `PowerEfficiency`).
- `PowerEfficiency` — proxies `MechanicalNode.PowerEfficiency` (0.0–1.0), read by the `DeteriorableTickPatch`.
- `Awake()`: gets `Enterable`, `MechanicalNode`, `PausableBuilding`; subscribes `EntererAdded`/`EntererRemoved` and `PausableBuilding.PausedChanged`; sets `WorkplacePriority` to `VeryLow`. No reflection.
- `OnEntererAdded`: disables the bot's `NeedManager` needs; adds bot to `ProtectedBots`; recomputes power.
- `OnEntererRemoved`: re-enables needs; removes from `ProtectedBots`; recomputes power.
- `InitializeEntity()` (fires once on placement and on save load): populates `ProtectedBots` from `EnterersInside` (safety net for bots loaded on map load) and does the initial power computation. The `Awake` event subscription is the primary path (`Enterer` resolving its loaded state → `Enter` → `Add` fires `EntererAdded` on load too). `IInitializableEntity` is the 1.1-compatible replacement for the removed `IStartableComponent`.
- `OnPausedChanged`: recomputes power (paused → `SetInputMultiplier(0f)`).
- `UpdatePowerConsumption()`: sets `MechanicalNode` input multiplier to `(PowerPerBot / 10) * NumberOfEnterersInside`, or `0` when paused. Re-run on every enter/leave, pause toggle, and at `InitializeEntity()` — these are the only state changes that affect power, so no per-frame polling is needed.
- `DeleteEntity()`: unsubscribes all events.

### `BotStorageBannerSetter : BaseComponent, IAwakableComponent, IFinishedStateListener, IDeletableEntity`
Sets bot-head texture and icon color on the building banner. Loads texture once (static), caches material, destroys it on `DeleteEntity`.

### `BotStorageConfigurator : Configurator`
Bindito config. `[Context("Game")]`. Binds `BotStorageBuilding` and `BotStorageBannerSetter` transient; registers `TemplateModule` decorators for `BotStorageBuildingSpec`: `BotStorageBuilding`, `WaitInsideIdlyWorkplaceBehavior`, `BotStorageBannerSetter`, `PausableBuilding`.

### `PreventUnstaffedStatusPatch : HarmonyPatch(StatusSubject, RegisterStatus)`
Prefix returning `false` (suppresses status) when the subject is a `BotStorageBuilding` and the status sprite name contains `"NoUnemployed"`. Prevents the "unstaffed" warning on this building.

### `DeteriorableTickPatch : HarmonyPatch(Deteriorable, Tick)`
Prefix: when the deteriorating bot is in `ProtectedBots`, it has a `PowerEfficiency` chance to skip the tick (`Random.value < storage.PowerEfficiency`). So stored bots deteriorate only in proportion to the power shortfall — not fully protected unless the building has full power. This is the mod's core feature and the reason `ProtectedBots` maps to the `BotStorageBuilding` (for its efficiency).

## Performance Characteristics (important for optimization work)
- `BotStorageBuilding` is **purely event-driven** — it does not implement `IUpdatableComponent` or `TickableComponent`, so there is **no per-frame and no per-tick work** for the mod's own components. Cost is zero except when a bot enters/leaves or the building is paused/unpaused (event handlers update needs, `ProtectedBots`, and power).
- The `InitializeEntity()` one-time population scan and the `Awake()` event subscriptions are the only load-time costs.
- `Deteriorable.Tick` patch runs once per game **tick** per deteriorating bot. Cost is a `TryGetValue` on a `ConcurrentDictionary` plus a `Random.value` compare (powered variant only).
- `ProtectedBots` is a static `ConcurrentDictionary`; entries are added/removed strictly via `EntererAdded`/`EntererRemoved`, so there is no drift or leak.
- `SimpleConfig` (`PowerPerBot`) is read inside `UpdatePowerConsumption`, which runs only on events — never in a hot loop.
