using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CasualtiesSaveEditor.Tui
{
    internal static class TuiProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool smoke = false;
            string savePath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--smoke-test") smoke = true; /// yes this boilerplate offers a smoke test.. im 95% sure this BP was made with some AI, but I don't care, it speeds things up. 
                else if (!args[i].StartsWith("--", StringComparison.Ordinal))
                    savePath = args[i];
            }
            if (string.IsNullOrEmpty(savePath)) savePath = SaveFile.DefaultSavePath();

            SaveFile save;
            try
            {
                save = SaveFile.Load(savePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                TryPause();
                return 1;
            }

            GameData game = new GameData(null, save.FilePath);
            if (smoke) return RunSmoke(save, game);

            TuiApp app = new TuiApp(save, game);
            app.Run();
            return 0;
        }

        private static void TryPause()
        {
            try
            {
                if (!Console.IsInputRedirected)
                {
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey(true);
                }
            }
            catch (Exception) { }
        }

        private static int RunSmoke(SaveFile save, GameData game)
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch (Exception) { }

            Console.WriteLine("save:      " + save.FilePath);
            Console.WriteLine("payload:   " + save.Payload.Length + " bytes");
            Console.WriteLine("body:      " + ((JObject)save.Root["body"]).Count);
            Console.WriteLine("limbs:     " + ((JArray)save.Root["limbs"]).Count);
            Console.WriteLine("items:     " + ((JArray)save.Root["items"]).Count);
            Console.WriteLine("settings:  " + ((JArray)save.Root["runSettings"]).Count);
            Console.WriteLine("catalog:   " + (game.Available ? game.ItemNames.Count +
                " item names" : "unavailable"));
            Console.WriteLine("warnings:  " + save.Warnings.Count);

            TuiApp app = new TuiApp(save, game);
            Dump(app, "MAIN MENU");

            app.Navigate(new BodyMenuScreen(app));
            Dump(app, "EDIT BODY");

            app.Navigate(new BodyScreen(app));
            Dump(app, "BODY & VITALS");

            app.Navigate(new LimbsScreen(app));
            Dump(app, "LIMBS");

            app.Navigate(new LimbDetailScreen(app, 0));
            Dump(app, "LIMB DETAIL");

            app.Navigate(new ItemsScreen(app));
            Dump(app, "ITEMS");

            app.Navigate(new ItemDetailScreen(app, 0));
            Dump(app, "ITEM DETAIL");

            app.Navigate(new LiquidsScreen(app, 0));
            Dump(app, "LIQUIDS (heroin)");

            app.Navigate(new LiquidsScreen(app, 3));
            Dump(app, "LIQUIDS (saline)");

            string[] liquidIds = LiquidUtil.SortedIds(game, null);
            app.Navigate(new LiquidPickerScreen(app, "saline", liquidIds, null));
            Dump(app, "CHOOSE LIQUID");

            app.Navigate(new MindScreen(app));
            Dump(app, "EDIT MIND");

            app.Navigate(new SettingsScreen(app));
            Dump(app, "SETTINGS");

            app.Navigate(new PresetsScreen(app));
            Dump(app, "PRESETS");

            app.Navigate(new HelpScreen(app));
            Dump(app, "HELP");

            Console.WriteLine("TUI SMOKE OK");
            return 0;
        }

        private static void Dump(TuiApp app, string name)
        {
            Console.WriteLine();
            Console.WriteLine("=== " + name + " ===");
            string frame = app.RenderForTest(100, 30);
            string[] lines = frame.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                if (i == lines.Length - 1 && line.Length == 0) continue;
                Console.WriteLine(line);
            }
        }
    }
}
