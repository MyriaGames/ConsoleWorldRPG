using Myria.Lib.Core.Systems.World;

namespace Myria.ConsoleApp.Entities.NPCs
{
    // v0.3 quest rework: generated quests (supply requests from NPCs low on stock, bounties for
    // overrun rooms near an Adventurers' Guild house) and guild membership. All of it lives on the
    // server's world state, so it's online only - mirrors Myria.Wpf's MultiplayerDialogPanelViewModel.
    internal static class DynamicQuestHandler
    {
        // ── Supply requests (any NPC) ────────────────────────────────────────────

        public static void OfferRequests(Character Character, Npc npc)
        {
            if (!ConsoleHubClient.IsConnected) return;
            var offers = ConsoleHubClient.GetDynamicQuestOffersAsync(npc.Id).GetAwaiter().GetResult();
            if (offers.Count == 0) return;
            PickOffer(Character, offers, Localization.T("console.dyn.requests_header"));
        }

        // ── Adventurers' Guild house ─────────────────────────────────────────────

        public static void GuildMenu(Character Character, Npc npc)
        {
            if (!ConsoleHubClient.IsConnected)
            {
                Console.WriteLine(Localization.T("console.adv.offline"));
                return;
            }

            while (true)
            {
                Console.WriteLine(Localization.T("console.adv.welcome"));
                Console.WriteLine(Localization.T(Character.AdventurersGuildMember ? "guild.adv.member" : "guild.adv.not_member"));
                Console.WriteLine(Localization.T(Character.AdventurersGuildMember ? "console.adv.menu_leave" : "console.adv.menu_join"));
                if (Character.AdventurersGuildMember)
                    Console.WriteLine(Localization.T("console.adv.menu_bounties"));
                Console.WriteLine(Localization.T("console.npc.menu_leave"));
                Console.Write("> ");
                switch (Console.ReadLine()?.Trim())
                {
                    case "1": ToggleMembership(Character); break;
                    case "2" when Character.AdventurersGuildMember: ShowBounties(Character, npc); break;
                    case "0": Console.WriteLine(Localization.T("console.npc.leave_generic")); return;
                    default:  Console.WriteLine(Localization.T("ui_invalid_option")); break;
                }
            }
        }

        private static void ToggleMembership(Character Character)
        {
            bool wasMember = Character.AdventurersGuildMember;
            bool ok = (wasMember
                ? ConsoleHubClient.LeaveAdventurersGuildAsync()
                : ConsoleHubClient.JoinAdventurersGuildAsync()).GetAwaiter().GetResult();
            if (!ok)
            {
                Console.WriteLine(Localization.T("console.adv.failed"));
                return;
            }
            Character.AdventurersGuildMember = !wasMember;
            Console.WriteLine(Localization.T(wasMember ? "guild.adv.left" : "guild.adv.joined"));
        }

        private static void ShowBounties(Character Character, Npc npc)
        {
            var offers = ConsoleHubClient.GetDynamicQuestOffersAsync(npc.Id).GetAwaiter().GetResult();
            if (offers.Count == 0)
            {
                Console.WriteLine(Localization.T("guild.adv.no_bounties"));
                return;
            }
            PickOffer(Character, offers, Localization.T("console.adv.bounties_header"));
        }

        // ── Accept ───────────────────────────────────────────────────────────────

        private static void PickOffer(Character Character, List<DynamicQuestOfferDto> offers, string header)
        {
            Console.WriteLine(header);
            for (int i = 0; i < offers.Count; i++)
            {
                var preview = DynamicQuests.FromDto(offers[i].Preview);
                Console.WriteLine(Localization.T("console.dyn.offer_line", i + 1, QuestText.Name(preview),
                    Localization.T("quest.dyn.reward", preview.RewardXp, new Money(preview.RewardGold).ToString())));
                Console.WriteLine(Localization.T("console.dyn.offer_desc", QuestText.Description(preview)));
            }

            Console.Write(Localization.T("console.quest.accept_prompt"));
            if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 1 || choice > offers.Count)
                return;

            // Sized against what's still open now - someone else may have taken part of it meanwhile.
            var dto = ConsoleHubClient.AcceptDynamicQuestAsync(offers[choice - 1].Token).GetAwaiter().GetResult();
            if (dto is null)
            {
                Console.WriteLine(Localization.T("quest.dyn.accept_failed"));
                return;
            }
            var quest = DynamicQuests.FromDto(dto);
            QuestManager.RegisterDynamic(quest);
            QuestManager.StartQuest(Character, quest);
            Console.WriteLine(Localization.T("console.quest.accepted", QuestText.Name(quest)));
            Console.WriteLine(Localization.T("console.dyn.offer_desc", QuestText.Description(quest)));
        }

        // ── Hand-in ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Hands in a finished generated quest. The server decides (right NPC / guild house, items
        /// in the bag); on success the Console mirrors what it did - items out, rewards in, quest
        /// gone - since it doesn't apply the server's CharacterUpdated pushes.
        /// </summary>
        public static bool HandIn(Character Character, Quest quest)
        {
            if (!ConsoleHubClient.QuestActionAsync(quest.Id, isReturn: true).GetAwaiter().GetResult())
            {
                Console.WriteLine(Localization.T("console.dyn.handin_failed"));
                return false;
            }

            foreach (var (itemId, amount) in quest.RequiredItems)
            {
                int remaining = amount;
                foreach (var stack in Character.Inventory.Items.Where(i => i.Id == itemId).ToList())
                {
                    if (remaining <= 0) break;
                    int take = Math.Min(stack.StackSize, remaining);
                    stack.StackSize -= take;
                    remaining -= take;
                    if (stack.StackSize <= 0) Character.Inventory.RemoveItem(stack);
                }
            }

            quest.GrantRewards(Character);
            Character.ActiveQuests.Remove(quest);
            QuestManager.UnregisterDynamic(quest.Id);
            return true;
        }
    }
}
