using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CasualtiesSaveEditor.Tui
{
    public sealed class LiquidsScreen : TuiScreen
    {
        private readonly int _itemIndex;
        private readonly JObject _item;
        private readonly ListModel _list = new ListModel();
        private List<LiquidEntryRef> _entries = new List<LiquidEntryRef>();
        private int _visibleRows = 10;

        public LiquidsScreen(TuiApp app, int itemIndex) : base(app)
        {
            _itemIndex = itemIndex;
            JArray items = app.Save.Root["items"] as JArray;
            if (items != null && itemIndex >= 0 && itemIndex < items.Count)
                _item = items[itemIndex] as JObject;
            BuildList();
        }

        private JObject Components
        {
            get
            {
                JArray components = App.Save.Root["itemComponents"] as JArray;
                if (components == null || _itemIndex < 0 ||
                    _itemIndex >= components.Count) return null;
                return components[_itemIndex] as JObject;
            }
        }

        private string ItemId
        {
            get
            {
                JToken id = _item == null ? null : _item["id"];
                return id != null && id.Type == JTokenType.String ? (string)id : "";
            }
        }

        private string ItemName
        {
            get
            {
                if (App.Game == null) return ItemId;
                string name = App.Game.ItemName(ItemId);
                return name.Length > 0 ? name : ItemId;
            }
        }

        private ItemLiquidInfo ItemLiquid
        {
            get { return LiquidUtil.ItemInfo(ItemId); }
        }

        private double Capacity
        {
            get
            {
                ItemLiquidInfo info = ItemLiquid;
                return info == null ? 0.0 : info.Capacity;
            }
        }

        private double TotalAmount()
        {
            double total = 0.0;
            foreach (LiquidStackRef stack in LiquidUtil.FindStacks(Components))
                total += stack.TotalAmount();
            return total;
        }

        private static string Number(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void ReportChange(string message)
        {
            double total = TotalAmount();
            double capacity = Capacity;
            if (capacity > 0.0 && total > capacity + 0.001)
            {
                App.SetMessage(message + "  Over capacity: " + Number(total) +
                    " / " + Number(capacity) + " ml.", Theme.Warn);
            }
            else
            {
                App.SetMessage(message, Theme.Good);
            }
        }

        private void BuildList()
        {
            int previous = _list.Selected;
            _list.Clear();
            _entries = LiquidUtil.Entries(Components);
            for (int i = 0; i < _entries.Count; i++)
                _list.AddItem(new ListRow("", true, false, _entries[i]));
            _list.EnsureSelection();
            if (previous >= 0 && previous < _list.Rows.Count)
                _list.Selected = previous;
        }

        public override void OnShow()
        {
            BuildList();
        }

        public override string Help
        {
            get
            {
                return "enter amount   l liquid id   a add   x remove   c clear   " +
                    "s save   esc back";
            }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            buffer.TextClipped(layout.ContentLeft, y,
                "LIQUIDS  -  " + ItemName, layout.ContentWidth, Theme.Header);
            y++;

            List<LiquidStackRef> stacks = LiquidUtil.FindStacks(Components);
            ItemLiquidInfo itemInfo = ItemLiquid;
            double capacity = Capacity;
            double total = TotalAmount();
            string subheader;
            if (stacks.Count == 0)
            {
                subheader = "No liquid container component."
                    + (capacity > 0.0
                        ? " Capacity " + Number(capacity) + " ml."
                        : "")
                    + " Press a to create a WaterContainerItem stack.";
            }
            else
            {
                string kind = itemInfo != null && itemInfo.Injector
                    ? "injector" : "container";
                string amount = capacity > 0.0
                    ? Number(total) + " / " + Number(capacity) + " ml"
                    : Number(total) + " ml";
                subheader = string.Format("{0}   {1}   total {2}",
                    stacks[0].TypeName, kind, amount);
                if (capacity > 0.0 && total > capacity + 0.001)
                    subheader += "   OVER CAPACITY";
            }
            List<string> wrapped = LayoutPainter.Wrap(subheader, layout.ContentWidth);
            for (int i = 0; i < wrapped.Count && i < 2; i++)
            {
                buffer.TextClipped(layout.ContentLeft, y, wrapped[i],
                    layout.ContentWidth, Theme.Dim);
                y++;
            }
            y++;

            if (_entries.Count == 0)
            {
                buffer.TextClipped(layout.ContentLeft, y,
                    stacks.Count == 0
                        ? "This item has no liquid contents."
                        : "Empty container. Press a to add a liquid.",
                    layout.ContentWidth, Theme.Warn);
            }

            int bottom = layout.ContentBottom;
            int visible = Math.Max(1, bottom - y + 1);
            _visibleRows = visible;
            if (_entries.Count > 0)
            {
                buffer.TextClipped(layout.ContentLeft, y,
                    string.Format("{0,2}  {1,-24} {2,-18} {3,-7} {4,10}",
                        "#", "Liquid", "Liquid ID", "Use", "Amount"),
                    layout.ContentWidth, Theme.Dim);
                y++;
                int rowVisible = Math.Max(1, bottom - y + 1);
                _visibleRows = rowVisible;
                _list.EnsureVisible(rowVisible);
                for (int i = 0; i < rowVisible; i++)
                {
                    int index = _list.Scroll + i;
                    if (index >= _entries.Count) break;
                    LiquidEntryRef entry = _entries[index];
                    string amount = entry.AmountToken == null
                        ? "?"
                        : LiquidUtil.Amount(entry.AmountToken).ToString("0.###",
                            CultureInfo.InvariantCulture);
                    string line = string.Format(
                        "{0,2}  {1,-24} {2,-18} {3,-7} {4,10} ml",
                        index + 1,
                        Clip(LiquidUtil.LiquidName(App.Game, entry.LiquidId), 24),
                        Clip(entry.LiquidId, 18),
                        Clip(LiquidUtil.UseLabel(entry.LiquidId), 7),
                        amount);
                    int rowY = y + i;
                    if (index == _list.Selected)
                    {
                        buffer.FillRow(rowY, layout.ContentLeft,
                            layout.ContentWidth, ' ', Theme.Selected);
                        buffer.TextClipped(layout.ContentLeft, rowY, line,
                            layout.ContentWidth, Theme.Selected);
                    }
                    else
                    {
                        buffer.TextClipped(layout.ContentLeft, rowY, line,
                            layout.ContentWidth, Theme.Text);
                    }
                }
                if (_entries.Count > rowVisible)
                {
                    string pos = (_list.Selected + 1).ToString() + "/" +
                        _entries.Count.ToString();
                    buffer.TextClipped(layout.ContentLeft +
                        layout.ContentWidth - pos.Length, layout.ContentTop,
                        pos, pos.Length, Theme.Dim);
                }
            }
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
            if (key.Key == ConsoleKey.Enter) { EditAmount(); return; }
            if (key.KeyChar == 'l' || key.KeyChar == 'L') { ChangeLiquid(); return; }
            if (key.KeyChar == 'a' || key.KeyChar == 'A') { AddLiquid(); return; }
            if (key.KeyChar == 'x' || key.KeyChar == 'X') { RemoveLiquid(); return; }
            if (key.KeyChar == 'c' || key.KeyChar == 'C') { ClearLiquids(); return; }
        }

        private LiquidEntryRef Selected
        {
            get
            {
                if (_list.Selected < 0 || _list.Selected >= _entries.Count) return null;
                return _entries[_list.Selected];
            }
        }

        private void EditAmount()
        {
            LiquidEntryRef entry = Selected;
            if (entry == null) return;
            JToken amount = entry.AmountToken;
            JToken shape = amount == null ? (JToken)new JValue(0.0) : amount;
            App.StartText("Amount (ml)", JsonText.Display(shape), shape,
                delegate(JToken value)
                {
                    entry.Entry["amount"] = value;
                    App.MarkDirty();
                    ReportChange(LiquidUtil.LiquidName(App.Game, entry.LiquidId) +
                        ": " + Number(LiquidUtil.Amount(value)) + " ml");
                });
        }

        private void ChangeLiquid()
        {
            LiquidEntryRef entry = Selected;
            if (entry == null) return;
            string[] ids = LiquidUtil.SortedIds(App.Game, Components);
            LiquidsScreen self = this;
            App.Navigate(new LiquidPickerScreen(App, entry.LiquidId, ids,
                delegate(string liquidId)
                {
                    entry.Entry["liquidId"] = new JValue(liquidId);
                    App.MarkDirty();
                    ReportChange("Liquid set to " +
                        LiquidUtil.LiquidName(App.Game, liquidId) + " [" +
                        LiquidUtil.UseLabel(liquidId) + "].");
                    self.BuildList();
                }));
        }

        private void AddLiquid()
        {
            JObject components = Components;
            if (components == null) return;
            List<LiquidStackRef> stacks = LiquidUtil.FindStacks(components);

            if (stacks.Count == 0)
            {
                App.StartConfirm(
                    "No liquid container on this item. Create a WaterContainerItem " +
                    "stack?  (y/n)",
                    delegate
                    {
                        LiquidStackRef stack = LiquidUtil.EnsureStack(components);
                        App.MarkDirty();
                        OpenPickerForAdd(stack);
                    },
                    null);
                return;
            }
            OpenPickerForAdd(stacks[0]);
        }

        private void OpenPickerForAdd(LiquidStackRef stack)
        {
            string[] ids = LiquidUtil.SortedIds(App.Game, Components);
            LiquidsScreen self = this;
            App.Navigate(new LiquidPickerScreen(App, "", ids,
                delegate(string liquidId)
                {
                    JToken shape = new JValue(100.0);
                    App.StartText("Amount (ml)", "100", shape,
                        delegate(JToken value)
                        {
                            double amount = (double)value;
                            LiquidUtil.AddEntry(stack, liquidId, amount);
                            App.MarkDirty();
                            self.BuildList();
                            if (self._list.Rows.Count > 0)
                                self._list.Selected = self._list.Rows.Count - 1;
                            self.ReportChange("Added " +
                                LiquidUtil.LiquidName(App.Game, liquidId) + " " +
                                Number(amount) + " ml [" +
                                LiquidUtil.UseLabel(liquidId) + "].");
                        });
                }));
        }

        private void RemoveLiquid()
        {
            LiquidEntryRef entry = Selected;
            if (entry == null) return;
            string name = LiquidUtil.LiquidName(App.Game, entry.LiquidId);
            LiquidsScreen self = this;
            App.StartConfirm("Remove " + name + "?  (y/n)",
                delegate
                {
                    LiquidUtil.RemoveEntry(entry);
                    App.MarkDirty();
                    self.BuildList();
                    self.ReportChange("Removed " + name + ".");
                },
                null);
        }

        private void ClearLiquids()
        {
            JObject components = Components;
            if (components == null) return;
            List<LiquidStackRef> stacks = LiquidUtil.FindStacks(components);
            if (stacks.Count == 0) return;
            LiquidsScreen self = this;
            App.StartConfirm("Clear all liquid contents from this item?  (y/n)",
                delegate
                {
                    foreach (LiquidStackRef stack in stacks)
                        LiquidUtil.Clear(stack);
                    App.MarkDirty();
                    self.BuildList();
                    self.ReportChange("Cleared liquid contents.");
                },
                null);
        }
    }

    public sealed class LiquidPickerScreen : TuiScreen
    {
        private readonly ListModel _list = new ListModel();
        private readonly string[] _ids;
        private readonly Action<string> _onPick;
        private readonly string _current;
        private int _visibleRows = 12;

        public LiquidPickerScreen(TuiApp app, string currentId, string[] ids,
            Action<string> onPick) : base(app)
        {
            _current = currentId == null ? "" : currentId;
            _ids = ids;
            _onPick = onPick;

            _list.AddItem(new ListRow("  Custom liquid ID...", true, false, null));

            List<string> injectable = ids
                .Where(delegate(string id)
                {
                    LiquidTypeInfo info = LiquidCatalog.Info(id);
                    return info != null && info.Injectable;
                })
                .ToList();
            List<string> other = ids
                .Where(delegate(string id)
                {
                    LiquidTypeInfo info = LiquidCatalog.Info(id);
                    return info == null || !info.Injectable;
                })
                .ToList();

            if (injectable.Count > 0)
            {
                _list.AddHeader("  Injectable liquids");
                foreach (string id in injectable) AddLiquidRow(app, id);
            }
            if (other.Count > 0)
            {
                _list.AddHeader("  Other liquids");
                foreach (string id in other) AddLiquidRow(app, id);
            }
            _list.EnsureSelection();
            for (int i = 1; i < _list.Rows.Count; i++)
            {
                if (string.Equals(_list.Rows[i].Tag as string, _current,
                        StringComparison.Ordinal))
                {
                    _list.Selected = i;
                    break;
                }
            }
        }

        private void AddLiquidRow(TuiApp app, string id)
        {
            string name = LiquidUtil.LiquidName(app.Game, id);
            LiquidTypeInfo info = LiquidCatalog.Info(id);
            string flags = "";
            if (info != null)
            {
                if (info.Injectable) flags = "  [injectable]";
                else if (info.HealthUsable) flags = "  [health use]";
            }
            string label = "  " + name + "  (" + id + ")" + flags;
            _list.AddItem(new ListRow(label, true, false, id));
        }

        public override string Help
        {
            get
            {
                return "enter choose   arrows move   [injectable] marks syringe liquids   " +
                    "esc cancel";
            }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            buffer.TextClipped(layout.ContentLeft, y, "CHOOSE LIQUID",
                layout.ContentWidth, Theme.Header);
            y++;
            buffer.TextClipped(layout.ContentLeft, y,
                "Grouped by injectable; custom IDs allowed. Names from the game registry.",
                layout.ContentWidth, Theme.Dim);
            y += 2;

            int bottom = layout.ContentBottom;
            int visible = Math.Max(1, bottom - y + 1);
            _visibleRows = visible;
            _list.EnsureVisible(visible);
            for (int i = 0; i < visible; i++)
            {
                int index = _list.Scroll + i;
                if (index >= _list.Rows.Count) break;
                ListRow row = _list.Rows[index];
                string line = row.Text;
                int rowY = y + i;
                if (index == _list.Selected)
                {
                    buffer.FillRow(rowY, layout.ContentLeft, layout.ContentWidth,
                        ' ', Theme.Selected);
                    buffer.TextClipped(layout.ContentLeft, rowY, line,
                        layout.ContentWidth, Theme.Selected);
                }
                else
                {
                    buffer.TextClipped(layout.ContentLeft, rowY, line,
                        layout.ContentWidth, row.Header ? Theme.Header : Theme.Text);
                }
            }

            if (_list.Rows.Count > visible)
            {
                string pos = (_list.Selected + 1).ToString() + "/" +
                    _list.Rows.Count.ToString();
                buffer.TextClipped(layout.ContentLeft + layout.ContentWidth -
                    pos.Length, layout.ContentTop, pos, pos.Length, Theme.Dim);
            }
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
                if (_list.Selected < 0) return;
                object tag = _list.Rows[_list.Selected].Tag;
                if (tag == null)
                {
                    LiquidPickerScreen self = this;
                    App.StartText("Liquid ID", _current, new JValue(_current),
                        delegate(JToken value)
                        {
                            string id = ((string)value).Trim();
                            if (id.Length == 0)
                            {
                                App.SetMessage("Liquid ID cannot be empty.",
                                    Theme.Danger);
                                return;
                            }
                            App.Back();
                            if (self._onPick != null) self._onPick(id);
                        });
                    if (App.Prompt != null) App.Prompt.MaxLength = 80;
                    return;
                }
                string chosen = (string)tag;
                App.Back();
                if (_onPick != null) _onPick(chosen);
            }
        }
    }
}
