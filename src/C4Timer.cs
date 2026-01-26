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

[PluginMetadata(Id = "C4Timer", Version = "1.0.2", Name = "C4Timer-SwiftlyCS2", Author = "Yeezy", Description = "C4Timer")]
public partial class C4Timer : BasePlugin
{
  private float _bombPlantedTime = float.NaN;
  private CPlantedC4? _plantedBomb = null;
  private readonly IOptionsMonitor<Config> _config;
  private bool _showTimer = false;

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
        if (bomb == null || !bomb.IsValid)
            return HookResult.Continue;

        _plantedBomb = bomb;
        _bombPlantedTime = Core.Engine.GlobalVars.CurrentTime;
        _showTimer = true;

        @event.DontBroadcast = true;
        PlayBombPlantedSound();

        return HookResult.Continue;
    }


    [GameEventHandler(HookMode.Pre)]

  public HookResult OnRoundEnd(EventRoundEnd @event)
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
        if (!_showTimer ||
            _plantedBomb == null ||
            !_plantedBomb.IsValid ||
            float.IsNaN(_bombPlantedTime))
        {
            return;
        }

        float currentTime = Core.Engine.GlobalVars.CurrentTime;
        var DefaultBombTimer = Core.ConVar.Find<int>("mp_c4timer").Value;
        float remainingTime = DefaultBombTimer - (currentTime - _bombPlantedTime);

        string html;

        if (remainingTime <= 0f)
        {
            html = """
        <font size='36' color='#ff3333'><b BOOM </b></font>
        """;
            _showTimer = false;
        }
        else
        {
            html = $"""
        <font size='38' color='#ff4444'><b>C4 PLANTED</b></font><br>
        <font size='26' color='#ffffff'>
        Time Left: {remainingTime:F2}s
        </font>
        """;
        }

        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (!player.IsValid || player.IsFakeClient)
                continue;

            player.SendCenterHTMLAsync(html, 0);
        }
    }


    private void CleanupBombTimer()
    {
        _showTimer = false;

        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (!player.IsValid || player.IsFakeClient)
                continue;

            player.SendCenterHTMLAsync("", 1);
        }
    }

    public override void Unload()
  {
    CleanupBombTimer();
    Console.WriteLine("[Bomb Timer] Unloaded.");
  }
}
