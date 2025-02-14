﻿using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace DarkCodex
{
    public class Settings
    {
        [JsonProperty]
        public int version = 1;

        [JsonProperty]
        public bool showBootupWarning = true;

        [JsonProperty]
        public bool allowAchievements = true;

        [JsonProperty]
        public bool stopAreaEffectsDuringCutscenes = true;

        [JsonProperty]
        public List<string> doNotLoad = new List<string>() {
            "General.patchBasicFreebieFeats",
        };

        [JsonProperty]
        public bool polymorphKeepInventory = false;

        [JsonProperty]
        public bool polymorphKeepModel = false;

        [JsonProperty]
        public bool debug_1 = false;
        [JsonProperty]
        public bool debug_2 = false;
        [JsonProperty]
        public bool debug_3 = false;
        [JsonProperty]
        public bool debug_4 = false;

        public static Config.Manager<Settings> StateManager = new Config.Manager<Settings>(Path.Combine(Main.ModPath, "settings.json"));
    }
}
