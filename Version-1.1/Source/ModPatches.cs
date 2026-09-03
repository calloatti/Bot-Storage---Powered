using HarmonyLib;
using Timberborn.DeteriorationSystem;
using Timberborn.StatusSystem;

namespace Calloatti.BotStorage
{
  [HarmonyPatch(typeof(StatusSubject), nameof(StatusSubject.RegisterStatus))]
  public static class PreventUnstaffedStatusPatch
  {
    public static bool Prefix(StatusSubject __instance, StatusToggle statusToggle)
    {
      if (__instance.GetComponent<BotStorageBuilding>() != null)
      {
        string spriteName = statusToggle.StatusSpecification.SpriteName ?? "";

        if (spriteName.Contains("NoUnemployed"))
        {
          return false;
        }
      }
      return true;
    }
  }

  [HarmonyPatch(typeof(Deteriorable), nameof(Deteriorable.Tick))]
  public static class DeteriorableTickPatch
  {
    public static bool Prefix(Deteriorable __instance)
    {
      if (BotStorageBuilding.ProtectedBots.TryGetValue(__instance, out var storage))
      {
        // Generates a float between 0.0 and 1.0. 
        // If efficiency is 0.75, there is a 75% chance to skip deterioration this tick.
        if (UnityEngine.Random.value < storage.PowerEfficiency)
        {
          return false; // Skip the tick (no deterioration)
        }
      }

      return true; // Let vanilla deterioration happen
    }
  }
}
