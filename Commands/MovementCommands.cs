namespace Myria.ConsoleApp.Commands
{
    public static class MovementCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input == "look")
            {
                Printer.ShowLook(Character.CurrentRoom);
                return true;
            }

            if (input.StartsWith("move "))
            {
                string dir = input.Substring(5).Trim();
                if (dir.Length < 2)
                {
                    dir = dir switch
                    {
                        "n" => "north",
                        "e" => "east",
                        "s" => "south",
                        "w" => "west",
                        _   => dir
                    };
                }
                Move(Character, dir);
                return true;
            }

            return false;
        }

        private static void Move(Character Character, string direction)
        {
            if (!Character.CurrentRoom.Exits.TryGetValue(direction, out Room nextRoom))
            {
                Console.WriteLine(Localization.T("console.move.blocked"));
                return;
            }

            if (nextRoom.RequirementType == RoomRequirementType.Level && Character.Level < nextRoom.AccessLevel)
            {
                Console.WriteLine(Localization.T("console.move.level_required", nextRoom.AccessLevel));
                return;
            }

            // v0.3 dungeons: deeper only from a cleared room. Online the server's per-party run
            // decides (and the move happens there first); offline the local run does.
            bool allowed = ConsoleHubClient.IsConnected
                ? ConsoleHubClient.JoinRoomAsync(nextRoom.Id).GetAwaiter().GetResult()
                : Myria.Lib.Core.Systems.Dungeons.LocalDungeonRuns.CanMove(Character.CurrentRoom, nextRoom);
            if (!allowed)
            {
                Console.WriteLine(Localization.T(nextRoom.IsDungeonRoom ? "console.move.dungeon_locked" : "console.move.blocked"));
                return;
            }

            var leftDungeon = Myria.Lib.Core.Systems.Dungeons.DungeonRules.DungeonIdOf(Character.CurrentRoom);
            if (leftDungeon is not null && Myria.Lib.Core.Systems.Dungeons.DungeonRules.DungeonIdOf(nextRoom) != leftDungeon)
            {
                Console.WriteLine(Localization.T("console.move.dungeon_reset"));
                foreach (var dungeonRoom in GameService.Rooms.Values.Where(r => r.DungonId == Character.CurrentRoom.DungonId))
                    dungeonRoom.Corpses = new();
            }

            // Same rule as the realm server (GameHub.MoveToRoomAsync), so offline and online agree on
            // where a defeat respawns you. (It checked Npcs for the literal "Healer", which never matched.)
            if (nextRoom.NpcRefs.Any(n => n?.Type == NpcType.Healer))
                Character.LastHealerRoomId = nextRoom.Id;

            Character.CurrentRoom = nextRoom;
            Character.CurrentRoomId = nextRoom.Id;
            Myria.Lib.Core.Systems.Dungeons.LocalDungeonRuns.NoteLocation(nextRoom);
            Console.WriteLine(Localization.T("console.move.moved", direction));
            Character.CurrentRoom.Describe();
        }

    }

}
