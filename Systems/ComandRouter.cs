using Myria.ConsoleApp.Commands;

namespace Myria.ConsoleApp.Systems
{
    public class CommandRouter
    {
        private readonly Character _player;

        public CommandRouter(Character Character)
        {
            _player = Character;
        }

        public bool HandleCommand(string input)
        {
            input = input.Trim();

            // Lowercase only the command verb (first word) so command matching stays
            // case-insensitive — the way it worked before this fix, when the whole line was
            // lowercased — while preserving the case of everything after it: chat message text,
            // and case-sensitive arguments like character names (e.g. "friend add Test",
            // "w Test hello" — lowercasing "Test" to "test" broke friend requests, whispers,
            // party/trade/guild invites, since the server matches character names case-sensitively).
            int firstSpace = input.IndexOf(' ');
            input = firstSpace < 0
                ? input.ToLowerInvariant()
                : input[..firstSpace].ToLowerInvariant() + input[firstSpace..];

            return
                // First: while in a PvP fight its attack/cast/use/flee must win over the normal commands.
                PvpCommands.Handle(input, _player) ||
                InventoryCommands.Handle(input, _player) ||
                NpcCommands.Handle(input, _player) ||
                CharacterCommands.Handle(input, _player) ||
                SkillCommands.Handle(input, _player) ||
                MovementCommands.Handle(input, _player) ||
                CombatCommands.Handle(input, _player) ||
                LootCommands.Handle(input, _player) ||
                DebugCommands.Handle(input, _player) ||
                GatherCommands.Handle(input, _player) ||
                MapCommands.Handle(input, _player) ||
                SocialCommands.Handle(input, _player) ||
                PartyCommands.Handle(input, _player) ||
                TradeCommands.Handle(input, _player) ||
                ShopCommands.Handle(input, _player) ||
                GuildCommands.Handle(input, _player);
        }

    }

}
