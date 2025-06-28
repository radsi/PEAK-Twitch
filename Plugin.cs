using BepInEx;
using BepInEx.Logging;
using System;
using System.Text.RegularExpressions;
using UnityEngine;
using BepInEx.Configuration;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Client;
using System.Reflection;

namespace Twitch
{
    [BepInPlugin("radsi.twitch", "Twitch", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<string> user;
        public static ConfigEntry<int> bitsToKill;
        public static ConfigEntry<int> bitsToPassout;
        public static ConfigEntry<int> bitsToPush;
        public static ConfigEntry<int> bitsToRandomaffliction;
        public static ConfigEntry<int> bitsToClearaffliction;
        public static ConfigEntry<int> bitsToFullstamina;
        public static ConfigEntry<int> bitsToDropitems;
        public static ConfigEntry<int> bitsToCrashgame;
        public static ConfigEntry<int> bitsToGivelollipop;
        public MyTwitchBot bot;

        public void Awake()
        {
            user = Config.Bind("Twitch", "User", "username", "Twitch user");
            bitsToKill = Config.Bind("Bits", "Kill", 1000, "Bits to kill player");
            bitsToPassout = Config.Bind("Bits", "Pass out", 250, "Bits to pass out player");
            bitsToPush = Config.Bind("Bits", "Push", 400, "Bits to push player");
            bitsToRandomaffliction = Config.Bind("Bits", "Random affliction", 300, "Bits to give a random affliction");
            bitsToClearaffliction = Config.Bind("Bits", "Clear afflictions", 100, "Bits to clear every affliction");
            bitsToFullstamina = Config.Bind("Bits", "Full stamina", 200, "Bits to give full stamina");
            bitsToDropitems = Config.Bind("Bits", "Drop items", 600, "Bits to drop every item");
            bitsToCrashgame = Config.Bind("Bits", "Crash game", 2000, "Bits to crash game");
            bitsToGivelollipop = Config.Bind("Bits", "Give lollipop", 500, "Bits to give lollipop");

            Logger.LogInfo("Connecting Twitch bot...");
            bot = new MyTwitchBot(user.Value.ToLowerInvariant(), Logger);
            bot.Connect();
        }
        public void OnDestroy()
        {
            bot?.Disconnect();
        }
    }

    public class MyTwitchBot
    {
        private readonly string channel;
        private readonly ManualLogSource logger;
        private TwitchClient client;

        public MyTwitchBot(string channel, ManualLogSource logger)
        {
            this.channel = channel;
            this.logger = logger;
        }

        public void Connect()
        {
            var credentials = new ConnectionCredentials("JustinFan0", "Kappa");
            client = new TwitchClient();
            client.Initialize(credentials, channel);

            client.OnMessageReceived += OnMessageReceived;

            client.Connect();
        }

        public void Disconnect()
        {
            if (client != null && client.IsConnected)
            {
                client.Disconnect();
            }
        }

        public void OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            string user = e.ChatMessage.Username;
            string msg = e.ChatMessage.Message;

            int bits = ExtractBits(e.ChatMessage.RawIrcMessage);

            if (bits > 0 && Character.localCharacter != null)
            {
                logger.LogInfo($"[BITS] {user} donated {bits} bits: {msg}");

                if (bits == Plugin.bitsToPush.Value)
                {
                    logger.LogInfo($"[ACTION] Push triggered by {user} with {bits} bits!");
                    Character.localCharacter.gameObject.transform.Find("Scout").Find("SFX").Find("Movement").Find("SFX Jump").gameObject.SetActive(true);
                    Character.localCharacter.GetType().GetMethod("AddForce", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(Character.localCharacter, new object[] { Camera.main.transform.forward * 6000f, 1f, 1f });
                }
                else if(bits == Plugin.bitsToPassout.Value)
                {
                    logger.LogInfo($"[ACTION] Passout triggered by {user} with {bits} bits!");
                    Character.localCharacter.PassOutInstantly();
                }
                else if(bits == Plugin.bitsToKill.Value)
                {
                    logger.LogInfo($"[ACTION] Kill triggered by {user} with {bits} bits!");
                    Character.localCharacter.GetType().GetMethod("DieInstantly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(Character.localCharacter, null);
                }
                else if(bits == Plugin.bitsToRandomaffliction.Value)
                {
                    logger.LogInfo($"[ACTION] Random affliction triggered by {user} with {bits} bits!");
                    var values = Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE));
                    var randomStatus = (CharacterAfflictions.STATUSTYPE)values.GetValue(UnityEngine.Random.Range(0, values.Length));

                    Character.localCharacter.refs.afflictions.AddStatus(randomStatus, 0.05f, false);
                }
                else if(bits == Plugin.bitsToClearaffliction.Value)
                {
                    logger.LogInfo($"[ACTION] Clear afflictions triggered by {user} with {bits} bits!");
                    Character.localCharacter.refs.afflictions.ClearAllStatus(false);
                }
                else if(bits == Plugin.bitsToFullstamina.Value)
                {
                    logger.LogInfo($"[ACTION] Full stamina triggered by {user} with {bits} bits!");
                    Character.localCharacter.AddStamina(1);
                }
                else if (bits == Plugin.bitsToDropitems.Value)
                {
                    logger.LogInfo($"[ACTION] Drop items triggered by {user} with {bits} bits!");
                    Character.localCharacter.GetType().GetMethod("DropAllItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(Character.localCharacter, new object[] {true});
                }
                else if (bits == Plugin.bitsToCrashgame.Value)
                {
                    logger.LogInfo($"[ACTION] Crash game triggered by {user} with {bits} bits!");
                    UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.Abort);
                }
                else if (bits == Plugin.bitsToGivelollipop.Value)
                {
                    logger.LogInfo($"[ACTION] Give lollipop triggered by {user} with {bits} bits!");
                    Character.localCharacter.refs.items.GetType().GetMethod("SpawnItemInHand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(Character.localCharacter, new object[] { "Big Lollipop" });
                }
            }
        }

        private int ExtractBits(string rawIrc)
        {
            var match = Regex.Match(rawIrc, @"bits=(\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, out int bits) ? bits : 0;
        }
    }
}
