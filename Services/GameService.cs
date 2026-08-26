namespace Myria.ConsoleApp.Services
{
    public static class GameService
    {
        public static IReadOnlyDictionary<int, Room> Rooms => Myria.Lib.Core.Services.GameService.Rooms;

        public static bool InitializeGame(Character Character)
        {
            try
            {
                // Console-only UI strings (menu/prompt text) live separately from the shared
                // game-content strings in Myria.Lib.Core/Data/locales — registered once,
                // reapplied automatically on every language (re)load.
                Localization.RegisterAdditionalLocaleDirectory("Data/misc/locales");

                SettingsService.Load();
                Localization.Load(Settings.Current.LanguageSettings.Local);

                // Matches Wpf's App.xaml.cs: empty means "use the built-in localhost default".
                if (!string.IsNullOrWhiteSpace(Settings.Current.ServerAddress))
                    ConsoleHubClient.AuthBaseUrl = ConsoleHubClient.NormalizeAddress(Settings.Current.ServerAddress);

                var progress = new Progress<string>(step =>
                    Console.WriteLine($"Loading {step}..."));

                Myria.Lib.Core.Services.GameService.InitializeGame(progress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Initialization failed: {ex.Message}");
                return false;
            }

            return true;
        }

    }

}
