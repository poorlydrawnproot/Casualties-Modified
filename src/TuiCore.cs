using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CasualtiesSaveEditor.Tui
{
    public struct Style : IEquatable<Style>
    {
        public int Foreground;
        public int Background;
        public bool Bold;
        public bool Dim;

        public Style(int foreground)
        {
            Foreground = foreground;
            Background = -1;
            Bold = false;
            Dim = false;
        }

        public Style(int foreground, int background)
        {
            Foreground = foreground;
            Background = background;
            Bold = false;
            Dim = false;
        }

        public Style(int foreground, int background, bool bold, bool dim)
        {
            Foreground = foreground;
            Background = background;
            Bold = bold;
            Dim = dim;
        }

        public Style WithBold()
        {
            return new Style(Foreground, Background, true, Dim);
        }

        public Style WithDim()
        {
            return new Style(Foreground, Background, Bold, true);
        }

        public bool Equals(Style other)
        {
            return Foreground == other.Foreground &&
                Background == other.Background &&
                Bold == other.Bold &&
                Dim == other.Dim;
        }

        public override bool Equals(object obj)
        {
            return obj is Style && Equals((Style)obj);
        }

        public override int GetHashCode()
        {
            return Foreground ^ (Background * 31) ^ (Bold ? 7 : 0) ^ (Dim ? 13 : 0);
        }

        public string Ansi()
        {
            StringBuilder code = new StringBuilder("\u001b[0");
            if (Foreground >= 0)
                code.Append(";38;5;").Append(Foreground.ToString());
            if (Background >= 0)
                code.Append(";48;5;").Append(Background.ToString());
            if (Bold) code.Append(";1");
            if (Dim) code.Append(";2");
            code.Append('m');
            return code.ToString();
        }
    }

    public static class Theme
    {
        public const int Back = 232;
        public const int Panel = 235;

        public static readonly Style Default = new Style(252, Back);
        public static readonly Style Frame = new Style(240, Back);
        public static readonly Style Title = new Style(220, Back, true, false);
        public static readonly Style Quip = new Style(245, Back, false, true);
        public static readonly Style Text = new Style(252, Back, false, false);
        public static readonly Style Dim = new Style(242, Back, false, false);
        public static readonly Style Header = new Style(117, Back, true, false);
        public static readonly Style Accent = new Style(45, Back, false, false);
        public static readonly Style Selected = new Style(232, 220, true, false);
        public static readonly Style Good = new Style(114, Back, false, false);
        public static readonly Style Warn = new Style(214, Back, false, false);
        public static readonly Style Danger = new Style(203, Back, false, false);
        public static readonly Style Input = new Style(229, 236, false, false);
        public static readonly Style Cursor = new Style(232, 220, true, false);
    }

    public sealed class ScreenBuffer
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        private readonly char[][] _chars;
        private readonly Style[][] _styles;

        public ScreenBuffer(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            _chars = new char[Height][];
            _styles = new Style[Height][];
            for (int y = 0; y < Height; y++)
            {
                _chars[y] = new char[Width];
                _styles[y] = new Style[Width];
                for (int x = 0; x < Width; x++)
                {
                    _chars[y][x] = ' ';
                    _styles[y][x] = Theme.Default;
                }
            }
        }

        public void Clear(Style style)
        {
            Fill(0, 0, Width, Height, ' ', style);
        }

        public void Set(int x, int y, char ch, Style style)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            _chars[y][x] = ch;
            _styles[y][x] = style;
        }

        public void Text(int x, int y, string text, Style style)
        {
            if (text == null) return;
            if (y < 0 || y >= Height) return;
            for (int i = 0; i < text.Length; i++)
            {
                int px = x + i;
                if (px >= Width) break;
                if (px < 0) continue;
                char ch = text[i];
                if (ch == '\r' || ch == '\n') continue;
                _chars[y][px] = ch;
                _styles[y][px] = style;
            }
        }

        public void TextClipped(int x, int y, string text, int maxWidth, Style style)
        {
            if (text == null || maxWidth <= 0) return;
            if (text.Length > maxWidth) text = text.Substring(0, maxWidth);
            Text(x, y, text, style);
        }

        public void Fill(int x, int y, int width, int height, char ch, Style style)
        {
            for (int yy = y; yy < y + height; yy++)
                for (int xx = x; xx < x + width; xx++)
                    Set(xx, yy, ch, style);
        }

        public void FillRow(int y, int x, int width, char ch, Style style)
        {
            for (int xx = x; xx < x + width; xx++)
                Set(xx, y, ch, style);
        }

        public string Render(bool color)
        {
            StringBuilder output = new StringBuilder(Width * Height * 2);
            if (!color)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++) output.Append(_chars[y][x]);
                    output.Append('\n');
                }
                return output.ToString();
            }

            Style current = Theme.Default;
            bool hasStyle = false;
            for (int y = 0; y < Height; y++)
            {
                output.Append("\u001b[").Append((y + 1).ToString()).Append(";1H");
                for (int x = 0; x < Width; x++)
                {
                    Style style = _styles[y][x];
                    if (!hasStyle || !style.Equals(current))
                    {
                        output.Append(style.Ansi());
                        current = style;
                        hasStyle = true;
                    }
                    output.Append(_chars[y][x]);
                }
            }
            output.Append("\u001b[0m");
            return output.ToString();
        }
    }

    public sealed class Layout
    {
        public int ScreenWidth;
        public int ScreenHeight;
        public int PanelLeft;
        public int PanelWidth;
        public int PanelTop;
        public int PanelBottom;
        public int ContentLeft;
        public int ContentWidth;
        public int ContentTop;
        public int ContentBottom;
        public int StatusY;
        public int FooterY;

        public bool TooSmall
        {
            get { return ScreenWidth < 70 || ScreenHeight < 22; }
        }

        public static Layout Create(int width, int height)
        {
            Layout layout = new Layout();
            layout.ScreenWidth = Math.Max(1, width);
            layout.ScreenHeight = Math.Max(1, height);
            layout.PanelWidth = Math.Min(layout.ScreenWidth, 100);
            if (layout.ScreenWidth < 70) layout.PanelWidth = layout.ScreenWidth;
            layout.PanelLeft = (layout.ScreenWidth - layout.PanelWidth) / 2;
            layout.PanelTop = 0;
            layout.PanelBottom = layout.ScreenHeight - 1;
            layout.ContentLeft = layout.PanelLeft + 3;
            layout.ContentWidth = Math.Max(10, layout.PanelWidth - 6);
            layout.ContentTop = 2;
            layout.ContentBottom = layout.PanelBottom - 3;
            layout.StatusY = layout.PanelBottom - 2;
            layout.FooterY = layout.PanelBottom - 1;
            return layout;
        }
    }

    public sealed class Terminal
    {
        private const int StdInputHandle = -10;
        private const int StdOutputHandle = -11;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr handle, uint mode);

        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool Color { get; private set; }
        private bool _initialized;
        private bool _altScreen;
        private bool _savedOutputMode;
        private bool _savedInputMode;
        private uint _oldOutputMode;
        private uint _oldInputMode;
        private string _oldTitle;

        public Terminal()
        {
            Width = 100;
            Height = 30;
            Color = true;
        }

        public void Init()
        {
            try
            {
                _oldTitle = Console.Title;
            }
            catch (Exception) { _oldTitle = null; }

            try
            {
                IntPtr handle = GetStdHandle(StdOutputHandle);
                uint mode;
                if (GetConsoleMode(handle, out mode))
                {
                    _oldOutputMode = mode;
                    _savedOutputMode = true;
                    SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
                }
            }
            catch (Exception) { }

            try
            {
                IntPtr handle = GetStdHandle(StdInputHandle);
                uint mode;
                if (GetConsoleMode(handle, out mode))
                {
                    _oldInputMode = mode;
                    _savedInputMode = true;
                }
            }
            catch (Exception) { }

            try
            {
                Console.TreatControlCAsInput = true;
            }
            catch (Exception) { }

            try
            {
                Console.Title = "Casualties : Modified";
                Console.Write("\u001b[?1049h\u001b[?25l\u001b[2J\u001b[H");
                _altScreen = true;
            }
            catch (Exception) { _altScreen = false; }

            _initialized = true;
            UpdateSize();
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            try
            {
                Console.Write("\u001b[0m\u001b[?25h");
                if (_altScreen) Console.Write("\u001b[?1049l");
            }
            catch (Exception) { }
            try
            {
                Console.CursorVisible = true;
            }
            catch (Exception) { }
            try
            {
                Console.ResetColor();
            }
            catch (Exception) { }

            try
            {
                if (_savedInputMode)
                    SetConsoleMode(GetStdHandle(StdInputHandle), _oldInputMode);
            }
            catch (Exception) { }
            try
            {
                if (_savedOutputMode)
                    SetConsoleMode(GetStdHandle(StdOutputHandle), _oldOutputMode);
            }
            catch (Exception) { }
            try
            {
                if (_oldTitle != null) Console.Title = _oldTitle;
            }
            catch (Exception) { }

            _initialized = false;
        }

        public void UpdateSize()
        {
            try
            {
                Width = Console.WindowWidth;
                Height = Console.WindowHeight;
            }
            catch (Exception)
            {
                Width = 100;
                Height = 30;
            }
            if (Width < 20) Width = 100;
            if (Height < 10) Height = 30;
        }

        public void Draw(ScreenBuffer buffer)
        {
            try
            {
                Console.Out.Write(buffer.Render(Color));
                Console.Out.Flush();
            }
            catch (Exception) { }
        }
    }

    public sealed class ListRow
    {
        public string Text;
        public bool Selectable;
        public bool Header;
        public object Tag;

        public ListRow(string text, bool selectable, bool header, object tag)
        {
            Text = text;
            Selectable = selectable;
            Header = header;
            Tag = tag;
        }
    }

    public sealed class ListModel
    {
        public readonly List<ListRow> Rows = new List<ListRow>();
        public int Selected = -1;
        public int Scroll;

        public void Clear()
        {
            Rows.Clear();
            Selected = -1;
            Scroll = 0;
        }

        public void AddHeader(string text)
        {
            Rows.Add(new ListRow(text, false, true, null));
        }

        public void AddItem(string text, object tag)
        {
            Rows.Add(new ListRow(text, true, false, tag));
        }

        public void AddItem(ListRow row)
        {
            Rows.Add(row);
        }

        public void EnsureSelection()
        {
            if (Rows.Count == 0) { Selected = -1; return; }
            if (Selected >= 0 && Selected < Rows.Count && Rows[Selected].Selectable)
                return;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Selectable)
                {
                    Selected = i;
                    return;
                }
            }
            Selected = -1;
        }

        public void Move(int delta, int visible)
        {
            if (Selected < 0) { EnsureSelection(); return; }
            int index = Selected;
            int direction = delta < 0 ? -1 : 1;
            int steps = Math.Abs(delta);
            for (int s = 0; s < steps; s++)
            {
                int next = index;
                do
                {
                    next += direction;
                    if (next < 0 || next >= Rows.Count) return;
                } while (!Rows[next].Selectable);
                index = next;
            }
            Selected = index;
            EnsureVisible(visible);
        }

        public void Home(int visible)
        {
            EnsureSelection();
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Selectable)
                {
                    Selected = i;
                    Scroll = 0;
                    EnsureVisible(visible);
                    return;
                }
            }
        }

        public void End(int visible)
        {
            for (int i = Rows.Count - 1; i >= 0; i--)
            {
                if (Rows[i].Selectable)
                {
                    Selected = i;
                    EnsureVisible(visible);
                    return;
                }
            }
        }

        public void EnsureVisible(int visible)
        {
            if (visible < 1) visible = 1;
            if (Selected < Scroll) Scroll = Selected;
            if (Selected >= Scroll + visible) Scroll = Selected - visible + 1;
            int maximum = Math.Max(0, Rows.Count - visible);
            if (Scroll > maximum) Scroll = maximum;
            if (Scroll < 0) Scroll = 0;
        }

        public ListRow Current
        {
            get
            {
                if (Selected < 0 || Selected >= Rows.Count) return null;
                return Rows[Selected];
            }
        }
    }

    public static class LayoutPainter
    {
        public static void DrawFrame(ScreenBuffer buffer, Layout layout, string title,
            bool dirty)
        {
            string fullTitle = title + (dirty ? " *" : "");
            buffer.Clear(Theme.Default);

            int top = layout.PanelTop;
            int bottom = layout.PanelBottom;
            int left = layout.PanelLeft;
            int width = layout.PanelWidth;

            buffer.FillRow(top, left, width, '=', Theme.Frame);
            buffer.FillRow(bottom, left, width, '=', Theme.Frame);

            int labelWidth = fullTitle.Length + 2;
            int labelLeft = left + Math.Max(0, (width - labelWidth) / 2);
            buffer.TextClipped(labelLeft, top, " " + fullTitle + " ", width, Theme.Title);

            for (int y = top + 1; y < bottom; y++)
            {
                buffer.Text(left, y, "||", Theme.Frame);
                buffer.Text(left + width - 2, y, "||", Theme.Frame);
            }

            string copyright = "(c) 2026 Nobody in Particular";
            buffer.TextClipped(layout.ContentLeft, layout.FooterY, copyright,
                layout.ContentWidth, Theme.Dim);
        }

        public static void DrawList(ScreenBuffer buffer, Layout layout, ListModel list,
            int top, int bottom, bool showPosition)
        {
            int visible = Math.Max(0, bottom - top + 1);
            if (visible <= 0) return;
            list.EnsureSelection();
            list.EnsureVisible(visible);

            for (int i = 0; i < visible; i++)
            {
                int index = list.Scroll + i;
                if (index >= list.Rows.Count) break;
                ListRow row = list.Rows[index];
                int y = top + i;
                if (index == list.Selected)
                {
                    buffer.FillRow(y, layout.ContentLeft, layout.ContentWidth, ' ',
                        Theme.Selected);
                    buffer.TextClipped(layout.ContentLeft, y, row.Text,
                        layout.ContentWidth, Theme.Selected);
                }
                else
                {
                    Style style = row.Header ? Theme.Header
                        : (row.Selectable ? Theme.Text : Theme.Dim);
                    buffer.TextClipped(layout.ContentLeft, y, row.Text,
                        layout.ContentWidth, style);
                }
            }

            if (showPosition && list.Rows.Count > visible)
            {
                string pos = (list.Selected + 1).ToString() + "/" +
                    list.Rows.Count.ToString();
                buffer.TextClipped(layout.ContentLeft + layout.ContentWidth - pos.Length,
                    bottom, pos, pos.Length, Theme.Dim);
            }
        }

        public static List<string> Wrap(string text, int width)
        {
            List<string> lines = new List<string>();
            if (text == null) return lines;
            if (width < 1) width = 1;
            string[] paragraphs = text.Replace("\r", "").Split('\n');
            foreach (string paragraph in paragraphs)
            {
                if (paragraph.Length == 0)
                {
                    lines.Add("");
                    continue;
                }
                string remaining = paragraph;
                while (remaining.Length > width)
                {
                    int cut = remaining.LastIndexOf(' ', width);
                    if (cut <= 0) cut = width;
                    lines.Add(remaining.Substring(0, cut).TrimEnd());
                    remaining = remaining.Substring(cut).TrimStart();
                }
                if (remaining.Length > 0) lines.Add(remaining);
            }
            return lines;
        }
    }
}
