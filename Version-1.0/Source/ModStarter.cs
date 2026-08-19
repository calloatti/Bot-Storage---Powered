using HarmonyLib;
using Timberborn.ModManagerScene;
using Calloatti.Config;
using UnityEngine;

namespace Calloatti.BotStorage
{
  public class BotStorageModStarter : IModStarter
  {
    public static SimpleConfig Config { get; private set; }

    public void StartMod(IModEnvironment modEnvironment)
    {
      Config = new SimpleConfig(modEnvironment.ModPath);
      new Harmony("Calloatti.BotStoragePowered").PatchAll();
    }
  }
}
