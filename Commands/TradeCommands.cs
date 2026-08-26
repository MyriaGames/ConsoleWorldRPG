using Myria.ConsoleApp.Services;
using static Myria.ConsoleApp.Services.ConsoleHubClient;

namespace Myria.ConsoleApp.Commands
{
    public static class TradeCommands
    {
        private static TradeSnapshot? _lastSnap;
        private static string?        _partnerName;

        public static void OnTradeStarted(string partnerName)
        {
            _partnerName = partnerName;
            _lastSnap    = null;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Localization.T("console.trade.session_started", partnerName));
            Console.WriteLine(Localization.T("console.trade.help_in_session"));
            Console.ResetColor();
        }

        public static void OnTradeUpdated(TradeSnapshot snap)
        {
            _lastSnap = snap;
            PrintSnap(snap);
        }

        public static void OnTradeCompleted()
        {
            _partnerName = null;
            _lastSnap    = null;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(Localization.T("console.trade.completed"));
            Console.ResetColor();
        }

        public static void OnTradeCancelled(string reason)
        {
            _partnerName = null;
            _lastSnap    = null;
            string msg = reason switch
            {
                "partner_offline"    => Localization.T("console.trade.cancelled_partner_offline"),
                "item_not_available" => Localization.T("console.trade.cancelled_item_unavailable"),
                "gold_not_available" => Localization.T("console.trade.cancelled_gold_unavailable"),
                _                    => Localization.T("console.trade.cancelled_generic")
            };
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(Localization.T("console.trade.prefixed", msg));
            Console.ResetColor();
        }

        public static bool Handle(string input, Character _)
        {
            if (input == "trade")
            {
                Console.WriteLine(Localization.T("console.trade.help"));
                return true;
            }

            if (input.StartsWith("trade accept "))
            {
                string id = input[13..].Trim();
                if (!ConsoleHubClient.PendingTradeProposals.ContainsKey(id))
                {
                    Console.WriteLine(Localization.T("console.trade.no_pending", id));
                    return true;
                }
                ConsoleHubClient.AcceptTradeAsync(id).GetAwaiter().GetResult();
                return true;
            }

            if (input.StartsWith("trade decline "))
            {
                string id = input[14..].Trim();
                if (!ConsoleHubClient.PendingTradeProposals.ContainsKey(id))
                {
                    Console.WriteLine(Localization.T("console.trade.no_pending", id));
                    return true;
                }
                ConsoleHubClient.DeclineTradeAsync(id).GetAwaiter().GetResult();
                Console.WriteLine(Localization.T("console.trade.proposal_declined"));
                return true;
            }

            if (input.StartsWith("trade add item "))
            {
                string rest  = input[15..].Trim();
                string[] parts = rest.Split(' ');
                if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
                {
                    Console.WriteLine(Localization.T("console.trade.add_item_usage"));
                    return true;
                }
                string itemId = parts[0];
                int qty = 1;
                if (parts.Length > 1 && int.TryParse(parts[1], out int q)) qty = q;
                if (qty <= 0) { Console.WriteLine(Localization.T("console.trade.quantity_positive")); return true; }
                ConsoleHubClient.AddTradeItemAsync(itemId, qty).GetAwaiter().GetResult();
                return true;
            }

            if (input.StartsWith("trade add gold "))
            {
                string amtStr = input[15..].Trim();
                if (!long.TryParse(amtStr, out long amount) || amount < 0)
                {
                    Console.WriteLine(Localization.T("console.trade.add_gold_usage"));
                    return true;
                }
                ConsoleHubClient.SetTradeGoldAsync(amount).GetAwaiter().GetResult();
                return true;
            }

            if (input.StartsWith("trade remove "))
            {
                string itemId = input[13..].Trim();
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    Console.WriteLine(Localization.T("console.trade.remove_usage"));
                    return true;
                }
                ConsoleHubClient.RemoveTradeItemAsync(itemId).GetAwaiter().GetResult();
                return true;
            }

            if (input == "trade show")
            {
                if (_lastSnap is null)
                    Console.WriteLine(Localization.T("console.trade.no_active_session"));
                else
                    PrintSnap(_lastSnap);
                return true;
            }

            if (input == "trade confirm")
            {
                ConsoleHubClient.ConfirmTradeAsync().GetAwaiter().GetResult();
                Console.WriteLine(Localization.T("console.trade.marked_ready"));
                return true;
            }

            if (input == "trade cancel")
            {
                ConsoleHubClient.CancelTradeAsync().GetAwaiter().GetResult();
                return true;
            }

            // trade <playerName> — propose trade
            if (input.StartsWith("trade "))
            {
                string target = input[6..].Trim();
                if (string.IsNullOrWhiteSpace(target)) { Console.WriteLine(Localization.T("console.trade.propose_usage")); return true; }
                if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.not_connected")); return true; }
                ConsoleHubClient.ProposeTradeAsync(target).GetAwaiter().GetResult();
                Console.WriteLine(Localization.T("console.trade.proposal_sent", target));
                return true;
            }

            return false;
        }

        private static void PrintSnap(TradeSnapshot snap)
        {
            string partner = _partnerName ?? Localization.T("console.trade.default_partner_name");
            Console.WriteLine(Localization.T("console.trade.header", partner));
            Console.WriteLine(Localization.T("console.trade.your_offer"));
            if (snap.MyItems.Count == 0 && snap.MyGold == 0)
                Console.WriteLine(Localization.T("console.trade.nothing"));
            foreach (var i in snap.MyItems)
                Console.WriteLine(Localization.T("console.trade.item_line", i.ItemId, Localization.T(i.Name), i.Quantity));
            if (snap.MyGold > 0)
                Console.WriteLine(Localization.T("console.trade.gold_line", snap.MyGold));
            Console.WriteLine(Localization.T("console.trade.their_offer", partner));
            if (snap.TheirItems.Count == 0 && snap.TheirGold == 0)
                Console.WriteLine(Localization.T("console.trade.nothing"));
            foreach (var i in snap.TheirItems)
                Console.WriteLine(Localization.T("console.trade.item_line", i.ItemId, Localization.T(i.Name), i.Quantity));
            if (snap.TheirGold > 0)
                Console.WriteLine(Localization.T("console.trade.gold_line", snap.TheirGold));
            string myReady   = snap.ImReady      ? Localization.T("console.trade.ready")     : Localization.T("console.trade.not_ready");
            string theirRdy  = snap.TheyAreReady ? Localization.T("console.trade.ready")     : Localization.T("console.trade.not_ready");
            Console.WriteLine(Localization.T("console.trade.ready_line", myReady, partner, theirRdy));
            Console.WriteLine(Localization.T("console.trade.footer"));
        }
    }
}
