namespace Myria.ConsoleApp.Utils
{
    public class Printer
    {
        public static void ShowInvalidCommand()
        {
            PrintError(Localization.T("console.error.unknown_command"));
        }

        public static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ {message}");
            Console.ResetColor();
        }

        public static void PrintWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ {message}");
            Console.ResetColor();
        }

        public static void PrintSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✔ {message}");
            Console.ResetColor();
        }

        public static void PrintColoredItemName(Item item)
        {
            string itemName = Localization.T(item.Name);
            string displayName = item is EquipmentItem eq && eq.UpgradeLevel > 0
                                 ? $"{itemName} +{eq.UpgradeLevel}"
                                 : itemName;
            if (item.Rarity == ItemRarity.Godly)
            {
                PrintRainbow(displayName);
                return;
            }
            var color = item.Rarity switch
            {
                ItemRarity.Common    => ConsoleColor.Gray,
                ItemRarity.Uncommon  => ConsoleColor.Green,
                ItemRarity.Rare      => ConsoleColor.Blue,
                ItemRarity.Epic      => ConsoleColor.Magenta,
                ItemRarity.Unique    => ConsoleColor.Yellow,
                ItemRarity.Legendary => ConsoleColor.DarkYellow,
                _                    => ConsoleColor.Gray
            };
            Console.ForegroundColor = color;
            Console.Write($" - {displayName}");
            Console.ResetColor();
        }

        private static void PrintRainbow(string text)
        {
            var colors = new[]
            {
                ConsoleColor.Red, ConsoleColor.Yellow, ConsoleColor.Green,
                ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.Magenta
            };
            int i = 0;
            foreach (char c in text)
            {
                Console.ForegroundColor = colors[i++ % colors.Length];
                Console.Write(c);
            }
            Console.ResetColor();
        }

        public static void ShowLook(Room room)
        {
            Console.WriteLine(Localization.T("console.look.header", room.DisplayName(), room.DisplayDescription()));

            if (room.Exits.Any())
                Console.WriteLine(Localization.T("console.look.exits", string.Join(", ", room.Exits.Keys)));

            if (room.IsCity && room.Npcs.Count > 0)
            {
                Console.WriteLine(Localization.T("console.look.people_header"));
                foreach (var npc in room.Npcs)
                    Console.WriteLine(Localization.T("console.common.list_item", npc));
            }

            if (room.Corpses.Any())
            {
                Console.WriteLine(Localization.T("console.look.corpses_header"));
                foreach (var corpse in room.Corpses)
                    corpse.Describe();
            }

            if (room.GatheringSpots.Any())
            {
                Console.WriteLine(Localization.T("console.look.gathering_header"));
                foreach (var spot in room.GatheringSpots)
                    Console.WriteLine(Localization.T("console.look.gathering_item", spot.Name, spot.Description));
            }

            if (room.HasMonsters)
            {
                Console.WriteLine(Localization.T("console.look.monsters_header"));
                foreach (Monster monster in room.Monsters)
                    Console.WriteLine(Localization.T("console.common.list_item", Localization.T(monster.Name)));
            }
        }

        public static void ShowHelp()
        {
            Console.WriteLine(Localization.T("console.help.title"));
            HelpLine("move <direction>",              "console.help.desc.move");
            HelpLine("look",                           "console.help.desc.look");
            HelpLine("fight",                          "console.help.desc.fight");
            HelpLine("group fight",                    "console.help.desc.group_fight");
            HelpLine("attack [n]",                      "console.help.desc.attack");
            HelpLine("cast <skill> [n]",                "console.help.desc.cast");
            HelpLine("group status",                    "console.help.desc.group_status");
            HelpLine("status",                           "console.help.desc.status");
            HelpLine("character",                        "console.help.desc.character");
            HelpLine("stats",                             "console.help.desc.stats");
            HelpLine("alloc <stat> [n]",                  "console.help.desc.alloc");
            HelpLine("jobs",                              "console.help.desc.jobs");
            HelpLine("skills",                            "console.help.desc.skills");
            HelpLine("combine",                           "console.help.desc.combine");
            HelpLine("slots",                             "console.help.desc.slots");
            HelpLine("inventory",                         "console.help.desc.inventory");
            HelpLine("go to <npc name>",                  "console.help.desc.go_to");
            HelpLine("equip <item name>",                 "console.help.desc.equip");
            HelpLine("use <item name>",                   "console.help.desc.use");
            HelpLine("loot",                              "console.help.desc.loot");
            HelpLine("gather <source name>",              "console.help.desc.gather");
            HelpLine("map",                               "console.help.desc.map");
            HelpLine("help",                              "console.help.desc.help");
            HelpLine("players",                           "console.help.desc.players");
            HelpLine("say <message>",                     "console.help.desc.say");
            HelpLine("g <message>",                       "console.help.desc.g");
            HelpLine("w <Character> <message>",           "console.help.desc.w");
            HelpLine("whisper <Character> <message>",     "console.help.desc.whisper");
            HelpLine("party",                             "console.help.desc.party");
            HelpLine("party <message>",                   "console.help.desc.party_message");
            HelpLine("party invite <Character>",          "console.help.desc.party_invite");
            HelpLine("party accept <id>",                 "console.help.desc.party_accept");
            HelpLine("party leave",                       "console.help.desc.party_leave");
            HelpLine("party kick <Character>",            "console.help.desc.party_kick");
            HelpLine("party promote <Character>",         "console.help.desc.party_promote");
            HelpLine("friend list",                       "console.help.desc.friend_list");
            HelpLine("friend requests",                   "console.help.desc.friend_requests");
            HelpLine("friend add <name>",                 "console.help.desc.friend_add");
            HelpLine("friend accept <id>",                "console.help.desc.friend_accept");
            HelpLine("friend remove <id>",                "console.help.desc.friend_remove");
            HelpLine("block list",                        "console.help.desc.block_list");
            HelpLine("block <name>",                      "console.help.desc.block");
            HelpLine("unblock <id>",                      "console.help.desc.unblock");
            HelpLine("trade <Character>",                 "console.help.desc.trade");
            HelpLine("trade accept <id>",                 "console.help.desc.trade_accept");
            HelpLine("trade decline <id>",                "console.help.desc.trade_decline");
            HelpLine("trade add item <id> [qty]",         "console.help.desc.trade_add_item");
            HelpLine("trade add gold <amount>",           "console.help.desc.trade_add_gold");
            HelpLine("trade remove <itemId>",             "console.help.desc.trade_remove");
            HelpLine("trade show",                        "console.help.desc.trade_show");
            HelpLine("trade confirm",                     "console.help.desc.trade_confirm");
            HelpLine("trade cancel",                      "console.help.desc.trade_cancel");
            HelpLine("shop open",                         "console.help.desc.shop_open");
            HelpLine("shop close",                        "console.help.desc.shop_close");
            HelpLine("shop list",                         "console.help.desc.shop_list");
            HelpLine("shop add <id> <qty> <price>",       "console.help.desc.shop_add");
            HelpLine("shop remove <listingId>",           "console.help.desc.shop_remove");
            HelpLine("shop visit <Character>",            "console.help.desc.shop_visit");
            HelpLine("shop buy <Character> <id> [qty]",   "console.help.desc.shop_buy");
            HelpLine("logout",                            "console.help.desc.logout");
            HelpLine("exit",                              "console.help.desc.exit");
        }

        private static void HelpLine(string syntax, string descKey) =>
            Console.WriteLine(Localization.T("console.help.entry", syntax.PadRight(27), Localization.T(descKey)));

        public static void ShowDebugHelp()
        {
            Console.WriteLine(Localization.T("console.help.debug_title"));
            HelpLine("/set level <level>", "console.help.desc.debug_set_level");
            HelpLine("/skillmaster",       "console.help.desc.debug_skillmaster");
            HelpLine("/help",              "console.help.desc.debug_help");
        }

        public static void ShowStatus(Character Character)
        {
            Console.WriteLine(Localization.T("console.status.title", Character.Name));
            Console.ForegroundColor = ConsoleColor.Cyan;
            int    classLvl = ClassManager.GetClassLevel(Character, Character.Class);
            string classXp  = JobXpService.FormatProgress(ClassManager.GetClassXp(Character, Character.Class));
            Console.WriteLine(Localization.T("console.status.class", Character.Class, classLvl, classXp));
            Console.ResetColor();
            Console.WriteLine(Localization.T("console.status.level", Character.Level, Character.Experience, Character.ExpForNextLvl));
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(Localization.T("console.status.hp", Character.CurrentHealth, Character.MaxHealth));
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(Localization.T("console.status.mana", Character.CurrentMana, Character.MaxMana));
            Console.ResetColor();
            Console.WriteLine(Localization.T("console.status.stat_line", "STR", Character.Stats.Strength, Character.GetBonusFromGear(g => g.BonusSTR)));
            Console.WriteLine(Localization.T("console.status.stat_line", "DEX", Character.Stats.Dexterity, Character.GetBonusFromGear(g => g.BonusDEX)));
            Console.WriteLine(Localization.T("console.status.stat_line", "END", Character.Stats.Endurance, Character.GetBonusFromGear(g => g.BonusEND)));
            Console.WriteLine(Localization.T("console.status.stat_line", "INT", Character.Stats.Intelligence, Character.GetBonusFromGear(g => g.BonusINT)));
            Console.WriteLine(Localization.T("console.status.stat_line", "SPR", Character.Stats.Spirit, Character.GetBonusFromGear(g => g.BonusSPR)));
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine(Localization.T("console.status.derived_line", "ATK:",  Character.TotalPhysicalAttack  - Character.GetBonusFromGear(g => g.BonusATK),  Character.GetBonusFromGear(g => g.BonusATK)));
            Console.WriteLine(Localization.T("console.status.derived_line", "DEF:",  Character.TotalPhysicalDefense - Character.GetBonusFromGear(g => g.BonusDEF),  Character.GetBonusFromGear(g => g.BonusDEF)));
            Console.WriteLine(Localization.T("console.status.derived_line", "MATK:", Character.TotalMagicAttack     - Character.GetBonusFromGear(g => g.BonusMATK), Character.GetBonusFromGear(g => g.BonusMATK)));
            Console.WriteLine(Localization.T("console.status.derived_line", "MDEF:", Character.TotalMagicDefense    - Character.GetBonusFromGear(g => g.BonusMDEF), Character.GetBonusFromGear(g => g.BonusMDEF)));
            Console.WriteLine(Localization.T("console.status.derived_line", "Aim:",  Character.TotalAim             - Character.GetBonusFromGear(g => g.BonusAim),  Character.GetBonusFromGear(g => g.BonusAim)));
            Console.WriteLine(Localization.T("console.status.derived_line", "Eva:",  Character.TotalEvasion         - Character.GetBonusFromGear(g => g.BonusEvasion), Character.GetBonusFromGear(g => g.BonusEvasion)));
            Console.WriteLine(Localization.T("console.status.crit_block", Character.CritChance, Character.BlockChance));
            Console.ResetColor();

            Console.WriteLine(Localization.T("console.status.equipped_header"));
            Console.Write(Localization.T("console.status.equip_weapon"));
            if (Character.WeaponSlot == null) Console.Write(Localization.T("console.common.none")); else PrintColoredItemName(Character.WeaponSlot);
            Console.WriteLine();
            Console.Write(Localization.T("console.status.equip_armor"));
            if (Character.ArmorSlot == null) Console.Write(Localization.T("console.common.none")); else PrintColoredItemName(Character.ArmorSlot);
            Console.WriteLine();
            Console.Write(Localization.T("console.status.equip_accessory"));
            if (Character.AccessorySlot == null) Console.Write(Localization.T("console.common.none")); else PrintColoredItemName(Character.AccessorySlot);
            Console.WriteLine();
        }

        public static void ShowCharacter(Character Character)
        {
            Console.WriteLine(Localization.T("console.character.title", Character.Name));
            Console.WriteLine(Localization.T("console.character.race", Character.Race));

            int classLvl = ClassManager.GetClassLevel(Character, Character.Class);
            string classXp = JobXpService.FormatProgress(ClassManager.GetClassXp(Character, Character.Class));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Localization.T("console.status.class", Character.Class, classLvl, classXp));
            Console.ResetColor();
            Console.WriteLine(Localization.T("console.character.level", Character.Level, Character.Experience, Character.ExpForNextLvl));

            Console.WriteLine(Localization.T("console.character.base_stats_header"));
            PrintStatLine("STR", Character.Stats.Strength, Character.Stats.StrengthBonus,    ClassManager.GetClassBonusForStat(Character, "STR"), Character.GetBonusFromGear(g => g.BonusSTR));
            PrintStatLine("DEX", Character.Stats.Dexterity, Character.Stats.DexterityBonus,   ClassManager.GetClassBonusForStat(Character, "DEX"), Character.GetBonusFromGear(g => g.BonusDEX));
            PrintStatLine("END", Character.Stats.Endurance, Character.Stats.EnduranceBonus,   ClassManager.GetClassBonusForStat(Character, "END"), Character.GetBonusFromGear(g => g.BonusEND));
            PrintStatLine("INT", Character.Stats.Intelligence, Character.Stats.IntelligenceBonus, ClassManager.GetClassBonusForStat(Character, "INT"), Character.GetBonusFromGear(g => g.BonusINT));
            PrintStatLine("SPR", Character.Stats.Spirit, Character.Stats.SpiritBonus,        ClassManager.GetClassBonusForStat(Character, "SPR"), Character.GetBonusFromGear(g => g.BonusSPR));

            if (Character.Stats.UnusedPoints > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(Localization.T("console.character.unspent_points", Character.Stats.UnusedPoints));
                Console.ResetColor();
            }

            Console.WriteLine(Localization.T("console.character.derived_stats_header"));
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(Localization.T("console.character.hp", Character.CurrentHealth, Character.MaxHealth));
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(Localization.T("console.character.mp", Character.CurrentMana, Character.MaxMana));
            Console.ResetColor();
            Console.WriteLine(Localization.T("console.character.atk", Character.TotalPhysicalAttack));
            Console.WriteLine(Localization.T("console.character.def", Character.TotalPhysicalDefense));
            Console.WriteLine(Localization.T("console.character.matk", Character.TotalMagicAttack));
            Console.WriteLine(Localization.T("console.character.mdef", Character.TotalMagicDefense));
            Console.WriteLine(Localization.T("console.character.aim", Character.TotalAim));
            Console.WriteLine(Localization.T("console.character.eva", Character.TotalEvasion));
            Console.WriteLine(Localization.T("console.character.crit_block", Character.CritChance, Character.BlockChance));
        }

        private static void PrintStatLine(string name, int @base, int allocated, int classBon, int gear)
        {
            int total = @base + allocated + classBon + gear;
            string parts = "";
            if (allocated > 0) parts += Localization.T("console.character.stat_alloc", allocated);
            if (classBon  > 0) parts += Localization.T("console.character.stat_class", classBon);
            if (gear      > 0) parts += Localization.T("console.character.stat_gear", gear);
            Console.WriteLine(Localization.T("console.character.stat_line", name, @base, parts, total));
        }

        public static void ShowEquipSuccess(Item item) => PrintSuccess(Localization.T("console.equip.success", Localization.T(item.Name)));
        public static void ShowEquipFail(string reason) => PrintError(Localization.T("console.equip.fail", reason));

        public static void ShowJobs(Character Character)
        {
            Console.WriteLine(Localization.T("console.jobs.title"));

            if (Character.Jobs.Count == 0)
            {
                Console.WriteLine(Localization.T("console.jobs.none"));
                return;
            }

            if (!string.IsNullOrEmpty(Character.ActiveJobId))
            {
                string activeName = JobManager.GetById(Character.ActiveJobId) is { } activeJob ? Localization.T(activeJob.Name) : Character.ActiveJobId;
                var    cd         = JobManager.GetCooldownRemaining(Character);
                string cdStr      = cd > TimeSpan.Zero
                    ? Localization.T("console.jobs.cooldown_switch", FormatCooldown(cd))
                    : Localization.T("console.jobs.cooldown_ready");
                Console.WriteLine(Localization.T("console.jobs.active", activeName, cdStr));
            }
            else
            {
                Console.WriteLine(Localization.T("console.jobs.no_active"));
            }
            Console.WriteLine();

            foreach (var pj in Character.Jobs)
            {
                var    job      = JobManager.GetById(pj.JobId);
                string name     = job is { } j ? Localization.T(j.Name) : pj.JobId;
                bool   isActive = pj.JobId == Character.ActiveJobId;
                string tag      = isActive ? Localization.T("console.jobs.active_tag") : "";

                Console.WriteLine(Localization.T("console.jobs.entry", name, tag));
                Console.WriteLine(Localization.T("console.jobs.skill_line", JobXpService.GetLevel(pj.SkillXp), JobXpService.FormatProgress(pj.SkillXp)));
                Console.WriteLine(Localization.T("console.jobs.knowledge_line", JobXpService.GetLevel(pj.KnowledgeXp), JobXpService.FormatProgress(pj.KnowledgeXp)));
                Console.WriteLine(Localization.T("console.jobs.fame_line", JobXpService.GetLevel(pj.FameXp), JobXpService.FormatProgress(pj.FameXp)));
            }
        }

        public static string FormatCooldown(TimeSpan cd)
        {
            if (cd.TotalDays >= 1)  return Localization.T("console.common.cooldown_days", (int)cd.TotalDays, cd.Hours);
            if (cd.TotalHours >= 1) return Localization.T("console.common.cooldown_hours", (int)cd.TotalHours, cd.Minutes);
            return Localization.T("console.common.cooldown_minutes", cd.Minutes);
        }

    }

}
