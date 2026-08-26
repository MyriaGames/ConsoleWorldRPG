namespace Myria.ConsoleApp.Entities.NPCs
{
    // Mirrors Myria.Wpf's SkillPage. Public — also invoked directly from Commands/DebugCommands.
    public static class SkillMasterHandler
    {
        public static void SkillMasterMenu(Character Character)
        {
            while (true)
            {
                Console.WriteLine(Localization.T("console.npc.skill_master_title"));
                Console.WriteLine(Localization.T("console.npc.skill_master_view"));
                Console.WriteLine(Localization.T("console.npc.skill_master_combine"));
                Console.WriteLine(Localization.T("console.npc.skill_master_slots"));
                Console.WriteLine(Localization.T("console.npc.menu_leave"));
                Console.Write("> ");
                switch (Console.ReadLine()?.Trim())
                {
                    case "1": SkillCommands.ShowSkillsMenu(Character); break;
                    case "2": SkillCommands.CombineMenu(Character);    break;
                    case "3": SkillCommands.SlotsMenu(Character);      break;
                    case "0": return;
                    default:  Printer.PrintError(Localization.T("ui_invalid_option")); break;
                }
            }
        }
    }
}
