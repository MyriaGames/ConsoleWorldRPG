namespace Myria.ConsoleApp.Commands
{
    public static class SkillCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input == "skills")  { ShowSkillsMenu(Character); return true; }
            if (input == "combine") { CombineMenu(Character);    return true; }
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

            // Combined skills
            var combined = Character.CombinedSkills.Where(c => c.ResolvedSkill != null).ToList();
            Console.WriteLine(Localization.T("console.skill.combined_header", combined.Count));
            if (combined.Count == 0)
                Console.WriteLine(Localization.T("console.skill.combined_empty"));
            else
                foreach (var c in combined)
                {
                    var sk = c.ResolvedSkill!;
                    var src = string.Join(" + ", c.SkillIds.Select(id =>
                        learned.FirstOrDefault(s => s.Id == id)?.Name ?? id));
                    Console.WriteLine(Localization.T("console.skill.combined_line", c.DisplayName.PadRight(22), SkillSummary(sk).PadRight(30), src));
                }

            // Active slots
            var slots = Character.SkillSlots;
            Console.WriteLine(Localization.T("console.skill.slots_header", slots.Count, Character.SkillSlotCount));
            if (slots.Count == 0)
                Console.WriteLine(Localization.T("console.skill.slots_empty"));
            else
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    string tag = slot.Source switch
                    {
                        SlottedSkillSource.Combined       => Localization.T("console.skill.tag_combined"),
                        SlottedSkillSource.CompositeFusion => Localization.T("console.skill.tag_fusion"),
                        _ => ""
                    };
                    string name = slot.ResolvedSkill?.Name ?? slot.SkillId;
                    Console.WriteLine(Localization.T("console.skill.slot_line", i + 1, name, tag));
                }

            Console.WriteLine(Localization.T("console.skill.footer_hint"));
        }

        // ── combine ───────────────────────────────────────────────────────────────

        public static void CombineMenu(Character Character)
        {
            var learned = Character.Skills;
            if (learned.Count < 2)
            {
                Console.WriteLine(Localization.T("console.skill.combine_need_more"));
                return;
            }

            Console.WriteLine(Localization.T("console.skill.combine_title"));
            Console.WriteLine(Localization.T("console.skill.combine_hint"));
            Console.WriteLine(Localization.T("console.skill.combine_cancel_hint"));

            for (int i = 0; i < learned.Count; i++)
                PrintSkillLine($"  {i + 1,2}.", learned[i]);

            while (true)
            {
                Console.Write("\n> ");
                string raw = Console.ReadLine()?.Trim() ?? "";
                if (raw.Equals("cancel", StringComparison.OrdinalIgnoreCase)) return;

                var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var ids   = new List<string>();
                bool valid = true;

                foreach (var part in parts)
                {
                    if (!int.TryParse(part, out int idx) || idx < 1 || idx > learned.Count)
                    {
                        Printer.PrintError(Localization.T("console.skill.invalid_number", part));
                        valid = false;
                        break;
                    }
                    ids.Add(learned[idx - 1].Id);
                }

                if (!valid) continue;

                if (ids.Count < 2 || ids.Count > 5)
                {
                    Printer.PrintError(Localization.T("console.skill.choose_2_to_5"));
                    continue;
                }

                // Preview the result before committing
                var preview = SkillCombinationService.Combine(ids);
                if (preview == null)
                {
                    Printer.PrintError(Localization.T("console.skill.combine_failed"));
                    continue;
                }

                string namesStr = string.Join(" + ", ids.Select(id =>
                    learned.FirstOrDefault(s => s.Id == id)?.Name ?? id));
                Console.WriteLine(Localization.T("console.skill.combining_line", namesStr));
                Console.WriteLine(Localization.T("console.skill.result_line", preview.Name, SkillSummary(preview)));
                Console.Write(Localization.T("console.common.confirm_prompt"));
                string confirm = Console.ReadLine()?.Trim().ToLower() ?? "";
                if (confirm != "y") { Console.WriteLine(Localization.T("msg.cancelled")); continue; }

                var result = SkillCombinationService.TryCreateForCharacter(Character, ids);
                if (result == null)
                    Printer.PrintError(Localization.T("console.skill.combine_duplicate"));
                else
                {
                    if (ConsoleHubClient.IsConnected)
                        _ = ConsoleHubClient.CombineSkillsAsync(ids);
                    CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
                    Printer.PrintSuccess(Localization.T("console.skill.combine_created", result.DisplayName));
                    Console.WriteLine(Localization.T("console.skill.slots_hint"));
                }
                return;
            }
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
                        var s   = Character.SkillSlots[i];
                        string tag  = s.Source switch
                        {
                            SlottedSkillSource.Combined        => Localization.T("console.skill.tag_combined"),
                            SlottedSkillSource.CompositeFusion => Localization.T("console.skill.tag_fusion"),
                            _ => ""
                        };
                        Console.WriteLine(Localization.T("console.skill.slot_line", i + 1, s.ResolvedSkill?.Name ?? s.SkillId, tag));
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
                        string tag   = src switch
                        {
                            SlottedSkillSource.Combined        => Localization.T("console.skill.tag_combined"),
                            SlottedSkillSource.CompositeFusion => Localization.T("console.skill.tag_fusion"),
                            _ => Localization.T("console.skill.tag_regular")
                        };
                        Console.WriteLine(Localization.T("console.skill.available_line", letter, name, tag));
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
            foreach (var c in Character.CombinedSkills.Where(c => c.ResolvedSkill != null))
            {
                if (!Character.SkillSlots.Any(sl => sl.Source == SlottedSkillSource.Combined && sl.SkillId == c.Id))
                    list.Add((c.Id, c.DisplayName, SlottedSkillSource.Combined));
            }
            foreach (var f in Character.CompositeSkills.Where(f => f.ResolvedSkill != null))
            {
                if (!Character.SkillSlots.Any(sl => sl.Source == SlottedSkillSource.CompositeFusion && sl.SkillId == f.Id))
                    list.Add((f.Id, f.DisplayName, SlottedSkillSource.CompositeFusion));
            }
            return list;
        }
    }
}
