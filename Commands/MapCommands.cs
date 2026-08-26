namespace Myria.ConsoleApp.Commands
{
    public static class MapCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (!input.StartsWith("map")) return false;

            Dungeon? dungeon = DungeonRegistry.GetDungeonByRoom(Character.CurrentRoom);
            Cave? cave = CaveRegistry.GetCaveByRoom(Character.CurrentRoom);
            City? city = CityRegistry.GetCityByRoom(Character.CurrentRoom);
            Forest? forest = ForestRegistry.GetForestByRoom(Character.CurrentRoom);

            if (input.StartsWith("map ") && input != "map world")
            {
                string mapName = input.Substring(4);
                var searchDungeon = DungeonRegistry.GetDungeonByName(mapName);
                var searchCave    = CaveRegistry.GetCaveByName(mapName);
                var searchCity    = CityRegistry.GetCityByName(mapName);
                var searchForest  = ForestRegistry.GetForestByName(mapName);

                if (searchDungeon != null)      PrintAreaMap(searchDungeon.Name, searchDungeon.MapFile, Character);
                else if (searchCave != null)    PrintAreaMap(searchCave.Name, searchCave.MapFile, Character);
                else if (searchCity != null)    PrintAreaMap(searchCity.Name, searchCity.MapFile, Character);
                else if (searchForest != null)  PrintAreaMap(searchForest.Name, searchForest.MapFile, Character);
                else Console.WriteLine(Localization.T("console.map.not_found"));
            }
            else if (cave == null && dungeon == null && forest == null &&
                     (city == null || !city.IsBig) || input == "map world")
            {
                PrintAreaMap("World", "Data/Maps/world_map.json", Character);
            }
            else if (cave != null)    PrintAreaMap(cave.Name, cave.MapFile, Character);
            else if (dungeon != null) PrintAreaMap(dungeon.Name, dungeon.MapFile, Character);
            else if (city != null && city.IsBig) PrintAreaMap(city.Name, city.MapFile, Character);
            else if (forest != null)  PrintAreaMap(forest.Name, forest.MapFile, Character);

            return true;
        }

        private static void PrintAreaMap(string areaName, string? mapFile, Character Character)
        {
            if (string.IsNullOrEmpty(mapFile) || !File.Exists(mapFile))
            {
                Console.WriteLine(Localization.T("console.map.not_found"));
                return;
            }

            string map = File.ReadAllText(mapFile);
            string roomName = Character.CurrentRoom.DisplayName();
            if (!string.IsNullOrWhiteSpace(roomName) && map.Contains(roomName))
                map = map.Replace(roomName, roomName + " ⭐", StringComparison.Ordinal);

            Console.WriteLine(Localization.T("console.map.header", areaName));
            Console.WriteLine(map);
        }

    }

}
