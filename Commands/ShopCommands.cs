using Myria.ConsoleApp.Services;
using static Myria.ConsoleApp.Services.ConsoleHubClient;

namespace Myria.ConsoleApp.Commands
{
    public static class ShopCommands
    {
        private static bool _shopOpen;
        private static List<ShopListing> _myListings = new();

        public static void OnShopListUpdated(List<ShopListing> listings)
        {
            _myListings = listings;
            _shopOpen   = true;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Localization.T("console.shop.listings_updated", listings.Count));
            foreach (var l in listings)
                Console.WriteLine(Localization.T("console.shop.listing_line", l.ListingId, Localization.T(l.Name), l.Quantity, l.Price));
            Console.ResetColor();
        }

        public static void OnShopSale(string buyerName, string itemName, int qty, long paid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(Localization.T("console.shop.sale", buyerName, qty, Localization.T(itemName), paid));
            Console.ResetColor();
        }

        public static void OnShopBuyResult(bool ok, string error, string itemName, int qty, long paid)
        {
            if (ok)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(Localization.T("console.shop.purchased", qty, Localization.T(itemName), paid));
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                string msg = error switch
                {
                    "insufficient_gold"  => Localization.T("console.shop.error_insufficient_gold"),
                    "insufficient_stock" => Localization.T("console.shop.error_insufficient_stock"),
                    "no_shop"            => Localization.T("console.shop.error_no_shop"),
                    "no_listing"         => Localization.T("console.shop.error_no_listing"),
                    _                    => Localization.T("console.shop.error_generic", error)
                };
                Console.WriteLine(Localization.T("console.shop.prefixed", msg));
                Console.ResetColor();
            }
        }

        public static bool Handle(string input, Character _)
        {
            if (input == "shop" || input == "shop help")
            {
                Console.WriteLine(Localization.T("console.shop.help"));
                return true;
            }

            if (input == "shop open")
            {
                if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.not_connected")); return true; }
                ConsoleHubClient.OpenShopAsync().GetAwaiter().GetResult();
                Console.WriteLine(Localization.T("console.shop.opening"));
                return true;
            }

            if (input == "shop close")
            {
                if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.not_connected")); return true; }
                ConsoleHubClient.CloseShopAsync().GetAwaiter().GetResult();
                _shopOpen   = false;
                _myListings = new();
                Console.WriteLine(Localization.T("console.shop.closed"));
                return true;
            }

            if (input == "shop list")
            {
                if (_myListings.Count == 0) { Console.WriteLine(Localization.T("console.shop.no_listings")); return true; }
                Console.WriteLine(Localization.T("console.shop.my_shop_header", _myListings.Count));
                foreach (var l in _myListings)
                    Console.WriteLine(Localization.T("console.shop.listing_line", l.ListingId, Localization.T(l.Name), l.Quantity, l.Price));
                return true;
            }

            if (input.StartsWith("shop add "))
            {
                string rest   = input[9..].Trim();
                var parts     = rest.Split(' ');
                if (parts.Length < 3)
                {
                    Console.WriteLine(Localization.T("console.shop.add_usage"));
                    return true;
                }
                string itemId = parts[0];
                if (!int.TryParse(parts[1], out int qty) || qty <= 0)   { Console.WriteLine(Localization.T("console.shop.invalid_quantity")); return true; }
                if (!long.TryParse(parts[2], out long price) || price <= 0) { Console.WriteLine(Localization.T("console.shop.invalid_price")); return true; }
                if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.not_connected")); return true; }
                ConsoleHubClient.AddShopListingAsync(itemId, qty, price).GetAwaiter().GetResult();
                Console.WriteLine(Localization.T("console.shop.listing_request_sent"));
                return true;
            }

            if (input.StartsWith("shop remove "))
            {
                string listingId = input[12..].Trim();
                if (string.IsNullOrWhiteSpace(listingId)) { Console.WriteLine(Localization.T("console.shop.remove_usage")); return true; }
                if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.not_connected")); return true; }
                ConsoleHubClient.RemoveShopListingAsync(listingId).GetAwaiter().GetResult();
                return true;
            }

            if (input.StartsWith("shop visit "))
            {
                string ownerName = input[11..].Trim();
                if (string.IsNullOrWhiteSpace(ownerName)) { Console.WriteLine(Localization.T("console.shop.visit_usage")); return true; }
                if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.not_connected")); return true; }
                var listings = ConsoleHubClient.BrowseShopAsync(ownerName).GetAwaiter().GetResult();
                if (listings.Count == 0)
                {
                    Console.WriteLine(Localization.T("console.shop.empty_or_closed", ownerName));
                    return true;
                }
                Console.WriteLine(Localization.T("console.shop.visit_header", ownerName, listings.Count));
                foreach (var l in listings)
                    Console.WriteLine(Localization.T("console.shop.listing_line", l.ListingId, Localization.T(l.Name), l.Quantity, l.Price));
                Console.WriteLine(Localization.T("console.shop.buy_hint", ownerName));
                return true;
            }

            if (input.StartsWith("shop buy "))
            {
                string rest  = input[9..].Trim();
                var parts    = rest.Split(' ');
                if (parts.Length < 2)
                {
                    Console.WriteLine(Localization.T("console.shop.buy_usage"));
                    return true;
                }
                string ownerName = parts[0];
                string listingId = parts[1];
                int qty = 1;
                if (parts.Length > 2 && int.TryParse(parts[2], out int q)) qty = q;
                if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.common.not_connected")); return true; }
                ConsoleHubClient.BuyFromShopAsync(ownerName, listingId, qty).GetAwaiter().GetResult();
                Console.WriteLine(Localization.T("console.shop.purchase_request_sent"));
                return true;
            }

            return false;
        }
    }
}
