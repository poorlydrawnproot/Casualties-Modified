using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CasualtiesSaveEditor.Tui
{
    public sealed class MenuEntry
    {
        public char Letter;
        public string Label;
        public Action Action;

        public MenuEntry(char letter, string label, Action action)
        {
            Letter = letter;
            Label = label;
            Action = action;
        }
    }

    public sealed class MainMenuScreen : TuiScreen
    {
        private readonly List<MenuEntry> _entries = new List<MenuEntry>();
        private int _selected;

        public MainMenuScreen(TuiApp app) : base(app)
        {
            _entries.Add(new MenuEntry('A', "Edit Body",
                delegate { App.Navigate(new BodyMenuScreen(App)); }));
            _entries.Add(new MenuEntry('B', "Edit Items",
                delegate { App.Navigate(new ItemsScreen(App)); }));
            _entries.Add(new MenuEntry('C', "Edit Mind",
                delegate { App.Navigate(new MindScreen(App)); }));
            _entries.Add(new MenuEntry('D', "Various Settings and Toggles",
                delegate { App.Navigate(new SettingsScreen(App)); }));
            _entries.Add(new MenuEntry('E', "Cave Johnson, we're done here.",
                delegate { App.RequestExit(); }));
        }

        public override string Help
        {
            get
            {
                return "up/down select   enter choose   A-E shortcuts   " +
                    "s save   F1 help   esc quit";
            }
        }

        public override void OnShow()
        {
            App.ChooseQuip();
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            if (y < layout.ContentBottom) y++;

            List<string> quip = LayoutPainter.Wrap(App.CurrentQuip,
                layout.ContentWidth);
            for (int i = 0; i < quip.Count && i < 5 && y <= layout.ContentBottom; i++)
            {
                buffer.TextClipped(layout.ContentLeft, y, quip[i],
                    layout.ContentWidth, Theme.Quip);
                y++;
            }

            y += 2;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (y > layout.ContentBottom) break;
                MenuEntry entry = _entries[i];
                string line = "  " + entry.Letter + ") " + entry.Label;
                if (i == _selected)
                {
                    buffer.FillRow(y, layout.ContentLeft, layout.ContentWidth, ' ',
                        Theme.Selected);
                    buffer.TextClipped(layout.ContentLeft, y, line,
                        layout.ContentWidth, Theme.Selected);
                }
                else
                {
                    buffer.TextClipped(layout.ContentLeft, y, line,
                        layout.ContentWidth, Theme.Text);
                }
                y++;
            }
        }

        public override void Handle(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.UpArrow)
            {
                _selected--;
                if (_selected < 0) _selected = _entries.Count - 1;
                return;
            }
            if (key.Key == ConsoleKey.DownArrow)
            {
                _selected++;
                if (_selected >= _entries.Count) _selected = 0;
                return;
            }
            if (key.Key == ConsoleKey.Home) { _selected = 0; return; }
            if (key.Key == ConsoleKey.End) { _selected = _entries.Count - 1; return; }
            if (key.Key == ConsoleKey.Enter)
            {
                _entries[_selected].Action();
                return;
            }

            char letter = char.ToUpperInvariant(key.KeyChar);
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Letter == letter)
                {
                    _selected = i;
                    _entries[i].Action();
                    return;
                }
            }
        }
    }

    public class MenuScreen : TuiScreen
    {
        private readonly string _header;
        private readonly string _subHeader;
        private readonly List<MenuEntry> _entries;
        private int _selected;

        public MenuScreen(TuiApp app, string header, string subHeader,
            List<MenuEntry> entries) : base(app)
        {
            _header = header;
            _subHeader = subHeader;
            _entries = entries;
        }

        public override string Help
        {
            get
            {
                return "up/down select   enter choose   letter shortcuts   " +
                    "s save   esc back";
            }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            buffer.TextClipped(layout.ContentLeft, y, _header,
                layout.ContentWidth, Theme.Header);
            y++;
            if (!string.IsNullOrEmpty(_subHeader))
            {
                buffer.TextClipped(layout.ContentLeft, y, _subHeader,
                    layout.ContentWidth, Theme.Dim);
                y++;
            }
            y += 2;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (y > layout.ContentBottom) break;
                MenuEntry entry = _entries[i];
                string line = "  " + entry.Letter + ") " + entry.Label;
                if (i == _selected)
                {
                    buffer.FillRow(y, layout.ContentLeft, layout.ContentWidth, ' ',
                        Theme.Selected);
                    buffer.TextClipped(layout.ContentLeft, y, line,
                        layout.ContentWidth, Theme.Selected);
                }
                else
                {
                    buffer.TextClipped(layout.ContentLeft, y, line,
                        layout.ContentWidth, Theme.Text);
                }
                y++;
            }
        }

        public override void Handle(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.UpArrow)
            {
                _selected--;
                if (_selected < 0) _selected = _entries.Count - 1;
                return;
            }
            if (key.Key == ConsoleKey.DownArrow)
            {
                _selected++;
                if (_selected >= _entries.Count) _selected = 0;
                return;
            }
            if (key.Key == ConsoleKey.Home) { _selected = 0; return; }
            if (key.Key == ConsoleKey.End) { _selected = _entries.Count - 1; return; }
            if (key.Key == ConsoleKey.Enter)
            {
                _entries[_selected].Action();
                return;
            }

            char letter = char.ToUpperInvariant(key.KeyChar);
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Letter == letter)
                {
                    _selected = i;
                    _entries[i].Action();
                    return;
                }
            }
        }
    }

    public sealed class BodyMenuScreen : MenuScreen
    {
        public BodyMenuScreen(TuiApp app) : base(app, "EDIT BODY",
            "Choose a body system.",
            new List<MenuEntry>
            {
                new MenuEntry('A', "Body stats and vitals",
                    delegate { app.Navigate(new BodyScreen(app)); }),
                new MenuEntry('B', "Limbs",
                    delegate { app.Navigate(new LimbsScreen(app)); }),
                new MenuEntry('Q', "Back",
                    delegate { app.Back(); })
            })
        {
        }
    }

    public sealed class SettingsScreen : TuiScreen
    {
        private readonly ListModel _list = new ListModel();
        private int _visibleRows = 12;

        public SettingsScreen(TuiApp app) : base(app)
        {
            BuildList();
        }

        private JArray Settings
        {
            get { return App.Save.Root["runSettings"] as JArray; }
        }

        public override void OnShow()
        {
            BuildList();
        }

        private void BuildList()
        {
            int previous = _list.Selected;
            _list.Clear();
            JArray settings = Settings;
            if (settings == null) return;
            for (int i = 0; i < settings.Count; i++)
                _list.AddItem(new ListRow("", true, false, i));
            _list.EnsureSelection();
            if (previous >= 0 && previous < _list.Rows.Count)
                _list.Selected = previous;
        }

        public override string Help
        {
            get
            {
                return "enter edit or toggle   p presets   arrows move   " +
                    "s save   esc back";
            }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            buffer.TextClipped(layout.ContentLeft, y,
                "VARIOUS SETTINGS AND TOGGLES", layout.ContentWidth, Theme.Header);
            y++;
            buffer.TextClipped(layout.ContentLeft, y,
                "Most are read when a run starts. p opens cheat presets.",
                layout.ContentWidth, Theme.Dim);
            y += 2;

            int listBottom = layout.ContentBottom - 3;
            int visible = Math.Max(1, listBottom - y + 1);
            _visibleRows = visible;
            _list.EnsureVisible(visible);
            JArray settings = Settings;
            if (settings != null)
            {
                for (int i = 0; i < visible; i++)
                {
                    int index = _list.Scroll + i;
                    if (index >= _list.Rows.Count) break;
                    JObject entry = settings[index] as JObject;
                    string key = entry == null || entry["Item1"] == null
                        ? "" : JsonText.Display(entry["Item1"]);
                    string label = App.Game == null ? "" : App.Game.RunLabel(key);
                    string value = entry == null || entry["Item2"] == null
                        ? "" : JsonText.Display(entry["Item2"]);
                    int rowY = y + i;
                    int keyWidth = Math.Min(26, Math.Max(14, layout.ContentWidth / 3));
                    string left = key.PadRight(keyWidth) + " " + label;
                    int valueWidth = Math.Min(16, value.Length);
                    int maxLeft = layout.ContentWidth - valueWidth - 2;
                    if (maxLeft > 0 && left.Length > maxLeft)
                        left = left.Substring(0, maxLeft);
                    string line = left.PadRight(layout.ContentWidth - valueWidth) +
                        value;

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
                            layout.ContentWidth, Theme.Text);
                        if (value.Length > 0)
                        {
                            buffer.TextClipped(layout.ContentLeft +
                                layout.ContentWidth - value.Length, rowY, value,
                                value.Length, Theme.Accent);
                        }
                    }
                }
            }

            if (_list.Rows.Count > visible)
            {
                string pos = (_list.Selected + 1).ToString() + "/" +
                    _list.Rows.Count.ToString();
                buffer.TextClipped(layout.ContentLeft + layout.ContentWidth - pos.Length,
                    layout.ContentTop, pos, pos.Length, Theme.Dim);
            }

            if (_list.Selected >= 0 && settings != null &&
                _list.Selected < settings.Count)
            {
                JObject entry = settings[_list.Selected] as JObject;
                string key = entry == null || entry["Item1"] == null
                    ? "" : JsonText.Display(entry["Item1"]);
                string description = App.Game == null
                    ? "" : App.Game.RunDescription(key);
                if (description.Length > 0)
                {
                    List<string> lines = LayoutPainter.Wrap(description,
                        layout.ContentWidth);
                    int start = layout.ContentBottom - 2;
                    for (int i = 0; i < lines.Count && i < 3; i++)
                        buffer.TextClipped(layout.ContentLeft, start + i,
                            lines[i], layout.ContentWidth, Theme.Dim);
                }
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
            if (key.Key == ConsoleKey.Enter) { EditSelected(); return; }
            if (key.KeyChar == 'p' || key.KeyChar == 'P')
            {
                App.Navigate(new PresetsScreen(App));
            }
        }

        private void EditSelected()
        {
            JArray settings = Settings;
            if (settings == null) return;
            int index = _list.Selected;
            if (index < 0 || index >= settings.Count) return;
            JObject entry = settings[index] as JObject;
            if (entry == null) return;
            JToken token = entry["Item2"];
            if (token == null) return;

            string key = entry["Item1"] == null ? "" : JsonText.Display(entry["Item1"]);
            string label = App.Game == null ? key : App.Game.RunLabel(key);

            if (token.Type == JTokenType.Boolean)
            {
                entry["Item2"] = new JValue(!(bool)token);
                App.MarkDirty();
                App.SetMessage(label + ": " + JsonText.Display(entry["Item2"]),
                    Theme.Good);
                return;
            }

            App.StartText(label, JsonText.Display(token), token,
                delegate(JToken value)
                {
                    entry["Item2"] = value;
                    App.MarkDirty();
                    App.SetMessage(label + ": " + JsonText.Display(value),
                        Theme.Good);
                });
        }
    }

    public sealed class PresetsScreen : TuiScreen
    {
        private readonly ListModel _list = new ListModel();
        private int _selected;

        public PresetsScreen(TuiApp app) : base(app)
        {
            _list.AddItem(new ListRow("Refill vitals", true, false, "vitals"));
            _list.AddItem(new ListRow("Clear ailments", true, false, "ailments"));
            _list.AddItem(new ListRow("Heal all limbs", true, false, "limbs"));
            _list.AddItem(new ListRow("Repair all items", true, false, "items"));
            _list.EnsureSelection();
        }

        public override string Help
        {
            get { return "enter apply preset   up/down select   s save   esc back"; }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            buffer.TextClipped(layout.ContentLeft, y, "CHEAT PRESETS",
                layout.ContentWidth, Theme.Header);
            y++;
            buffer.TextClipped(layout.ContentLeft, y,
                "Presets change the in-memory save immediately. Press s to write.",
                layout.ContentWidth, Theme.Dim);
            y += 2;

            List<string> descriptions = new List<string>
            {
                "Set hunger, thirst, stamina, energy, oxygen, blood and vitals to healthy values.",
                "Zero sickness, radiation, bleeding, fibrillation, venom and other ailments.",
                "Restore skin and muscle health, clear pain, bleeding, infection and fractures.",
                "Set every item's condition to 1.0."
            };

            int bottom = layout.ContentBottom - 3;
            for (int i = 0; i < _list.Rows.Count && y <= bottom; i++)
            {
                string line = "  " + _list.Rows[i].Text;
                if (i == _selected)
                {
                    buffer.FillRow(y, layout.ContentLeft, layout.ContentWidth, ' ',
                        Theme.Selected);
                    buffer.TextClipped(layout.ContentLeft, y, line,
                        layout.ContentWidth, Theme.Selected);
                }
                else
                {
                    buffer.TextClipped(layout.ContentLeft, y, line,
                        layout.ContentWidth, Theme.Text);
                }
                y++;
            }

            List<string> wrap = LayoutPainter.Wrap(descriptions[_selected],
                layout.ContentWidth);
            int start = layout.ContentBottom - 2;
            for (int i = 0; i < wrap.Count && i < 3; i++)
                buffer.TextClipped(layout.ContentLeft, start + i, wrap[i],
                    layout.ContentWidth, Theme.Dim);
        }

        public override void Handle(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.UpArrow)
            {
                _selected--;
                if (_selected < 0) _selected = _list.Rows.Count - 1;
                _list.Selected = _selected;
                return;
            }
            if (key.Key == ConsoleKey.DownArrow)
            {
                _selected++;
                if (_selected >= _list.Rows.Count) _selected = 0;
                _list.Selected = _selected;
                return;
            }
            if (key.Key == ConsoleKey.Enter)
            {
                Apply(_selected);
                return;
            }
            for (int i = 0; i < _list.Rows.Count; i++)
            {
                if (i == key.KeyChar - '1')
                {
                    _selected = i;
                    _list.Selected = i;
                    Apply(i);
                    return;
                }
            }
        }

        private void Apply(int index)
        {
            JObject body = App.Save.Root["body"] as JObject;
            if (index == 0)
            {
                if (body != null) BodyDefs.RefillVitals(body);
                App.SetMessage("Vitals refilled.", Theme.Good);
            }
            else if (index == 1)
            {
                if (body != null) BodyDefs.ClearAilments(body);
                App.SetMessage("Ailments cleared.", Theme.Good);
            }
            else if (index == 2)
            {
                JArray limbs = App.Save.Root["limbs"] as JArray;
                if (limbs != null)
                    foreach (JToken limb in limbs) LimbOps.Heal(limb as JObject);
                App.SetMessage("All limbs healed.", Theme.Good);
            }
            else if (index == 3)
            {
                JArray items = App.Save.Root["items"] as JArray;
                if (items != null)
                {
                    foreach (JToken token in items)
                    {
                        JObject item = token as JObject;
                        if (item != null && item["condition"] != null)
                            item["condition"] = new JValue(1.0);
                    }
                }
                App.SetMessage("All items repaired.", Theme.Good);
            }
            App.MarkDirty();
        }
    }

    public sealed class HelpScreen : TuiScreen
    {
        private static readonly string[] Lines = new string[]
        {
            "GLOBAL",
            "  up / down      move the selection",
            "  enter          choose, open or edit",
            "  s / ctrl+s     save save.sv with a timestamped backup",
            "  esc / q        go back (quit from the main menu)",
            "  F1             this help screen",
            "  ctrl+c         quit",
            "",
            "EDITING",
            "  booleans toggle immediately on enter",
            "  numbers and text open a prompt",
            "  prompt: enter confirms, esc cancels, arrows move the cursor",
            "",
            "SCREENS",
            "  Edit Body      body stats and the 15 limb slots",
            "  Edit Items     inventory, worn items, liquid contents (l) and raw JSON (c)",
            "  Edit Mind      STR/RES/INT skills, XP and mental state",
            "  Settings       run modifiers, toggles and cheat presets (p)",
            "",
            "SAVE NOTES",
            "  15 limbs: head, thorax, abdomen, arms 3-8, legs 9-14; crus = shin.",
            "  Items: slot >= 0 is a body slot; slot -1 + wearSlot means inside that container.",
            "  Liquids: l opens contents; picker marks [injectable] and shows capacity.",
            "",
            "The game must be closed before saving. Unknown save fields are preserved."
        };

        public HelpScreen(TuiApp app) : base(app)
        {
        }

        public override string Help
        {
            get { return "press any key to return   ctrl+c quit"; }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            for (int i = 0; i < Lines.Length && y <= layout.ContentBottom; i++)
            {
                string line = Lines[i];
                Style style = Theme.Text;
                if (line.Length > 0 && line == line.ToUpperInvariant() &&
                    !line.StartsWith(" "))
                    style = Theme.Header;
                else if (line.StartsWith("  "))
                    style = Theme.Dim;
                buffer.TextClipped(layout.ContentLeft, y, line,
                    layout.ContentWidth, style);
                y++;
            }
        }

        public override void Handle(ConsoleKeyInfo key)
        {
            App.Back();
        }
    }
}
