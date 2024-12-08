/*           ________               __
           / ____/ /_  ____  _____/ /___  __
          / / __/ __ \/ __ \/ ___/ __/ / / /
         / /_/ / / / / /_/ (__  ) /_/ /_/ /
         \____/_/ /_/\____/____/\__/\__, /
                                   /____/

This plugin is exclusively licensed to Enchanted.gg and may not be edited or sold without explicit permission.

© 2024 Ghosty & Enchanted.gg
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Gamble", "Ghosty", "1.1.0")]
    public class Gamble : RustPlugin
    {
        private ConfigData _config;
        private bool _roundInProgress = false;
        private Dictionary<string, int> _participants = new Dictionary<string, int>();
        private List<string> _participantOrder = new List<string>();
        private Timer _countdownTimer = null;
        private Timer _autoStartTimer = null;
        private System.Random _random = new System.Random();
        private float _timeLeft;

        private class ConfigData
        {
            public float CountdownTime { get; set; } = 600f;
            public string CurrencyShortName { get; set; } = "scrap";
            public bool BroadcastToAll { get; set; } = true;
            public string BroadcastPrefix { get; set; } = "<size=16>[ Gamble <size=9>By Ghosty</size> ]</size>\n\n• ";
            public float AutoStartInterval { get; set; } = 3600f;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigData();
            SaveConfig(_config);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                PrintError("Invalid configuration file, creating a new one!");
                LoadDefaultConfig();
            }
            SaveConfig(_config);
        }

        protected override void SaveConfig() => SaveConfig(_config);
        private void SaveConfig(ConfigData config) => Config.WriteObject(config);

        private void OnServerInitialized()
        {
            _roundInProgress = false;
            _participants.Clear();
            _participantOrder.Clear();
            if (_countdownTimer != null && !_countdownTimer.Destroyed) _countdownTimer.Destroy();
            if (_autoStartTimer != null && !_autoStartTimer.Destroyed) _autoStartTimer.Destroy();
            _autoStartTimer = timer.Every(_config.AutoStartInterval, CheckAndStartRoundAutomatically);
        }

        [ChatCommand("gamble")]
        private void GambleCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                SendReply(player, $"{_config.BroadcastPrefix} Usage: /gamble <amount>");
                return;
            }

            if (!int.TryParse(args[0], out int amount) || amount <= 0)
            {
                SendReply(player, $"{_config.BroadcastPrefix} Please enter a valid positive amount of scrap.");
                return;
            }

            var definition = ItemManager.FindItemDefinition(_config.CurrencyShortName);
            if (definition == null)
            {
                SendReply(player, $"{_config.BroadcastPrefix} Invalid currency configured.");
                return;
            }

            int playerScrap = (int)player.inventory.GetAmount(definition.itemid);
            if (playerScrap < amount)
            {
                SendReply(player, $"{_config.BroadcastPrefix} You do not have enough {_config.CurrencyShortName} to join the gamble.");
                return;
            }

            int removed = RemoveItems(player, _config.CurrencyShortName, amount);
            if (removed < amount)
            {
                SendReply(player, $"{_config.BroadcastPrefix} Failed to remove required scrap from your inventory. Please try again.");
                return;
            }

            AddParticipant(player.UserIDString, amount);
            int totalPot = GetTotalPot();
            float playerChance = ((float)_participants[player.UserIDString] / (float)totalPot) * 100f;
            SendReply(player, $"{_config.BroadcastPrefix} You have entered the gamble with {amount} scrap. Your current chance of winning: {playerChance:0.00}%");

            if (!_roundInProgress) StartCountdown();
        }

        private void CheckAndStartRoundAutomatically()
        {
            if (!_roundInProgress) StartCountdown();
        }

        private void AddParticipant(string playerId, int amount)
        {
            if (!_participants.ContainsKey(playerId))
            {
                _participants[playerId] = amount;
                _participantOrder.Add(playerId);
            }
            else
            {
                _participants[playerId] += amount;
            }
        }

        private void StartCountdown()
        {
            _roundInProgress = true;
            _timeLeft = _config.CountdownTime;
            BroadcastMessage($"A new gambling round has started! Use /gamble <amount> to join. Winner will be chosen in {FormatTime(_config.CountdownTime)}.");
            _countdownTimer = timer.Every(1f, OnCountdownTick);
        }

        private void OnCountdownTick()
        {
            _timeLeft -= 1f;
            if (_timeLeft <= 0)
            {
                _countdownTimer.Destroy();
                EndRound();
                return;
            }

            if (_timeLeft > 60 && (_timeLeft % 60 == 0))
            {
                int minutesLeft = Mathf.FloorToInt(_timeLeft / 60f);
                BroadcastMessage($"Gamble ends in {minutesLeft} {(minutesLeft > 1 ? "minutes" : "minute")}! Use /gamble <amount> to join!");
            }
            else if (_timeLeft <= 60 && _timeLeft > 10 && (_timeLeft % 10 == 0))
            {
                BroadcastMessage($"Gamble ends in {_timeLeft} seconds! Use /gamble <amount> to join!");
            }
            else if (_timeLeft <= 10)
            {
                BroadcastMessage($"Gamble ends in {_timeLeft} {( _timeLeft == 1 ? "second" : "seconds")}! Use /gamble <amount> to join!");
            }
        }

        private void EndRound()
        {
            if (_participants.Count == 0)
            {
                _roundInProgress = false;
                BroadcastMessage("No participants joined the gamble. No winner this round.");
                return;
            }

            string winnerId = _participantOrder[_random.Next(_participantOrder.Count)];
            int totalPot = GetTotalPot();
            int winnerAmount = _participants[winnerId];
            float winnerChance = ((float)winnerAmount / (float)totalPot) * 100f;

            var winnerBasePlayer = BasePlayer.FindByID(Convert.ToUInt64(winnerId));
            if (winnerBasePlayer != null && winnerBasePlayer.IsConnected && winnerBasePlayer.inventory != null)
            {
                GiveItems(winnerBasePlayer, _config.CurrencyShortName, totalPot);
                BroadcastMessage($"The winner of the gamble is {winnerBasePlayer.displayName}, winning {totalPot} scrap! They had a {winnerChance:0.00}% chance to win.");
            }
            else
            {
                BroadcastMessage($"The winner (ID: {winnerId}) is offline. Winnings could not be delivered. They had a {winnerChance:0.00}% chance to win.");
            }

            _roundInProgress = false;
            _participants.Clear();
            _participantOrder.Clear();
        }

        private int GetTotalPot()
        {
            int total = 0;
            foreach (var amt in _participants.Values) total += amt;
            return total;
        }

        private void BroadcastMessage(string message)
        {
            if (_config.BroadcastToAll)
            {
                PrintToChat($"{_config.BroadcastPrefix} {message}");
            }
            else
            {
                Puts(message);
            }
        }

        private int RemoveItems(BasePlayer player, string shortname, int amount)
        {
            var definition = ItemManager.FindItemDefinition(shortname);
            if (definition == null || player?.inventory == null) return 0;

            int removed = 0;
            removed += TakeFromContainer(player.inventory.containerMain, definition.itemid, amount - removed);
            if (removed < amount) removed += TakeFromContainer(player.inventory.containerBelt, definition.itemid, amount - removed);
            if (removed < amount) removed += TakeFromContainer(player.inventory.containerWear, definition.itemid, amount - removed);

            player.SendNetworkUpdate();
            return removed;
        }

        private int TakeFromContainer(ItemContainer container, int itemId, int amount)
        {
            int removed = 0;
            if (container == null || amount <= 0) return 0;

            foreach (var item in container.itemList.ToArray())
            {
                if (item.info.itemid == itemId)
                {
                    int take = Math.Min(item.amount, amount - removed);
                    item.amount -= take;
                    removed += take;
                    item.MarkDirty();
                    if (item.amount <= 0) item.Remove();
                    if (removed >= amount) break;
                }
            }

            return removed;
        }

        private void GiveItems(BasePlayer player, string shortname, int amount)
        {
            var definition = ItemManager.FindItemDefinition(shortname);
            if (definition == null) return;
            var item = ItemManager.Create(definition, amount);
            if (!item.MoveToContainer(player.inventory.containerMain))
            {
                if (!item.MoveToContainer(player.inventory.containerBelt))
                {
                    if (!item.MoveToContainer(player.inventory.containerWear))
                    {
                        item.Drop(player.transform.position, Vector3.up);
                    }
                }
            }

            player.SendNetworkUpdate();
        }

        private string FormatTime(float seconds)
        {
            if (seconds >= 60f)
            {
                int mins = Mathf.FloorToInt(seconds / 60f);
                return $"{mins} {(mins > 1 ? "minutes" : "minute")}";
            }

            return $"{seconds} seconds";
        }
    }
}
