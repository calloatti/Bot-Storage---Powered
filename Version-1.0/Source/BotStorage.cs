using Bindito.Core;
using HarmonyLib;
using System.Collections.Concurrent;
using Timberborn.AssetSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Buildings;
using Timberborn.DeteriorationSystem;
using Timberborn.EnterableSystem;
using Timberborn.EntitySystem;
using Timberborn.MechanicalSystem;
using Timberborn.ModManagerScene;
using Timberborn.NeedSystem;
using Timberborn.StatusSystem;
using Timberborn.TemplateInstantiation;
using Timberborn.WorkSystem;
using UnityEngine;
using Calloatti.Config;

namespace Calloatti.BotStorage
{
  public class BotStorageModStarter : IModStarter
  {
    public static SimpleConfig Config { get; private set; }

    public void StartMod(IModEnvironment modEnvironment)
    {
      Config = new SimpleConfig(modEnvironment.ModPath);
      new Harmony("calloatti.botstorage").PatchAll();
    }
  }

  public record BotStorageBuildingSpec : ComponentSpec;

  public class BotStorageBuilding : BaseComponent, IAwakableComponent, IInitializableEntity, IDeletableEntity
  {
    private Enterable _enterable;
    private MechanicalNode _mechanicalNode;
    private PausableBuilding _pausableBuilding;

    public static readonly ConcurrentDictionary<Deteriorable, BotStorageBuilding> ProtectedBots = new();

    // Helper for the Harmony patch to grab the dynamic efficiency (0.0 to 1.0)
    public float PowerEfficiency => _mechanicalNode != null ? _mechanicalNode.PowerEfficiency : 0f;

    public void Awake()
    {
      _enterable = GetComponent<Enterable>();
      _mechanicalNode = GetComponent<MechanicalNode>();
      _pausableBuilding = GetComponent<PausableBuilding>();

      _enterable.EntererAdded += OnEntererAdded;
      _enterable.EntererRemoved += OnEntererRemoved;

      if (_pausableBuilding != null)
      {
        _pausableBuilding.PausedChanged += OnPausedChanged;
      }

      GetComponent<WorkplacePriority>()?.SetPriority(Timberborn.PrioritySystem.Priority.VeryLow);
    }

    public void DeleteEntity()
    {
      if (_enterable != null)
      {
        _enterable.EntererAdded -= OnEntererAdded;
        _enterable.EntererRemoved -= OnEntererRemoved;
      }

      if (_pausableBuilding != null)
      {
        _pausableBuilding.PausedChanged -= OnPausedChanged;
      }
    }

    private void UpdatePowerConsumption()
    {
      if (_mechanicalNode != null)
      {
        bool isPaused = _pausableBuilding != null && _pausableBuilding.Paused;
        if (isPaused)
        {
          _mechanicalNode.SetInputMultiplier(0f);
        }
        else
        {
          float powerPerBot = BotStorageModStarter.Config.GetFloat("PowerPerBot");
          float baseBlueprintPower = 10f;
          float multiplier = (powerPerBot / baseBlueprintPower) * _enterable.NumberOfEnterersInside;

          _mechanicalNode.SetInputMultiplier(multiplier);
        }
      }
    }

    private void OnEntererAdded(object sender, EntererAddedEventArgs e)
    {
      NeedManager nm = e.Enterer.GetComponent<NeedManager>();
      if (nm != null) foreach (var n in nm.NeedSpecs) nm.DisableUpdate(n.Id);

      Deteriorable deteriorable = e.Enterer.GetComponent<Deteriorable>();
      if (deteriorable != null) ProtectedBots.TryAdd(deteriorable, this);

      UpdatePowerConsumption();
    }

    private void OnEntererRemoved(object sender, EntererRemovedEventArgs e)
    {
      NeedManager nm = e.Enterer.GetComponent<NeedManager>();
      if (nm != null) foreach (var n in nm.NeedSpecs) nm.EnableUpdate(n.Id);

      Deteriorable deteriorable = e.Enterer.GetComponent<Deteriorable>();
      if (deteriorable != null) ProtectedBots.TryRemove(deteriorable, out _);

      UpdatePowerConsumption();
    }

    public void InitializeEntity()
    {
      foreach (var bot in _enterable.EnterersInside)
      {
        Deteriorable deteriorable = bot.GetComponent<Deteriorable>();
        if (deteriorable != null) ProtectedBots.TryAdd(deteriorable, this);
      }
      UpdatePowerConsumption();
    }

    private void OnPausedChanged(object sender, System.EventArgs e)
    {
      UpdatePowerConsumption();
    }
  }

  public class BotStorageBannerSetter : BaseComponent, IAwakableComponent, IFinishedStateListener, IDeletableEntity
  {
    private static readonly Color BannerIconColor = new Color(0.33f, 0.33f, 0.33f);
    private readonly IAssetLoader _assetLoader;

    private BlockObject _blockObject;
    private MeshRenderer _meshRenderer;
    private Material _cachedMaterial;

    private static Texture2D _botHeadTexture;
    private static bool _textureLoaded = false;

    private static readonly int IconColorProperty = Shader.PropertyToID("_DetailAlbedoUV2Color");
    private static readonly int TextureProperty = Shader.PropertyToID("_DetailAlbedoMap2");

    public BotStorageBannerSetter(IAssetLoader assetLoader)
    {
      _assetLoader = assetLoader;
    }

    public void Awake()
    {
      _blockObject = GetComponent<BlockObject>();
      BuildingModel component = GetComponent<BuildingModel>();

      if (!_textureLoaded)
      {
        _botHeadTexture = _assetLoader.LoadSafe<Texture2D>("Sprites/Goods/BotHeadIcon");
        _textureLoaded = true;
      }

      Transform bannerTransform = component.FinishedModel.transform.Find("BannerMesh");

      if (bannerTransform != null)
      {
        _meshRenderer = bannerTransform.GetComponent<MeshRenderer>();
      }
      else
      {
        _meshRenderer = component.FinishedModel.GetComponentInChildren<MeshRenderer>();
      }
    }

    public void OnEnterFinishedState()
    {
      if (_meshRenderer != null && _botHeadTexture != null)
      {
        if (_cachedMaterial == null)
        {
          _cachedMaterial = _meshRenderer.material;
        }

        _cachedMaterial.SetTexture(TextureProperty, _botHeadTexture);
        _cachedMaterial.SetColor(IconColorProperty, BannerIconColor);
      }
    }

    public void OnExitFinishedState() { }

    public void DeleteEntity()
    {
      if (_cachedMaterial != null)
      {
        UnityEngine.Object.Destroy(_cachedMaterial);
        _cachedMaterial = null;
      }
    }
  }

  [Context("Game")]
  public class BotStorageConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<BotStorageBuilding>().AsTransient();
      Bind<BotStorageBannerSetter>().AsTransient();
      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule()
    {
      var builder = new TemplateModule.Builder();

      builder.AddDecorator<BotStorageBuildingSpec, BotStorageBuilding>();
      builder.AddDecorator<BotStorageBuildingSpec, WaitInsideIdlyWorkplaceBehavior>();
      builder.AddDecorator<BotStorageBuildingSpec, BotStorageBannerSetter>();
      builder.AddDecorator<BotStorageBuildingSpec, PausableBuilding>();

      return builder.Build();
    }
  }

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