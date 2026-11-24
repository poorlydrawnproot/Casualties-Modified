using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace CasualtiesSaveEditor.Tui
{
    public abstract class TuiScreen
    {
        protected readonly TuiApp App;

        protected TuiScreen(TuiApp app)
        {
            App = app;
        }

        public virtual string Help
        {
            get { return "arrows move   enter select   s save   F1 help   esc back"; }
        }

        public virtual void OnShow() { }
        public abstract void Draw(ScreenBuffer buffer, Layout layout);
        public virtual void Handle(ConsoleKeyInfo key) { }
    }

    public sealed class TextPrompt
    {
        public string Label;
        public string Value;
        public int Cursor;
        public int MaxLength;
        public JToken Original;
        public Action<JToken> Commit;
        public string Error;

        public TextPrompt()
        {
            Value = "";
            MaxLength = 400;
        }
    }

    public sealed class ConfirmPrompt
    {
        public string Message;
        public Action Yes;
        public Action No;

        public ConfirmPrompt(string message, Action yes, Action no)
        {
            Message = message;
            Yes = yes;
            No = no;
        }
    }

    public sealed class TuiApp
    {
        public SaveFile Save { get; private set; }
        public GameData Game { get; private set; }
        public bool Dirty { get; private set; }
        public string Message { get; private set; }
        public Style MessageStyle { get; private set; }
        public TextPrompt Prompt { get; private set; }
        public ConfirmPrompt Confirm { get; private set; }
        public string CurrentQuip { get; private set; }

        private readonly Terminal _terminal;
        private readonly Stack<TuiScreen> _history = new Stack<TuiScreen>();
        private TuiScreen _current;
        private bool _running;
        private readonly Random _random = new Random();
        private DateTime _nextQuip;

        private const string FrameTitle = "CASUALTIES : MODIFIED";

        public static readonly string[] Quips = new string[]
        {
            "Would anybody like to make their casualties known? Just me? Aww..",
            "Give your experiment superpowers!",
            "You can really mess an Expie up here.",
            "Godmode, but for the rest of us.",
            "Hey Orsoniks if you're reading this come say hi to " +
                "@justanothernovabeast on the cord!",
            "In case of an investigation by any federal entity or similar, I do " +
                "not have any involvement with this group or with the people in it, " +
                "I do not know how I am here, probably added by a third party, I do " +
                "not support any actions by the member of this group.",
            "Making my way down, falling fast, corpses past im deathbound!",
            "But can it fix this Sawians Spike Trap mishap? No-",
            "Tutorial at https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            "ESBU <-On-> ^Top!",
            "Some folks may say to not the Expie, I say do.",
            "Shoutout to everybody who made Casualties Unknown, great game by the way.",
            "C:U PS3 Port When",
            "This project is free software!"
        };

        public TuiApp(SaveFile save, GameData game)
        {
            Save = save;
            Game = game;
            _terminal = new Terminal();
            MessageStyle = Theme.Dim;
            _current = new MainMenuScreen(this);
            ChooseQuip();
        }

        public TuiScreen Current
        {
            get { return _current; }
        }

        public void Run()
        {
            _terminal.Init();
            _running = true;
            try
            {
                bool redraw = true;
                _nextQuip = DateTime.Now.AddSeconds(9);
                while (_running)
                {
                    _terminal.UpdateSize();
                    if (redraw) Draw();

                    if (DateTime.Now >= _nextQuip && _current is MainMenuScreen)
                    {
                        ChooseQuip();
                        _nextQuip = DateTime.Now.AddSeconds(9);
                        Draw();
                    }

                    int width = _terminal.Width;
                    int height = _terminal.Height;
                    bool keyAvailable = false;
                    try
                    {
                        keyAvailable = Console.KeyAvailable;
                    }
                    catch (Exception) { }

                    if (keyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        Message = null;
                        HandleKey(key);
                        _terminal.UpdateSize();
                        if (_terminal.Width != width || _terminal.Height != height)
                            redraw = true;
                        redraw = true;
                    }
                    else
                    {
                        Thread.Sleep(60);
                        _terminal.UpdateSize();
                        if (_terminal.Width != width || _terminal.Height != height)
                            redraw = true;
                    }
                }
            }
            finally
            {
                _terminal.Shutdown();
            }
        }

        public void Navigate(TuiScreen screen)
        {
            if (_current != null) _history.Push(_current);
            _current = screen;
            _current.OnShow();
            Message = null;
            if (_current is MainMenuScreen) _nextQuip = DateTime.Now.AddSeconds(9);
        }

        public void Back()
        {
            if (_history.Count == 0)
            {
                RequestExit();
                return;
            }
            _current = _history.Pop();
            _current.OnShow();
            Message = null;
            if (_current is MainMenuScreen)
                _nextQuip = DateTime.Now.AddSeconds(9);
        }

        public void RequestExit()
        {
            if (!Dirty)
            {
                _running = false;
                return;
            }
            Confirm = new ConfirmPrompt(
                "Unsaved changes. Save before leaving?  (y) save  (n) discard  (esc) cancel",
                delegate
                {
                    if (SaveChanges()) _running = false;
                },
                delegate
                {
                    _running = false;
                });
        }

        public bool SaveChanges()
        {
            if (Save == null) return false;

            if (SaveFile.IsGameRunning())
            {
                SetMessage("CasualtiesUnknown.exe is running. Close the game before saving.",
                    Theme.Danger);
                return false;
            }

            try
            {
                string backup = Save.Save(Save.FilePath);
                Dirty = false;
                string text = "Saved " + Save.FilePath;
                if (backup != null) text += "  (backup " + System.IO.Path.GetFileName(backup) + ")";
                SetMessage(text, Theme.Good);
                return true;
            }
            catch (Exception ex)
            {
                SetMessage(ex.Message, Theme.Danger);
                return false;
            }
        }

        public void MarkDirty()
        {
            Dirty = true;
        }

        public void SetMessage(string message, Style style)
        {
            Message = message;
            MessageStyle = style;
        }

        public void StartText(string label, string initial, JToken original,
            Action<JToken> commit)
        {
            TextPrompt prompt = new TextPrompt();
            prompt.Label = label;
            prompt.Value = initial == null ? "" : initial;
            prompt.Cursor = prompt.Value.Length;
            prompt.Original = original;
            prompt.Commit = commit;
            Prompt = prompt;
            Confirm = null;
            Message = null;
        }

        public void StartConfirm(string message, Action yes, Action no)
        {
            Confirm = new ConfirmPrompt(message, yes, no);
            Prompt = null;
            Message = null;
        }

        public void ChooseQuip()
        {
            if (Quips.Length == 0) { CurrentQuip = ""; return; }
            int index;
            do { index = _random.Next(Quips.Length); }
            while (Quips.Length > 1 && Quips[index] == CurrentQuip);
            CurrentQuip = Quips[index];
        }

        public void Draw()
        {
            _terminal.UpdateSize();
            Layout layout = Layout.Create(_terminal.Width, _terminal.Height);
            ScreenBuffer buffer = new ScreenBuffer(_terminal.Width, _terminal.Height);
            buffer.Clear(Theme.Default);

            if (layout.TooSmall)
            {
                string message = "Please enlarge the window to at least 70 x 22.";
                buffer.TextClipped(Math.Max(0, (_terminal.Width - message.Length) / 2),
                    _terminal.Height / 2, message, _terminal.Width, Theme.Warn);
                _terminal.Draw(buffer);
                return;
            }

            LayoutPainter.DrawFrame(buffer, layout, FrameTitle, Dirty);
            _current.Draw(buffer, layout);

            int statusY = layout.StatusY;
            if (Prompt != null)
            {
                if (!string.IsNullOrEmpty(Prompt.Error))
                {
                    buffer.TextClipped(layout.ContentLeft, statusY - 1,
                        Prompt.Error, layout.ContentWidth, Theme.Danger);
                }
                DrawPrompt(buffer, layout, statusY);
            }
            else if (Confirm != null)
            {
                List<string> lines = LayoutPainter.Wrap(Confirm.Message,
                    layout.ContentWidth);
                int start = Math.Max(layout.ContentTop, statusY - lines.Count + 1);
                for (int i = 0; i < lines.Count && start + i <= statusY; i++)
                    buffer.TextClipped(layout.ContentLeft, start + i, lines[i],
                        layout.ContentWidth, Theme.Warn);
            }
            else
            {
                if (Message != null)
                {
                    buffer.TextClipped(layout.ContentLeft, statusY, Message,
                        layout.ContentWidth, MessageStyle);
                }
                else
                {
                    buffer.TextClipped(layout.ContentLeft, statusY, _current.Help,
                        layout.ContentWidth - 10, Theme.Dim);
                    string state = Dirty ? "UNSAVED" : "saved";
                    buffer.TextClipped(layout.ContentLeft + layout.ContentWidth - state.Length,
                        statusY, state, state.Length,
                        Dirty ? Theme.Warn : Theme.Dim);
                }
            }

            _terminal.Draw(buffer);
        }

        private void DrawPrompt(ScreenBuffer buffer, Layout layout, int statusY)
        {
            string prefix = Prompt.Label + ": ";
            int fieldWidth = layout.ContentWidth - prefix.Length - 1;
            if (fieldWidth < 10) fieldWidth = 10;

            int start = 0;
            if (Prompt.Cursor >= fieldWidth)
                start = Prompt.Cursor - fieldWidth + 1;
            int length = Math.Min(fieldWidth, Math.Max(0, Prompt.Value.Length - start));
            string window = Prompt.Value.Substring(start, length);

            buffer.TextClipped(layout.ContentLeft, statusY, prefix,
                layout.ContentWidth, Theme.Header);
            int valueX = layout.ContentLeft + prefix.Length;
            buffer.TextClipped(valueX, statusY, window,
                layout.ContentWidth - prefix.Length - 1, Theme.Input);
            int cursorX = valueX + (Prompt.Cursor - start);
            if (cursorX < layout.ContentLeft + layout.ContentWidth)
                buffer.Set(cursorX, statusY, ' ', Theme.Cursor);
        }

        private void HandleKey(ConsoleKeyInfo key)
        {
            if (Confirm != null)
            {
                HandleConfirm(key);
                return;
            }
            if (Prompt != null)
            {
                HandlePrompt(key);
                return;
            }

            bool control = (key.Modifiers & ConsoleModifiers.Control) != 0;
            if (control && key.Key == ConsoleKey.C)
            {
                RequestExit();
                return;
            }
            if (key.Key == ConsoleKey.F1)
            {
                if (!(_current is HelpScreen))
                    Navigate(new HelpScreen(this));
                return;
            }
            if (key.Key == ConsoleKey.S && control)
            {
                SaveChanges();
                return;
            }
            if (key.KeyChar == 's' || key.KeyChar == 'S')
            {
                SaveChanges();
                return;
            }
            if (key.Key == ConsoleKey.Escape)
            {
                Back();
                return;
            }
            if (key.KeyChar == 'q' || key.KeyChar == 'Q')
            {
                Back();
                return;
            }
            _current.Handle(key);
        }

        private void HandleConfirm(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Escape)
            {
                Confirm = null;
                SetMessage("Canceled.", Theme.Dim);
                return;
            }
            char ch = char.ToLowerInvariant(key.KeyChar);
            if (ch == 'y' || key.Key == ConsoleKey.Enter)
            {
                Action yes = Confirm.Yes;
                Confirm = null;
                if (yes != null) yes();
            }
            else if (ch == 'n')
            {
                Action no = Confirm.No;
                Confirm = null;
                if (no != null) no();
            }
        }

        private void HandlePrompt(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Escape)
            {
                Prompt = null;
                SetMessage("Edit canceled.", Theme.Dim);
                return;
            }
            if (key.Key == ConsoleKey.Enter)
            {
                JToken value;
                try
                {
                    value = JsonText.Parse(Prompt.Original, Prompt.Value, Prompt.Label);
                }
                catch (Exception ex)
                {
                    Prompt.Error = ex.Message;
                    return;
                }
                Action<JToken> commit = Prompt.Commit;
                Prompt = null;
                if (commit != null) commit(value);
                return;
            }

            Prompt.Error = null;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (Prompt.Cursor > 0)
                {
                    Prompt.Value = Prompt.Value.Remove(Prompt.Cursor - 1, 1);
                    Prompt.Cursor--;
                }
                return;
            }
            if (key.Key == ConsoleKey.Delete)
            {
                if (Prompt.Cursor < Prompt.Value.Length)
                    Prompt.Value = Prompt.Value.Remove(Prompt.Cursor, 1);
                return;
            }
            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (Prompt.Cursor > 0) Prompt.Cursor--;
                return;
            }
            if (key.Key == ConsoleKey.RightArrow)
            {
                if (Prompt.Cursor < Prompt.Value.Length) Prompt.Cursor++;
                return;
            }
            if (key.Key == ConsoleKey.Home)
            {
                Prompt.Cursor = 0;
                return;
            }
            if (key.Key == ConsoleKey.End)
            {
                Prompt.Cursor = Prompt.Value.Length;
                return;
            }

            char ch = key.KeyChar;
            if (ch >= ' ' && ch != '\u007f' && Prompt.Value.Length < Prompt.MaxLength)
            {
                Prompt.Value = Prompt.Value.Insert(Prompt.Cursor, ch.ToString());
                Prompt.Cursor++;
            }
        }

        public string RenderForTest(int width, int height)
        {
            Layout layout = Layout.Create(width, height);
            ScreenBuffer buffer = new ScreenBuffer(width, height);
            buffer.Clear(Theme.Default);
            LayoutPainter.DrawFrame(buffer, layout, FrameTitle, Dirty);
            _current.Draw(buffer, layout);
            buffer.TextClipped(layout.ContentLeft, layout.StatusY, _current.Help,
                layout.ContentWidth, Theme.Dim);
            return buffer.Render(false);
        }
    }
}
