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

            if (nextRoom.IsDungeonRoom && nextRoom.Monsters.Count == 0)
                nextRoom.IsCleared = true;

            if (Character.CurrentRoom.IsDungeonRoom && !Character.CurrentRoom.IsCleared)
            {
                Console.WriteLine(Localization.T("console.move.dungeon_locked"));
                return;
            }
            else if (Character.CurrentRoom.IsDungeonRoom && !nextRoom.IsDungeonRoom)
            {
                Console.WriteLine(Localization.T("console.move.dungeon_reset"));
                int dungId = Character.CurrentRoom.DungonId;
                foreach (var dungeonRoom in GameService.Rooms.Values.Where(r => r.DungonId == dungId))
                {
                    dungeonRoom.IsCleared = false;
                    dungeonRoom.Corpses = new();
                }
            }

            if (nextRoom.RequirementType == RoomRequirementType.Level && Character.Level < nextRoom.AccessLevel)
            {
                Console.WriteLine(Localization.T("console.move.level_required", nextRoom.AccessLevel));
                return;
            }

            if (nextRoom.IsCity && nextRoom.Npcs.Contains("Healer"))
                Character.LastHealerRoomId = nextRoom.Id;

            Character.CurrentRoom = nextRoom;
            _ = ConsoleHubClient.JoinRoomAsync(Character.CurrentRoom.Id);
            Console.WriteLine(Localization.T("console.move.moved", direction));
            Character.CurrentRoom.Describe();
        }

    }

}
