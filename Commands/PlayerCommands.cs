namespace Myria.ConsoleApp.Commands
{
    public static class CharacterCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input == "status")
            {
                Printer.ShowStatus(Character);
                return true;
            }
            else if (input == "character" || input == "stats")
            {
                Printer.ShowCharacter(Character);
                return true;
            }
            else if (input.StartsWith("alloc "))
            {
                AllocStat(input[6..].Trim(), Character);
                return true;
            }
            else if (input == "jobs")
            {
                Printer.ShowJobs(Character);
                return true;
            }
            else if (input.StartsWith("say "))
            {
                string msg = input[4..].Trim();
                if (msg.Length > 500) { Console.WriteLine(Localization.T("console.common.message_too_long")); return true; }
                if (!ConsoleHubClient.IsConnected)
                    Console.WriteLine(Localization.T("console.common.offline"));
                else
                    _ = ConsoleHubClient.SendMessageAsync(msg, "room");
                return true;
            }
            else if (input.StartsWith("g "))
            {
                string msg = input[2..].Trim();
                if (msg.Length > 500) { Console.WriteLine(Localization.T("console.common.message_too_long")); return true; }
                if (!ConsoleHubClient.IsConnected)
                    Console.WriteLine(Localization.T("console.common.offline"));
                else
                    _ = ConsoleHubClient.SendMessageAsync(msg, "global");
                return true;
            }
            else if (input.StartsWith("whisper ") || input.StartsWith("w "))
            {
                int prefixLen = input.StartsWith("w ") ? 2 : 8;
                string rest   = input[prefixLen..].Trim();
                int space = rest.IndexOf(' ');
                if (space < 1) { Console.WriteLine(Localization.T("console.player.whisper_usage")); return true; }
                string target = rest[..space];
                string msg    = rest[(space + 1)..].Trim();
                if (msg.Length > 500) { Console.WriteLine(Localization.T("console.common.message_too_long")); return true; }
                if (!ConsoleHubClient.IsConnected)
                    Console.WriteLine(Localization.T("console.common.offline"));
                else
                    _ = ConsoleHubClient.SendMessageAsync(msg, "whisper", target);
                return true;
            }
            else if (input == "players")
            {
                if (!ConsoleHubClient.IsConnected)
                {
                    Console.WriteLine(Localization.T("console.common.offline"));
                }
                else
                {
                    var others = ConsoleHubClient.CurrentRoomCharacters
                        .Where(n => n != Character.Name)
                        .ToList();
                    if (others.Count == 0)
                        Console.WriteLine(Localization.T("console.player.no_others"));
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine(Localization.T("console.player.characters_here", string.Join(", ", others)));
                        Console.ResetColor();
                    }
                }
                return true;
            }
            else if (input == "help")
            {
                Printer.ShowHelp();
                return true;
            }
            else if (input == "heal")
            {
                Console.WriteLine(Localization.T("console.player.heal_disabled"));
                return true;
            }
            else if (input == "logout")
            {
                if (ConsoleHubClient.HasToken)
                {
                    // See Game.SaveCharacter's remarks: SaveSession alone (the server's own
                    // authoritative session state) - not also a separate client-local REST save
                    // that could overwrite it with stale data.
                    bool ok = ConsoleHubClient.IsConnected && ConsoleHubClient.SaveSessionAsync().GetAwaiter().GetResult();
                    if (!ok)
                    {
                        Printer.PrintWarning(Localization.T("console.player.server_save_failed"));
                        CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
                    }
                }
                else
                {
                    CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
                }
                Console.Clear();
                Game.LogOut = true;
                return true;
            }

            return false;
        }

        private static void AllocStat(string args, Character Character)
        {
            string[] parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                Console.WriteLine(Localization.T("console.player.alloc_usage"));
                return;
            }
            string statKey = parts[0].ToUpper();
            int amount = parts.Length > 1 && int.TryParse(parts[1], out int n) ? n : 1;
            amount = Math.Min(amount, Character.Stats.UnusedPoints);
            if (amount <= 0)
            {
                Console.WriteLine(Localization.T("console.player.no_unspent_points"));
                return;
            }
            switch (statKey)
            {
                case "STR": Character.Stats.StrengthBonus     += amount; break;
                case "DEX": Character.Stats.DexterityBonus    += amount; break;
                case "END": Character.Stats.EnduranceBonus    += amount; break;
                case "INT": Character.Stats.IntelligenceBonus += amount; break;
                case "SPR": Character.Stats.SpiritBonus       += amount; break;
                default:
                    Console.WriteLine(Localization.T("console.player.unknown_stat", statKey));
                    return;
            }
            Character.Stats.UnusedPoints -= amount;
            Console.WriteLine(Localization.T("console.player.stat_allocated", amount, statKey, Character.Stats.UnusedPoints));

            _ = ConsoleHubClient.SyncStatAllocationAsync(
                Character.Stats.StrengthAdded, Character.Stats.DexterityAdded, Character.Stats.EnduranceAdded,
                Character.Stats.IntelligenceAdded, Character.Stats.SpiritAdded, Character.Stats.UnusedPoints);
        }

    }

}
