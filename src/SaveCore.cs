using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasualtiesSaveEditor
{
    public class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message) { }
        public SaveFormatException(string message, Exception inner) : base(message, inner) { }
    }

    /// RW access to save file

    public sealed class SaveFile
    {
        public string FilePath { get; private set; }
        public JObject Root { get; set; }
        public byte[] Payload { get; private set; }
        public List<string> Warnings { get; private set; }

        private SaveFile()
        {
            Warnings = new List<string>();
        }

        public static SaveFile Load(string path)
        {
            if (path == null) throw new ArgumentNullException("path");
            byte[] raw;
            try
            {
                raw = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                throw new SaveFormatException("Cannot read " + path + ": " + ex.Message, ex);
            }

            if (raw.Length < 2 || raw[0] != 0x1F || raw[1] != 0x8B)
                throw new SaveFormatException(
                    path + " does not start with the gzip magic bytes (1F 8B).");

            byte[] payload;
            try
            {
                using (MemoryStream input = new MemoryStream(raw))
                using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
                using (MemoryStream output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    payload = output.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new SaveFormatException(
                    path + " is not a valid gzip stream: " + ex.Message, ex);
            }

            if (payload.Length > 64 * 1024 * 1024)
                throw new SaveFormatException(
                    "The decompressed save is " + payload.Length +
                    " bytes; refusing files over 64 MiB.");

            string text = new UTF8Encoding(false).GetString(payload);
            JObject root;
            try
            {
                root = JObject.Parse(text);
            }
            catch (Exception ex)
            {
                throw new SaveFormatException(
                    "The save payload is not valid JSON: " + ex.Message, ex);
            }

            SaveFile save = new SaveFile();
            save.FilePath = Path.GetFullPath(path);
            save.Root = root;
            save.Payload = payload;
            save.Warnings = Validate(root);
            return save;
        }

        public string PrettyJson()
        {
            return Root.ToString(Formatting.Indented) + Environment.NewLine;
        }

        public string CompactJson()
        {
            return Root.ToString(Formatting.None);
        }

        /// Make the save! 
        public string Save(string path)
        {
            return Save(path, true);
        }

        public string Save(string path, bool makeBackup)
        {
            if (path == null) throw new ArgumentNullException("path");
            Validate(Root);

            string text = Root.ToString(Formatting.None);
            byte[] payload = new UTF8Encoding(false).GetBytes(text);
            byte[] packed;
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress, true))
                {
                    gzip.Write(payload, 0, payload.Length);
                }
                packed = output.ToArray();
            }

            byte[] check;
            using (MemoryStream input = new MemoryStream(packed))
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                gzip.CopyTo(output);
                check = output.ToArray();
            }
            if (!ByteArraysEqual(payload, check))
                throw new SaveFormatException(
                    "Internal error: gzip round-trip did not match; nothing was written.");

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string backup = null;
            if (makeBackup && File.Exists(fullPath))
            {
                backup = NextBackupPath(fullPath);
                File.Copy(fullPath, backup, false);
            }

            string temp = fullPath + ".tmp";
            File.WriteAllBytes(temp, packed);
            try
            {
                if (File.Exists(fullPath))
                    File.Replace(temp, fullPath, null);
                else
                    File.Move(temp, fullPath);
            }
            catch (Exception)
            {
                // File.Replace is not supported everywhere; fall back to copy.
                File.Copy(temp, fullPath, true);
                try { File.Delete(temp); }
                catch (Exception) { }
            }

            FilePath = fullPath;
            Payload = payload;
            return backup;
        }

        public static bool IsGameRunning()
        {
            try
            {
                return Process.GetProcessesByName("CasualtiesUnknown").Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string DefaultSavePath()
        {
            List<string> candidates = new List<string>();
            try
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                candidates.Add(Path.Combine(profile, "AppData", "LocalLow",
                    "Orsoniks", "CasualtiesUnknown", "save.sv"));
            }
            catch (Exception) { }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "save.sv"));
            candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "save.sv")));
            candidates.Add(Path.Combine(Environment.CurrentDirectory, "save.sv"));
            candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "save.sv")));

            foreach (string candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate)) return candidate;
                }
                catch (Exception) { }
            }
            return candidates[0];
        }

        private static string NextBackupPath(string path)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string basePath = path + ".bak-" + stamp;
            string candidate = basePath;
            int counter = 1;
            while (File.Exists(candidate))
            {
                candidate = basePath + "-" + counter.ToString(CultureInfo.InvariantCulture);
                counter++;
            }
            return candidate;
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public static List<string> Validate(JObject root)
        {
            List<string> warnings = new List<string>();
            if (root == null) throw new SaveFormatException("The save is empty.");

            JObject body = root["body"] as JObject;
            if (body == null)
                throw new SaveFormatException("Missing or invalid 'body' object.");

            JToken skills = body["skills"];
            if (skills != null && !(skills is JObject))
                throw new SaveFormatException("body.skills is not an object.");

            JArray limbs = root["limbs"] as JArray;
            if (limbs == null)
                throw new SaveFormatException("Missing or invalid 'limbs' array.");
            for (int i = 0; i < limbs.Count; i++)
                if (!(limbs[i] is JObject))
                    throw new SaveFormatException("limbs[" + i + "] is not an object.");

            JArray items = root["items"] as JArray;
            if (items == null)
                throw new SaveFormatException("Missing or invalid 'items' array.");
            for (int i = 0; i < items.Count; i++)
                if (!(items[i] is JObject))
                    throw new SaveFormatException("items[" + i + "] is not an object.");

            JArray components = root["itemComponents"] as JArray;
            if (components == null)
                throw new SaveFormatException(
                    "Missing 'itemComponents' array; it must stay aligned with 'items'.");
            if (components.Count != items.Count)
                throw new SaveFormatException(
                    "itemComponents has " + components.Count + " entries but items has " +
                    items.Count + "; they must stay aligned.");

            JArray limbComponents = root["limbComponents"] as JArray;
            if (limbComponents == null)
                warnings.Add("limbComponents is missing or not an array.");
            else if (limbComponents.Count != limbs.Count)
                warnings.Add("limbComponents has " + limbComponents.Count +
                    " entries but limbs has " + limbs.Count + ".");

            JArray settings = root["runSettings"] as JArray;
            if (settings != null)
            {
                for (int i = 0; i < settings.Count; i++)
                {
                    JObject entry = settings[i] as JObject;
                    if (entry == null || entry["Item1"] == null || entry["Item2"] == null)
                        throw new SaveFormatException(
                            "runSettings[" + i + "] is not an Item1/Item2 object.");
                }
            }
            else if (root["runSettings"] != null)
            {
                throw new SaveFormatException("runSettings is not an array.");
            }

            foreach (JToken token in root.DescendantsAndSelf())
            {
                if (token.Type == JTokenType.Float)
                {
                    double value = (double)token;
                    if (double.IsNaN(value) || double.IsInfinity(value))
                        throw new SaveFormatException(
                            "The save contains a non-finite number at " + token.Path + ".");
                }
            }

            return warnings;
        }
    }

    /// Item names and friend's Labels from the lang file
    public sealed class GameData
    {
        public string LangPath { get; private set; }
        public Dictionary<string, string> ItemNames { get; private set; }
        public Dictionary<string, string> ItemDescriptions { get; private set; }
        public Dictionary<string, string> LiquidNames { get; private set; }
        public Dictionary<string, string> RunLabels { get; private set; }
        public Dictionary<string, string> RunDescriptions { get; private set; }

        public GameData(string root, string savePath)
        {
            ItemNames = new Dictionary<string, string>(StringComparer.Ordinal);
            ItemDescriptions = new Dictionary<string, string>(StringComparer.Ordinal);
            LiquidNames = new Dictionary<string, string>(StringComparer.Ordinal);
            RunLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            RunDescriptions = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (LiquidTypeInfo liquid in LiquidCatalog.Types)
                LiquidNames[liquid.Id] = Prettify(liquid.LocaleName);

            List<string> candidates = new List<string>();
            if (!string.IsNullOrEmpty(root)) candidates.Add(root);
            if (!string.IsNullOrEmpty(savePath))
                candidates.AddRange(GameLocator.DetectGameRoots(savePath));

            foreach (string candidate in candidates)
            {
                string lang = GameLocator.FindLangFile(candidate);
                if (lang != null)
                {
                    LangPath = lang;
                    break;
                }
            }
            if (LangPath != null) Load(LangPath);
        }

        public bool Available
        {
            get { return LangPath != null; }
        }

        public IList<string> ItemIds
        {
            get
            {
                return ItemNames.Keys
                    .OrderBy(delegate(string id) { return ItemName(id).ToLowerInvariant(); })
                    .ThenBy(delegate(string id) { return id; })
                    .ToList();
            }
        }

        public string ItemName(string id)
        {
            if (id == null) return "";
            string name;
            return ItemNames.TryGetValue(id, out name) ? name : "";
        }

        public string ItemDescription(string id)
        {
            if (id == null) return "";
            string text;
            return ItemDescriptions.TryGetValue(id, out text) ? text : "";
        }

        public IList<string> LiquidIds
        {
            get
            {
                return LiquidNames.Keys
                    .OrderBy(delegate(string id) { return LiquidName(id).ToLowerInvariant(); })
                    .ThenBy(delegate(string id) { return id; }, StringComparer.Ordinal)
                    .ToList();
            }
        }

        public string LiquidName(string id)
        {
            if (id == null) return "";
            string name;
            if (LiquidNames.TryGetValue(id, out name)) return name;
            return Prettify(id);
        }

        public void RegisterLiquid(string id)
        {
            if (string.IsNullOrEmpty(id) || LiquidNames.ContainsKey(id)) return;
            LiquidNames[id] = Prettify(id);
        }

        public string RunLabel(string key)
        {
            if (key == null) return "";
            string label;
            return RunLabels.TryGetValue(key, out label) ? label : Prettify(key);
        }

        public string RunDescription(string key)
        {
            if (key == null) return "";
            string text;
            return RunDescriptions.TryGetValue(key, out text) ? text : "";
        }

        public static string Prettify(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string spaced = Regex.Replace(key, "(?<=[a-z0-9])(?=[A-Z])", " ");
            spaced = spaced.Replace('_', ' ').Replace('-', ' ');
            spaced = spaced.Trim();
            if (spaced.Length == 0) return key;
            return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        private void Load(string langPath)
        {
            JObject data;
            try
            {
                string text = File.ReadAllText(langPath, new UTF8Encoding(false));
                data = JObject.Parse(text);
            }
            catch (Exception)
            {
                return;
            }

            JObject main = data["main"] as JObject;
            if (main != null)
            {
                foreach (JProperty property in main.Properties())
                {
                    JValue nameValue = property.Value as JValue;
                    if (nameValue == null || nameValue.Type != JTokenType.String) continue;
                    JToken desc = main[property.Name + "dsc"];
                    if (desc == null || desc.Type != JTokenType.String) continue;
                    ItemNames[property.Name] = (string)nameValue;
                    ItemDescriptions[property.Name] = (string)desc;
                }
            }

            JObject other = data["other"] as JObject;
            if (other != null)
            {
                foreach (JProperty property in other.Properties())
                {
                    if (!property.Name.StartsWith("runset", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (property.Name.EndsWith("dsc", StringComparison.OrdinalIgnoreCase))
                        continue;
                    JValue label = property.Value as JValue;
                    if (label == null || label.Type != JTokenType.String) continue;
                    string key = property.Name.Substring("runset".Length);
                    RunLabels[key] = (string)label;
                    JToken desc = other[property.Name + "dsc"];
                    if (desc != null && desc.Type == JTokenType.String)
                        RunDescriptions[key] = (string)desc;
                }

                foreach (LiquidTypeInfo liquid in LiquidCatalog.Types)
                {
                    JToken name = other[liquid.LocaleName];
                    if (name != null && name.Type == JTokenType.String)
                        LiquidNames[liquid.Id] = (string)name;
                }
            }
        }
    }

    public static class GameLocator
    {
        public static string FindLangFile(string root)
        {
            if (string.IsNullOrEmpty(root)) return null;
            try
            {
                if (File.Exists(root))
                    return string.Equals(Path.GetFileName(root), "EN.json",
                        StringComparison.OrdinalIgnoreCase) ? Path.GetFullPath(root) : null;

                string[] candidates = new string[]
                {
                    Path.Combine(root, "CasualtiesUnknown_Data", "Lang", "EN.json"),
                    Path.Combine(root, "Lang", "EN.json"),
                    Path.Combine(root, "EN.json"),
                    Path.Combine(root, "..", "Lang", "EN.json")
                };
                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
            }
            catch (Exception) { }
            return null;
        }

        public static List<string> DetectGameRoots(string savePath)
        {
            List<string> roots = new List<string>();

            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(savePath));
                string log = Path.Combine(dir, "Player.log");
                if (File.Exists(log))
                {
                    int lines = 0;
                    using (StreamReader reader = new StreamReader(log))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null && lines < 400)
                        {
                            lines++;
                            Match match = Regex.Match(line,
                                @"Mono path\[0\] = '(.+?)[/\\]CasualtiesUnknown_Data[/\\]Managed'");
                            if (match.Success)
                            {
                                roots.Add(match.Groups[1].Value);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception) { }

            foreach (string steamRoot in SteamRoots())
            {
                roots.Add(Path.Combine(steamRoot, "steamapps", "common", "Casualties Unknown"));
                try
                {
                    string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(vdf)) continue;
                    string text = File.ReadAllText(vdf);
                    foreach (Match match in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
                    {
                        string library = match.Groups[1].Value.Replace("\\\\", "\\");
                        roots.Add(Path.Combine(library, "steamapps", "common",
                            "Casualties Unknown"));
                    }
                }
                catch (Exception) { }
            }

            return roots;
        }

        private static List<string> SteamRoots()
        {
            List<string> roots = new List<string>();
            AddRegistryPath(roots, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
            AddRegistryPath(roots, Registry.LocalMachine,
                @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            AddRegistryPath(roots, Registry.LocalMachine,
                @"SOFTWARE\Valve\Steam", "InstallPath");

            string programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrEmpty(programFilesX86))
                roots.Add(Path.Combine(programFilesX86, "Steam"));
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (!string.IsNullOrEmpty(programFiles))
                roots.Add(Path.Combine(programFiles, "Steam"));

            return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AddRegistryPath(List<string> roots, RegistryKey hive,
            string subkey, string valueName)
        {
            try
            {
                using (RegistryKey key = hive.OpenSubKey(subkey))
                {
                    if (key == null) return;
                    object value = key.GetValue(valueName);
                    if (value is string && ((string)value).Length > 0)
                        roots.Add((string)value);
                }
            }
            catch (Exception) { }
        }
    }

    public static class JsonText
    {
        public static string Display(JToken token)
        {
            if (token == null) return "null";
            switch (token.Type)
            {
                case JTokenType.Boolean:
                    return ((bool)token) ? "true" : "false";
                case JTokenType.Integer:
                    return Convert.ToInt64(token, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture);
                case JTokenType.Float:
                    return ((double)token).ToString("R", CultureInfo.InvariantCulture);
                case JTokenType.String:
                    return (string)token;
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return "null";
                case JTokenType.Object:
                case JTokenType.Array:
                    return token.ToString(Formatting.Indented);
                default:
                    return token.ToString();
            }
        }

        public static JToken Parse(JToken original, string text, string name)
        {
            if (text == null) text = "";
            string trimmed = text.Trim();
            JTokenType type = original == null ? JTokenType.Null : original.Type;

            if (type == JTokenType.Boolean)
            {
                string low = trimmed.ToLowerInvariant();
                if (low == "true" || low == "yes" || low == "1" || low == "on")
                    return new JValue(true);
                if (low == "false" || low == "no" || low == "0" || low == "off")
                    return new JValue(false);
                throw new FormatException(name + ": expected true or false.");
            }
            if (type == JTokenType.Integer)
            {
                long value;
                if (!long.TryParse(trimmed, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out value))
                    throw new FormatException(name + ": expected a whole number.");
                return new JValue(value);
            }
            if (type == JTokenType.Float)
            {
                double value;
                if (!double.TryParse(trimmed, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out value))
                    throw new FormatException(name + ": expected a number.");
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new FormatException(name + ": number must be finite.");
                return new JValue(value);
            }
            if (type == JTokenType.String)
                return new JValue(text);

            if (trimmed.Length == 0 || trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
                return JValue.CreateNull();

            JToken parsed;
            try
            {
                parsed = JToken.Parse(trimmed);
            }
            catch (Exception ex)
            {
                throw new FormatException(name + ": expected a JSON value (" + ex.Message + ").");
            }
            if (type == JTokenType.Object && !(parsed is JObject))
                throw new FormatException(name + ": expected a JSON object.");
            if (type == JTokenType.Array && !(parsed is JArray))
                throw new FormatException(name + ": expected a JSON array.");
            return parsed;
        }
    }
}
