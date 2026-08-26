namespace Myria.ConsoleApp.Utils
{
    /// <summary>
    /// Console-specific presentation methods for Myria.Lib.Core entities.
    /// These are extension methods so Myria.Lib.Core stays UI-agnostic.
    /// </summary>
    public static class ConsoleExtensions
    {
        // Room.Name/.Description are locale keys unless IsLiteral is set (e.g. "room.royas.main"
        // resolving to "Royas") — mirrors Myria.Wpf's ViewModel_PageRoom:
        // `room.IsLiteral ? room.Name : Localization.T(room.Name)`.
        public static string DisplayName(this Room room) =>
            room.IsLiteral ? room.Name : Localization.T(room.Name);

        public static string DisplayDescription(this Room room) =>
            room.IsLiteral ? room.Description : Localization.T(room.Description);

        public static void Describe(this Room room)
        {
            Console.WriteLine($"\n{room.DisplayName()}");
            Console.WriteLine(room.DisplayDescription());

            if (room.Exits.Any())
                Console.WriteLine("Exits: " + string.Join(", ", room.Exits.Keys));

            if (room.HasMonsters && !room.IsCleared)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("⚔ Danger lurks here.");
                Console.ResetColor();
            }
        }

        public static void Describe(this Corpse corpse)
        {
            Console.Write($"  💀 {corpse.Name}");
            if (corpse.IsLooted)
                Console.Write(" (looted)");
            else
                Console.Write($" — {corpse.Loot.Count} item(s)");
            Console.WriteLine();
        }

        public static void ListItems(this Inventory inventory)
        {
            if (!inventory.Items.Any())
            {
                Console.WriteLine("Your inventory is empty.");
                return;
            }

            Console.WriteLine($"Inventory ({inventory.Items.Count}/{inventory.Capacity}):");
            foreach (var item in inventory.Items)
            {
                Console.Write($"  ");
                Printer.PrintColoredItemName(item);
                if (item.StackSize > 1)
                    Console.Write($" x{item.StackSize}");
                Console.WriteLine();
            }
        }

    }

}
