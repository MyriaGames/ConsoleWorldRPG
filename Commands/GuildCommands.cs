namespace Myria.ConsoleApp.Commands
{
    public static class GuildCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input == "guild")
            {
                ShowHelp();
                return true;
            }

            if (input.StartsWith("guild "))
            {
                string sub = input[6..].Trim();
                HandleSub(sub, Character);
                return true;
            }

            return false;
        }

        private static void ShowHelp()
        {
            Console.WriteLine(Localization.T("console.guild.help"));
        }

        private static void HandleSub(string sub, Character Character)
        {
            if (!ConsoleHubClient.IsConnected)
            {
                Console.WriteLine(Localization.T("console.common.offline"));
                return;
            }

            if (sub.StartsWith("invite "))
            {
                string target = sub[7..].Trim();
                _ = ConsoleHubClient.GuildSendInviteAsync(target, asRookie: false);
                Console.WriteLine(Localization.T("console.guild.invite_sent", target));
            }
            else if (sub.StartsWith("inviterookie "))
            {
                string target = sub[13..].Trim();
                _ = ConsoleHubClient.GuildSendInviteAsync(target, asRookie: true);
                Console.WriteLine(Localization.T("console.guild.rookie_invite_sent", target));
            }
            else if (sub.StartsWith("accept "))
            {
                if (int.TryParse(sub[7..].Trim(), out int inviteId))
                    _ = ConsoleHubClient.GuildAcceptInviteAsync(inviteId);
                else
                    Console.WriteLine(Localization.T("console.guild.accept_usage"));
            }
            else if (sub.StartsWith("decline "))
            {
                if (int.TryParse(sub[8..].Trim(), out int inviteId))
                    _ = ConsoleHubClient.GuildDeclineInviteAsync(inviteId);
                else
                    Console.WriteLine(Localization.T("console.guild.decline_usage"));
            }
            else if (sub == "leave")
            {
                _ = ConsoleHubClient.GuildLeaveAsync();
            }
            else if (sub.StartsWith("kick "))
            {
                string target = sub[5..].Trim();
                _ = ConsoleHubClient.GuildKickAsync(target);
            }
            else if (sub.StartsWith("promoterookie "))
            {
                string target = sub[14..].Trim();
                _ = ConsoleHubClient.GuildPromoteRookieAsync(target);
            }
            else if (sub.StartsWith("promote "))
            {
                string target = sub[8..].Trim();
                _ = ConsoleHubClient.GuildPromoteAsync(target);
            }
            else if (sub.StartsWith("demote "))
            {
                string target = sub[7..].Trim();
                _ = ConsoleHubClient.GuildDemoteAsync(target);
            }
            else if (sub.StartsWith("transfer "))
            {
                string target = sub[9..].Trim();
                _ = ConsoleHubClient.GuildTransferLeadershipAsync(target);
            }
            else if (sub == "disband")
            {
                _ = ConsoleHubClient.GuildDisbandAsync();
            }
            else if (sub.StartsWith("hire "))
            {
                string target = sub[5..].Trim();
                _ = ConsoleHubClient.GuildHireRookieAsync(target);
            }
            else if (sub.StartsWith("fire "))
            {
                string target = sub[5..].Trim();
                _ = ConsoleHubClient.GuildFireRookieAsync(target);
            }
            else if (sub == "property")
            {
                _ = ShowPropertyAsync();
            }
            else if (sub.StartsWith("buyhouse "))
            {
                if (int.TryParse(sub[9..].Trim(), out int cityRoomId))
                    _ = BuyHouseAsync(cityRoomId);
                else
                    Console.WriteLine(Localization.T("console.guild.buyhouse_usage"));
            }
            else if (sub == "establishbase")
            {
                _ = EstablishBaseAsync(Character);
            }
            else
            {
                ShowHelp();
            }
        }

        // ── Guild property/base ─────────────────────────────────────────────────

        private static async Task ShowPropertyAsync()
        {
            var prop = await ConsoleHubClient.GetGuildPropertyAsync();
            if (prop is null)
            {
                Console.WriteLine(Localization.T("console.guild.no_property"));
                return;
            }

            string typeLabel = prop.Type == "House" ? Localization.T("console.guild.type_house") : Localization.T("console.guild.type_base");
            Console.WriteLine(Localization.T("console.guild.property_header", typeLabel, prop.AcquiredAt));
            if (prop.Rooms.Length == 0)
            {
                Console.WriteLine(Localization.T("console.guild.no_rooms_built"));
                return;
            }
            foreach (var r in prop.Rooms)
            {
                string built = r.IsBuilt ? Localization.T("console.guild.built_tag") : Localization.T("console.guild.not_built_tag");
                Console.WriteLine(Localization.T("console.guild.room_line", r.Name, r.RoomType, built, r.MinRankRequired));
            }
        }

        private static async Task BuyHouseAsync(int cityRoomId)
        {
            var (success, error) = await ConsoleHubClient.PurchaseGuildHouseAsync(cityRoomId);
            if (success) Printer.PrintSuccess(Localization.T("console.guild.house_purchased"));
            else Printer.PrintError(error);
        }

        private static async Task EstablishBaseAsync(Character Character)
        {
            var (success, error) = await ConsoleHubClient.EstablishGuildBaseAsync(Character.CurrentRoomId);
            if (success) Printer.PrintSuccess(Localization.T("console.guild.base_established"));
            else Printer.PrintError(error);
        }
    }
}
