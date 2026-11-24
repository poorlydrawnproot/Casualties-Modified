using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CasualtiesSaveEditor
{
    /// One component object that owns a liquid stack
    /// (usually WaterContainerItem).

    public sealed class LiquidStackRef
    {
        public JProperty Property;
        public JObject Component;
        public JArray Stack;
        public string TypeName;

        public double TotalAmount()
        {
            double total = 0.0;
            foreach (JToken token in Stack)
            {
                JObject entry = token as JObject;
                if (entry == null) continue;
                JToken amount = entry["amount"];
                if (amount == null) continue;
                total += LiquidUtil.Amount(amount);
            }
            return total;
        }
    }

    public sealed class LiquidEntryRef
    {
        public LiquidStackRef Stack;
        public int Index;
        public JObject Entry;

        public string LiquidId
        {
            get
            {
                JToken token = Entry["liquidId"];
                return token != null && token.Type == JTokenType.String
                    ? (string)token : "";
            }
        }

        public JToken AmountToken
        {
            get { return Entry["amount"]; }
        }
    }

    public sealed class LiquidTypeInfo
    {
        public readonly string Id;
        public readonly string LocaleName;
        public readonly bool Injectable;
        public readonly bool HealthUsable;
        public readonly double InjectionSickness;

        public LiquidTypeInfo(string id, string localeName, bool injectable,
            bool healthUsable, double injectionSickness)
        {
            Id = id;
            LocaleName = localeName;
            Injectable = injectable;
            HealthUsable = healthUsable;
            InjectionSickness = injectionSickness;
        }
    }

    public sealed class ItemLiquidInfo
    {
        public readonly string Id;
        public readonly double Capacity;
        public readonly bool AutoFill;
        public readonly bool Injector;

        public ItemLiquidInfo(string id, double capacity, bool autoFill,
            bool injector)
        {
            Id = id;
            Capacity = capacity;
            AutoFill = autoFill;
            Injector = injector;
        }
    }

    public static class ItemLiquidCatalog
    {
        public static readonly ItemLiquidInfo[] Items = ItemLiquidRegistryData.Items;
        private static readonly Dictionary<string, ItemLiquidInfo> ById = BuildIndex();

        private static Dictionary<string, ItemLiquidInfo> BuildIndex()
        {
            Dictionary<string, ItemLiquidInfo> index =
                new Dictionary<string, ItemLiquidInfo>(StringComparer.Ordinal);
            foreach (ItemLiquidInfo info in Items) index[info.Id] = info;
            return index;
        }

        public static ItemLiquidInfo Info(string itemId)
        {
            if (itemId == null) return null;
            ItemLiquidInfo info;
            return ById.TryGetValue(itemId, out info) ? info : null;
        }
    }

    public static class LiquidCatalog
    {
        public static readonly LiquidTypeInfo[] Types = LiquidRegistryData.Types;
        private static readonly Dictionary<string, LiquidTypeInfo> ById = BuildIndex();

        private static Dictionary<string, LiquidTypeInfo> BuildIndex()
        {
            Dictionary<string, LiquidTypeInfo> index =
                new Dictionary<string, LiquidTypeInfo>(StringComparer.Ordinal);
            foreach (LiquidTypeInfo info in Types) index[info.Id] = info;
            return index;
        }

        public static LiquidTypeInfo Info(string liquidId)
        {
            if (liquidId == null) return null;
            LiquidTypeInfo info;
            return ById.TryGetValue(liquidId, out info) ? info : null;
        }

        public static bool IsInjectable(string liquidId)
        {
            LiquidTypeInfo info = Info(liquidId);
            return info != null && info.Injectable;
        }

    }

    public static class LiquidUtil
    {
        public const string ContainerKey =
            "WaterContainerItem, Assembly-CSharp, Version=0.0.0.0, " +
            "Culture=neutral, PublicKeyToken=null";

        public static string ShortType(string key)
        {
            if (key == null) return "";
            int comma = key.IndexOf(',');
            return comma > 0 ? key.Substring(0, comma).Trim() : key.Trim();
        }

        public static bool LooksLikeLiquidComponent(JProperty property)
        {
            if (property == null) return false;
            JObject component = property.Value as JObject;
            if (component == null) return false;
            JArray stack = component["stack"] as JArray;
            if (stack == null) return false;
            if (stack.Count == 0)
                return ShortType(property.Name).IndexOf("Container",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (JToken token in stack)
            {
                JObject entry = token as JObject;
                if (entry != null && entry["liquidId"] != null) return true;
            }
            return false;
        }

        public static List<LiquidStackRef> FindStacks(JObject components)
        {
            List<LiquidStackRef> stacks = new List<LiquidStackRef>();
            if (components == null) return stacks;
            foreach (JProperty property in components.Properties())
            {
                if (!LooksLikeLiquidComponent(property)) continue;
                JObject component = (JObject)property.Value;
                LiquidStackRef reference = new LiquidStackRef();
                reference.Property = property;
                reference.Component = component;
                reference.Stack = (JArray)component["stack"];
                reference.TypeName = ShortType(property.Name);
                stacks.Add(reference);
            }
            return stacks;
        }

        public static List<LiquidEntryRef> Entries(JObject components)
        {
            List<LiquidEntryRef> entries = new List<LiquidEntryRef>();
            foreach (LiquidStackRef stack in FindStacks(components))
            {
                for (int i = 0; i < stack.Stack.Count; i++)
                {
                    JObject entry = stack.Stack[i] as JObject;
                    if (entry == null) continue;
                    LiquidEntryRef reference = new LiquidEntryRef();
                    reference.Stack = stack;
                    reference.Index = i;
                    reference.Entry = entry;
                    entries.Add(reference);
                }
            }
            return entries;
        }

        /// Returns the first liquid stack, creating a WaterContainerItem
        /// component if the item does not already have one. 
		
        public static LiquidStackRef EnsureStack(JObject components)
        {
            List<LiquidStackRef> stacks = FindStacks(components);
            if (stacks.Count > 0) return stacks[0];

            JObject component = new JObject();
            component["stack"] = new JArray();
            components[ContainerKey] = component;

            LiquidStackRef reference = new LiquidStackRef();
            reference.Property = components.Property(ContainerKey);
            reference.Component = component;
            reference.Stack = (JArray)component["stack"];
            reference.TypeName = ShortType(ContainerKey);
            return reference;
        }

        public static JObject AddEntry(LiquidStackRef stack, string liquidId,
            double amount)
        {
            JObject entry = new JObject();
            entry["liquidId"] = new JValue(liquidId);
            entry["amount"] = new JValue(amount);
            stack.Stack.Add(entry);
            return entry;
        }

        public static void RemoveEntry(LiquidEntryRef entry)
        {
            if (entry == null || entry.Stack == null) return;
            if (entry.Index >= 0 && entry.Index < entry.Stack.Stack.Count)
                entry.Stack.Stack.RemoveAt(entry.Index);
        }

        public static void Clear(LiquidStackRef stack)
        {
            if (stack != null) stack.Stack.Clear();
        }

        public static double Amount(JToken token)
        {
            if (token == null) return 0.0;
            if (token.Type == JTokenType.Integer) return (double)(long)token;
            if (token.Type == JTokenType.Float) return (double)token;
            double value;
            if (double.TryParse(token.ToString(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value)) return value;
            return 0.0;
        }

        public static string UseLabel(string liquidId)
        {
            LiquidTypeInfo info = LiquidCatalog.Info(liquidId);
            if (info == null) return liquidId == null || liquidId.Length == 0
                ? "" : "custom";
            if (info.Injectable && info.HealthUsable) return "inj/health";
            if (info.Injectable) return "inject";
            if (info.HealthUsable) return "health";
            return "oral";
        }

        public static ItemLiquidInfo ItemInfo(string itemId)
        {
            return ItemLiquidCatalog.Info(itemId);
        }

        public static double CapacityForItem(string itemId)
        {
            ItemLiquidInfo info = ItemLiquidCatalog.Info(itemId);
            return info == null ? 0.0 : info.Capacity;
        }

        public static string LiquidName(GameData game, string liquidId)
        {
            if (string.IsNullOrEmpty(liquidId)) return "";
            if (game != null) return game.LiquidName(liquidId);
            return GameData.Prettify(liquidId);
        }

        public static string Summarize(GameData game, JObject components)
        {
            List<LiquidEntryRef> entries = Entries(components);
            if (entries.Count == 0) return "no liquids";
            List<string> parts = new List<string>();
            foreach (LiquidEntryRef entry in entries)
            {
                string amount = entry.AmountToken == null
                    ? "?"
                    : Amount(entry.AmountToken).ToString("0.###",
                        CultureInfo.InvariantCulture);
                parts.Add(LiquidName(game, entry.LiquidId) + " " + amount + "ml");
            }
            return string.Join(", ", parts.ToArray());
        }

        public static string[] SortedIds(GameData game, JObject components)
        {
            SortedSet<string> ids = new SortedSet<string>(StringComparer.Ordinal);
            if (game != null)
                foreach (string id in game.LiquidIds) ids.Add(id);
            if (components != null)
                foreach (LiquidEntryRef entry in Entries(components))
                    if (entry.LiquidId.Length > 0) ids.Add(entry.LiquidId);
            return ids
                .OrderBy(delegate(string id)
                {
                    return LiquidName(game, id).ToLowerInvariant();
                })
                .ThenBy(delegate(string id) { return id; }, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
