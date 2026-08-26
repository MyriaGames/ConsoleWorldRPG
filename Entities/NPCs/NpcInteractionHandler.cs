using Myria.Lib.Core.Services;
using Myria.Lib.Core.Systems.Enums;

namespace Myria.ConsoleApp.Entities.NPCs
{
    // Entry point / router for NPC interactions. Per-service-type menus live in their own
    // handler classes in this folder (QuestDialogHandler, HealerHandler, ShopHandler,
    // CraftHandler, UpgradeHandler, JobMasterHandler, ClassMasterHandler, SkillMasterHandler),
    // mirroring Myria.Wpf's per-panel ViewModel split under View/Pages/Game/IngameWindow/NpcInteraction.
    public static class NpcInteractionHandler
    {
        // ── Entry point ──────────────────────────────────────────────────────────

        public static void InteractWithNpc(string input, Character Character)
        {
            var npc = FindNpc(input, Character);
            if (npc == null)
            {
                Console.WriteLine(Localization.T("console.npc.not_found", input));
                return;
            }

            Console.WriteLine(Localization.T("console.npc.approach", npc.Type));

            // CN3: offer quest returns first (show regardless of NPC, since GiverNpcId
            // is not yet set in quests.json — TODO: filter by npcId once data is ready)
            QuestDialogHandler.OfferQuestReturns(Character, npc);

            // Quest acceptance — show quests available from this NPC.
            // Falls back to all available quests while GiverNpcId data is incomplete.
            QuestDialogHandler.OfferQuestAcceptance(Character, npc);

            // Service-specific menu
            switch (npc.Type)
            {
                case NpcType.Healer:         HealerHandler.HealerMenu(Character, npc); break;
                case NpcType.Smith:
                case NpcType.Leathersmith:
                case NpcType.Tailor:
                case NpcType.Artificer:
                case NpcType.Enchanter:
                case NpcType.Alchemist:
                case NpcType.Cook:           SmithMenu(Character, npc); break;
                case NpcType.Woodcutter:
                case NpcType.Miner:
                case NpcType.Herbalist:      GatheringMasterMenu(Character, npc); break;
                case NpcType.Shop:           ShopHandler.ShopMenu(Character, npc); break;
                case NpcType.SkillMaster:
                    if (npc.Services.Contains("change_class"))
                        ClassMasterHandler.ClassMasterMenu(Character);
                    else
                        SkillMasterHandler.SkillMasterMenu(Character);
                    break;
                case NpcType.Villager:
                default:
                    Console.WriteLine(Localization.T("console.npc.nothing_else", npc.Type));
                    break;
            }
        }

        // ── NPC lookup ───────────────────────────────────────────────────────────

        private static Npc? FindNpc(string input, Character Character)
        {
            // Try: exact ID, ID-prefix, then NpcType name — all case-insensitive.
            var refs = Character.CurrentRoom.NpcRefs;
            if (refs.Count == 0) return null;

            return refs.FirstOrDefault(n =>
                       n.Id.Equals(input, StringComparison.OrdinalIgnoreCase))
                ?? refs.FirstOrDefault(n =>
                       n.Id.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                ?? refs.FirstOrDefault(n =>
                       n.Type.ToString().Equals(input.Replace("_", ""), StringComparison.OrdinalIgnoreCase));
        }

        // ── Smith / craftsperson ─────────────────────────────────────────────────

        private static void SmithMenu(Character Character, Npc npc)
        {
            bool hasJobMaster = npc.Services.Contains("learn_job") && !string.IsNullOrEmpty(npc.MasterJobId);

            while (true)
            {
                Console.WriteLine(Localization.T("console.npc.smith_welcome", npc.Type));
                Console.WriteLine(Localization.T("console.npc.smith_menu_buy"));
                Console.WriteLine(Localization.T("console.npc.smith_menu_upgrade"));
                Console.WriteLine(Localization.T("console.npc.smith_menu_craft"));
                Console.WriteLine(Localization.T("console.npc.menu_sell"));
                if (hasJobMaster)
                    Console.WriteLine(Localization.T("console.npc.menu_job_progress", npc.MasterJobId));
                Console.WriteLine(Localization.T("console.npc.menu_leave"));
                Console.Write("> ");
                var choice = Console.ReadLine()?.Trim();
                switch (choice)
                {
                    case "1": ShopHandler.SmithShopMenu(Character, npc); break;
                    case "2": UpgradeHandler.UpgradeMenu(Character, npc); break;
                    case "3": CraftHandler.CraftMenu(Character, npc); break;
                    case "4": ShopHandler.SellMenu(Character, npc); break;
                    case "5" when hasJobMaster: JobMasterHandler.JobMasterMenu(Character, npc.MasterJobId!); break;
                    case "0": Console.WriteLine(Localization.T("console.npc.leave_smith")); return;
                    default:  Console.WriteLine(Localization.T("ui_invalid_option")); break;
                }
            }
        }

        // ── Gathering master (woodcutter / miner / herbalist) ─────────────────────

        private static void GatheringMasterMenu(Character Character, Npc npc)
        {
            bool hasJobMaster = npc.Services.Contains("learn_job") && !string.IsNullOrEmpty(npc.MasterJobId);

            while (true)
            {
                Console.WriteLine(Localization.T("console.npc.gathering_welcome", npc.Type));
                if (npc.Services.Contains("shop_equipment") || npc.Services.Contains("shop_general"))
                    Console.WriteLine(Localization.T("console.npc.menu_browse_stock"));
                Console.WriteLine(Localization.T("console.npc.menu_sell"));
                if (hasJobMaster)
                    Console.WriteLine(Localization.T("console.npc.menu_job_progress", npc.MasterJobId));
                Console.WriteLine(Localization.T("console.npc.menu_leave"));
                Console.Write("> ");
                var choice = Console.ReadLine()?.Trim();
                switch (choice)
                {
                    case "1" when npc.Services.Contains("shop_equipment") || npc.Services.Contains("shop_general"):
                        ShopHandler.ShopMenu(Character, npc); break;
                    case "4": ShopHandler.SellMenu(Character, npc); break;
                    case "5" when hasJobMaster: JobMasterHandler.JobMasterMenu(Character, npc.MasterJobId!); break;
                    case "0": Console.WriteLine(Localization.T("console.npc.leave_generic")); return;
                    default:  Console.WriteLine(Localization.T("ui_invalid_option")); break;
                }
            }
        }
    }
}
