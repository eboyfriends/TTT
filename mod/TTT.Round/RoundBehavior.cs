using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using TTT.Public.Action;
using TTT.Public.Behaviors;
using TTT.Public.Extensions;
using TTT.Public.Formatting;
using TTT.Public.Mod.Role;
using TTT.Public.Mod.Round;
using TTT.Public.Player;

namespace TTT.Round;

public class RoundBehavior(IRoleService _roleService) : IRoundService
{
    private Round? _round;
    private BasePlugin? _plugin;
    private RoundStatus _roundStatus = RoundStatus.Paused;
    private int _roundId = 1;
    
    public void Start(BasePlugin plugin)
    {
        _plugin = plugin;
        plugin.RegisterListener<Listeners.OnTick>(TickWaiting);
        plugin.AddCommandListener("jointeam", OnTeamJoin);
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(BlockDamage, HookMode.Pre);
    }

    public RoundStatus GetRoundStatus()
    {
        return _roundStatus;
    }

    public void SetRoundStatus(RoundStatus roundStatus)
    {
        switch (roundStatus)
        {
            case RoundStatus.Ended:
                ForceEnd();
                break;
            case RoundStatus.Waiting:
                _round = new Round(_roleService, null, _roundId);
                break;
            case RoundStatus.Started:
                ForceStart();
                break;
            case RoundStatus.Paused:
                WaitForPlayers();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(roundStatus), roundStatus, "Invalid round status.");
        }
        _roundStatus = roundStatus;
    }

    public void WaitForPlayers()
    {
        var players = Utilities.GetPlayers().Where(player => player.IsValid && player.PawnIsAlive).ToList();

        if (players.Count >= 2)
        {
            _round = new Round(_roleService, null, _roundId);
            Server.PrintToChatAll(StringUtils.FormatTTT("Enough players are present. Starting the round in 10 seconds."));
            
            _plugin?.AddTimer(30.0f, () =>
            {
                var currentPlayers = Utilities.GetPlayers().Where(player => player.IsValid && player.PawnIsAlive).ToList();

                if (currentPlayers.Count >= 2)
                {
                    SetRoundStatus(RoundStatus.Started);
                    return; 
                }
                else
                {
                    Server.PrintToChatAll(StringUtils.FormatTTT("Waiting for more players to join the game."));
                }
            }, TimerFlags.REPEAT);
        }
        else
        {
            _plugin?.AddTimer(30.0f, () =>
            {
                var currentPlayers = Utilities.GetPlayers().Where(player => player.IsValid && player.PawnIsAlive).ToList();
                if (currentPlayers.Count >= 2)
                {
                    SetRoundStatus(RoundStatus.Started);
                    return;
                }
                
                Server.PrintToChatAll(StringUtils.FormatTTT("Waiting for more players to join the game."));
            }, TimerFlags.REPEAT);
        }
    }
    public void TickWaiting()
    {
        if (_round == null)
        {
            _round = new Round(_roleService, null, _roundId);
            return;
        }

        if (_roundStatus != RoundStatus.Waiting) return;
        _round.Tick();

        if (_round.GraceTime() != 0) return;
        
        if (Utilities.GetPlayers().Where(player => player is { IsValid: true, PawnIsAlive: true }).ToList().Count <= 2)
        {
            Server.PrintToChatAll(StringUtils.FormatTTT("Not enough players to start the round."));
            SetRoundStatus(RoundStatus.Paused); 
            return; 
        }
        
        SetRoundStatus(RoundStatus.Started); 
    }

    public void ForceStart()
    {
        foreach (var player in Utilities.GetPlayers().Where(player => player.IsReal()).Where(player => player.IsReal()).ToList()) player.VoiceFlags = VoiceFlags.Normal;
        _round!.Start();
        ServerExtensions.GetGameRules().RoundTime = 360;
        Utilities.SetStateChanged(ServerExtensions.GetGameRulesProxy(), "CCSGameRulesProxy", "m_pGameRules");
    }

    public void ForceEnd()
    {
        if (_roundStatus == RoundStatus.Ended) return;
        _roundStatus = RoundStatus.Ended;
        Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!.TerminateRound(5,
            RoundEndReason.RoundDraw);
    }

    public void EndRound()
    {
        if (_roundStatus == RoundStatus.Started && Utilities.GetPlayers().Count(player => player.PawnIsAlive) == 1)
        {
            ForceEnd();
        }

        var traitorCount = _roleService.GetTraitors().Count(player => player.PawnIsAlive);
        var innocentCount = _roleService.GetInnocents().Count(player => player.PawnIsAlive);
        var detectiveCount = _roleService.GetDetectives().Count(player => player.PawnIsAlive);

        if (_roundStatus == RoundStatus.Started && (traitorCount == 0 || innocentCount + detectiveCount == 0))
        {
            ForceEnd();
        }
    }

    private HookResult BlockDamage(DynamicHook hook)
    {
        if (hook.GetParam<CEntityInstance>(0).DesignerName is not "player") return HookResult.Continue;
        return _roundStatus != RoundStatus.Waiting ? HookResult.Continue : HookResult.Stop;
    }

    private HookResult OnTeamJoin(CCSPlayerController? executor, CommandInfo info)
    {
        if (_roundStatus != RoundStatus.Started) return HookResult.Continue;
        if (executor == null) return HookResult.Continue;
        if (!executor.IsReal()) return HookResult.Continue;
        if (_roleService.GetRole(executor) != Role.Unassigned) return HookResult.Continue;
        Server.NextFrame(() => executor.CommitSuicide(false, true));

        return HookResult.Continue;   
    }

    private HookResult OnRoundEnd(EventRoundEnd _, GameEventInfo __)
    {
        _roundId++;   
        return HookResult.Continue;
    }
}
