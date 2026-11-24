using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CasualtiesSaveEditor.Tui
{
    public sealed class FieldRef
    {
        public JObject Container;
        public string Key;
        public string Label;

        public FieldRef(JObject container, string key, string label)
        {
            Container = container;
            Key = key;
            Label = label;
        }

        public JToken Token
        {
            get
            {
                if (Container == null) return null;
                return Container[Key];
            }
        }
    }

    public class FieldListScreen : TuiScreen
    {
        protected readonly ListModel List = new ListModel();
        protected string Header = "";
        protected string SubHeader = "";
        protected int ExtraBottomLines;
        private int _lastVisible = 20;

        public FieldListScreen(TuiApp app) : base(app)
        {
        }

        protected void AddGroup(string title, params FieldRef[] refs)
        {
            List<FieldRef> present = new List<FieldRef>();
            foreach (FieldRef reference in refs)
                if (reference.Token != null) present.Add(reference);
            if (present.Count == 0) return;

            List.AddHeader(title);
            foreach (FieldRef reference in present)
                List.AddItem(new ListRow("", true, false, reference));
        }

        protected static FieldRef Ref(JObject container, string key, string label)
        {
            return new FieldRef(container, key, label);
        }

        protected void FinishBuild()
        {
            List.EnsureSelection();
        }

        protected int VisibleRows(Layout layout)
        {
            int top = ListTop(layout);
            int bottom = ListBottom(layout);
            return Math.Max(1, bottom - top + 1);
        }

        protected int ListTop(Layout layout)
        {
            int lines = 0;
            if (!string.IsNullOrEmpty(Header)) lines++;
            if (!string.IsNullOrEmpty(SubHeader))
            {
                List<string> wrapped = LayoutPainter.Wrap(SubHeader, layout.ContentWidth);
                lines += Math.Min(2, wrapped.Count);
            }
            if (lines > 0) lines++;
            return layout.ContentTop + lines;
        }

        protected int ListBottom(Layout layout)
        {
            return layout.ContentBottom - ExtraBottomLines;
        }

        public override string Help
        {
            get
            {
                return "arrows move   pgup/pgdn page   enter edit or toggle   s save   esc back";
            }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            if (!string.IsNullOrEmpty(Header))
            {
                buffer.TextClipped(layout.ContentLeft, y, Header,
                    layout.ContentWidth, Theme.Header);
                y++;
            }
            if (!string.IsNullOrEmpty(SubHeader))
            {
                List<string> lines = LayoutPainter.Wrap(SubHeader, layout.ContentWidth);
                for (int i = 0; i < lines.Count && i < 2; i++)
                {
                    buffer.TextClipped(layout.ContentLeft, y, lines[i],
                        layout.ContentWidth, Theme.Dim);
                    y++;
                }
            }

            int top = ListTop(layout);
            int bottom = ListBottom(layout);
            int visible = Math.Max(0, bottom - top + 1);
            if (visible <= 0) return;
            _lastVisible = visible;
            List.EnsureSelection();
            List.EnsureVisible(visible);

            for (int i = 0; i < visible; i++)
            {
                int index = List.Scroll + i;
                if (index >= List.Rows.Count) break;
                ListRow row = List.Rows[index];
                int rowY = top + i;
                if (row.Header)
                {
                    buffer.TextClipped(layout.ContentLeft, rowY, row.Text,
                        layout.ContentWidth, Theme.Header);
                    continue;
                }
                FieldRef field = row.Tag as FieldRef;
                if (field == null) continue;
                JToken token = field.Token;
                string value = token == null ? "<missing>" : JsonText.Display(token);
                bool selected = index == List.Selected;

                int labelWidth = Math.Min(30, Math.Max(12, layout.ContentWidth / 2 - 4));
                string label = field.Label;
                if (label.Length > labelWidth) label = label.Substring(0, labelWidth);
                string line = label.PadRight(labelWidth) + "  " + value;
                if (selected)
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
                    if (layout.ContentWidth >= 60)
                    {
                        string key = "[" + field.Key + "]";
                        if (key.Length < layout.ContentWidth)
                        {
                            buffer.TextClipped(layout.ContentLeft + layout.ContentWidth -
                                key.Length, rowY, key, key.Length, Theme.Dim);
                        }
                    }
                }
            }

            if (List.Rows.Count > visible)
            {
                string pos = (List.Selected + 1).ToString() + "/" +
                    List.Rows.Count.ToString();
                buffer.TextClipped(layout.ContentLeft + layout.ContentWidth - pos.Length,
                    layout.ContentTop, pos, pos.Length, Theme.Dim);
            }
        }

        public override void Handle(ConsoleKeyInfo key)
        {
            int visible = Math.Max(1, _lastVisible);
            if (key.Key == ConsoleKey.UpArrow) { List.Move(-1, visible); return; }
            if (key.Key == ConsoleKey.DownArrow) { List.Move(1, visible); return; }
            if (key.Key == ConsoleKey.PageUp) { List.Move(-visible, visible); return; }
            if (key.Key == ConsoleKey.PageDown) { List.Move(visible, visible); return; }
            if (key.Key == ConsoleKey.Home) { List.Home(visible); return; }
            if (key.Key == ConsoleKey.End) { List.End(visible); return; }
            if (key.Key == ConsoleKey.Enter)
            {
                EditSelected();
                return;
            }
        }

        protected void EditSelected()
        {
            FieldRef field = List.Current == null ? null : List.Current.Tag as FieldRef;
            if (field == null) return;
            JToken token = field.Token;
            if (token == null) return;

            if (token.Type == JTokenType.Boolean)
            {
                field.Container[field.Key] = new JValue(!(bool)token);
                App.MarkDirty();
                App.SetMessage(field.Label + ": " +
                    JsonText.Display(field.Container[field.Key]), Theme.Good);
                return;
            }

            App.StartText(field.Label, JsonText.Display(token), token,
                delegate(JToken value)
                {
                    field.Container[field.Key] = value;
                    App.MarkDirty();
                    App.SetMessage(field.Label + ": " + JsonText.Display(value),
                        Theme.Good);
                });
        }
    }

    public static class BodyDefs
    {
        public static readonly Dictionary<string, string> Labels =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "maxSpeed", "Max speed" },
            { "jumpSpeed", "Jump speed" },
            { "moveForce", "Move force" },
            { "temporarySlowdown", "Temporary slowdown" },
            { "slowdownAmount", "Slowdown amount" },
            { "weightOffset", "Weight offset" },
            { "bloodOxygen", "Blood oxygen" },
            { "bloodVolume", "Blood volume" },
            { "heartRate", "Heart rate" },
            { "respiratoryRate", "Respiratory rate" },
            { "bloodPressure", "Blood pressure" },
            { "bloodVesselSize", "Blood vessel size" },
            { "bloodViscosity", "Blood viscosity" },
            { "fibrillationProgress", "Fibrillation progress" },
            { "fibrillationForced", "Fibrillation forced" },
            { "heartRatePressureOffset", "Heart rate pressure offset" },
            { "bloodPressureChangeFromMedicine", "BP change from medicine" },
            { "hasPulmonaryEmbolism", "Pulmonary embolism" },
            { "strokeAmount", "Stroke" },
            { "internalBleeding", "Internal bleeding" },
            { "hemothorax", "Hemothorax" },
            { "septicShock", "Septic shock" },
            { "shock", "Shock" },
            { "painShock", "Pain shock" },
            { "adrenaline", "Adrenaline" },
            { "curAdrenaline", "Current adrenaline" },
            { "happiness", "Happiness" },
            { "hunger", "Hunger" },
            { "thirst", "Thirst" },
            { "stamina", "Stamina" },
            { "energy", "Energy" },
            { "caffeinated", "Caffeinated" },
            { "brainHealth", "Brain health" },
            { "consciousness", "Consciousness" },
            { "temperature", "Temperature (C)" },
            { "sicknessAmount", "Sickness" },
            { "immunity", "Immunity" },
            { "antibioticImmunityTime", "Antibiotic immunity time" },
            { "radiationSickness", "Radiation sickness" },
            { "brainGrowSickness", "Brain growth sickness" },
            { "hearingLoss", "Hearing loss" },
            { "traumaAmount", "Trauma" },
            { "wetness", "Wetness" },
            { "dirtyness", "Dirtiness" },
            { "snowAmount", "Snow amount" },
            { "badSleepAmount", "Bad sleep" },
            { "goodSleepTime", "Good sleep time" },
            { "desensitizedMult", "Desensitization multiplier" },
            { "corpsesSeen", "Corpses seen" },
            { "disfigured", "Disfigured" },
            { "eyeGone", "Eye gone" },
            { "bothEyesGone", "Both eyes gone" },
            { "usedNeuralBooster", "Used neural booster" },
            { "clawHealth", "Claw health" },
            { "clawRegrowTime", "Claw regrow time" },
            { "triedRollingLastStand", "Tried last stand" },
            { "succesfullyRolledLastStand", "Succeeded last stand" },
            { "lastStandTime", "Last stand time" },
            { "venomTotal", "Venom total" },
            { "venomCurrent", "Venom current" },
            { "venomFake", "Fake venom" }
        };

        public static readonly string[][] Groups = new string[][]
        {
            new string[] { "Vitals",
                "hunger", "thirst", "stamina", "energy", "bloodOxygen",
                "bloodVolume", "heartRate", "respiratoryRate", "bloodPressure",
                "temperature", "consciousness", "happiness", "adrenaline",
                "curAdrenaline", "caffeinated" },
            new string[] { "Circulation and blood",
                "bloodVesselSize", "bloodViscosity", "fibrillationProgress",
                "fibrillationForced", "internalBleeding", "hemothorax",
                "hasPulmonaryEmbolism", "strokeAmount",
                "bloodPressureChangeFromMedicine", "heartRatePressureOffset",
                "septicShock", "shock", "painShock" },
            new string[] { "Trauma, infection and status",
                "brainHealth", "traumaAmount", "sicknessAmount", "immunity",
                "antibioticImmunityTime", "radiationSickness",
                "brainGrowSickness", "hearingLoss", "snowAmount",
                "desensitizedMult", "corpsesSeen", "disfigured", "eyeGone",
                "bothEyesGone", "badSleepAmount", "goodSleepTime",
                "wetness", "dirtyness" },
            new string[] { "Movement and special",
                "maxSpeed", "jumpSpeed", "moveForce", "temporarySlowdown",
                "slowdownAmount", "weightOffset", "usedNeuralBooster",
                "clawHealth", "clawRegrowTime", "triedRollingLastStand",
                "succesfullyRolledLastStand", "lastStandTime" },
            new string[] { "Toxins",
                "venomTotal", "venomCurrent", "venomFake" }
        };

        public static readonly HashSet<string> Known = BuildKnown();

        private static HashSet<string> BuildKnown()
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (string[] group in Groups)
                for (int i = 1; i < group.Length; i++) keys.Add(group[i]);
            return keys;
        }

        public static string Label(string key)
        {
            string label;
            return Labels.TryGetValue(key, out label) ? label : GameData.Prettify(key);
        }

        public static void RefillVitals(JObject body)
        {
            SetNumber(body, "hunger", 100.0);
            SetNumber(body, "thirst", 100.0);
            SetNumber(body, "stamina", 100.0);
            SetNumber(body, "energy", 100.0);
            SetNumber(body, "bloodOxygen", 100.0);
            SetNumber(body, "bloodVolume", 100.0);
            SetNumber(body, "consciousness", 100.0);
            SetNumber(body, "brainHealth", 100.0);
            SetNumber(body, "heartRate", 70.0);
            SetNumber(body, "respiratoryRate", 100.0);
            SetNumber(body, "bloodPressure", 120.0);
            SetNumber(body, "temperature", 37.0);
            SetNumber(body, "adrenaline", 0.0);
            SetNumber(body, "curAdrenaline", 0.0);
            SetNumber(body, "caffeinated", 0.0);
        }

        public static void ClearAilments(JObject body)
        {
            string[] numbers = new string[]
            {
                "sicknessAmount", "radiationSickness", "septicShock",
                "internalBleeding", "hemothorax", "fibrillationProgress",
                "strokeAmount", "venomTotal", "venomCurrent", "venomFake",
                "brainGrowSickness", "painShock", "shock", "traumaAmount",
                "hearingLoss", "badSleepAmount", "snowAmount", "dirtyness",
                "wetness", "antibioticImmunityTime"
            };
            foreach (string key in numbers) SetNumber(body, key, 0.0);
            SetBool(body, "fibrillationForced", false);
            SetBool(body, "hasPulmonaryEmbolism", false);
            SetBool(body, "disfigured", false);
            SetBool(body, "eyeGone", false);
            SetBool(body, "bothEyesGone", false);
        }

        private static void SetNumber(JObject body, string key, double value)
        {
            if (body[key] != null) body[key] = new JValue(value);
        }

        private static void SetBool(JObject body, string key, bool value)
        {
            if (body[key] != null) body[key] = new JValue(value);
        }
    }

    public static class LimbOps
    {
        public static void Heal(JObject limb)
        {
            if (limb == null) return;
            string[] numbers = new string[]
            {
                "infectionAmount", "pain", "bleedAmount", "dislocationTimer",
                "boneHealTimer", "disinfectionTime", "bandageSlowAmount",
                "skinHealAmount", "furBloodAmount"
            };
            foreach (string key in numbers)
                if (limb[key] != null) limb[key] = new JValue(0.0);
            if (limb["skinHealth"] != null) limb["skinHealth"] = new JValue(100.0);
            if (limb["muscleHealth"] != null) limb["muscleHealth"] = new JValue(100.0);
            if (limb["shrapnel"] != null) limb["shrapnel"] = new JValue(0);
            SetBool(limb, "dislocated", false);
            SetBool(limb, "broken", false);
            SetBool(limb, "splinted", false);
            SetBool(limb, "infected", false);
            SetBool(limb, "blockedBleeding", false);
        }

        private static void SetBool(JObject obj, string key, bool value)
        {
            if (obj[key] != null) obj[key] = new JValue(value);
        }
    }

    public sealed class BodyScreen : FieldListScreen
    {
        public BodyScreen(TuiApp app) : base(app)
        {
            Header = "BODY & VITALS";
            SubHeader = "Physical state, circulation, trauma and status.";
            JObject body = app.Save.Root["body"] as JObject;
            if (body == null) return;

            HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
            foreach (JProperty property in body.Properties()) present.Add(property.Name);

            foreach (string[] group in BodyDefs.Groups)
            {
                List<FieldRef> refs = new List<FieldRef>();
                for (int i = 1; i < group.Length; i++)
                {
                    if (present.Contains(group[i]))
                        refs.Add(Ref(body, group[i], BodyDefs.Label(group[i])));
                }
                AddGroup(group[0], refs.ToArray());
            }

            List<string> unknown = body.Properties()
                .Select(delegate(JProperty p) { return p.Name; })
                .Where(delegate(string key)
                {
                    return key != "skills" && !BodyDefs.Known.Contains(key);
                })
                .OrderBy(delegate(string key) { return key; }, StringComparer.Ordinal)
                .ToList();
            if (unknown.Count > 0)
            {
                List.AddHeader("Other / not categorized");
                foreach (string key in unknown)
                    List.AddItem(new ListRow("", true, false,
                        Ref(body, key, BodyDefs.Label(key))));
            }

            FinishBuild();
        }

        public override string Help
        {
            get
            {
                return "arrows move   pgup/pgdn page   enter edit or toggle   " +
                    "s save   esc back";
            }
        }
    }

    public sealed class MindScreen : FieldListScreen
    {
        public MindScreen(TuiApp app) : base(app)
        {
            Header = "EDIT MIND";
            SubHeader = "Skills, XP thresholds and mental state.";
            JObject body = app.Save.Root["body"] as JObject;
            if (body == null) return;
            JObject skills = body["skills"] as JObject;

            if (skills != null)
            {
                string[] stats = new string[] { "STR", "RES", "INT" };
                string[] names = new string[] { "STR", "RES", "INT" };
                for (int i = 0; i < stats.Length; i++)
                {
                    string stat = stats[i];
                    List<FieldRef> refs = new List<FieldRef>();
                    AddIf(skills, refs, stat, names[i] + " level");
                    AddIf(skills, refs, "exp" + stat, names[i] + " XP");
                    AddIf(skills, refs, "min" + stat, names[i] + " XP floor");
                    AddIf(skills, refs, "max" + stat, names[i] + " XP ceiling");
                    AddIf(skills, refs, stat + "GainMult", names[i] + " gain multiplier");
                    AddIf(skills, refs, stat + "From10", names[i] + " offset from 10");
                    AddGroup(names[i], refs.ToArray());
                }

                List<string> known = new List<string>();
                foreach (string stat in stats)
                {
                    known.Add(stat);
                    known.Add("exp" + stat);
                    known.Add("min" + stat);
                    known.Add("max" + stat);
                    known.Add(stat + "GainMult");
                    known.Add(stat + "From10");
                }
                List<string> unknown = skills.Properties()
                    .Select(delegate(JProperty p) { return p.Name; })
                    .Where(delegate(string key) { return !known.Contains(key); })
                    .OrderBy(delegate(string key) { return key; }, StringComparer.Ordinal)
                    .ToList();
                if (unknown.Count > 0)
                {
                    List.AddHeader("Other skill fields");
                    foreach (string key in unknown)
                        List.AddItem(new ListRow("", true, false,
                            Ref(skills, key, GameData.Prettify(key))));
                }
            }
            else
            {
                List.AddHeader("No body.skills object in this save.");
            }

            string[] mindKeys = new string[]
            {
                "happiness", "brainHealth", "traumaAmount", "desensitizedMult",
                "brainGrowSickness", "usedNeuralBooster", "shock", "painShock",
                "strokeAmount"
            };
            List<FieldRef> mindRefs = new List<FieldRef>();
            foreach (string key in mindKeys)
                if (body[key] != null)
                    mindRefs.Add(Ref(body, key, BodyDefs.Label(key)));
            AddGroup("Mind state", mindRefs.ToArray());

            FinishBuild();
        }

        private static void AddIf(JObject obj, List<FieldRef> refs, string key,
            string label)
        {
            if (obj[key] != null) refs.Add(new FieldRef(obj, key, label));
        }

        public override string Help
        {
            get
            {
                return "arrows move   enter edit or toggle   s save   esc back";
            }
        }
    }

    public sealed class LimbsScreen : TuiScreen
    {
        private readonly ListModel _list = new ListModel();
        private int _visibleRows = 15;

        public LimbsScreen(TuiApp app) : base(app)
        {
            BuildList();
        }

        private JArray Limbs
        {
            get { return App.Save.Root["limbs"] as JArray; }
        }

        private void BuildList()
        {
            int previous = _list.Selected;
            _list.Clear();
            JArray limbs = Limbs;
            if (limbs == null) return;
            for (int i = 0; i < limbs.Count; i++)
                _list.AddItem(new ListRow("", true, false, i));
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
                return "arrows move   enter inspect   h heal selected   " +
                    "H heal all   s save   esc back";
            }
        }

        public override void Draw(ScreenBuffer buffer, Layout layout)
        {
            int y = layout.ContentTop;
            buffer.TextClipped(layout.ContentLeft, y, "LIMBS",
                layout.ContentWidth, Theme.Header);
            y++;
            buffer.TextClipped(layout.ContentLeft, y,
                "15 fixed slots; names recovered from the player prefab " +
                "(crus = shin / lower leg).",
                layout.ContentWidth, Theme.Dim);
            y += 2;

            int bottom = layout.ContentBottom;
            int visible = Math.Max(1, bottom - y + 1);
            _visibleRows = visible;
            _list.EnsureVisible(visible);
            JArray limbs = Limbs;
            if (limbs == null) return;

            for (int i = 0; i < visible; i++)
            {
                int index = _list.Scroll + i;
                if (index >= _list.Rows.Count) break;
                JObject limb = limbs[index] as JObject;
                string line = FormatLimb(index, limb);
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

            string pos = (_list.Selected + 1).ToString() + "/" +
                _list.Rows.Count.ToString();
            if (_list.Rows.Count > visible)
                buffer.TextClipped(layout.ContentLeft + layout.ContentWidth - pos.Length,
                    layout.ContentTop, pos, pos.Length, Theme.Dim);
        }

        private static string FormatLimb(int index, JObject limb)
        {
            if (limb == null) return "Limb " + (index + 1);
            string flags = "";
            if (IsTrue(limb, "broken")) flags += "  broken";
            if (IsTrue(limb, "infected")) flags += "  infected";
            if (IsTrue(limb, "dismembered")) flags += "  missing";
            return string.Format(
                "{0,2} {1,-18} skin {2,5} muscle {3,5} pain {4,4} bleed {5,4}{6}",
                index + 1,
                LimbCatalog.Label(index),
                JsonText.Display(limb["skinHealth"]),
                JsonText.Display(limb["muscleHealth"]),
                JsonText.Display(limb["pain"]),
                JsonText.Display(limb["bleedAmount"]),
                flags);
        }

        private static bool IsTrue(JObject obj, string key)
        {
            JToken token = obj[key];
            return token != null && token.Type == JTokenType.Boolean && (bool)token;
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
                    App.Navigate(new LimbDetailScreen(App, _list.Selected));
                return;
            }
            if (key.KeyChar == 'h')
            {
                JArray limbs = Limbs;
                if (limbs != null && _list.Selected >= 0 && _list.Selected < limbs.Count)
                {
                    LimbOps.Heal(limbs[_list.Selected] as JObject);
                    App.MarkDirty();
                    App.SetMessage("Healed " +
                        LimbCatalog.Label(_list.Selected) + ".", Theme.Good);
                }
                return;
            }
            if (key.KeyChar == 'H')
            {
                JArray limbs = Limbs;
                if (limbs != null)
                {
                    foreach (JToken token in limbs)
                        LimbOps.Heal(token as JObject);
                    App.MarkDirty();
                    App.SetMessage("Healed all limbs.", Theme.Good);
                }
            }
        }
    }

    public sealed class LimbDetailScreen : FieldListScreen
    {
        private readonly int _index;
        private readonly JObject _limb;

        public LimbDetailScreen(TuiApp app, int index) : base(app)
        {
            _index = index;
            JArray limbs = app.Save.Root["limbs"] as JArray;
            if (limbs == null || index < 0 || index >= limbs.Count) return;
            _limb = limbs[index] as JObject;
            if (_limb == null) return;

            Header = "LIMB " + (index + 1) + " - " + LimbCatalog.Label(index);
            SubHeader = "Save key " + LimbCatalog.Key(index) +
                " (crus = shin / lower leg). Condition, flags, timers and prosthetic data.";
            BuildFromLimb();
        }

        private void BuildFromLimb()
        {
            AddGroup("Condition",
                Ref(_limb, "skinHealth", "Skin health"),
                Ref(_limb, "muscleHealth", "Muscle health"),
                Ref(_limb, "pain", "Pain"),
                Ref(_limb, "bleedAmount", "Bleeding"),
                Ref(_limb, "infectionAmount", "Infection amount"),
                Ref(_limb, "shrapnel", "Shrapnel"));

            AddGroup("Flags",
                Ref(_limb, "dislocated", "Dislocated"),
                Ref(_limb, "broken", "Broken"),
                Ref(_limb, "splinted", "Splinted"),
                Ref(_limb, "infected", "Infected"),
                Ref(_limb, "blockedBleeding", "Bleeding blocked"),
                Ref(_limb, "dismembered", "Dismembered / missing"));

            AddGroup("Healing timers",
                Ref(_limb, "dislocationTimer", "Dislocation timer"),
                Ref(_limb, "boneHealTimer", "Bone heal timer"),
                Ref(_limb, "disinfectionTime", "Disinfection time"),
                Ref(_limb, "bandageSlowAmount", "Bandage slow amount"),
                Ref(_limb, "skinHealAmount", "Skin heal amount"),
                Ref(_limb, "furBloodAmount", "Fur blood"));

            JObject prosthetic = _limb["prostheticInfo"] as JObject;
            if (prosthetic != null)
            {
                AddGroup("Prosthetic (advanced)",
                    Ref(prosthetic, "hasProsthetic", "Has prosthetic"),
                    Ref(prosthetic, "itemId", "Prosthetic item ID"),
                    Ref(prosthetic, "forceChange", "Force change"),
                    Ref(prosthetic, "armorChange", "Armor change"),
                    Ref(prosthetic, "destroyItem", "Destroy item"),
                    Ref(prosthetic, "appearancePath", "Appearance path"),
                    Ref(prosthetic, "maxHoles", "Max holes"),
                    Ref(prosthetic, "r", "Color R"),
                    Ref(prosthetic, "g", "Color G"),
                    Ref(prosthetic, "b", "Color B"));
            }

            HashSet<string> known = new HashSet<string>(StringComparer.Ordinal)
            {
                "skinHealth", "muscleHealth", "pain", "bleedAmount",
                "infectionAmount", "shrapnel", "dislocated", "broken",
                "splinted", "infected", "blockedBleeding", "dismembered",
                "dislocationTimer", "boneHealTimer", "disinfectionTime",
                "bandageSlowAmount", "skinHealAmount", "furBloodAmount",
                "prostheticInfo"
            };
            List<string> unknown = _limb.Properties()
                .Select(delegate(JProperty p) { return p.Name; })
                .Where(delegate(string key) { return !known.Contains(key); })
                .OrderBy(delegate(string key) { return key; }, StringComparer.Ordinal)
                .ToList();
            if (unknown.Count > 0)
            {
                List.AddHeader("Other limb fields");
                foreach (string key in unknown)
                    List.AddItem(new ListRow("", true, false,
                        Ref(_limb, key, GameData.Prettify(key))));
            }

            FinishBuild();
        }

        public override string Help
        {
            get
            {
                return "arrows move   enter edit or toggle   h heal this limb   " +
                    "s save   esc back";
            }
        }

        public override void Handle(ConsoleKeyInfo key)
        {
            if (key.KeyChar == 'h' || key.KeyChar == 'H')
            {
                LimbOps.Heal(_limb);
                App.MarkDirty();
                App.SetMessage("Healed limb " + (_index + 1) + ".", Theme.Good);
                return;
            }
            base.Handle(key);
        }
    }
}
