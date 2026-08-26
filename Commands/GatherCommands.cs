namespace Myria.ConsoleApp.Commands
{
    public static class GatherCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (!input.StartsWith("gather ")) return false;

            int gatherBonus = JobManager.GetGatherKnowledgeBonus(Character);
            int effectiveRemaining = Character.CurrentRoom.GathersRemaining + gatherBonus;
            if (effectiveRemaining < 1 ||
                (Character.RoomGatheringStatus.TryGetValue(Character.CurrentRoom.Id, out var lastGather)
                 && lastGather == DateTime.Now.Date))
            {
                if (effectiveRemaining < 1)
                    Character.RoomGatheringStatus[Character.CurrentRoom.Id] = DateTime.Now.Date;

                Console.WriteLine(Localization.T("console.gather.exhausted"));
                return true;
            }

            string spotName = input.Substring(7).Trim().ToLower();
            var spot = Character.CurrentRoom.GatheringSpots.FirstOrDefault(g =>
                g.Id.Equals(spotName, StringComparison.OrdinalIgnoreCase) ||
                g.Name.Equals(spotName, StringComparison.OrdinalIgnoreCase));

            if (spot == null) { Console.WriteLine(Localization.T("console.gather.not_found", spotName)); return true; }

            if (!Character.HasToolFor(spot.Type)) { Console.WriteLine(Localization.T("console.gather.no_tool")); return true; }

            if (ConsoleHubClient.IsConnected)
            {
                var result = ConsoleHubClient.GatherAsync(Character.CurrentRoom.Id).GetAwaiter().GetResult();
                if (result is null || !result.Success)
                {
                    Printer.PrintError(result?.Reason ?? Localization.T("console.gather.fail_fallback"));
                    return true;
                }
                if (result.ItemId != null && ItemFactory.TryCreateItem(result.ItemId, out var serverItem))
                {
                    serverItem.StackSize = result.Amount;
                    if (Character.Inventory.AddItem(serverItem, Character))
                        Console.WriteLine(Localization.T("console.gather.success", result.Amount, Localization.T(serverItem.Name)));
                    else
                        Console.WriteLine(Localization.T("console.gather.inventory_full"));
                }
                if (!string.IsNullOrEmpty(result.JobId) && result.SkillXpGained > 0)
                    JobManager.GrantSkillXp(Character, result.JobId, (int)result.SkillXpGained);
                if (result.RemainingGathers >= 0)
                    Character.CurrentRoom.GathersRemaining = result.RemainingGathers;
                return true;
            }

            if (!ItemFactory.TryCreateItem(spot.GatheredItemId, out var item))
            {
                Printer.PrintError(Localization.T("console.gather.item_not_found", spot.GatheredItemId));
                return true;
            }

            string jobId = spot.Type switch
            {
                GatheringType.Ore  => "miner",
                GatheringType.Tree => "woodcutter",
                GatheringType.Herb => "herbalist",
                _                  => ""
            };
            if (!string.IsNullOrEmpty(jobId))
                item.StackSize = JobManager.GetGatherAmount(Character, jobId);

            if (Character.Inventory.AddItem(item, Character))
            {
                Console.WriteLine(Localization.T("console.gather.success", item.StackSize, Localization.T(item.Name)));
                Character.CurrentRoom.GathersRemaining--;
                if (!string.IsNullOrEmpty(jobId))
                    JobManager.GrantSkillXp(Character, jobId, 10);
            }
            else
            {
                Console.WriteLine(Localization.T("console.gather.inventory_full"));
            }

            return true;
        }

    }

}
