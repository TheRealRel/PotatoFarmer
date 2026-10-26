// Settings that moved to a new section or name: the old value is copied once and the old line dropped from the file.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using HarmonyLib;

namespace PotatoFarmer
{
    internal static class ConfigMove
    {
        // "section|key" -> value, read straight from the file (before anything is bound, it is the file as the player left it).
        internal static Dictionary<string, string> Read(string path)
        {
            var values = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(path)) return values;
                string section = "";
                foreach (var raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2); continue; }
                    int equals = line.IndexOf('=');
                    if (equals > 0) values[section + "|" + line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Couldn't read {Path.GetFileName(path)}: {e.Message}"); }
            return values;
        }

        // moves: old "section|key" -> the bound entry it became. Run once every setting is bound.
        internal static void Apply(ConfigFile config, Dictionary<string, string> before, Dictionary<string, ConfigEntryBase> moves)
        {
            try
            {
                var orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) as Dictionary<ConfigDefinition, string>;
                int moved = 0;
                foreach (var move in moves)
                {
                    var entry = move.Value;
                    int bar = move.Key.IndexOf('|');
                    var oldDef = new ConfigDefinition(move.Key.Substring(0, bar), move.Key.Substring(bar + 1));
                    if (before.TryGetValue(move.Key, out var value) && !before.ContainsKey(entry.Definition.Section + "|" + entry.Definition.Key))
                    {
                        try { entry.BoxedValue = TomlTypeConverter.ConvertToValue(value, entry.SettingType); moved++; }
                        catch (Exception e) { Plugin.Log.LogWarning($"Couldn't copy '{value}' into {entry.Definition.Key}: {e.Message}"); }
                    }
                    orphans?.Remove(oldDef);
                }
                if (moved > 0) Plugin.Log.LogInfo($"Settings regrouped: {moved} values carried over to their new places.");
                config.Save();
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Couldn't carry the old settings over: {e.Message}"); }
        }
    }
}
