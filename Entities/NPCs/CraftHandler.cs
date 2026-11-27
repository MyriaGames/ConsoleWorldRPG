using Myria.Lib.Core.Services;

namespace Myria.ConsoleApp.Entities.NPCs
{
    // Mirrors Myria.Wpf's CraftPanel.
    internal static class CraftHandler
    {
        public static void CraftMenu(Character Character, Npc npc)
        {
            // J20: filter recipes by Character's Knowledge level for this NPC's job
            string jobId      = npc.MasterJobId ?? "blacksmith";
            int    knowledge  = JobXpService.GetLevel(JobManager.GetOrAdd(Character, jobId).KnowledgeXp);
            var    recipes    = CraftingService.GetRecipes(npc.Id)
                                    .Where(r => r.RequiredKnowledgeLevel <= knowledge)
                                    .ToArray();

            if (recipes.Length == 0)
            {
                Console.WriteLine(Localization.T("console.npc.no_recipes"));
                return;
            }

            while (true)
            {
                Console.WriteLine(Localization.T("console.npc.crafting_header", jobId, knowledge));
                for (int i = 0; i < recipes.Length; i++)
                {
                    var r = recipes[i];
                    string ingredients = string.Join(", ", r.Ingredients.Select(ing => $"{ing.Amount}x {ing.ItemId}"));
                    Console.WriteLine(Localization.T("console.npc.recipe_line", i + 1, r.OutputId, ingredients, r.XpReward));
                }
                Console.WriteLine(Localization.T("console.npc.cancel_option"));
                Console.Write("> ");

                var input = Console.ReadLine()?.Trim();
                if (input == "0") return;
                if (!int.TryParse(input, out int choice) || choice < 1 || choice > recipes.Length)
                {
                    Printer.PrintError(Localization.T("ui_invalid_option"));
                    continue;
                }

                var recipe = recipes[choice - 1];

                Console.Write(Localization.T("console.npc.quantity_prompt"));
                string? qInput = Console.ReadLine()?.Trim();
                int qty = string.IsNullOrEmpty(qInput) ? 1 : (int.TryParse(qInput, out int q) && q > 0 ? q : 1);

                if (ConsoleHubClient.IsConnected)
                {
                    var result = ConsoleHubClient.CraftAsync(npc.Id, recipe.OutputId, qty).GetAwaiter().GetResult();
                    if (result is null || !result.Success)
                    {
                        Printer.PrintError(result?.Reason ?? Localization.T("console.npc.craft_failed"));
                        continue;
                    }
                    // Apply server delta locally
                    foreach (var ing in recipe.Ingredients)
                    {
                        int remaining = ing.Amount * qty;
                        foreach (var stack in Character.Inventory.Items.Where(i => i.Id == ing.ItemId).ToList())
                        {
                            if (remaining <= 0) break;
                            int take = Math.Min(stack.StackSize, remaining);
                            stack.StackSize -= take;
                            remaining -= take;
                            if (stack.StackSize <= 0) Character.Inventory.RemoveItem(stack);
                        }
                    }
                    if (result.ItemId != null && ItemFactory.TryCreateItem(result.ItemId, out var serverOut))
                    {
                        serverOut.StackSize = result.Amount;
                        Character.Inventory.AddItem(serverOut, Character);
                        Printer.PrintSuccess(Localization.T("console.npc.crafted", result.Amount, Localization.T(serverOut.Name)));
                    }
                    if (!string.IsNullOrEmpty(result.JobId) && result.SkillXpGained > 0)
                        JobManager.GrantSkillXp(Character, result.JobId, (int)result.SkillXpGained);
                }
                else
                {
                    // Validate ingredients
                    bool hasAll = true;
                    foreach (var ing in recipe.Ingredients)
                    {
                        int owned = Character.Inventory.Items.Where(i => i.Id == ing.ItemId).Sum(i => i.StackSize);
                        if (owned < ing.Amount * qty)
                        {
                            Printer.PrintError(Localization.T("console.npc.need_ingredient", ing.Amount * qty, ing.ItemId, owned));
                            hasAll = false; break;
                        }
                    }
                    if (!hasAll) continue;

                    // Consume ingredients
                    foreach (var ing in recipe.Ingredients)
                    {
                        int remaining = ing.Amount * qty;
                        foreach (var stack in Character.Inventory.Items.Where(i => i.Id == ing.ItemId).ToList())
                        {
                            if (remaining <= 0) break;
                            int take = Math.Min(stack.StackSize, remaining);
                            stack.StackSize -= take;
                            remaining -= take;
                            if (stack.StackSize <= 0) Character.Inventory.RemoveItem(stack);
                        }
                    }

                    if (ItemFactory.TryCreateItem(recipe.OutputId, out var output))
                    {
                        // J15: apply skill quality multiplier to equipment output
                        if (output is EquipmentItem crafted)
                        {
                            crafted.CraftQuality = (float)JobXpService.GetCraftQualityFromXp(
                                JobManager.GetOrAdd(Character, jobId).SkillXp);
                            crafted.Bonuses = crafted.BaseStats.Scale(crafted.CraftQuality);
                        }
                        output.StackSize = qty;
                        Character.Inventory.AddItem(output, Character);
                        JobManager.GrantSkillXp(Character, jobId, recipe.XpReward * qty);
                        Printer.PrintSuccess(Localization.T("console.npc.crafted", qty, Localization.T(output.Name)));
                    }
                    else
                    {
                        Printer.PrintError(Localization.T("console.npc.craft_item_not_found"));
                    }
                }
            }
        }
    }
}
