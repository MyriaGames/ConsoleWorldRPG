namespace Myria.ConsoleApp.Commands
{
    public static class NpcCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input.StartsWith("go to "))
            {
                string npc = input.Substring(6).Trim();
                NpcInteractionHandler.InteractWithNpc(npc, Character);
                return true;
            }

            return false;
        }

    }

}
