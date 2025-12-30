using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Events;
using Microsoft.Extensions.Configuration;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Sounds;
using SwiftlyS2.Shared.Menus;
using Microsoft.Extensions.Options;

namespace C4Timer;

[PluginMetadata(Id = "C4Timer", Version = "1.0.0", Name = "C4Timer-SwiftlyCS2", Author = "Yeezy", Description = "C4Timer")]
public partial class C4Timer : BasePlugin
{
  private float _bombPlantedTime = float.NaN;
  private CPlantedC4? _plantedBomb = null;
  private readonly IOptionsMonitor<Config> _config;
  // Menu references
  private TextMenuOption _timerOption = null!;
  private IMenuAPI _bombTimerMenu = null!;

  public C4Timer(ISwiftlyCore core) : base(core)
  {
    Core.Configuration
                .InitializeJsonWithModel<Config>("config.jsonc", "C4Timer")
                .Configure(builder =>
                {
                  builder.AddJsonFile("config.jsonc", optional: true, reloadOnChange: true);
                });

    var services = new ServiceCollection();
    services.AddSwiftly(Core)
            .AddOptionsWithValidateOnStart<Config>()
            .BindConfiguration("C4Timer");

    var serviceProvider = services.BuildServiceProvider();
    _config = serviceProvider.GetRequiredService<IOptionsMonitor<Config>>();
  }

  public override void Load(bool hotReload)
  {
    Console.WriteLine("[Bomb Timer] Loaded successfully.");
  }

  [GameEventHandler(HookMode.Pre)]
  public HookResult OnBombPlanted(EventBombPlanted @event)
  {
    if (!_config.CurrentValue.Timer) 
            return HookResult.Continue;

    var bomb = FindPlantedBomb();
    if (bomb == null || !bomb.IsValid) return HookResult.Continue;

    _plantedBomb = bomb;
    _bombPlantedTime = Core.Engine.GlobalVars.CurrentTime;

    @event.DontBroadcast = true;
    CreateBombTimerMenu();
    PlayBombPlantedSound();

    return HookResult.Continue;
  }

  [GameEventHandler(HookMode.Pre)]

  public HookResult OnRoundStart(EventRoundEnd @event)
  {
    CleanupBombTimer();
    ResetBombState();
    return HookResult.Continue;
  }

  private CPlantedC4? FindPlantedBomb()
  {
    var bombs = Core.EntitySystem.GetAllEntitiesByDesignerName<CPlantedC4>("planted_c4");
    return bombs.FirstOrDefault(b => b.IsValid);
  }

  private void ResetBombState()
  {
    _bombPlantedTime = float.NaN;
    _plantedBomb = null;
  }

  private void CreateBombTimerMenu()
  {
    // Close any existing menu
    if (_bombTimerMenu != null)
    {
      Core.MenusAPI.CloseMenu(_bombTimerMenu);
    }

    _timerOption = new TextMenuOption("C4 Time: 40.00s")
    {
      TextSize = MenuOptionTextSize.Medium,
      PlaySound = false
    };

    _bombTimerMenu = Core.MenusAPI.CreateBuilder()
        .Design.SetMenuTitle("Bomb Timer")
        .DisableExit()
        .DisableSound()
        .AddOption(_timerOption)
        .Build();

    _bombTimerMenu.Configuration.HideFooter = true;
    _bombTimerMenu.DefaultComment = "";
    Core.MenusAPI.OpenMenu(_bombTimerMenu); // Shows to all players
  }

  private void PlayBombPlantedSound()
  {
    using var soundEvent = new SoundEvent()
    {
      Name = "Event.BombPlanted" // Correct and reliable CS2 sound
    };

    soundEvent.Recipients.AddAllPlayers();
    soundEvent.Emit();
  }

  [EventListener<EventDelegates.OnTick>]
  public void UpdateBombTimer()
  {
    // Full safety check — prevents any crash
    if (_plantedBomb == null || !_plantedBomb.IsValid ||
        float.IsNaN(_bombPlantedTime) ||
        _timerOption == null ||
        _bombTimerMenu == null)
    {
      return;
    }

    float currentTime = Core.Engine.GlobalVars.CurrentTime;
    float remainingTime = _plantedBomb.TimerLength - (currentTime - _bombPlantedTime);

    if (remainingTime <= 0f)
    {
      _timerOption.Text = "C4 Time: BOOM!";
      return;
    }

    _timerOption.Text = $"C4 Time: {remainingTime:F2}s";
  }

  private void CleanupBombTimer()
  {
    if (_bombTimerMenu != null)
    {
      try
      {
        Core.MenusAPI.CloseMenu(_bombTimerMenu);
      }
      catch { /* ignore if already closed */ }

      Core.MenusAPI.CloseMenu(_bombTimerMenu);

      _bombTimerMenu = null;
    }

    _timerOption = null!;
  }

  public override void Unload()
  {
    CleanupBombTimer();
    Console.WriteLine("[Bomb Timer] Unloaded.");
  }
}
