namespace Myria.ConsoleApp.Commands
{
    public static class PartyCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input == "party")
            {
                ShowParty();
                return true;
            }

            if (input.StartsWith("party "))
            {
                string sub = input[6..].Trim();
                HandleSub(sub);
                return true;
            }

            return false;
        }

        private static void ShowParty()
        {
            if (!ConsoleHubClient.IsConnected)
            {
                Console.WriteLine(Localization.T("console.common.offline"));
                return;
            }

            var members = ConsoleHubClient.CurrentPartyMembers;
            if (members.Count == 0)
            {
                Console.WriteLine(Localization.T("console.party.not_in_party"));
                return;
            }

            Console.WriteLine(Localization.T("console.party.header", members.Count));
            foreach (var m in members)
            {
                string tag = m == ConsoleHubClient.CurrentPartyLeader ? Localization.T("console.party.leader_tag") : "";
                Console.WriteLine(Localization.T("console.common.list_item_tagged", m, tag));
            }
        }

        private static void HandleSub(string sub)
        {
            if (!ConsoleHubClient.IsConnected)
            {
                Console.WriteLine(Localization.T("console.common.offline"));
                return;
            }

            if (sub.StartsWith("invite "))
            {
                string username = sub[7..].Trim();
                _ = ConsoleHubClient.InviteToPartyAsync(username);
                Console.WriteLine(Localization.T("console.party.invite_sent", username));
            }
            else if (sub.StartsWith("accept "))
            {
                string partyId = sub[7..].Trim();
                _ = ConsoleHubClient.AcceptPartyInviteAsync(partyId);
            }
            else if (sub.StartsWith("decline "))
            {
                string[] parts = sub[8..].Trim().Split(' ', 2);
                string partyId = parts[0];
                string from    = parts.Length > 1 ? parts[1] : "";
                _ = ConsoleHubClient.DeclinePartyInviteAsync(partyId, from);
                Console.WriteLine(Localization.T("console.party.invite_declined"));
            }
            else if (sub == "leave")
            {
                _ = ConsoleHubClient.LeavePartyAsync();
                Console.WriteLine(Localization.T("console.party.left"));
            }
            else if (sub.StartsWith("kick "))
            {
                string target = sub[5..].Trim();
                _ = ConsoleHubClient.KickFromPartyAsync(target);
                Console.WriteLine(Localization.T("console.party.kick_sent", target));
            }
            else if (sub.StartsWith("promote "))
            {
                string target = sub[8..].Trim();
                _ = ConsoleHubClient.TransferPartyLeaderAsync(target);
                Console.WriteLine(Localization.T("console.party.promote_sent", target));
            }
            else if (sub.Length > 0)
            {
                if (sub.Length > 500) { Console.WriteLine(Localization.T("console.common.message_too_long")); return; }
                _ = ConsoleHubClient.SendMessageAsync(sub, "party");
            }
            else
            {
                Console.WriteLine(Localization.T("console.party.help"));
            }
        }
    }
}
