using BepInEx;
using BepInEx.Logging;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http;
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
        public static ConfigEntry<string> oauth;
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

        private string clientId = "gdjyhvlyot8gtw2c4ter1ezq9xc6se";

        public void Awake()
        {
            user = Config.Bind("Twitch", "User", "username", "Twitch user");
            oauth = Config.Bind("Twitch", "OAuth", "oauth:token", "OAuth token");
            bitsToKill = Config.Bind("Bits", "Kill", 1000, "Bits to kill player");
            bitsToPassout = Config.Bind("Bits", "Pass out", 250, "Bits to pass out player");
            bitsToPush = Config.Bind("Bits", "Push", 400, "Bits to push player");
            bitsToRandomaffliction = Config.Bind("Bits", "Random affliction", 300, "Bits to give a random affliction");
            bitsToClearaffliction = Config.Bind("Bits", "Clear afflictions", 100, "Bits to clear every affliction");
            bitsToFullstamina = Config.Bind("Bits", "Full stamina", 200, "Bits to give full stamina");
            bitsToDropitems = Config.Bind("Bits", "Drop items", 600, "Bits to drop every item");
            bitsToCrashgame = Config.Bind("Bits", "Crash game", 2000, "Bits to crash game");
            bitsToGivelollipop = Config.Bind("Bits", "Give lollipop", 500, "Bits to give lollipop");

            Logger.LogInfo($"OAuth token currently: {oauth.Value}");

            if (oauth.Value.Length < 20)
            {
                Logger.LogInfo("OAuth token not found or invalid, starting Device Code Grant flow");
                Task.Run(async () =>
                {
                    try
                    {
                        string token = await RunDeviceCodeFlowAsync();
                        if (!string.IsNullOrEmpty(token))
                        {
                            oauth.Value = "oauth:" + token;
                            Config.Save();
                            Logger.LogInfo("OAuth token granted and saved.");
                        }
                        else
                        {
                            Logger.LogError("Failed to obtain OAuth token.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Exception during OAuth flow: {ex}");
                    }

                    ConnectBot();
                });
            }
            else
            {
                Logger.LogInfo("OAuth token found, connecting bot directly");
                ConnectBot();
            }
        }

        private void ConnectBot()
        {
            Logger.LogInfo("Connecting Twitch bot...");
            bot = new MyTwitchBot(user.Value.ToLowerInvariant(), Logger);
            bot.Connect();
        }

        private async Task<string> RunDeviceCodeFlowAsync()
        {
            Logger.LogInfo("Starting Twitch Device Code Flow");

            using (HttpClient httpClient = new HttpClient())
            {
                var deviceCodeRequest = new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "scope", "chat:read" }
                };

                var content = new FormUrlEncodedContent(deviceCodeRequest);

                var response = await httpClient.PostAsync("https://id.twitch.tv/oauth2/device", content);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogError($"Error requesting device code: {response.StatusCode}");
                    return null;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                var deviceCodeResponse = JsonUtility.FromJson<DeviceCodeResponse>(responseBody);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = deviceCodeResponse.verification_uri,
                    UseShellExecute = true
                });

                await Task.Delay(TimeSpan.FromSeconds(30));

                while (true)
                {
                    try
                    {
                        var tokenRequest = new Dictionary<string, string>
                        {
                            { "client_id", clientId },
                            { "device_code", deviceCodeResponse.device_code },
                            { "grant_type", "urn:ietf:params:oauth:grant-type:device_code" }
                        };

                        var tokenContent = new FormUrlEncodedContent(tokenRequest);
                        var tokenResponse = await httpClient.PostAsync("https://id.twitch.tv/oauth2/token", tokenContent);
                        var tokenResponseBody = await tokenResponse.Content.ReadAsStringAsync();

                        if (tokenResponse.IsSuccessStatusCode)
                        {
                            var tokenObj = JsonUtility.FromJson<TwitchTokenResponse>(tokenResponseBody);
                            Logger.LogInfo("Authorization complete, token obtained.");
                            return tokenObj.access_token;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Exception during token polling: {ex.Message}");
                        return null;
                    }
                }
            }
        }

        [Serializable]
        class DeviceCodeResponse
        {
            public string device_code;
            public string user_code;
            public string verification_uri;
            public int expires_in;
            public int interval;
        }

        [Serializable]
        class DeviceCodeErrorResponse
        {
            public string error;
            public string error_description;
        }

        [Serializable]
        class TwitchTokenResponse
        {
            public string access_token;
            public string refresh_token;
            public int expires_in;
            public string[] scope;
            public string token_type;
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
            var credentials = new ConnectionCredentials(Plugin.user.Value, Plugin.oauth.Value.Replace("oauth:", ""));
            client = new TwitchClient();
            client.Initialize(credentials, channel);

            client.OnLog += (s, e) => logger.LogInfo($"[TwitchLib] {e.Data}");
            client.OnConnected += (s, e) => logger.LogInfo($"Connected to {e.AutoJoinChannel}");
            client.OnJoinedChannel += (s, e) => logger.LogInfo($"Joined channel {e.Channel}");
            client.OnDisconnected += (s, e) => logger.LogInfo("Disconnected from Twitch");
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
            int bits = ExtractBits(msg);

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

        private int ExtractBits(string message)
        {
            var match = Regex.Match(message, @"cheer(\d+)", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, out int bits) ? bits : 0;
        }
    }
}
