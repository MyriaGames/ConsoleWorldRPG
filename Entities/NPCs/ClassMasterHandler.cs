namespace Myria.ConsoleApp.Entities.NPCs
{
    // Mirrors Myria.Wpf's ClassPanel.
    internal static class ClassMasterHandler
    {
        public static void ClassMasterMenu(Character Character)
        {
            var allClasses  = ClassProfile.All.Keys.ToList();
            if (!RaceProfile.All.TryGetValue(Character.Race, out var raceProfile)) return;

            while (true)
            {
                int  activeLvl = ClassManager.GetClassLevel(Character, Character.Class);
                long activeXp  = ClassManager.GetClassXp(Character, Character.Class);

                Console.WriteLine(Localization.T("console.npc.class_master_title"));
                Console.WriteLine(Localization.T("console.npc.active_class", Character.Class, activeLvl, JobXpService.FormatProgress(activeXp)));
                Console.WriteLine();

                for (int i = 0; i < allClasses.Count; i++)
                {
                    var    cls       = allClasses[i];
                    bool   isActive  = cls == Character.Class;
                    bool   forbidden = raceProfile.ForbiddenClasses.Contains(cls);
                    int    lvl       = ClassManager.GetClassLevel(Character, cls);
                    string progress  = JobXpService.FormatProgress(ClassManager.GetClassXp(Character, cls));

                    string tag = isActive ? Localization.T("console.npc.class_tag_active") : forbidden ? Localization.T("console.npc.class_tag_forbidden") : "";
                    Console.WriteLine(Localization.T("console.npc.class_line", i + 1, cls.PadRight(16), tag.PadRight(12), lvl, progress));
                }

                Console.WriteLine(Localization.T("console.npc.class_menu_leave"));
                Console.Write("> ");
                string input = Console.ReadLine()?.Trim() ?? "";
                if (input == "0") return;

                if (!int.TryParse(input, out int choice) || choice < 1 || choice > allClasses.Count)
                {
                    Printer.PrintError(Localization.T("ui_invalid_option"));
                    continue;
                }

                var chosen = allClasses[choice - 1];

                if (chosen == Character.Class)
                {
                    Console.WriteLine(Localization.T("console.npc.already_active_class"));
                    continue;
                }

                if (raceProfile.ForbiddenClasses.Contains(chosen))
                {
                    Printer.PrintError(Localization.T("console.npc.class_forbidden_for_race", chosen, Character.Race));
                    continue;
                }

                if (!ClassManager.CanChangeClass(Character))
                {
                    var remaining = ClassManager.GetClassChangeCooldownRemaining(Character);
                    int days = (int)Math.Ceiling(remaining.TotalDays);
                    Printer.PrintWarning(Localization.T("console.npc.class_cooldown", days));
                    continue;
                }

                ClassManager.SetClass(Character, chosen);
                if (ConsoleHubClient.IsConnected)
                    _ = ConsoleHubClient.ChangeClassAsync(chosen.ToString());
                Printer.PrintSuccess(Localization.T("console.npc.class_changed", Character.Class));
            }
        }
    }
}
