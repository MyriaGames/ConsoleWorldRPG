namespace Myria.ConsoleApp.Entities.NPCs
{
    internal static class HealerHandler
    {
        public static void HealerMenu(Character Character, Npc npc)
        {
            while (true)
            {
                Console.WriteLine(Localization.T("console.npc.healer_menu_heal"));
                if (Character.PotionTierAvailable < 2)
                {
                    Console.WriteLine(Localization.T("console.npc.healer_menu_buy_healing"));
                    Console.WriteLine(Localization.T("console.npc.healer_menu_buy_mana"));
                }
                Console.WriteLine(Localization.T("console.npc.menu_sell"));
                Console.WriteLine(Localization.T("console.npc.menu_leave"));
                Console.Write("> ");
                switch (Console.ReadLine()?.Trim())
                {
                    case "1":
                        if (ConsoleHubClient.IsConnected)
                        {
                            var healResult = ConsoleHubClient.HealAsync().GetAwaiter().GetResult();
                            if (healResult == null || !healResult.Success)
                            {
                                Printer.PrintError(healResult?.Reason ?? Localization.T("console.npc.heal_failed"));
                                break;
                            }
                            Character.CurrentHealth = healResult.CharacterHp;
                            Character.CurrentMana   = healResult.CharacterMana;
                        }
                        else
                        {
                            npc.HealingAction(Character);
                        }
                        Console.WriteLine(Localization.T("console.npc.healed"));
                        break;
                    case "2":
                        ShopHandler.TryBuy(Character, npc, ItemFactory.CreateItem("t1_healing_potion"), 100);
                        break;
                    case "3":
                        ShopHandler.TryBuy(Character, npc, ItemFactory.CreateItem("t1_mana_potion"), 120);
                        break;
                    case "4":
                        ShopHandler.SellMenu(Character, npc);
                        break;
                    case "0":
                        Console.WriteLine(Localization.T("console.npc.leave_generic"));
                        return;
                    default:
                        Console.WriteLine(Localization.T("ui_invalid_option"));
                        break;
                }
            }
        }
    }
}
