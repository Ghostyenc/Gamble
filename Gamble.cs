/*           ________               __
           / ____/ /_  ____  _____/ /___  __
          / / __/ __ \/ __ \/ ___/ __/ / / /
         / /_/ / / / / /_/ (__  ) /_/ /_/ /
         \____/_/ /_/\____/____/\__/\__, /
                                   /____/

This plugin is exclusively licensed to Enchanted.gg and may not be edited or sold without explicit permission.

© 2025 Ghosty & Enchanted.gg
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Gamble", "Ghosty", "2.0.0")]
    public class Gamble : RustPlugin
    {
        private ConfigData _config;
        private bool _roundInProgress;
        private readonly Dictionary<string, int> _participants = new Dictionary<string, int>();
        private readonly List<string> _participantOrder = new List<string>();
        private Timer _countdownTimer;
        private Timer _autoStartTimer;
        private System.Random _random = new System.Random();
        private float _timeLeft;

        #region Config
        private class ConfigData
        {
            public float CountdownTime { get; set; } = 600f;
            public string CurrencyShortName { get; set; } = "scrap";
            public bool BroadcastToAll { get; set; } = true;
            public string BroadcastPrefix { get; set; } = "<color=#dbb403><b>[Gamble]</b></color> ";
            public float AutoStartInterval { get; set; } = 1800f;
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
        #endregion

        void OnServerInitialized()
        {
            _roundInProgress = false;
            _participants.Clear();
            _participantOrder.Clear();
            _countdownTimer?.Destroy();
            _autoStartTimer?.Destroy();
            _autoStartTimer = timer.Every(_config.AutoStartInterval, CheckAndStartRoundAutomatically);
        }

        [ChatCommand("gamble")]
        private void GambleCommand(BasePlayer player, string command, string[] args)
        {
            if (!_roundInProgress)
            {
                SendReply(player, Prefix() + "No gamble round is running! Wait for the next round to open.");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, Prefix() + "Usage: <color=#ffb400>/gamble <scrap amount></color>");
                return;
            }

            if (!int.TryParse(args[0], out int amount) || amount <= 0)
            {
                SendReply(player, Prefix() + "Please enter a valid positive amount.");
                return;
            }

            if (_participants.ContainsKey(player.UserIDString))
            {
                SendReply(player, Prefix() + "You've already entered this round! Wait for the next round to try again.");
                return;
            }

            var def = ItemManager.FindItemDefinition(_config.CurrencyShortName);
            if (def == null)
            {
                SendReply(player, Prefix() + "Internal error: configured currency not found.");
                return;
            }

            int playerScrap = (int)player.inventory.GetAmount(def.itemid);
            if (playerScrap < amount)
            {
                SendReply(player, Prefix() + $"You don't have enough <color=#dbb403>{_config.CurrencyShortName}</color>.");
                return;
            }

            int removed = RemoveItems(player, _config.CurrencyShortName, amount);
            if (removed < amount)
            {
                SendReply(player, Prefix() + $"Failed to remove required {_config.CurrencyShortName}.");
                return;
            }

            AddParticipant(player.UserIDString, amount);

            float playerChance = 100f * amount / GetTotalPot();
            SendReply(player, Prefix() + $"You entered with <color=#dbb403>{amount} {def.displayName.translated}</color> | Your odds: <color=#5cb85c>{playerChance:0.0}%</color>.");

            BroadcastToAll(Prefix() + $"<color=#b6e2ff>{player.displayName}</color> joined the gamble. ({_participants.Count} participants, pot: <color=#dbb403>{GetTotalPot()} {def.displayName.translated}</color>)\nUse /pot to check the current pot.");
        }

        [ChatCommand("pot")]
        private void PotCommand(BasePlayer player, string command, string[] args)
        {
            if (!_roundInProgress)
            {
                SendReply(player, Prefix() + "No round is running currently.");
                return;
            }

            var def = ItemManager.FindItemDefinition(_config.CurrencyShortName);
            int pot = GetTotalPot();
            string msg = $"{Prefix()}<color=#dbb403>POT:</color> <color=#dbb403>{pot} {def.displayName.translated}</color> | <color=#b6e2ff>{_participants.Count}</color> participants";
            if (_participants.ContainsKey(player.UserIDString))
            {
                int you = _participants[player.UserIDString];
                float odds = 100f * you / pot;
                msg += $" | <color=#5cb85c>Your odds: {odds:0.0}%</color>";
            }
            if (_timeLeft > 0)
                msg += $" | <color=#ababab>Time left: {FormatTime(_timeLeft)}</color>";
            SendReply(player, msg);
        }

        [ChatCommand("startgamble")]
        private void StartGambleCommand(BasePlayer player, string command, string[] args)
        {
            if (player != null && player.net?.connection != null && player.net.connection.authLevel < 1)
            {
                SendReply(player, Prefix() + "You don't have permission to use this command.");
                return;
            }

            if (_roundInProgress)
            {
                SendReply(player, Prefix() + "A gamble round is already running!");
                return;
            }
            StartCountdown();
            BroadcastToAll(Prefix() + $"<color=#e3ffb6>Admin started a new gamble round!</color> Use <color=#ffb400>/gamble <scrap amount></color> to join.");
        }

        private void CheckAndStartRoundAutomatically()
        {
            if (!_roundInProgress)
                StartCountdown();
        }

        private void AddParticipant(string playerId, int amount)
        {
            _participants[playerId] = amount;
            _participantOrder.Add(playerId);
        }

        private void StartCountdown()
        {
            _roundInProgress = true;
            _timeLeft = _config.CountdownTime;
            BroadcastToAll(Prefix() + $"<color=#fff6b0>Gamble round started!</color> Use <color=#ffb400>/gamble <scrap amount></color> to enter. Winner in <color=#5cb85c>{FormatTime(_config.CountdownTime)}</color>.");
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

            if (_timeLeft == 60 || _timeLeft == 30 || _timeLeft == 10 || (_timeLeft <= 5 && _timeLeft > 0))
            {
                BroadcastToAll(Prefix() + $"Gamble ends in <color=#dbb403>{(int)_timeLeft}</color> seconds! Use <color=#ffb400>/gamble <scrap amount></color> to join!");
            }
        }

        private void EndRound()
        {
            if (_participants.Count == 0)
            {
                _roundInProgress = false;
                BroadcastToAll(Prefix() + "<color=#ff9f9f>No one joined. No winner this round.</color>");
                return;
            }

            string winnerId = PickWinner();
            int totalPot = GetTotalPot();
            int winnerAmount = _participants[winnerId];
            float winnerChance = 100f * winnerAmount / totalPot;
            var winnerPlayer = BasePlayer.FindByID(Convert.ToUInt64(winnerId));
            var def = ItemManager.FindItemDefinition(_config.CurrencyShortName);

            if (winnerPlayer != null && winnerPlayer.IsConnected && winnerPlayer.inventory != null)
            {
                GiveItems(winnerPlayer, _config.CurrencyShortName, totalPot);
                BroadcastToAll(Prefix() +
                    $"<color=#b6e2ff>{winnerPlayer.displayName}</color> wins <color=#dbb403>{totalPot} {def.displayName.translated}</color> with <color=#5cb85c>{winnerChance:0.0}%</color> odds! 🎉");
            }
            else
            {
                BroadcastToAll(Prefix() + $"The winner (ID: {winnerId}) is offline and did not receive the winnings. Their odds: {winnerChance:0.0}%.");
            }

            _roundInProgress = false;
            _participants.Clear();
            _participantOrder.Clear();
        }

        private string PickWinner()
        {
            int totalPot = GetTotalPot();
            int roll = _random.Next(1, totalPot + 1);
            int sum = 0;
            foreach (var entry in _participantOrder)
            {
                sum += _participants[entry];
                if (roll <= sum)
                    return entry;
            }
            return _participantOrder[_random.Next(_participantOrder.Count)];
        }

        private int GetTotalPot() => _participants.Values.Sum();

        private void BroadcastToAll(string message)
        {
            if (_config.BroadcastToAll)
                PrintToChat(message);
            else
                Puts(message);
        }

        private string Prefix() => _config.BroadcastPrefix;

        private int RemoveItems(BasePlayer player, string shortname, int amount)
        {
            var def = ItemManager.FindItemDefinition(shortname);
            if (def == null || player?.inventory == null) return 0;
            int removed = 0;
            removed += TakeFromContainer(player.inventory.containerMain, def.itemid, amount - removed);
            if (removed < amount) removed += TakeFromContainer(player.inventory.containerBelt, def.itemid, amount - removed);
            if (removed < amount) removed += TakeFromContainer(player.inventory.containerWear, def.itemid, amount - removed);
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
            var def = ItemManager.FindItemDefinition(shortname);
            if (def == null) return;
            var item = ItemManager.Create(def, amount);
            if (!item.MoveToContainer(player.inventory.containerMain))
                if (!item.MoveToContainer(player.inventory.containerBelt))
                    if (!item.MoveToContainer(player.inventory.containerWear))
                        item.Drop(player.transform.position, Vector3.up);
            player.SendNetworkUpdate();
        }

        private string FormatTime(float seconds)
        {
            if (seconds >= 60f)
                return $"{Mathf.FloorToInt(seconds / 60f)} minute{(seconds >= 120f ? "s" : "")}";
            return $"{(int)seconds} seconds";
        }
    }
}
