using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasualtiesSaveEditor.Tui
{
    public sealed class ItemsScreen : TuiScreen
    {
        private readonly ListModel _list = new ListModel();
        private int _visibleRows = 12;

        public ItemsScreen(TuiApp app) : base(app)
        {
            BuildList();
        }

        private JArray Items
        {
            get { return App.Save.Root["items"] as JArray; }
        }

        private JArray Components
        {
            get { return App.Save.Root["itemComponents"] as JArray; }
        }

        public override void OnShow()
        {
            BuildList();
        }

        private void BuildList()
        {
            int previous = _list.Selected;
            _list.Clear();
            JArray items = Items;
            if (items == null) return;
            for (int i = 0; i < items.Count; i++)
                _list.AddItem(new ListRow("", true, false, i));
            _list.EnsureSelection();
            if (previous >= 0 && previous < _list.Rows.Count)
                _list.Selected = previous;
        }

        public override string Help
        {
            get
            {
                return "enter edit   l liquids   a add   d duplicate   x remove   " +
                    "f repair all   s save   esc back";
            }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            buffer.TextClipped(layout.ContentLeft, y, "ITEMS",
                layout.ContentWidth, Theme.Header);
            y++;
            buffer.TextClipped(layout.ContentLeft, y,
                "slot >= 0 = body slot; slot -1 + wearSlot = inside that " +
                "worn container.", layout.ContentWidth, Theme.Dim);
            y += 2;

            int bottom = layout.ContentBottom;
            int visible = Math.Max(1, bottom - y + 1);
            _visibleRows = visible;
            _list.EnsureVisible(visible);
            JArray items = Items;
            if (items == null) return;

            for (int i = 0; i < visible; i++)
            {
                int index = _list.Scroll + i;
                if (index >= _list.Rows.Count) break;
                JObject item = items[index] as JObject;
                string line = FormatItem(index, item);
                int rowY = y + i;
                if (index == _list.Selected)
                {
                    buffer.FillRow(rowY, layout.ContentLeft, layout.ContentWidth, ' ',
                        Theme.Selected);
                    buffer.TextClipped(layout.ContentLeft, rowY, line,
                        layout.ContentWidth, Theme.Selected);
                }
                else
                {
                    buffer.TextClipped(layout.ContentLeft, rowY, line,
                        layout.ContentWidth, Theme.Text);
                }
            }

            if (_list.Rows.Count > visible)
            {
                string pos = (_list.Selected + 1).ToString() + "/" +
                    _list.Rows.Count.ToString();
                buffer.TextClipped(layout.ContentLeft + layout.ContentWidth - pos.Length,
                    layout.ContentTop, pos, pos.Length, Theme.Dim);
            }
        }

        private string FormatItem(int index, JObject item)
        {
            if (item == null) return "Item " + (index + 1);
            string id = JsonText.Display(item["id"]);
            string name = App.Game == null ? "" : App.Game.ItemName(id);
            string condition = ShortNumber(item["condition"]);
            string slot = JsonText.Display(item["slot"]);
            JToken wear = item["wearSlot"];
            string wearText = wear == null || wear.Type == JTokenType.Null
                ? "-" : JsonText.Display(wear);
            bool favourite = item["favourited"] != null &&
                item["favourited"].Type == JTokenType.Boolean &&
                (bool)item["favourited"];
            return string.Format(
                "{0,2}  {1,-17} {2,-20} cond {3,5} slot {4,3} wear {5,-6} {6}",
                index + 1, Clip(id, 17), Clip(name, 20), condition, slot,
                Clip(wearText, 6), favourite ? "FAV" : "");
        }

        private static string ShortNumber(JToken token)
        {
            if (token == null) return "";
            if (token.Type == JTokenType.Float)
            {
                double value = (double)token;
                if (Math.Abs(value) >= 1000.0)
                    return value.ToString("0.###E+0", CultureInfo.InvariantCulture);
                return value.ToString("0.###", CultureInfo.InvariantCulture);
            }
            return JsonText.Display(token);
        }

        private static string Clip(string text, int width)
        {
            if (text == null) return "";
            return text.Length <= width ? text : text.Substring(0, width);
        }

        public override void Handle(ConsoleKeyInfo key)
        {
            int visible = Math.Max(1, _visibleRows);
            if (key.Key == ConsoleKey.UpArrow) { _list.Move(-1, visible); return; }
            if (key.Key == ConsoleKey.DownArrow) { _list.Move(1, visible); return; }
            if (key.Key == ConsoleKey.PageUp) { _list.Move(-visible, visible); return; }
            if (key.Key == ConsoleKey.PageDown) { _list.Move(visible, visible); return; }
            if (key.Key == ConsoleKey.Home) { _list.Home(visible); return; }
            if (key.Key == ConsoleKey.End) { _list.End(visible); return; }
            if (key.Key == ConsoleKey.Enter)
            {
                if (_list.Selected >= 0)
                    App.Navigate(new ItemDetailScreen(App, _list.Selected));
                return;
            }
            if (key.KeyChar == 'l' || key.KeyChar == 'L')
            {
                if (_list.Selected >= 0)
                    App.Navigate(new LiquidsScreen(App, _list.Selected));
                return;
            }
            if (key.KeyChar == 'a') { AddItem(); return; }
            if (key.KeyChar == 'd') { DuplicateItem(); return; }
            if (key.KeyChar == 'x') { RemoveItem(); return; }
            if (key.KeyChar == 'f') { RepairAll(); return; }
        }

        private void AddItem()
        {
            JArray items = Items;
            JArray components = Components;
            if (items == null || components == null) return;

            string id = "bandage";
            if (App.Game != null && App.Game.ItemName(id).Length == 0)
            {
                System.Collections.Generic.IList<string> ids = App.Game.ItemIds;
                if (ids.Count > 0) id = ids[0];
            }

            JObject item = new JObject();
            item["id"] = new JValue(id);
            item["condition"] = new JValue(1.0);
            item["slot"] = new JValue(4L);
            item["wearSlot"] = JValue.CreateNull();
            item["favourited"] = new JValue(false);
            item["label"] = new JValue("");
            item["swatch"] = new JValue(0L);

            items.Add(item);
            components.Add(new JObject());
            App.MarkDirty();
            BuildList();
            _list.Selected = items.Count - 1;
            App.SetMessage("Added " + id + ".", Theme.Good);
        }

        private void DuplicateItem()
        {
            JArray items = Items;
            JArray components = Components;
            if (items == null || components == null) return;
            int index = _list.Selected;
            if (index < 0 || index >= items.Count) return;

            JObject item = (JObject)items[index].DeepClone();
            JObject component = components[index] is JObject
                ? (JObject)components[index].DeepClone() : new JObject();
            items.Insert(index + 1, item);
            components.Insert(index + 1, component);
            App.MarkDirty();
            BuildList();
            _list.Selected = index + 1;
            App.SetMessage("Duplicated item " + (index + 1) + ".", Theme.Good);
        }

        private void RemoveItem()
        {
            JArray items = Items;
            JArray components = Components;
            if (items == null || components == null) return;
            int index = _list.Selected;
            if (index < 0 || index >= items.Count) return;

            JObject item = items[index] as JObject;
            string id = item == null ? "this item" : JsonText.Display(item["id"]);
            App.StartConfirm("Remove " + id + "?  (y) remove  (n) cancel",
                delegate
                {
                    JArray currentItems = Items;
                    JArray currentComponents = Components;
                    if (currentItems == null || currentComponents == null) return;
                    if (index < currentItems.Count) currentItems.RemoveAt(index);
                    if (index < currentComponents.Count) currentComponents.RemoveAt(index);
                    App.MarkDirty();
                    BuildList();
                    if (_list.Selected >= _list.Rows.Count)
                        _list.Selected = _list.Rows.Count - 1;
                    App.SetMessage("Removed item " + (index + 1) + ".", Theme.Good);
                },
                null);
        }

        private void RepairAll()
        {
            JArray items = Items;
            if (items == null) return;
            int count = 0;
            foreach (JToken token in items)
            {
                JObject item = token as JObject;
                if (item != null && item["condition"] != null)
                {
                    item["condition"] = new JValue(1.0);
                    count++;
                }
            }
            App.MarkDirty();
            App.SetMessage("Repaired " + count + " item(s).", Theme.Good);
        }
    }

    public sealed class ItemDetailScreen : FieldListScreen
    {
        private readonly int _index;
        private readonly JObject _item;

        public ItemDetailScreen(TuiApp app, int index) : base(app)
        {
            _index = index;
            JArray items = app.Save.Root["items"] as JArray;
            if (items == null || index < 0 || index >= items.Count) return;
            _item = items[index] as JObject;
            if (_item == null) return;

            Header = "ITEM " + (index + 1);
            ExtraBottomLines = 2;
            BuildFields();
        }

        private void BuildFields()
        {
            AddGroup("Item",
                Ref(_item, "id", "Item ID"),
                Ref(_item, "condition", "Condition"),
                Ref(_item, "slot", "Body slot (-1 = worn/inside)"),
                Ref(_item, "wearSlot", "Container slot (null = worn)"),
                Ref(_item, "favourited", "Favourited"),
                Ref(_item, "label", "Label"),
                Ref(_item, "swatch", "Swatch"));

            HashSet<string> known = new HashSet<string>(StringComparer.Ordinal)
            {
                "id", "condition", "slot", "wearSlot", "favourited", "label",
                "swatch"
            };
            List<string> unknown = _item.Properties()
                .Select(delegate(JProperty p) { return p.Name; })
                .Where(delegate(string key) { return !known.Contains(key); })
                .OrderBy(delegate(string key) { return key; }, StringComparer.Ordinal)
                .ToList();
            if (unknown.Count > 0)
            {
                List.AddHeader("Other item fields");
                foreach (string key in unknown)
                    List.AddItem(new ListRow("", true, false,
                        Ref(_item, key, GameData.Prettify(key))));
            }

            FinishBuild();
        }

        private JObject Component
        {
            get
            {
                JArray components = App.Save.Root["itemComponents"] as JArray;
                if (components == null || _index < 0 || _index >= components.Count)
                    return null;
                return components[_index] as JObject;
            }
        }

        public override string Help
        {
            get
            {
                return "arrows move   enter edit   l liquids   c raw component JSON   " +
                    "s save   esc back";
            }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            string id = _item == null || _item["id"] == null
                ? "" : JsonText.Display(_item["id"]);
            string name = App.Game == null ? "" : App.Game.ItemName(id);
            SubHeader = name.Length > 0 ? name + "  (" + id + ")" : id;
            base.Draw(buffer, layout);

            JObject component = Component;
            List<LiquidEntryRef> liquids = LiquidUtil.Entries(component);
            ItemLiquidInfo itemInfo = LiquidUtil.ItemInfo(id);
            string capacity = itemInfo != null && itemInfo.Capacity > 0.0
                ? " / " + itemInfo.Capacity.ToString("0.###",
                    System.Globalization.CultureInfo.InvariantCulture) + "ml"
                : "";
            string summary;
            if (liquids.Count > 0)
            {
                summary = "Liquids: " + LiquidUtil.Summarize(App.Game, component) +
                    capacity + (itemInfo != null && itemInfo.Injector
                        ? "  injector" : "") +
                    "   (l edit liquids, c raw JSON)";
            }
            else if (itemInfo != null && itemInfo.Capacity > 0.0)
            {
                summary = "Liquid capacity: " + itemInfo.Capacity.ToString(
                    "0.###", System.Globalization.CultureInfo.InvariantCulture) +
                    "ml" + (itemInfo.Injector ? ", injector" : "") +
                    "   (l liquids, c raw JSON)";
            }
            else
            {
                int entries = component == null ? 0 : component.Count;
                summary = "Component data: " + entries +
                    " entr" + (entries == 1 ? "y" : "ies") +
                    "   (l liquids, c raw JSON)";
            }
            buffer.TextClipped(layout.ContentLeft, layout.ContentBottom - 1,
                summary, layout.ContentWidth, Theme.Dim);
        }

        public override void Handle(ConsoleKeyInfo key)
        {
            if (key.KeyChar == 'l' || key.KeyChar == 'L')
            {
                App.Navigate(new LiquidsScreen(App, _index));
                return;
            }
            if (key.KeyChar == 'c' || key.KeyChar == 'C')
            {
                EditComponent();
                return;
            }
            base.Handle(key);
        }

        private void EditComponent()
        {
            JObject component = Component;
            if (component == null)
            {
                component = new JObject();
                JArray components = App.Save.Root["itemComponents"] as JArray;
                if (components == null || _index < 0 || _index >= components.Count)
                    return;
            }
            App.StartText("Component JSON",
                component.ToString(Formatting.None), component,
                delegate(JToken value)
                {
                    JObject obj = value as JObject;
                    if (obj == null)
                    {
                        App.SetMessage("Component data must be a JSON object.",
                            Theme.Danger);
                        return;
                    }
                    JArray components = App.Save.Root["itemComponents"] as JArray;
                    if (components != null && _index >= 0 && _index < components.Count)
                    {
                        components[_index] = obj;
                        App.MarkDirty();
                        App.SetMessage("Component JSON updated.", Theme.Good);
                    }
                });
            if (App.Prompt != null) App.Prompt.MaxLength = 4000;
        }
    }
}
