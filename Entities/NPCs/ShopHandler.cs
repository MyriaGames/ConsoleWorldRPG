namespace Myria.ConsoleApp.Entities.NPCs
{
    // Mirrors Myria.Wpf's ShopPanel — general/smith equipment browsing, buying and selling.
    internal static class ShopHandler
    {
        // ── General shop ─────────────────────────────────────────────────────────

        public static void ShopMenu(Character Character, Npc npc)
        {
            var stock = npc.ItemRefs.ToList();
            if (stock.Count == 0) { Console.WriteLine(Localization.T("console.npc.shop_empty")); return; }

            while (true)
            {
                Console.WriteLine(Localization.T("console.npc.shop_header"));
                for (int i = 0; i < stock.Count; i++)
                    Console.WriteLine(Localization.T("console.npc.shop_item_line", i + 1, Localization.T(stock[i].Name), stock[i].BuyPrice));
                Console.WriteLine(Localization.T("console.npc.shop_footer_hint"));
                Console.Write("> ");
                var input = Console.ReadLine()?.Trim().ToLower();

                if (input == "0") return;
                if (input == "s") { SellMenu(Character, npc); continue; }
                if (!int.TryParse(input, out int choice)) { Printer.PrintError(Localization.T("console.common.enter_number")); continue; }
                if (choice < 1 || choice > stock.Count) { Printer.PrintError(Localization.T("console.npc.no_such_item")); continue; }
                TryBuy(Character, npc, stock[choice - 1], stock[choice - 1].BuyPrice);
            }
        }

        // ── Smith equipment shop ─────────────────────────────────────────────────

        public static void SmithShopMenu(Character Character, Npc npc)
        {
            var stock = npc.ItemRefs.OfType<EquipmentItem>().ToList();
            if (stock.Count == 0) { Console.WriteLine(Localization.T("console.npc.nothing_for_class")); return; }

            while (true)
            {
                for (int i = 0; i < stock.Count; i++)
                    Console.WriteLine(Localization.T("console.npc.shop_item_line", i + 1, Localization.T(stock[i].Name), stock[i].BuyPrice));
                Console.Write(Localization.T("console.npc.buy_prompt"));

                if (!int.TryParse(Console.ReadLine(), out int choice)) { Printer.PrintError(Localization.T("console.common.enter_number")); continue; }
                if (choice == 0) return;
                if (choice < 1 || choice > stock.Count) { Printer.PrintError(Localization.T("console.npc.no_such_item")); continue; }
                TryBuy(Character, npc, stock[choice - 1], stock[choice - 1].BuyPrice);
            }
        }

        // ── Sell ─────────────────────────────────────────────────────────────────

        public static void SellMenu(Character Character, Npc npc)
        {
            while (true)
            {
                Character.Inventory.ListItems();
                Console.Write(Localization.T("console.npc.sell_prompt"));
                var itemName = Console.ReadLine()?.Trim();
                if (itemName is "back" or "0" or null) return;

                var item = InventoryUtils.ResolveInventoryItem(itemName, Character);
                if (item == null) { Printer.PrintError(Localization.T("console.npc.dont_have", itemName)); continue; }

                Console.WriteLine(Localization.T("console.npc.sell_item_line", item.StackSize, Localization.T(item.Name), item.SellValue));
                Console.Write(Localization.T("console.npc.sell_quantity_prompt"));
                if (!int.TryParse(Console.ReadLine(), out int amount) || amount <= 0 || amount > item.StackSize)
                {
                    Printer.PrintError(Localization.T("console.npc.invalid_quantity"));
                    continue;
                }

                if (ConsoleHubClient.IsConnected)
                {
                    var serverResult = ConsoleHubClient.SellItemToNpcAsync(item.Id, amount).GetAwaiter().GetResult();
                    if (serverResult == null || !serverResult.Success)
                    {
                        Printer.PrintError(serverResult?.Reason switch
                        {
                            "money_full" => Localization.T("npc.action.sell.moneyFull"),
                            null         => Localization.T("console.npc.sell_failed"),
                            var other    => other,
                        });
                        continue;
                    }

                    if (item.StackSize <= serverResult.Quantity)
                        Character.Inventory.RemoveItem(item);
                    else
                        item.StackSize -= serverResult.Quantity;
                    Character.Money.TryAdd(serverResult.TotalGain);
                    Console.WriteLine(Localization.T("console.npc.sold", serverResult.Quantity, Localization.T(item.Name), serverResult.TotalGain));
                    continue;
                }

                var result = npc.SellItem(Character, item, amount);
                if (result.Success)
                    Console.WriteLine(Localization.T("console.npc.sold", amount, Localization.T(item.Name), amount * item.SellValue));
                else
                    Printer.PrintError(Localization.T(result.MessageKey, result.MessageArgs));
            }
        }

        // ── Shared buy helper ─────────────────────────────────────────────────────

        public static void TryBuy(Character Character, Npc npc, Item item, int cost)
        {
            if (ConsoleHubClient.IsConnected)
            {
                int quantity = item.StackSize > 0 ? item.StackSize : 1;
                var serverResult = ConsoleHubClient.BuyFromNpcShopAsync(npc.Id, item.Id, quantity).GetAwaiter().GetResult();
                if (serverResult == null || !serverResult.Success)
                {
                    Printer.PrintError(serverResult?.Reason ?? Localization.T("console.npc.buy_failed"));
                    return;
                }

                var toAdd = ItemFactory.CreateItem(item.Id, serverResult.Quantity);
                if (toAdd != null && Character.Inventory.AddItem(toAdd, Character))
                {
                    Character.Money.TrySpend(serverResult.TotalCost);
                    Printer.PrintSuccess(Localization.T("console.npc.added_to_inventory", Localization.T(item.Name)));
                }
                else
                {
                    Printer.PrintError(Localization.T("console.npc.inventory_full"));
                }
                return;
            }

            var result = npc.BuyItem(Character, item);
            if (result.Success)
                Printer.PrintSuccess(Localization.T("console.npc.added_to_inventory", Localization.T(item.Name)));
            else
                Printer.PrintError(Localization.T(result.MessageKey, result.MessageArgs));
        }
    }
}
