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

            if (room.IsDungeonRoom)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(room.DungeonStatus());
                Console.ResetColor();
            }
            else if (room.HasMonsters)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("⚔ Danger lurks here.");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// v0.3 dungeons: what's still alive in this room of the run, or that it's cleared. Online
        /// from the server (the run is per party); offline from the local run.
        /// </summary>
        public static string DungeonStatus(this Room room)
        {
            IEnumerable<string> names;
            if (ConsoleHubClient.IsConnected)
            {
                var state = ConsoleHubClient.GetDungeonStateAsync().GetAwaiter().GetResult();
                names = state?.RoomId == room.Id ? state.Remaining.Select(m => m.Name) : Enumerable.Empty<string>();
            }
            else
                names = Myria.Lib.Core.Systems.Dungeons.LocalDungeonRuns.For(room)?.Remaining(room).Select(m => m.Name)
                        ?? Enumerable.Empty<string>();

            var groups = names.GroupBy(n => n).Select(g => (g.Count() > 1 ? $"{g.Count()}× " : "") + Localization.T(g.Key)).ToList();
            return groups.Count == 0
                ? Localization.T("dungeon.room_cleared")
                : "⚔ " + Localization.T("dungeon.remaining", string.Join(", ", groups));
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
