﻿using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using TTT.Public.Behaviors;
using TTT.Public.Mod.Role;
using TTT.Public.Player;
using TTT.Round;

namespace TTT.Player;

public class PlayerBehavior() : IPlayerService, IPluginBehavior
{
    
    public void Start(BasePlugin plugin)
    {
        
    }
    
    [GameEventHandler]
    public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (@event.Userid == null) throw new NullReferenceException("Could not find player object when connecting.");
        CreatePlayer(@event.Userid);
        return HookResult.Continue;
    }
    
    private readonly Dictionary<CCSPlayerController, GamePlayer> _players = [];
    
    public Dictionary<CCSPlayerController, GamePlayer> GetPlayers()
    {
        return _players;
    }
    
    public void CreatePlayer(CCSPlayerController player)
    {
        if (_players.ContainsKey(player) || player.UserId == null) return;
        _players.Add(player, new GamePlayer(Role.Unassigned, 0, 110, player.UserId.Value));
    }
    
    public void AddKarma(CCSPlayerController player, int karma)
    {
        if (!_players.TryGetValue(player, out var value)) return;
        if (karma < 0) return;
        
        if (karma + value.Karma() > 110)
            value.SetKarma(110);
        else
            value.AddKarma(karma);
    }
    
    public void RemoveKarma(CCSPlayerController player, int karma)
    {
        if (!_players.TryGetValue(player, out var value)) return;
        if (karma < 0) return;
        
        if (value.Karma() - karma < 40) {
            Server.ExecuteCommand($"css_ban #{player.UserId} 1440 Karma too low"); // handle this shit here let GamePlayer be direct.
            value.SetKarma(40);
        } else {
            value.RemoveKarma(karma);
        }
    }

    public List<GamePlayer> Players()
    {
        return [.. _players.Values];
    }

    public GamePlayer GetPlayer(CCSPlayerController player)
    {
        if (player == null || _players == null)
        {
            throw new ArgumentNullException(nameof(player), "Player or Players dictionary cannot be null");
        }

        if (_players.TryGetValue(player, out var gamePlayer))
        {
            return gamePlayer;
        }

        return null;
    }

    public void RemovePlayer(CCSPlayerController player)
    {
        _players.Remove(player);
    }

    public void Clr()
    {
        foreach (var player in Players())
        {
            player.SetKiller(null);
            player.SetPlayerRole(Role.Unassigned);
            player.ResetCredits();
            player.ModifyKarma();
            player.SetFound(false);
            player.SetDead();
        }
    }
}