namespace Myria.ConsoleApp.Commands
{
    public static class SkillCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input == "skills")  { ShowSkillsMenu(Character); return true; }
            if (input == "slots")   { SlotsMenu(Character);      return true; }
            return false;
        }

        // ── skills ───────────────────────────────────────────────────────────────

        public static void ShowSkillsMenu(Character Character)
        {
            Console.WriteLine(Localization.T("console.skill.title"));

            // Learned base skills
            var learned = Character.Skills;
            Console.WriteLine(Localization.T("console.skill.learned_header", learned.Count));
            if (learned.Count == 0)
                Console.WriteLine(Localization.T("console.skill.learned_empty"));
            else
                for (int i = 0; i < learned.Count; i++)
                    PrintSkillLine($"  {i + 1,2}.", learned[i]);

            // Active slots
            var slots = Character.SkillSlots;
            Console.WriteLine(Localization.T("console.skill.slots_header", slots.Count, Character.SkillSlotCount));
            if (slots.Count == 0)
                Console.WriteLine(Localization.T("console.skill.slots_empty"));
            else
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    string name = slot.ResolvedSkill?.Name ?? slot.SkillId;
                    Console.WriteLine(Localization.T("console.skill.slot_line", i + 1, name, ""));
                }

            Console.WriteLine(Localization.T("console.skill.footer_hint"));
        }

        // ── slots ─────────────────────────────────────────────────────────────────

        public static void SlotsMenu(Character Character)
        {
            while (true)
            {
                Console.WriteLine(Localization.T("console.skill.slots_menu_header", Character.SkillSlots.Count, Character.SkillSlotCount));

                // Active slots
                Console.WriteLine(Localization.T("console.skill.active_slots_header"));
                if (Character.SkillSlots.Count == 0)
                    Console.WriteLine(Localization.T("console.skill.slots_menu_empty"));
                else
                    for (int i = 0; i < Character.SkillSlots.Count; i++)
                    {
                        var s = Character.SkillSlots[i];
                        Console.WriteLine(Localization.T("console.skill.slot_line", i + 1, s.ResolvedSkill?.Name ?? s.SkillId, ""));
                    }

                // Available skills not yet slotted — build a lettered list
                bool atCap = Character.SkillSlots.Count >= Character.SkillSlotCount;
                var available = BuildAvailableList(Character);

                Console.WriteLine(Localization.T("console.skill.available_header"));
                if (available.Count == 0 || atCap)
                    Console.WriteLine(atCap ? Localization.T("console.skill.slots_full") : Localization.T("console.common.none_paren"));
                else
                    for (int i = 0; i < available.Count; i++)
                    {
                        char letter  = (char)('a' + i);
                        var  (id, name, src) = available[i];
                        Console.WriteLine(Localization.T("console.skill.available_line", letter, name, Localization.T("console.skill.tag_regular")));
                    }

                Console.WriteLine(Localization.T("console.skill.slots_commands_hint"));
                Console.Write("> ");
                string line = Console.ReadLine()?.Trim().ToLower() ?? "";

                if (line == "done") return;

                if (line.StartsWith("slot "))
                {
                    string arg = line[5..].Trim();
                    if (arg.Length == 1 && arg[0] >= 'a')
                    {
                        int idx = arg[0] - 'a';
                        if (idx >= 0 && idx < available.Count)
                        {
                            var (id, name, src) = available[idx];
                            bool serverOk = true;
                            if (ConsoleHubClient.IsConnected)
                                serverOk = ConsoleHubClient.SlotSkillAsync(src.ToString(), id).GetAwaiter().GetResult();

                            if (serverOk && SkillSlotService.TryAddSlot(Character, src, id))
                            {
                                CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
                                Printer.PrintSuccess(Localization.T("console.skill.slot_added", name, Character.SkillSlots.Count));
                            }
                            else
                                Printer.PrintError(Localization.T("console.skill.slot_add_failed"));
                        }
                        else Printer.PrintError(Localization.T("console.skill.no_such_letter"));
                    }
                    else Printer.PrintError(Localization.T("console.skill.slot_usage"));
                }
                else if (line.StartsWith("unslot "))
                {
                    string arg = line[7..].Trim();
                    if (int.TryParse(arg, out int num) && num >= 1 && num <= Character.SkillSlots.Count)
                    {
                        var slot = Character.SkillSlots[num - 1];
                        string name = slot.ResolvedSkill?.Name ?? slot.SkillId;
                        if (ConsoleHubClient.IsConnected)
                            _ = ConsoleHubClient.UnslotSkillAsync(slot.Source.ToString(), slot.SkillId);
                        SkillSlotService.RemoveSlot(Character, slot.Source, slot.SkillId);
                        CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
                        Printer.PrintSuccess(Localization.T("console.skill.slot_removed", name, num));
                    }
                    else Printer.PrintError(Localization.T("console.skill.unslot_usage"));
                }
                else if (line.StartsWith("move "))
                {
                    var parts = line[5..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2
                        && int.TryParse(parts[0], out int from) && from >= 1 && from <= Character.SkillSlots.Count
                        && int.TryParse(parts[1], out int to)   && to   >= 1 && to   <= Character.SkillSlots.Count)
                    {
                        if (ConsoleHubClient.IsConnected)
                            _ = ConsoleHubClient.ReorderSkillSlotAsync(from - 1, to - 1);
                        SkillSlotService.ReorderSlots(Character, from - 1, to - 1);
                        CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
                        Printer.PrintSuccess(Localization.T("console.skill.slot_moved", from, to));
                    }
                    else Printer.PrintError(Localization.T("console.skill.move_usage"));
                }
                else
                {
                    Printer.PrintError(Localization.T("console.skill.unknown_slots_command"));
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void PrintSkillLine(string prefix, Skill skill)
        {
            Console.WriteLine(Localization.T("console.skill.skill_line", prefix, skill.Name.PadRight(22), SkillSummary(skill)));
        }

        private static string SkillSummary(Skill skill)
        {
            string target = skill.Target switch
            {
                SkillTarget.AllEnemies => Localization.T("console.skill.target_all"),
                SkillTarget.Self       => Localization.T("console.skill.target_self"),
                _                      => Localization.T("console.skill.target_single")
            };
            return Localization.T("console.skill.summary", skill.Type.ToString().PadRight(8), target.PadRight(12), skill.ManaCost);
        }

        private static List<(string Id, string Name, SlottedSkillSource Source)> BuildAvailableList(Character Character)
        {
            var list = new List<(string, string, SlottedSkillSource)>();

            foreach (var s in Character.Skills)
            {
                if (!Character.SkillSlots.Any(sl => sl.Source == SlottedSkillSource.Regular && sl.SkillId == s.Id))
                    list.Add((s.Id, s.Name, SlottedSkillSource.Regular));
            }
            return list;
        }
    }
}
