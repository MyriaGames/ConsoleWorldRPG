namespace Myria.ConsoleApp.Commands
{
    public static class LootCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input == "look corpses") { ShowCorpses(Character); return true; }
            if (input == "loot") { LootFirstCorpse(Character); return true; }
            return false;
        }

        private static void ShowCorpses(Character Character)
        {
            var corpses = Character.CurrentRoom.Corpses;
            if (!corpses.Any()) { Console.WriteLine(Localization.T("console.loot.no_corpses")); return; }
            foreach (var corpse in corpses)
                corpse.Describe();
        }

        private static void LootFirstCorpse(Character Character)
        {
            var corpse = Character.CurrentRoom.Corpses.FirstOrDefault(c => !c.IsLooted && c.Loot.Any());
            if (corpse == null) { Console.WriteLine(Localization.T("console.loot.none_lootable")); return; }

            Console.WriteLine(Localization.T("console.loot.looting", corpse.Name));
            foreach (var item in corpse.Loot)
            {
                if (Character.Inventory.AddItem(item, Character))
                {
                    Printer.PrintColoredItemName(item);
                    Console.WriteLine();
                }
                else
                {
                    Console.Write(Localization.T("console.loot.could_not_carry_prefix"));
                    Printer.PrintColoredItemName(item);
                    Console.WriteLine(Localization.T("console.loot.could_not_carry_suffix"));
                    return;
                }
            }
            corpse.Loot.Clear();
            corpse.IsLooted = true;
        }

    }

}
