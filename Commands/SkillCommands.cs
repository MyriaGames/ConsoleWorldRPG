namespace Myria.ConsoleApp.Commands
{
    public static class SkillCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input == "skills")  { ShowSkillsMenu(Character); return true; }
            if (input == "slots")   { SlotsMenu(Character);      return true; }
            if (input == "skill" || input.StartsWith("skill "))
            {
                var skill = ResolveLearnedSkill(Character, input.Length > 6 ? input[6..].Trim() : "");
                if (skill == null) Printer.PrintError(Localization.T("console.skill.no_such_skill"));
                else               SkillDetailMenu(Character, skill);
                return true;
            }
            return false;
        }

        // ── skill <n|name>: level progress, upgrade purchases, respec ────────────

        /// <summary>
        /// Console counterpart of WPF's Skill Detail screen. Online, the server is authoritative
        /// (GameHub.SpendSkillPoint/RespecSkill) and the same change is then mirrored locally -
        /// the console doesn't apply the server's CharacterUpdated pushes (same as slot changes).
        /// </summary>
        public static void SkillDetailMenu(Character Character, Skill baseSkill)
        {
            while (true)
            {
                SkillLevelingService.RecalculateLevelAndPoints(Character, baseSkill.Id);
                var progress  = SkillLevelingService.GetOrCreate(Character, baseSkill.Id);
                var effective = SkillLevelingService.ResolveEffectiveSkill(Character, baseSkill);
                var options   = baseSkill.UpgradeOptions;

                Console.WriteLine(Localization.T("console.skill.detail_header", baseSkill.Name, progress.Level));
                Console.WriteLine(Localization.T("console.skill.detail_stats",
                    progress.UsageCount, progress.UnspentPoints, effective.ManaCost, effective.ScalingFactor));

                Console.WriteLine(Localization.T("console.skill.upgrades_header"));
                if (options.Count == 0)
                    Console.WriteLine(Localization.T("console.common.none_paren"));
                for (int i = 0; i < options.Count; i++)
                    Console.WriteLine(Localization.T("console.skill.upgrade_line", i + 1, options[i].Description, UpgradeStatus(progress, options[i])));

                string respecCost = new Money(SkillLevelingService.RespecCostGold).ToString();
                Console.WriteLine(Localization.T("console.skill.detail_commands_hint", respecCost));
                Console.Write("> ");
                string line = Console.ReadLine()?.Trim().ToLower() ?? "";

                if (line == "done" || line == "") return;

                if (line.StartsWith("buy "))
                {
                    if (!int.TryParse(line[4..].Trim(), out int num) || num < 1 || num > options.Count)
                    {
                        Printer.PrintError(Localization.T("console.skill.buy_usage"));
                        continue;
                    }
                    BuyUpgrade(Character, baseSkill, options[num - 1]);
                }
                else if (line == "respec")
                {
                    RespecSkill(Character, baseSkill, progress, respecCost);
                }
                else
                {
                    Printer.PrintError(Localization.T("console.skill.unknown_detail_command"));
                }
            }
        }

        private static void BuyUpgrade(Character Character, Skill baseSkill, SkillUpgradeOption option)
        {
            // Validate locally first for a specific message (the hub only answers true/false), then
            // let the server decide when online.
            var progress = SkillLevelingService.GetOrCreate(Character, baseSkill.Id);
            int max = SkillLevelingService.GetEffectiveMaxPurchases(option);
            if (progress.Level < option.RequiredLevel)
            {
                Printer.PrintError(Localization.T("console.skill.buy_level_too_low", option.RequiredLevel));
                return;
            }
            if (max > 0 && progress.PurchasedUpgradeIds.Count(id => id == option.Id) >= max)
            {
                Printer.PrintError(Localization.T("console.skill.buy_maxed"));
                return;
            }
            if (progress.UnspentPoints <= 0)
            {
                Printer.PrintError(Localization.T("console.skill.buy_no_points"));
                return;
            }

            bool serverOk = !ConsoleHubClient.IsConnected
                || ConsoleHubClient.SpendSkillPointAsync(baseSkill.Id, option.Id).GetAwaiter().GetResult();

            if (serverOk && SkillLevelingService.TrySpendPoint(Character, baseSkill, option.Id, out _))
            {
                CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
                Printer.PrintSuccess(Localization.T("console.skill.bought", option.Description));
            }
            else
                Printer.PrintError(Localization.T("console.skill.buy_failed"));
        }

        private static void RespecSkill(Character Character, Skill baseSkill, SkillProgress progress, string respecCost)
        {
            // GameHub.RespecSkill charges even when there's nothing to refund - don't let that happen.
            if (progress.PurchasedUpgradeIds.Count == 0)
            {
                Printer.PrintError(Localization.T("console.skill.respec_nothing"));
                return;
            }

            Console.Write(Localization.T("console.skill.respec_confirm", baseSkill.Name, respecCost) + " ");
            if ((Console.ReadLine()?.Trim().ToLower() ?? "") is not ("y" or "yes" or "j" or "ja")) return;

            if (ConsoleHubClient.IsConnected)
            {
                // Server decides (and charges); mirror the charge locally best-effort - a stale local
                // balance mustn't turn an already-done server respec into a reported failure.
                if (!ConsoleHubClient.RespecSkillAsync(baseSkill.Id).GetAwaiter().GetResult())
                {
                    Printer.PrintError(Localization.T("console.skill.respec_failed"));
                    return;
                }
                Character.Money.TrySpend(SkillLevelingService.RespecCostGold);
            }
            else if (!Character.Money.TrySpend(SkillLevelingService.RespecCostGold))
            {
                Printer.PrintError(Localization.T("console.skill.respec_failed"));
                return;
            }

            SkillLevelingService.Respec(Character, baseSkill.Id);
            CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
            Printer.PrintSuccess(Localization.T("console.skill.respec_done",
                SkillLevelingService.GetOrCreate(Character, baseSkill.Id).UnspentPoints));
        }

        /// <summary>The bracketed status shown next to an upgrade: locked until its level, else
        /// "x/y" for capped upgrades or "bought x, repeatable" for uncapped ones.</summary>
        public static string UpgradeStatus(SkillProgress progress, SkillUpgradeOption option)
        {
            int bought = progress.PurchasedUpgradeIds.Count(id => id == option.Id);
            int max    = SkillLevelingService.GetEffectiveMaxPurchases(option);
            if (progress.Level < option.RequiredLevel)
                return Localization.T("console.skill.upgrade_locked", option.RequiredLevel);
            return max > 0
                ? Localization.T("console.skill.upgrade_count", bought, max)
                : Localization.T("console.skill.upgrade_repeatable", bought);
        }

        /// <summary>By 1-based number from the "skills" list, or by name/id (case-insensitive).</summary>
        private static Skill? ResolveLearnedSkill(Character Character, string arg)
        {
            if (arg.Length == 0) return null;
            if (int.TryParse(arg, out int num))
                return num >= 1 && num <= Character.Skills.Count ? Character.Skills[num - 1] : null;
            return Character.Skills.FirstOrDefault(s =>
                s.Name.Equals(arg, StringComparison.OrdinalIgnoreCase) || s.Id.Equals(arg, StringComparison.OrdinalIgnoreCase));
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
                    PrintSkillLine($"  {i + 1,2}.", learned[i], Character);

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

        private static void PrintSkillLine(string prefix, Skill skill, Character Character)
        {
            SkillLevelingService.RecalculateLevelAndPoints(Character, skill.Id);
            var progress = SkillLevelingService.GetOrCreate(Character, skill.Id);
            string points = progress.UnspentPoints > 0
                ? Localization.T("console.skill.points_suffix", progress.UnspentPoints) : "";
            Console.WriteLine(Localization.T("console.skill.skill_line", prefix, skill.Name.PadRight(22), SkillSummary(skill))
                + Localization.T("console.skill.level_suffix", progress.Level) + points);
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
