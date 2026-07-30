Include ..\AGENTS.md

# Bot Storage - Powered — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `botstorage`
- **Namespace:** `Calloatti.BotStorage`
- **ModId:** `Calloatti.BotStoragePowered`
- **Framework:** Harmony, Bindito DI
- **Publicizer:** removes `Timberborn.BlueprintSystem`
- **Min Game Version:** 1.0.12.5 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Variant of Bot Storage that requires power. Adds a powered bot storage building where bots are parked and protected from deterioration. Same core as Bot Storage but with power requirements.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `BotStorage.cs` | `IModStarter` entry point, `BotStorageBuilding` component, `BotStorageBannerSetter`, `BotStorageConfigurator`, `DeteriorableTickPatch` |
