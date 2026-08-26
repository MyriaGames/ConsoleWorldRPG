using System.Text.Json;

namespace Myria.ConsoleApp.Services
{
    public static class LoginManager
    {
        public static UserAccount UserAccount { get; private set; }
        public static string? SelectedRealmName { get; private set; }

        private const string LocalUserPath = "Data/users/localUser.json";

        // ── Singleplayer ─────────────────────────────────────────────────────────
        // Mirrors the WPF client's ViewModel_StartupMenuPage.SingleCharacterAction: a single
        // hardcoded local account, no server call, straight into character selection.
        public static Character PlaySingleplayer()
        {
            UserAccount user;
            if (File.Exists(LocalUserPath))
            {
                var json = File.ReadAllText(LocalUserPath);
                user = JsonSerializer.Deserialize<UserAccount>(json)!;
            }
            else
            {
                user = new UserAccount { Username = "localUser" };
                Directory.CreateDirectory("Data/users");
                File.WriteAllText(LocalUserPath,
                    JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true }));
            }

            UserAccount = user;
            return SelectOrCreateCharacter(user, user.CharacterNames);
        }

        // ── Multiplayer ──────────────────────────────────────────────────────────
        // Always talks to the server. No local account file is read or written here.
        public static void Register()
        {
            Console.Write(Localization.T("ui_register_username"));
            string username = Console.ReadLine()?.Trim() ?? "";
            if (username.Length < 3 || username.Length > 50)
            {
                Printer.PrintError(Localization.T("console.login.username_length"));
                return;
            }

            Console.Write(Localization.T("ui_register_password"));
            string password = ReadPassword();
            if (password.Length < 8)
            {
                Printer.PrintError(Localization.T("console.login.password_length"));
                return;
            }

            var (ok, error) = ConsoleHubClient.RegisterAsync(username, password).GetAwaiter().GetResult();
            if (ok)
                Printer.PrintSuccess(Localization.T("ui_register_successful") + " " + Localization.T("console.login.can_now_log_in"));
            else
                Printer.PrintError(error ?? Localization.T("ui_register_user_exists"));
            Console.Write(Localization.T("console.login.press_enter"));
            Console.ReadLine();
        }

        public static Character Login()
        {
            while (true)
            {
                Console.Write(Localization.T("ui_login_username") + ": ");
                string username = Console.ReadLine()?.Trim() ?? "";

                Console.Write(Localization.T("ui_login_password") + ": ");
                string password = ReadPassword();

                var (ok, error) = ConsoleHubClient.LoginAsync(username, password).GetAwaiter().GetResult();
                if (!ok)
                {
                    Printer.PrintError(error ?? Localization.T("ui_login_wrong_password"));
                    continue;
                }

                if (!SelectRealm())
                    continue;

                var user = new UserAccount { Username = username, CharacterNames = new() };
                UserAccount = user;

                // Character names are per-realm, so only fetch them now that BaseUrl actually
                // points at the chosen realm (mirrors ViewModel_LobbySelectionPage.JoinAction).
                var charNames = ConsoleHubClient.GetCharacterNamesAsync().GetAwaiter().GetResult();
                return SelectOrCreateCharacter(user, charNames);
            }
        }

        // ── Realm/lobby selection ────────────────────────────────────────────────
        // Mirrors the WPF client's Page_LobbySelection / LobbySelectionService: after auth
        // succeeds we still need to pick which realm (MyriaServer instance) to play on before
        // fetching characters, since character names and saves are per-realm. Returns false if
        // the player backs out (e.g. no realms available), in which case Login() loops back to
        // asking for credentials again.
        private static bool SelectRealm()
        {
            while (true)
            {
                Console.WriteLine("\n" + Localization.T("ui_realm_menu"));
                var realms = ConsoleHubClient.GetRealmsAsync().GetAwaiter().GetResult();

                if (realms.Count == 0)
                {
                    Console.WriteLine(Localization.T("ui_realm_none_available"));
                    return false;
                }

                for (int i = 0; i < realms.Count; i++)
                {
                    var r = realms[i];
                    string status = r.Online ? Localization.T("console.login.realm_online", r.PlayerCount) : Localization.T("console.login.realm_offline");
                    Console.WriteLine(Localization.T("console.login.realm_line", i + 1, r.Name, status));
                }
                Console.WriteLine("r. " + Localization.T("ui_realm_reload"));
                Console.WriteLine("0. " + Localization.T("ui_back"));
                Console.Write(Localization.T("ui_select_option"));

                string input = Console.ReadLine()?.Trim() ?? "";
                if (input == "0") return false;
                if (input.Equals("r", StringComparison.OrdinalIgnoreCase)) continue;

                if (int.TryParse(input, out int idx) && idx >= 1 && idx <= realms.Count)
                {
                    var chosen = realms[idx - 1];
                    if (!chosen.Online)
                    {
                        Console.WriteLine(Localization.T("ui_realm_offline"));
                        continue;
                    }

                    ConsoleHubClient.BaseUrl = chosen.Url;
                    SelectedRealmName        = chosen.Name;
                    return true;
                }

                Console.WriteLine(Localization.T("ui_invalid_option"));
            }
        }

        // Console.ReadLine() returns null on EOF (piped/redirected stdin closing, or a real
        // terminal EOF) rather than throwing — call sites that used `Console.ReadLine()!.Trim()`
        // would crash with an unhandled NullReferenceException in that case. Exit cleanly instead.
        private static string ReadLineOrExit()
        {
            string? line = Console.ReadLine();
            if (line != null) return line;

            Console.WriteLine(Localization.T("app.goodbye"));
            Environment.Exit(0);
            return ""; // unreachable — Environment.Exit does not return
        }

        // Masks typed characters with '*'. Falls back to plain ReadLine when input is
        // redirected (piped/non-interactive), since Console.ReadKey requires a real console.
        private static string ReadPassword()
        {
            if (Console.IsInputRedirected)
                return Console.ReadLine()?.Trim() ?? "";

            var sb = new System.Text.StringBuilder();
            ConsoleKeyInfo key;
            while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        sb.Length--;
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                    Console.Write('*');
                }
            }
            Console.WriteLine();
            return sb.ToString().Trim();
        }

        // ── Shared character select/create menu ─────────────────────────────────
        // Branches on ConsoleHubClient.HasToken: server-backed for multiplayer (Login), purely
        // local CharacterService saves for singleplayer (PlaySingleplayer never sets a token).
        private static Character SelectOrCreateCharacter(UserAccount user, List<string> charNames)
        {
            while (true)
            {
                Console.WriteLine("\n" + Localization.T("ui_character_menu"));
                for (int i = 0; i < charNames.Count; i++)
                    Console.WriteLine(Localization.T("console.login.numbered_line", i + 1, charNames[i]));

                bool canCreate = charNames.Count < 5;
                if (canCreate)
                    Console.WriteLine(Localization.T("ui_character_create"));

                Console.Write(Localization.T("ui_character_selection"));
                string input = ReadLineOrExit().Trim();

                if (input.ToLower() == "c" && canCreate)
                {
                    Console.Write(Localization.T("ui_character_creation_name"));
                    string charName = ReadLineOrExit().Trim();

                    var newCharacter = CreateNewCharacter(charName);

                    if (ConsoleHubClient.HasToken)
                    {
                        bool saved = ConsoleHubClient.SaveCharacterAsync(newCharacter).GetAwaiter().GetResult();
                        if (!saved)
                        {
                            Printer.PrintError(Localization.T("console.login.server_save_failed"));
                            continue;
                        }
                    }
                    else
                    {
                        user.CharacterNames.Add(charName);
                        File.WriteAllText(
                            Path.Combine("Data/users", $"{user.Username}.json"),
                            JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true }));
                        CharacterService.SaveCharacter(user, newCharacter);
                    }

                    return newCharacter;
                }

                if (int.TryParse(input, out int idx) && idx > 0 && idx <= charNames.Count)
                {
                    string selectedName = charNames[idx - 1];
                    if (ConsoleHubClient.HasToken)
                    {
                        var serverCharacter = ConsoleHubClient.LoadCharacterAsync(selectedName).GetAwaiter().GetResult();
                        if (serverCharacter != null) return serverCharacter;
                        Printer.PrintError(Localization.T("console.login.server_load_failed"));
                        continue;
                    }
                    return CharacterService.LoadCharacter(selectedName, user);
                }
            }
        }

        private static Character CreateNewCharacter(string name)
        {
            var Character = new Character(name, new Stats());
            Console.WriteLine(Localization.T("ui_creation_name_set") + Character.Name);
            Console.WriteLine();

            // ── Race selection ────────────────────────────────────────────────
            var allRaces = RaceProfile.All;
            var raceList = allRaces.Keys.ToList();

            Console.WriteLine(Localization.T("ui_creation_choose_race"));
            Console.WriteLine(new string('─', 60));
            for (int i = 0; i < raceList.Count; i++)
            {
                var race = raceList[i];
                var rp   = allRaces[race];
                var b    = rp.BaseStatBonus;
                var g    = rp.StatGrowth;
                Console.WriteLine(Localization.T("console.login.numbered_line", i + 1, race));
                Console.WriteLine($"   {Localization.T($"race.{race}.desc")}");
                Console.WriteLine($"   {Localization.T("ui_creation_race_stats")}: STR {10 + b["STR"],2}  DEX {10 + b["DEX"],2}  END {10 + b["END"],2}  INT {10 + b["INT"],2}  SPR {10 + b["SPR"],2}  HP +{rp.BaseHpBonus}  MP +{rp.BaseManaBonus}");
                Console.WriteLine($"   {Localization.T("ui_creation_race_growth")}: STR +{g["STR"]}  DEX +{g["DEX"]}  END +{g["END"]}  INT +{g["INT"]}  SPR +{g["SPR"]}  HP +{rp.HpPerLevel}  MP +{rp.ManaPerLevel}");
                Console.WriteLine();
            }

            string selectedRace = CharacterRace.Myralu;
            while (true)
            {
                Console.Write(Localization.T("ui_creation_select_race", raceList.Count));
                string raceInput = Console.ReadLine()?.Trim() ?? "";
                if (int.TryParse(raceInput, out int raceIdx) && raceIdx >= 1 && raceIdx <= raceList.Count)
                {
                    selectedRace = raceList[raceIdx - 1];
                    break;
                }
                Printer.PrintError(Localization.T("ui_invalid_option"));
            }

            var raceProfile = allRaces[selectedRace];
            Character.Race               = selectedRace;
            Character.RaceSelected       = true;
            Character.Stats.Strength     = 10 + raceProfile.BaseStatBonus["STR"];
            Character.Stats.Dexterity    = 10 + raceProfile.BaseStatBonus["DEX"];
            Character.Stats.Endurance    = 10 + raceProfile.BaseStatBonus["END"];
            Character.Stats.Intelligence = 10 + raceProfile.BaseStatBonus["INT"];
            Character.Stats.Spirit       = 10 + raceProfile.BaseStatBonus["SPR"];
            Character.Stats.BaseHealth  += raceProfile.BaseHpBonus;
            Character.Stats.BaseMana    += raceProfile.BaseManaBonus;
            Console.WriteLine(Localization.T("ui_creation_race_set") + selectedRace);
            Console.WriteLine();

            // ── Class selection ───────────────────────────────────────────────
            var allowedClasses = ClassProfile.All.Keys
                .Where(c => !raceProfile.ForbiddenClasses.Contains(c))
                .ToList();

            Console.WriteLine(Localization.T("ui_creation_choose_class"));
            Console.WriteLine(Localization.T("ui_creation_classes"));
            foreach (var klasse in allowedClasses)
                Console.WriteLine(klasse);

            Console.Write("> ");
            string classInput = Console.ReadLine()?.Trim().ToLower() ?? "";
            // Match typed class name (case-insensitive) against known class IDs.
            var chosen = allowedClasses
                .FirstOrDefault(c => c.Equals(classInput, StringComparison.OrdinalIgnoreCase))
                ?? allowedClasses.FirstOrDefault(c =>
                    c.Replace("mage", "").Equals(classInput.Replace("mage", ""), StringComparison.OrdinalIgnoreCase))
                ?? CharacterClass.Fighter;
            Character.Class = allowedClasses.Contains(chosen) ? chosen : allowedClasses[0];

            Console.WriteLine(Localization.T("ui_creation_class_set") + Character.Class);
            StartingEquipmentService.GrantStartingEquipment(Character);
            Character.CurrentRoom = RoomService.AllRooms.FirstOrDefault(r => r.Id == 1);
            return Character;
        }

    }

}
