namespace Myria.ConsoleApp.Entities.NPCs
{
    // Mirrors Myria.Wpf's UpgradePanel.
    internal static class UpgradeHandler
    {
        public static void UpgradeMenu(Character Character, Npc npc)
        {
            string jobId       = npc.MasterJobId ?? "blacksmith"; // J16: use NPC's actual job
            int    knowledgeLv = JobXpService.GetLevel(JobManager.GetOrAdd(Character, jobId).KnowledgeXp);
            int    maxUpgrade  = JobXpService.GetMaxUpgradeLevel(knowledgeLv); // J19

            var upgradable = Character.Inventory.Items.OfType<EquipmentItem>()
                .Where(e => e.UpgradeCategory == npc.UpgradeCategory && e.UpgradeLevel < maxUpgrade)
                .ToList();

            if (upgradable.Count == 0)
            {
                Console.WriteLine(Localization.T("console.npc.no_upgradable", maxUpgrade));
                return;
            }

            while (true)
            {
                Console.WriteLine(Localization.T("console.npc.upgrade_header", maxUpgrade));
                for (int i = 0; i < upgradable.Count; i++)
                    Console.WriteLine(Localization.T("console.npc.upgrade_item_line", i + 1, Localization.T(upgradable[i].Name), upgradable[i].UpgradeLevel));
                Console.Write(Localization.T("console.npc.upgrade_choice_prompt"));

                if (!int.TryParse(Console.ReadLine(), out int choice) || choice == 0) return;
                if (choice < 1 || choice > upgradable.Count) continue;

                var item = upgradable[choice - 1];

                if (ConsoleHubClient.IsConnected)
                {
                    var result = ConsoleHubClient.UpgradeAsync(npc.Id, item.Id).GetAwaiter().GetResult();
                    if (result is null || !result.Success)
                    {
                        Printer.PrintError(result?.Reason ?? Localization.T("console.npc.upgrade_failed"));
                        continue;
                    }
                    item.UpgradeLevel = result.UpgradeLevel;
                    if (!string.IsNullOrEmpty(result.JobId) && result.SkillXpGained > 0)
                        JobManager.GrantSkillXp(Character, result.JobId, (int)result.SkillXpGained);
                    Console.WriteLine(Localization.T("console.npc.upgraded", Localization.T(item.Name), item.UpgradeLevel));
                }
                else
                {
                    // J16: apply skill quality before upgrading
                    float quality = (float)JobXpService.GetSkillMultiplierFromXp(JobManager.GetOrAdd(Character, jobId).SkillXp);
                    if (quality > item.CraftQuality) item.CraftQuality = quality;

                    if (item.TryUpgrade(Character, maxUpgrade))
                    {
                        JobManager.GrantSkillXp(Character, jobId, 25);
                        Console.WriteLine(Localization.T("console.npc.upgraded", Localization.T(item.Name), item.UpgradeLevel));
                        upgradable = Character.Inventory.Items.OfType<EquipmentItem>()
                            .Where(e => e.UpgradeCategory == npc.UpgradeCategory && e.UpgradeLevel < maxUpgrade)
                            .ToList();
                    }
                    else
                    {
                        Printer.PrintError(Localization.T("console.npc.upgrade_failed"));
                    }
                }
            }
        }
    }
}
