namespace Myria.ConsoleApp.Commands
{
    public static class SocialCommands
    {
        public static bool Handle(string input, Character Character)
        {
            if (input == "friend list")
            {
                ShowFriends();
                return true;
            }
            if (input == "friend requests")
            {
                ShowFriendRequests();
                return true;
            }
            if (input.StartsWith("friend add "))
            {
                AddFriend(input[11..].Trim());
                return true;
            }
            if (input.StartsWith("friend accept "))
            {
                string idStr = input[14..].Trim();
                if (int.TryParse(idStr, out int id)) AcceptFriend(id);
                else Console.WriteLine("Usage: friend accept <id>");
                return true;
            }
            if (input.StartsWith("friend remove "))
            {
                string idStr = input[14..].Trim();
                if (int.TryParse(idStr, out int id)) RemoveFriend(id);
                else Console.WriteLine("Usage: friend remove <id>");
                return true;
            }
            if (input == "friend")
            {
                Console.WriteLine(Localization.T("console.social.friend_help"));
                return true;
            }
            if (input == "block list")
            {
                ShowBlocks();
                return true;
            }
            if (input.StartsWith("block ") && !input.StartsWith("block list"))
            {
                BlockCharacter(input[6..].Trim());
                return true;
            }
            if (input.StartsWith("unblock "))
            {
                string idStr = input[8..].Trim();
                if (int.TryParse(idStr, out int id)) Unblock(id);
                else Console.WriteLine("Usage: unblock <id>");
                return true;
            }
            return false;
        }

        private static void ShowFriends()
        {
            if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.offline")); return; }
            var friends = ConsoleHubClient.GetFriendsAsync().GetAwaiter().GetResult();
            if (friends.Count == 0) { Console.WriteLine(Localization.T("console.social.friend_list_empty")); return; }
            Console.WriteLine(Localization.T("console.social.friends_header", friends.Count));
            foreach (var f in friends)
            {
                string status = f.IsOnline ? Localization.T("console.social.online_tag") : Localization.T("console.social.offline_tag");
                Console.ForegroundColor = f.IsOnline ? ConsoleColor.Green : ConsoleColor.DarkGray;
                Console.WriteLine(Localization.T("console.social.friend_line", f.FriendshipId, f.Username, status));
                Console.ResetColor();
            }
        }

        private static void ShowFriendRequests()
        {
            if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.offline")); return; }
            var reqs = ConsoleHubClient.GetFriendRequestsAsync().GetAwaiter().GetResult();
            if (reqs.Count == 0) { Console.WriteLine(Localization.T("console.social.no_pending_requests")); return; }
            Console.WriteLine(Localization.T("console.social.requests_header", reqs.Count));
            foreach (var r in reqs)
                Console.WriteLine(Localization.T("console.social.request_line", r.FriendshipId, r.FromUsername));
        }

        private static void AddFriend(string name)
        {
            if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.offline")); return; }
            if (string.IsNullOrWhiteSpace(name)) { Console.WriteLine(Localization.T("console.social.friend_add_usage")); return; }
            bool ok = ConsoleHubClient.SendFriendRequestAsync(name).GetAwaiter().GetResult();
            if (ok)
                Console.WriteLine(Localization.T("console.social.friend_request_sent", name));
            else
                Console.WriteLine(Localization.T("console.social.friend_request_failed", name));
        }

        private static void AcceptFriend(int id)
        {
            if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.offline")); return; }
            bool ok = ConsoleHubClient.AcceptFriendRequestAsync(id).GetAwaiter().GetResult();
            Console.WriteLine(ok ? Localization.T("console.social.friend_accepted") : Localization.T("console.social.friend_accept_failed"));
        }

        private static void RemoveFriend(int id)
        {
            if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.offline")); return; }
            bool ok = ConsoleHubClient.RemoveFriendAsync(id).GetAwaiter().GetResult();
            Console.WriteLine(ok ? Localization.T("console.social.friend_removed") : Localization.T("console.social.friend_remove_failed"));
        }

        private static void ShowBlocks()
        {
            if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.offline")); return; }
            var blocks = ConsoleHubClient.GetBlocksAsync().GetAwaiter().GetResult();
            if (blocks.Count == 0) { Console.WriteLine(Localization.T("console.social.no_blocks")); return; }
            Console.WriteLine(Localization.T("console.social.blocks_header", blocks.Count));
            foreach (var b in blocks)
                Console.WriteLine(Localization.T("console.social.block_line", b.BlockId, b.Username));
        }

        private static void BlockCharacter(string name)
        {
            if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.offline")); return; }
            if (string.IsNullOrWhiteSpace(name)) { Console.WriteLine(Localization.T("console.social.block_usage")); return; }
            bool ok = ConsoleHubClient.BlockCharacterAsync(name).GetAwaiter().GetResult();
            Console.WriteLine(ok ? Localization.T("console.social.blocked", name) : Localization.T("console.social.block_failed", name));
        }

        private static void Unblock(int id)
        {
            if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.offline")); return; }
            bool ok = ConsoleHubClient.UnblockAsync(id).GetAwaiter().GetResult();
            Console.WriteLine(ok ? Localization.T("console.social.unblocked") : Localization.T("console.social.unblock_failed"));
        }
    }
}
