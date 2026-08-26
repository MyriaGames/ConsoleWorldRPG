namespace Myria.ConsoleApp.Commands
{
    public static class DebugCommands
    {
        private static bool _debug = false;

        public static bool Handle(string input, Character Character)
        {
            if (input == "/debug") { _debug = !_debug; return true; }
            if (!_debug) return false;

            if (input == "/help")
            {
                Printer.ShowDebugHelp();
                return true;
            }
            else if (input.StartsWith("/set ") && input.Length > 10 && input.Substring(5, 5) == "level")
            {
                if (int.TryParse(input.Substring(10), out int newLevel))
                {
                    while (Character.Level < newLevel)
                        Character.LevelUp();
                    if (Character.Level > newLevel)
                        Console.WriteLine(Localization.T("console.debug.level_only_increase"));
                    return true;
                }
            }
            else if (input == "/skillmaster")
            {
                SkillMasterHandler.SkillMasterMenu(Character);
                return true;
            }

            return false;
        }

    }

}
