using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Myria.Lib.Core.Entities.Jobs;
using Myria.Lib.Core.Models.BaseModel;
using Myria.Lib.Core.Utils;

namespace Myria.ConsoleApp.Services
{
    public static class ConsoleHubClient
    {
        // AuthBaseUrl points at the shared MyriaAuthServer (register/login) — fixed regardless
        // of realm. BaseUrl points at the selected realm's own MyriaServer instance (chosen via
        // the realm-selection menu in LoginManager, not this default).
        // Mirrors Myria.Wpf's ServerApiService.DefaultAuthBaseUrl: the official Myria auth server,
        // used whenever no server address is set. Local dev: set the address to "localhost:5050".
        public const string DefaultAuthBaseUrl = "https://myria.myria-games.duckdns.org/auth";
        public static string AuthBaseUrl { get; set; } = DefaultAuthBaseUrl;
        public static string BaseUrl { get; set; } = "http://localhost:5001";

        // Mirrors Myria.Wpf's ServerApiService.NormalizeAddress — lets the player type a bare
        // "host:port" in settings instead of a full URL. Defaults to https:// for anything but
        // loopback, so a remote server address the player types doesn't silently send their
        // login credentials over plain HTTP unless they're pointing at their own machine.
        public static string NormalizeAddress(string address)
        {
            address = address.Trim();
            if (address.Length == 0) return address;

            if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var host = address.Split('/')[0].Split(':')[0];
                var isLoopback = host is "localhost" or "127.0.0.1" or "::1";
                address = (isLoopback ? "http://" : "https://") + address;
            }

            // Every endpoint is built as $"{address}/api/...", so a trailing slash (easy to paste,
            // e.g. "myria.example.com/auth/") would produce "//api/..." - which misses the route
            // on the server and doesn't match a reverse proxy's path prefix either.
            return address.TrimEnd('/');
        }

        // Any operator can run their own Auth/Realm server, almost always with a self-signed
        // certificate (no domain to get a publicly-trusted one for). Trust-on-first-use each
        // server address independently via TrustedServerCertStore (same mechanism as Myria.Wpf's
        // ServerApiService) instead of failing every such connection outright, which is what the
        // default HttpClient would otherwise do. A host that ever presented a publicly trusted
        // certificate never falls back to TOFU, the official server never does, and a certificate
        // for another name is never accepted (security review 2026-11-26, A04 F1).
        private static HttpMessageHandler CreatePinnedHandler() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, cert, _, errors) =>
            {
                if (request.RequestUri is null) return false;
                return TrustedServerCertStore.IsAcceptable(
                    request.RequestUri.Authority, request.RequestUri.Host,
                    cert?.GetCertHashString(HashAlgorithmName.SHA256), errors, CaOnlyHosts);
            }
        };

        /// <summary>Hosts with a publicly trusted certificate: an untrusted certificate for them is
        /// always an attack, never a self-hosted server.</summary>
        public static readonly IReadOnlyList<string> CaOnlyHosts = [new Uri(DefaultAuthBaseUrl).Host];

        private static readonly HttpClient _http = new(CreatePinnedHandler());
        private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };
        private static HubConnection? _connection;
        private static string? _token;

        public static bool HasToken => _token is not null;

        public static bool IsConnected =>
            _connection?.State == HubConnectionState.Connected;

        // ── Room presence state ───────────────────────────────────────────────────
        public static List<string> CurrentRoomCharacters { get; } = new();

        // ── Party state ──────────────────────────────────────────────────────────
        public static List<string> CurrentPartyMembers { get; private set; } = new();
        public static string? CurrentPartyLeader { get; private set; }

        // ── Events ───────────────────────────────────────────────────────────────
        public static event Action<string, string, string>? ChatMessageReceived;   // sender, message, channel
        public static event Action<string>?               CharacterEntered;
        public static event Action<string>?               CharacterLeft;
        public static event Action<string>?               CharacterGathering;
        public static event Action<string>?               CharacterCrafting;
        public static event Action<string>?               CharacterUpgrading;
        public static event Action<string>?               CharacterInCombat;
        public static event Action<string>?               CharacterCombatEnded;
        public static event Action<List<string>>?         RoomCharactersReceived;
        public static event Action<string, string>?       PartyInviteReceived;     // fromUsername, partyId
        public static event Action<List<string>, string?>? PartyUpdated;           // members, leader
        public static event Action?                        PartyDisbanded;
        public static event Action?                        KickedFromParty;

        // ── Shop state ───────────────────────────────────────────────────────────
        public static event Action<string>?                ShopOpened;
        public static event Action<string>?                ShopClosed;
        public static event Action<List<ShopListing>>?     ShopListUpdated;
        public static event Action<string, string, int, long>? ShopSale;
        public static event Action<bool, string, string, int, long>? ShopBuyResult;

        // ── Group combat state ──────────────────────────────────────────────────
        public static GroupCombatSnapshot? ActiveGroupCombat { get; private set; }

        public static event Action<StartGroupCombatResult>? GroupCombatStarted;
        public static event Action<GroupCombatSnapshot>?    GroupCombatUpdated;
        public static event Action<GroupCombatSnapshot>?    GroupCombatFinished;
        public static event Action<int, string>?            CharacterRespawned;    // roomId, roomName

        // ── Trade state ──────────────────────────────────────────────────────────
        // tradeId -> proposerName, for pending incoming proposals
        public static Dictionary<string, string> PendingTradeProposals { get; } = new();

        public static event Action<string, string>?        TradeProposed;   // proposerName, tradeId
        public static event Action<string>?                TradeStarted;    // partnerName
        public static event Action<TradeSnapshot>?         TradeUpdated;
        public static event Action?                        TradeCompleted;
        public static event Action<string>?                TradeCancelled;  // reason

        // ── Party stats / shop errors ───────────────────────────────────────────
        public static event Action<string, int, int, int, int, int>? PartyMemberStats; // name, hp, maxHp, mana, maxMana, level
        public static event Action<string>?                 ShopError;

        // ── Guild state ──────────────────────────────────────────────────────────
        public static event Action<string, string>?         GuildMemberJoined;   // name, rank
        public static event Action<string, bool>?           GuildMemberLeft;     // name, wasKicked
        public static event Action<string, string>?         GuildRankChanged;    // name, newRank
        public static event Action<string, string>?         GuildLeaderChanged;  // oldLeader, newLeader
        public static event Action<string>?                 GuildRookieAdded;
        public static event Action?                         GuildDisbanded;
        public static event Action?                         GuildLeft;
        public static event Action?                         GuildKicked;
        public static event Action<int, int, string?, string?, bool>? GuildInviteReceived; // inviteId, guildId, guildName, guildTag, asRookie
        public static event Action<string>?                 GuildError;
        public static event Action<string, string>?         GuildMemberOnline;   // name, room
        public static event Action<string>?                 GuildMemberOffline;

        // ── REST auth ────────────────────────────────────────────────────────────
        public static async Task<(bool Success, string? Error)> LoginAsync(string username, string password)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{AuthBaseUrl}/api/auth/login",
                    new { username, password });
                if (!resp.IsSuccessStatusCode)
                    return (false, await ExtractErrorAsync(resp));

                var result = await resp.Content.ReadFromJsonAsync<AuthResponse>(_jsonOpts);
                if (result?.Token is null)
                    return (false, "Server response was missing a token.");

                _token = result.Token;
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _token);
                return (true, null);
            }
            catch (Exception ex) { return (false, $"Could not reach server: {ex.Message}"); }
        }

        public static async Task<(bool Success, string? Error)> RegisterAsync(string username, string password)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{AuthBaseUrl}/api/auth/register",
                    new { username, password });
                return resp.IsSuccessStatusCode ? (true, null) : (false, await ExtractErrorAsync(resp));
            }
            catch (Exception ex) { return (false, $"Could not reach server: {ex.Message}"); }
        }

        // Pulls the {"message": "..."} body the auth server sends on 4xx/5xx, falling back to
        // the raw status code when the body isn't the expected shape (e.g. an unhandled 500).
        private static async Task<string> ExtractErrorAsync(HttpResponseMessage resp)
        {
            try
            {
                var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_jsonOpts);
                if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("message", out var m) && m.GetString() is { } msg)
                    return msg;
            }
            catch { /* body wasn't JSON or didn't match — fall through to status code */ }

            return $"Server returned {(int)resp.StatusCode} {resp.StatusCode}.";
        }

        // The console has no realm/lobby-selection menu at all today (single fixed BaseUrl,
        // per TODO.md item 31) — this helper exists so a future realm-picker menu can be added
        // without touching ConsoleHubClient again, but wiring it into the login flow is
        // explicitly deferred; there is no existing UI to hang it on yet, matching the WPF-only
        // scoping already noted for realm selection.
        public static async Task<List<(string Id, string Name, string Url, bool Online, int PlayerCount)>> GetRealmsAsync()
        {
            List<RealmDto> realms;
            try
            {
                var resp = await _http.GetAsync($"{AuthBaseUrl}/api/realms");
                if (!resp.IsSuccessStatusCode) return [];
                realms = await resp.Content.ReadFromJsonAsync<List<RealmDto>>(_jsonOpts) ?? [];
            }
            catch { return []; }

            var results = new List<(string, string, string, bool, int)>();
            foreach (var realm in realms)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var resp = await _http.GetAsync($"{realm.Url}/api/status", cts.Token);
                    if (!resp.IsSuccessStatusCode)
                    {
                        results.Add((realm.Id, realm.Name, realm.Url, false, 0));
                        continue;
                    }
                    var status = await resp.Content.ReadFromJsonAsync<StatusDto>(_jsonOpts, cts.Token);
                    results.Add((realm.Id, realm.Name, realm.Url, true, status?.PlayerCount ?? 0));
                }
                catch { results.Add((realm.Id, realm.Name, realm.Url, false, 0)); }
            }
            return results;
        }

        private class RealmDto
        {
            public string Id   { get; set; } = "";
            public string Name { get; set; } = "";
            public string Url  { get; set; } = "";
        }

        private class StatusDto
        {
            public int PlayerCount { get; set; }
        }

        // ── Hub connection ────────────────────────────────────────────────────────
        public static async Task ConnectAsync()
        {
            if (_token is null) return;
            if (_connection is not null) await DisconnectAsync();

            // Sent so GameHub.OnConnectedAsync can reject an incompatible client outright - see
            // the versioning scheme in v0.3.md. Same assembly-version source UpdateService (Wpf)
            // compares against, so this client stays in lockstep with its own build automatically.
            var clientVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            _connection = new HubConnectionBuilder()
                .WithUrl($"{BaseUrl}/hubs/game?clientVersion={clientVersion}", opts =>
                {
                    opts.AccessTokenProvider = () => Task.FromResult<string?>(_token);
                    // Same trust-on-first-use cert handling as _http, so the SignalR connection
                    // doesn't fall back to default validation (which would reject a self-signed
                    // server cert outright) once BaseUrl points at https://.
                    opts.HttpMessageHandlerFactory = _ => CreatePinnedHandler();
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<List<string>>("VersionMismatch", _ =>
                Printer.PrintWarning(Localization.T("console.version_mismatch")));

            _connection.On<string, string, string>("ChatMessage", (sender, msg, channel) =>
                ChatMessageReceived?.Invoke(sender, msg, channel));

            _connection.On<string>("CharacterEntered", name =>
            {
                if (!CurrentRoomCharacters.Contains(name))
                    CurrentRoomCharacters.Add(name);
                CharacterEntered?.Invoke(name);
            });

            _connection.On<string>("CharacterLeft", name =>
            {
                CurrentRoomCharacters.Remove(name);
                CharacterLeft?.Invoke(name);
            });

            _connection.On<List<string>>("RoomCharacters", players =>
            {
                CurrentRoomCharacters.Clear();
                CurrentRoomCharacters.AddRange(players);
                RoomCharactersReceived?.Invoke(players);
            });

            _connection.On<string>("CharacterGathering", name => CharacterGathering?.Invoke(name));
            _connection.On<string>("CharacterCrafting", name => CharacterCrafting?.Invoke(name));
            _connection.On<string>("CharacterUpgrading", name => CharacterUpgrading?.Invoke(name));
            _connection.On<string>("CharacterInCombat", name => CharacterInCombat?.Invoke(name));
            _connection.On<string>("CharacterCombatEnded", name => CharacterCombatEnded?.Invoke(name));

            _connection.On<string, string>("PartyInvite", (from, partyId) =>
                PartyInviteReceived?.Invoke(from, partyId));

            _connection.On<List<string>, string?>("PartyUpdated", (members, leader) =>
            {
                CurrentPartyMembers = members;
                CurrentPartyLeader  = leader;
                PartyUpdated?.Invoke(members, leader);
            });

            _connection.On("PartyDisbanded", () =>
            {
                CurrentPartyMembers.Clear();
                CurrentPartyLeader = null;
                PartyDisbanded?.Invoke();
            });

            _connection.On("KickedFromParty", () =>
            {
                CurrentPartyMembers.Clear();
                CurrentPartyLeader = null;
                KickedFromParty?.Invoke();
            });

            _connection.On<string>("ShopOpened", ownerName =>
                ShopOpened?.Invoke(ownerName));

            _connection.On<string>("ShopClosed", ownerName =>
                ShopClosed?.Invoke(ownerName));

            _connection.On<List<ShopListing>>("ShopListUpdated", listings =>
                ShopListUpdated?.Invoke(listings));

            _connection.On<string, string, int, long>("ShopSale", (buyer, itemName, qty, paid) =>
                ShopSale?.Invoke(buyer, itemName, qty, paid));

            _connection.On<bool, string, string, int, long>("ShopBuyResult", (ok, err, itemName, qty, paid) =>
                ShopBuyResult?.Invoke(ok, err, itemName, qty, paid));

            _connection.On<StartGroupCombatResult>("GroupCombatStarted", result =>
            {
                ActiveGroupCombat = new GroupCombatSnapshot(
                    result.Characters, result.Monsters, result.CurrentTurnCharacterName,
                    false, false, new());
                GroupCombatStarted?.Invoke(result);
            });

            _connection.On<GroupCombatSnapshot>("GroupCombatUpdated", snap =>
            {
                ActiveGroupCombat = snap;
                GroupCombatUpdated?.Invoke(snap);
            });

            _connection.On<GroupCombatSnapshot>("GroupCombatFinished", snap =>
            {
                ActiveGroupCombat = null;
                GroupCombatFinished?.Invoke(snap);
            });

            _connection.On<int, string>("CharacterRespawned", (roomId, roomName) =>
                CharacterRespawned?.Invoke(roomId, roomName));

            _connection.On<string, string>("TradeProposed", (proposerName, tradeId) =>
            {
                PendingTradeProposals[tradeId] = proposerName;
                TradeProposed?.Invoke(proposerName, tradeId);
            });

            _connection.On<string>("TradeStarted", partnerName =>
                TradeStarted?.Invoke(partnerName));

            _connection.On<TradeSnapshot>("TradeUpdated", snap =>
                TradeUpdated?.Invoke(snap));

            _connection.On("TradeCompleted", () =>
                TradeCompleted?.Invoke());

            _connection.On<string>("TradeCancelled", reason =>
                TradeCancelled?.Invoke(reason));

            _connection.On<string, int, int, int, int, int>("PartyMemberStats",
                (name, hp, maxHp, mana, maxMana, level) =>
                    PartyMemberStats?.Invoke(name, hp, maxHp, mana, maxMana, level));

            _connection.On<string>("ShopError", msg =>
                ShopError?.Invoke(msg));

            _connection.On<string, string>("GuildMemberJoined", (n, r) =>
                GuildMemberJoined?.Invoke(n, r));

            _connection.On<string, bool>("GuildMemberLeft", (n, kicked) =>
                GuildMemberLeft?.Invoke(n, kicked));

            _connection.On<string, string>("GuildRankChanged", (n, r) =>
                GuildRankChanged?.Invoke(n, r));

            _connection.On<string, string>("GuildLeaderChanged", (o, nu) =>
                GuildLeaderChanged?.Invoke(o, nu));

            _connection.On<string>("GuildRookieAdded", n =>
                GuildRookieAdded?.Invoke(n));

            _connection.On("GuildDisbanded", () =>
                GuildDisbanded?.Invoke());

            _connection.On("GuildLeft", () =>
                GuildLeft?.Invoke());

            _connection.On("GuildKicked", () =>
                GuildKicked?.Invoke());

            _connection.On<int, int, string?, string?, bool>("GuildInviteReceived",
                (inviteId, guildId, guildName, guildTag, asRookie) =>
                    GuildInviteReceived?.Invoke(inviteId, guildId, guildName, guildTag, asRookie));

            _connection.On<string>("GuildError", msg =>
                GuildError?.Invoke(msg));

            _connection.On<string, string>("GuildMemberOnline", (n, r) =>
                GuildMemberOnline?.Invoke(n, r));

            _connection.On<string>("GuildMemberOffline", n =>
                GuildMemberOffline?.Invoke(n));

            try
            {
                await _connection.StartAsync();
            }
            catch { /* hub unavailable — game continues in single-Character mode */ }
        }

        public static async Task DisconnectAsync()
        {
            if (_connection is null) return;
            try { await _connection.StopAsync(); } catch { }
            await _connection.DisposeAsync();
            _connection = null;
            _token = null;
            _http.DefaultRequestHeaders.Authorization = null;
            CurrentPartyMembers.Clear();
            CurrentPartyLeader = null;
        }

        // ── Hub invocations ───────────────────────────────────────────────────────
        // Mirrors the WPF client's UserAccountService.CurrentCharacter?.Name — the friends/
        // blocks REST endpoints require a characterName so the server can resolve which of the
        // account's characters is acting (an account can own up to 5).
        public static string? CurrentCharacterName { get; private set; }

        public static async Task SetCharacterNameAsync(string characterName)
        {
            CurrentCharacterName = characterName;
            if (IsConnected)
                try { await _connection!.InvokeAsync("SetCharacterName", characterName); } catch { }
        }

        public static async Task JoinRoomAsync(int roomId)
        {
            CurrentRoomCharacters.Clear();
            if (IsConnected)
                try { await _connection!.InvokeAsync("JoinRoom", roomId); } catch { }
        }

        public static async Task SendMessageAsync(string message, string channel = "room", string? target = null)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("SendMessage", message, channel, target); } catch { }
        }

        public static async Task<bool> LoadCharacterOnServerAsync(string characterName)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("LoadCharacter", characterName); } catch { }
            return false;
        }

        public static async Task InviteToPartyAsync(string username)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("InviteToParty", username); } catch { }
        }

        public static async Task AcceptPartyInviteAsync(string partyId)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("AcceptPartyInvite", partyId); } catch { }
        }

        public static async Task DeclinePartyInviteAsync(string partyId, string fromUsername)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("DeclinePartyInvite", partyId, fromUsername); } catch { }
        }

        public static async Task LeavePartyAsync()
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("LeaveParty"); } catch { }
        }

        public static async Task KickFromPartyAsync(string targetName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("KickFromParty", targetName); } catch { }
        }

        public static async Task TransferPartyLeaderAsync(string targetName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("TransferPartyLeader", targetName); } catch { }
        }

        // ── Hub shop actions ──────────────────────────────────────────────────────
        public static async Task OpenShopAsync()
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("OpenShop"); } catch { }
        }

        public static async Task CloseShopAsync()
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("CloseShop"); } catch { }
        }

        public static async Task AddShopListingAsync(string itemId, int qty, long price)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("AddShopListing", itemId, qty, price); } catch { }
        }

        public static async Task RemoveShopListingAsync(string listingId)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("RemoveShopListing", listingId); } catch { }
        }

        public static async Task<List<ShopListing>> BrowseShopAsync(string ownerName)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<List<ShopListing>>("BrowseShop", ownerName); } catch { }
            return new();
        }

        public static async Task BuyFromShopAsync(string ownerName, string listingId, int qty)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("BuyFromShop", ownerName, listingId, qty); } catch { }
        }

        // ── Hub trade actions ─────────────────────────────────────────────────────
        public static async Task ProposeTradeAsync(string targetName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("ProposeTrade", targetName); } catch { }
        }

        public static async Task AcceptTradeAsync(string tradeId)
        {
            if (IsConnected)
            {
                PendingTradeProposals.Remove(tradeId);
                try { await _connection!.InvokeAsync("AcceptTrade", tradeId); } catch { }
            }
        }

        public static async Task DeclineTradeAsync(string tradeId)
        {
            if (IsConnected)
            {
                PendingTradeProposals.Remove(tradeId);
                try { await _connection!.InvokeAsync("DeclineTrade", tradeId); } catch { }
            }
        }

        public static async Task AddTradeItemAsync(string itemId, int quantity)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("AddTradeItem", itemId, quantity); } catch { }
        }

        public static async Task RemoveTradeItemAsync(string itemId)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("RemoveTradeItem", itemId); } catch { }
        }

        public static async Task SetTradeGoldAsync(long amount)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("SetTradeGold", amount); } catch { }
        }

        public static async Task ConfirmTradeAsync()
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("ConfirmTrade"); } catch { }
        }

        public static async Task CancelTradeAsync()
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("CancelTrade"); } catch { }
        }

        // ── Hub combat / world actions ────────────────────────────────────────────
        public static async Task<StartCombatResult?> StartCombatAsync(int roomId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<StartCombatResult>("StartCombat", roomId); } catch { }
            return null;
        }

        public static async Task<CombatTurnResult?> CharacterAttackAsync()
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<CombatTurnResult>("CharacterAttack"); } catch { }
            return null;
        }

        public static async Task<CombatTurnResult?> CharacterCastSkillAsync(string skillId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<CombatTurnResult>("CharacterCastSkill", skillId); } catch { }
            return null;
        }

        // soloOnly mirrors the WPF client's StartFight() (true — plain "fight", no party pull-in)
        // vs StartGroupFightLocal() (false — explicit "group fight", pulls in the whole party).
        public static async Task<StartGroupCombatResult?> StartGroupCombatAsync(int roomId, bool soloOnly = false)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<StartGroupCombatResult>("StartGroupCombat", roomId, soloOnly); } catch { }
            return null;
        }

        public static async Task<GroupCombatSnapshot?> GroupCharacterAttackAsync(int targetMonsterIndex)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<GroupCombatSnapshot>("GroupCharacterAttack", targetMonsterIndex); } catch { }
            return null;
        }

        public static async Task<GroupCombatSnapshot?> GroupCharacterCastSkillAsync(string skillId, int targetIndex)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<GroupCombatSnapshot>("GroupCharacterCastSkill", skillId, targetIndex); } catch { }
            return null;
        }

        public static async Task<GatherActionResult?> GatherAsync(int roomId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<GatherActionResult>("Gather", roomId); } catch { }
            return null;
        }

        public static async Task<CraftActionResult?> CraftAsync(string npcId, string recipeId, int quantity)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<CraftActionResult>("Craft", npcId, recipeId, quantity); } catch { }
            return null;
        }

        public static async Task<UpgradeActionResult?> UpgradeAsync(string npcId, string itemId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<UpgradeActionResult>("Upgrade", npcId, itemId); } catch { }
            return null;
        }

        // ── NPC shop ─────────────────────────────────────────────────────────────
        public static async Task<NpcShopBuyResult?> BuyFromNpcShopAsync(string npcId, string itemId, int quantity)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<NpcShopBuyResult>("BuyFromNpcShop", npcId, itemId, quantity); } catch { }
            return null;
        }

        public static async Task<NpcSellResult?> SellItemToNpcAsync(string itemId, int quantity)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<NpcSellResult>("SellItemToNpc", itemId, quantity); } catch { }
            return null;
        }

        // ── Stats / equipment / skills ──────────────────────────────────────────
        public static async Task SyncStatAllocationAsync(int strengthAdded, int dexterityAdded, int enduranceAdded,
            int intelligenceAdded, int spiritAdded, int unusedPoints)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("SyncStatAllocation", strengthAdded, dexterityAdded, enduranceAdded, intelligenceAdded, spiritAdded, unusedPoints); } catch { }
        }

        public static async Task<EquipItemResult?> EquipItemAsync(string itemId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<EquipItemResult>("EquipItem", itemId); } catch { }
            return null;
        }

        public static async Task<bool> UnequipItemAsync(string slotType)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("UnequipItem", slotType); } catch { }
            return false;
        }

        public static async Task<bool> UseItemAsync(string itemId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("UseItem", itemId); } catch { }
            return false;
        }

        public static async Task<bool> SlotSkillAsync(string source, string skillId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("SlotSkill", source, skillId); } catch { }
            return false;
        }

        public static async Task<bool> UnslotSkillAsync(string source, string skillId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("UnslotSkill", source, skillId); } catch { }
            return false;
        }

        public static async Task<bool> ReorderSkillSlotAsync(int fromIndex, int toIndex)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("ReorderSkillSlot", fromIndex, toIndex); } catch { }
            return false;
        }

        public static async Task<bool> SpendSkillPointAsync(string skillId, string upgradeId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("SpendSkillPoint", skillId, upgradeId); } catch { }
            return false;
        }

        public static async Task<bool> RespecSkillAsync(string skillId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("RespecSkill", skillId); } catch { }
            return false;
        }

        // ── Quest / job / class / heal / session ────────────────────────────────
        public static async Task<bool> QuestActionAsync(string questId, bool isReturn)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("QuestAction", questId, isReturn); } catch { }
            return false;
        }

        public static async Task<bool> ToggleJobAsync(string? jobId)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("ToggleJob", jobId); } catch { }
            return false;
        }

        public static async Task<bool> ChangeClassAsync(string cls)
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("ChangeClass", cls); } catch { }
            return false;
        }

        public static async Task<HealActionResult?> HealAsync()
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<HealActionResult>("Heal"); } catch { }
            return null;
        }

        public static async Task<bool> SaveSessionAsync()
        {
            if (IsConnected)
                try { return await _connection!.InvokeAsync<bool>("SaveSession"); } catch { }
            return false;
        }

        public static async Task AbandonCombatAsync()
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("AbandonCombat"); } catch { }
        }

        // ── Guild ────────────────────────────────────────────────────────────────
        public static async Task GuildSendInviteAsync(string targetName, bool asRookie)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildSendInvite", targetName, asRookie); } catch { }
        }

        public static async Task GuildAcceptInviteAsync(int inviteId)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildAcceptInvite", inviteId); } catch { }
        }

        public static async Task GuildDeclineInviteAsync(int inviteId)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildDeclineInvite", inviteId); } catch { }
        }

        public static async Task GuildLeaveAsync()
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildLeave"); } catch { }
        }

        public static async Task GuildKickAsync(string targetName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildKick", targetName); } catch { }
        }

        public static async Task GuildPromoteAsync(string targetName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildPromote", targetName); } catch { }
        }

        public static async Task GuildDemoteAsync(string targetName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildDemote", targetName); } catch { }
        }

        public static async Task GuildTransferLeadershipAsync(string targetName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildTransferLeadership", targetName); } catch { }
        }

        public static async Task GuildDisbandAsync()
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildDisband"); } catch { }
        }

        public static async Task GuildHireRookieAsync(string rookieName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildHireRookie", rookieName); } catch { }
        }

        public static async Task GuildPromoteRookieAsync(string rookieName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildPromoteRookie", rookieName); } catch { }
        }

        public static async Task GuildFireRookieAsync(string rookieName)
        {
            if (IsConnected)
                try { await _connection!.InvokeAsync("GuildFireRookie", rookieName); } catch { }
        }

        // ── REST guild property/base ─────────────────────────────────────────────
        // Mirrors Myria.Wpf/Services/ServerApiService's guild property partial — property/base
        // is REST-only (no hub methods), unlike the rest of the guild feature set above.
        public record GuildRoomInfo(int Id, int RoomId, string Name, string Description, string RoomType, string MinRankRequired, bool IsBuilt, DateTime? BuiltAt);
        public record GuildPropertyInfo(int Id, string Type, int? CityRoomId, int? BaseAnchorRoomId, long? PricePaid, DateTime AcquiredAt, GuildRoomInfo[] Rooms);

        public static async Task<GuildPropertyInfo?> GetGuildPropertyAsync()
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.GetAsync($"{BaseUrl}/api/guilds/property?characterName={Uri.EscapeDataString(me)}");
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<GuildPropertyInfo>(_jsonOpts);
            }
            catch { return null; }
        }

        public static async Task<(bool Success, string Error)> PurchaseGuildHouseAsync(int cityRoomId)
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.PostAsJsonAsync($"{BaseUrl}/api/guilds/property/house",
                    new { CharacterName = me, CityRoomId = cityRoomId });
                if (resp.IsSuccessStatusCode) return (true, "");
                return (false, await ExtractErrorAsync(resp));
            }
            catch (Exception ex) { return (false, $"Could not reach server: {ex.Message}"); }
        }

        public static async Task<(bool Success, string Error)> EstablishGuildBaseAsync(int anchorRoomId)
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.PostAsJsonAsync($"{BaseUrl}/api/guilds/property/base",
                    new { CharacterName = me, AnchorRoomId = anchorRoomId });
                if (resp.IsSuccessStatusCode) return (true, "");
                return (false, await ExtractErrorAsync(resp));
            }
            catch (Exception ex) { return (false, $"Could not reach server: {ex.Message}"); }
        }

        // ── REST character operations ─────────────────────────────────────────────
        public static async Task<List<string>> GetCharacterNamesAsync()
        {
            try
            {
                var resp = await _http.GetAsync($"{BaseUrl}/api/characters");
                if (!resp.IsSuccessStatusCode) return new();
                return await resp.Content.ReadFromJsonAsync<List<string>>(_jsonOpts) ?? new();
            }
            catch { return new(); }
        }

        public static async Task<bool> SaveCharacterAsync(Character Character)
        {
            try
            {
                Character.CurrentRoomId = Character.CurrentRoom?.Id ?? Character.CurrentRoomId;

                var req = new CharacterSaveDto
                {
                    Name                    = Character.Name,
                    Level                   = Character.Level,
                    Experience              = Character.Experience,
                    ExpForNextLvl           = Character.ExpForNextLvl,
                    PotionTierAvailable     = Character.PotionTierAvailable,
                    Class                   = Character.Class,
                    Race                    = Character.Race,
                    RaceSelected            = Character.RaceSelected,
                    CurrentRoomId           = Character.CurrentRoomId,
                    LastHealerRoomId        = Character.LastHealerRoomId,
                    CurrentHealth           = Character.CurrentHealth,
                    CurrentMana             = Character.CurrentMana,
                    StatStrength            = Character.Stats.Strength,
                    StatDexterity           = Character.Stats.Dexterity,
                    StatEndurance           = Character.Stats.Endurance,
                    StatIntelligence        = Character.Stats.Intelligence,
                    StatSpirit              = Character.Stats.Spirit,
                    StatStrengthBonus       = Character.Stats.StrengthBonus,
                    StatDexterityBonus      = Character.Stats.DexterityBonus,
                    StatEnduranceBonus      = Character.Stats.EnduranceBonus,
                    StatIntelligenceBonus   = Character.Stats.IntelligenceBonus,
                    StatSpiritBonus         = Character.Stats.SpiritBonus,
                    StatUnusedPoints        = Character.Stats.UnusedPoints,
                    StatBaseHealth          = Character.Stats.BaseHealth,
                    StatBaseMana            = Character.Stats.BaseMana,
                    WeaponItemId            = Character.WeaponSlot?.Id,
                    ArmorItemId             = Character.ArmorSlot?.Id,
                    AccessoryItemId         = Character.AccessorySlot?.Id,
                    MoneyBronze             = Character.Money.Balance.BronzeTotal,
                    MoneyCapacity           = Character.Money.Capacity,
                    InventoryPages          = Character.Inventory.Pages,
                    LastClassPenaltyApplied = Character.LastClassPenaltyApplied,
                    LastClassChanged        = Character.LastClassChanged,
                    ActiveJobId             = Character.ActiveJobId,
                    LastJobChanged          = Character.LastJobChanged,

                    InventoryItems = Character.Inventory.Items
                        .Select((item, idx) => new InventoryItemDto { ItemId = item.Id, StackSize = item.StackSize, SlotIndex = idx })
                        .ToList(),

                    SkillIds = Character.Skills.Select(s => s.Id).ToList(),

                    ActiveQuests = Character.ActiveQuests.Select(q => new ActiveQuestDto
                    {
                        QuestId          = q.Id,
                        Status           = (int)q.Status,
                        KillProgressJson = JsonSerializer.Serialize(
                            q.KillProgress.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)),
                        ItemProgressJson = JsonSerializer.Serialize(q.ItemProgress)
                    }).ToList(),

                    CompletedQuestIds = Character.CompletedQuests.Select(q => q.Id).ToList(),

                    RepeatableQuests = Character.RepeatableQuestRecords.Select(kv => new RepeatableQuestDto
                    {
                        QuestId            = kv.Key,
                        TimesCompleted     = kv.Value.TimesCompleted,
                        CompletionsToday   = kv.Value.CompletionsToday,
                        LastCompletionDate = kv.Value.LastCompletionDate
                    }).ToList(),

                    Jobs = Character.Jobs.Select(j => new JobDto
                    {
                        JobId            = j.JobId,
                        SkillXp          = j.SkillXp,
                        KnowledgeXp      = j.KnowledgeXp,
                        FameXp           = j.FameXp,
                        LastFameTickDay  = j.LastFameTickDay,
                        LastSkillUsedDay = j.LastSkillUsedDay
                    }).ToList(),

                    SkillSlots = Character.SkillSlots.Select((slot, idx) => new SkillSlotDto
                    {
                        SlotIndex = idx,
                        Source    = (int)slot.Source,
                        SkillId   = slot.SkillId
                    }).ToList(),

                    SkillProgress = Character.SkillProgress.Select(sp => new SkillProgressDto
                    {
                        SkillId             = sp.SkillId,
                        UsageCount          = sp.UsageCount,
                        Level               = sp.Level,
                        UnspentPoints       = sp.UnspentPoints,
                        PurchasedUpgradeIds = sp.PurchasedUpgradeIds.ToList()
                    }).ToList(),

                    KnownRunes = Character.KnownRunes.Select(r => new KnownRuneDto
                    {
                        InstanceId   = r.Id,
                        BaseRuneId   = r.BaseRuneId,
                        AddedWordIds = r.AddedWordIds.ToList()
                    }).ToList(),

                    RuneDictionary = Character.RuneDictionary.Select(e => new RuneDictEntryDto
                    {
                        WordId              = e.WordId,
                        CharacterLabel      = e.CharacterLabel,
                        IsOfficiallyLearned = e.IsOfficiallyLearned
                    }).ToList(),

                    RoomGatheringStatus = Character.RoomGatheringStatus.Select(kv => new RoomGatheringDto
                    {
                        RoomId         = kv.Key,
                        LastGatheredAt = kv.Value
                    }).ToList(),

                    ClassXp = Character.ClassXp.Select(kv => new ClassXpDto
                    {
                        Class = kv.Key,
                        Xp    = kv.Value
                    }).ToList()
                };

                var resp = await _http.PostAsJsonAsync($"{BaseUrl}/api/characters", req, _jsonOpts);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<Character?> LoadCharacterAsync(string name)
        {
            try
            {
                var resp = await _http.GetAsync(
                    $"{BaseUrl}/api/characters/{Uri.EscapeDataString(name)}");
                if (!resp.IsSuccessStatusCode) return null;

                var dto = await resp.Content.ReadFromJsonAsync<CharacterLoadDto>(_jsonOpts);
                if (dto is null) return null;

                // ── Build Stats ───────────────────────────────────────────────────
                var stats = new Myria.Lib.Core.Entities.Stats
                {
                    Strength          = dto.StatStrength,
                    Dexterity         = dto.StatDexterity,
                    Endurance         = dto.StatEndurance,
                    Intelligence      = dto.StatIntelligence,
                    Spirit            = dto.StatSpirit,
                    StrengthBonus     = dto.StatStrengthBonus,
                    DexterityBonus    = dto.StatDexterityBonus,
                    EnduranceBonus    = dto.StatEnduranceBonus,
                    IntelligenceBonus = dto.StatIntelligenceBonus,
                    SpiritBonus       = dto.StatSpiritBonus,
                    UnusedPoints      = dto.StatUnusedPoints,
                    BaseHealth        = dto.StatBaseHealth,
                    BaseMana          = dto.StatBaseMana
                };

                // ── Construct Character ──────────────────────────────────────────────
                var Character = new Character(dto.Name, stats)
                {
                    Level                   = dto.Level,
                    Experience              = dto.Experience,
                    ExpForNextLvl           = dto.ExpForNextLvl,
                    PotionTierAvailable     = dto.PotionTierAvailable,
                    Class                   = dto.Class,
                    Race                    = dto.Race,
                    RaceSelected            = dto.RaceSelected,
                    CurrentRoomId           = dto.CurrentRoomId,
                    LastHealerRoomId        = dto.LastHealerRoomId,
                    CurrentHealth           = dto.CurrentHealth,
                    CurrentMana             = dto.CurrentMana,
                    LastClassPenaltyApplied = dto.LastClassPenaltyApplied,
                    LastClassChanged        = dto.LastClassChanged,
                    ActiveJobId             = dto.ActiveJobId,
                    LastJobChanged          = dto.LastJobChanged
                };

                Character.CurrentRoom = RoomService.AllRooms.FirstOrDefault(r => r.Id == dto.CurrentRoomId);

                // ── Money ─────────────────────────────────────────────────────────
                Character.Money = new MoneyBag
                {
                    Balance  = new Myria.Lib.Core.Entities.Characters.Money(dto.MoneyBronze),
                    Capacity = dto.MoneyCapacity
                };

                // ── Inventory ─────────────────────────────────────────────────────
                Character.Inventory.Pages = dto.InventoryPages;
                Character.Inventory.Items.Clear();
                foreach (var ii in dto.InventoryItems.OrderBy(x => x.SlotIndex))
                {
                    var item = ItemFactory.CreateItem(ii.ItemId, ii.StackSize);
                    if (item is not null)
                        Character.Inventory.Items.Add(item);
                }

                // ── Equipment ─────────────────────────────────────────────────────
                if (!string.IsNullOrEmpty(dto.WeaponItemId))
                    Character.WeaponSlot = ItemFactory.CreateItem(dto.WeaponItemId) as Myria.Lib.Core.Entities.Items.EquipmentItem;
                if (!string.IsNullOrEmpty(dto.ArmorItemId))
                    Character.ArmorSlot = ItemFactory.CreateItem(dto.ArmorItemId) as Myria.Lib.Core.Entities.Items.EquipmentItem;
                if (!string.IsNullOrEmpty(dto.AccessoryItemId))
                    Character.AccessorySlot = ItemFactory.CreateItem(dto.AccessoryItemId) as Myria.Lib.Core.Entities.Items.EquipmentItem;

                // ── Active Quests ─────────────────────────────────────────────────
                Character.ActiveQuests.Clear();
                foreach (var aq in dto.ActiveQuests)
                {
                    var template = QuestManager.GetQuestById(aq.QuestId);
                    if (template is null) continue;
                    var quest = template.Clone();
                    quest.Status = (QuestStatus)aq.Status;
                    try
                    {
                        quest.KillProgress = (JsonSerializer.Deserialize<Dictionary<string, int>>(aq.KillProgressJson) ?? new())
                            .ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
                    }
                    catch { quest.KillProgress = new(); }
                    try
                    {
                        quest.ItemProgress = JsonSerializer.Deserialize<Dictionary<string, int>>(aq.ItemProgressJson) ?? new();
                    }
                    catch { quest.ItemProgress = new(); }
                    Character.ActiveQuests.Add(quest);
                }

                // ── Completed Quests ──────────────────────────────────────────────
                Character.CompletedQuests.Clear();
                foreach (var qId in dto.CompletedQuestIds)
                {
                    var template = QuestManager.GetQuestById(qId);
                    if (template is not null)
                        Character.CompletedQuests.Add(template.Clone());
                }

                // ── Repeatable Quest Records ──────────────────────────────────────
                Character.RepeatableQuestRecords.Clear();
                foreach (var rq in dto.RepeatableQuests)
                    Character.RepeatableQuestRecords[rq.QuestId] = new RepeatRecord
                    {
                        TimesCompleted     = rq.TimesCompleted,
                        CompletionsToday   = rq.CompletionsToday,
                        LastCompletionDate = rq.LastCompletionDate
                    };

                // ── Jobs ──────────────────────────────────────────────────────────
                Character.Jobs.Clear();
                foreach (var j in dto.Jobs)
                    Character.Jobs.Add(new CharacterJob
                    {
                        JobId            = j.JobId,
                        SkillXp          = j.SkillXp,
                        KnowledgeXp      = j.KnowledgeXp,
                        FameXp           = j.FameXp,
                        LastFameTickDay  = j.LastFameTickDay,
                        LastSkillUsedDay = j.LastSkillUsedDay
                    });

                // ── Skill Slots ───────────────────────────────────────────────────
                Character.SkillSlots.Clear();
                foreach (var slot in dto.SkillSlots.OrderBy(s => s.SlotIndex))
                    Character.SkillSlots.Add(new SkillSlot
                    {
                        Source  = (SlottedSkillSource)slot.Source,
                        SkillId = slot.SkillId
                    });

                // ── Skill Progress ───────────────────────────────────────────────────
                Character.SkillProgress.Clear();
                foreach (var sp in dto.SkillProgress)
                    Character.SkillProgress.Add(new Myria.Lib.Core.Entities.Skills.SkillProgress
                    {
                        SkillId = sp.SkillId,
                        UsageCount = sp.UsageCount,
                        Level = sp.Level,
                        UnspentPoints = sp.UnspentPoints,
                        PurchasedUpgradeIds = sp.PurchasedUpgradeIds.ToList()
                    });

                // ── Known Runes ───────────────────────────────────────────────────
                Character.KnownRunes.Clear();
                foreach (var r in dto.KnownRunes)
                    Character.KnownRunes.Add(new CompositeRune
                    {
                        Id           = r.InstanceId,
                        BaseRuneId   = r.BaseRuneId,
                        AddedWordIds = r.AddedWordIds.ToList()
                    });

                // ── Rune Dictionary ───────────────────────────────────────────────
                Character.RuneDictionary.Clear();
                foreach (var e in dto.RuneDictionary)
                    Character.RuneDictionary.Add(new CharacterRuneWordEntry
                    {
                        WordId              = e.WordId,
                        CharacterLabel      = e.CharacterLabel,
                        IsOfficiallyLearned = e.IsOfficiallyLearned
                    });

                // ── Room Gathering Status ─────────────────────────────────────────
                Character.RoomGatheringStatus.Clear();
                foreach (var rg in dto.RoomGatheringStatus)
                    Character.RoomGatheringStatus[rg.RoomId] = rg.LastGatheredAt;

                // ── Class XP ──────────────────────────────────────────────────────
                Character.ClassXp.Clear();
                foreach (var cx in dto.ClassXp)
                    Character.ClassXp[cx.Class] = cx.Xp;

                // ── Post-load resolution ──────────────────────────────────────────
                Character.Inventory.Items.RemoveAll(i => i is null);
                Character.RecalculateUnusedPoints();
                Character.ValidateQuestStatuses();
                SkillFactory.UpdateSkills(Character);
                BaseRuneService.ResolveRunes(Character);
                SkillSlotService.ResolveSlots(Character);
                SkillSlotService.MigrateIfEmpty(Character);
                foreach (var sp in Character.SkillProgress.ToList())
                    SkillLevelingService.RecalculateLevelAndPoints(Character, sp.SkillId);

                return Character;
            }
            catch { return null; }
        }

        // ── REST friend operations ────────────────────────────────────────────────
        public static async Task<List<FriendInfo>> GetFriendsAsync()
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.GetAsync($"{BaseUrl}/api/friends?characterName={Uri.EscapeDataString(me)}");
                if (!resp.IsSuccessStatusCode) return new();
                return await resp.Content.ReadFromJsonAsync<List<FriendInfo>>(_jsonOpts) ?? new();
            }
            catch { return new(); }
        }

        public static async Task<List<FriendRequestInfo>> GetFriendRequestsAsync()
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.GetAsync($"{BaseUrl}/api/friends/requests?characterName={Uri.EscapeDataString(me)}");
                if (!resp.IsSuccessStatusCode) return new();
                return await resp.Content.ReadFromJsonAsync<List<FriendRequestInfo>>(_jsonOpts) ?? new();
            }
            catch { return new(); }
        }

        public static async Task<bool> SendFriendRequestAsync(string characterName)
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.PostAsJsonAsync($"{BaseUrl}/api/friends/request",
                    new { characterName, fromCharacterName = me }, _jsonOpts);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> AcceptFriendRequestAsync(int friendshipId)
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.PostAsync(
                    $"{BaseUrl}/api/friends/{friendshipId}/accept?characterName={Uri.EscapeDataString(me)}", null);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> RemoveFriendAsync(int friendshipId)
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.DeleteAsync(
                    $"{BaseUrl}/api/friends/{friendshipId}?characterName={Uri.EscapeDataString(me)}");
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ── REST block operations ─────────────────────────────────────────────────
        public static async Task<List<BlockInfo>> GetBlocksAsync()
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.GetAsync($"{BaseUrl}/api/blocks?characterName={Uri.EscapeDataString(me)}");
                if (!resp.IsSuccessStatusCode) return new();
                return await resp.Content.ReadFromJsonAsync<List<BlockInfo>>(_jsonOpts) ?? new();
            }
            catch { return new(); }
        }

        public static async Task<bool> BlockCharacterAsync(string characterName)
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.PostAsJsonAsync($"{BaseUrl}/api/blocks",
                    new { characterName, fromCharacterName = me }, _jsonOpts);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> UnblockAsync(int blockId)
        {
            try
            {
                var me = CurrentCharacterName ?? "";
                var resp = await _http.DeleteAsync(
                    $"{BaseUrl}/api/blocks/{blockId}?characterName={Uri.EscapeDataString(me)}");
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private record AuthResponse(string Token, string Username, DateTime ExpiresAt);

        // ── Character save/load DTOs — must mirror MyriaServer's SaveCharacterRequest/
        // CharacterLoadResponse (flat shape, no nested "blob" field) exactly. ──────────
        private class CharacterSaveDto
        {
            public string   Name                    { get; set; } = "";
            public int      Level                   { get; set; }
            public long     Experience              { get; set; }
            public long     ExpForNextLvl           { get; set; }
            public int      PotionTierAvailable     { get; set; }
            public string   Class                   { get; set; } = "Fighter";
            public string   Race                    { get; set; } = "Myralu";
            public bool     RaceSelected            { get; set; }
            public int      CurrentRoomId           { get; set; }
            public int?     LastHealerRoomId        { get; set; }
            public int      CurrentHealth           { get; set; }
            public int      CurrentMana             { get; set; }
            public int      StatStrength            { get; set; }
            public int      StatDexterity           { get; set; }
            public int      StatEndurance           { get; set; }
            public int      StatIntelligence        { get; set; }
            public int      StatSpirit              { get; set; }
            public int      StatStrengthBonus       { get; set; }
            public int      StatDexterityBonus      { get; set; }
            public int      StatEnduranceBonus      { get; set; }
            public int      StatIntelligenceBonus   { get; set; }
            public int      StatSpiritBonus         { get; set; }
            public int      StatUnusedPoints        { get; set; }
            public int      StatBaseHealth          { get; set; }
            public int      StatBaseMana            { get; set; }
            public string?  WeaponItemId            { get; set; }
            public string?  ArmorItemId             { get; set; }
            public string?  AccessoryItemId         { get; set; }
            public long     MoneyBronze             { get; set; }
            public long     MoneyCapacity           { get; set; }
            public int      InventoryPages          { get; set; }
            public DateTime LastClassPenaltyApplied { get; set; }
            public DateTime LastClassChanged        { get; set; }
            public string?  ActiveJobId             { get; set; }
            public DateTime LastJobChanged          { get; set; }

            public List<InventoryItemDto>   InventoryItems      { get; set; } = new();
            public List<string>             SkillIds            { get; set; } = new();
            public List<ActiveQuestDto>     ActiveQuests        { get; set; } = new();
            public List<string>             CompletedQuestIds   { get; set; } = new();
            public List<RepeatableQuestDto> RepeatableQuests    { get; set; } = new();
            public List<JobDto>             Jobs                { get; set; } = new();
            public List<SkillSlotDto>       SkillSlots          { get; set; } = new();
            public List<SkillProgressDto>   SkillProgress       { get; set; } = new();
            public List<KnownRuneDto>       KnownRunes          { get; set; } = new();
            public List<RuneDictEntryDto>   RuneDictionary      { get; set; } = new();
            public List<RoomGatheringDto>   RoomGatheringStatus { get; set; } = new();
            public List<ClassXpDto>         ClassXp             { get; set; } = new();
        }

        // Load response (received from server) — same shape as save request
        private class CharacterLoadDto : CharacterSaveDto { }

        private class InventoryItemDto
        {
            public string ItemId    { get; set; } = "";
            public int    StackSize { get; set; } = 1;
            public int    SlotIndex { get; set; }
        }

        private class ActiveQuestDto
        {
            public string QuestId          { get; set; } = "";
            public int    Status           { get; set; }
            public string KillProgressJson { get; set; } = "{}";
            public string ItemProgressJson { get; set; } = "{}";
        }

        private class RepeatableQuestDto
        {
            public string    QuestId            { get; set; } = "";
            public int       TimesCompleted     { get; set; }
            public int       CompletionsToday   { get; set; }
            public DateTime? LastCompletionDate { get; set; }
        }

        private class JobDto
        {
            public string JobId            { get; set; } = "";
            public long   SkillXp          { get; set; }
            public long   KnowledgeXp      { get; set; }
            public long   FameXp           { get; set; }
            public int    LastFameTickDay  { get; set; }
            public int    LastSkillUsedDay { get; set; }
        }

        private class SkillSlotDto
        {
            public int    SlotIndex { get; set; }
            public int    Source    { get; set; }
            public string SkillId   { get; set; } = "";
        }

        private class SkillProgressDto
        {
            public string       SkillId             { get; set; } = "";
            public int          UsageCount          { get; set; }
            public int          Level               { get; set; } = 1;
            public int          UnspentPoints       { get; set; }
            public List<string> PurchasedUpgradeIds { get; set; } = new();
        }

        private class KnownRuneDto
        {
            public string       InstanceId   { get; set; } = "";
            public string       BaseRuneId   { get; set; } = "";
            public List<string> AddedWordIds { get; set; } = new();
        }

        private class RuneDictEntryDto
        {
            public string  WordId              { get; set; } = "";
            public string? CharacterLabel      { get; set; }
            public bool    IsOfficiallyLearned { get; set; }
        }

        private class RoomGatheringDto
        {
            public int      RoomId         { get; set; }
            public DateTime LastGatheredAt { get; set; }
        }

        private class ClassXpDto
        {
            public string Class { get; set; } = "Fighter";
            public long   Xp    { get; set; }
        }

        public record FriendInfo(int FriendshipId, string Username, bool IsOnline);
        public record FriendRequestInfo(int FriendshipId, string FromUsername);
        public record BlockInfo(int BlockId, string Username);
        public record TradeItem(string ItemId, string Name, int Quantity);
        public record TradeSnapshot(
            List<TradeItem> MyItems,
            List<TradeItem> TheirItems,
            long MyGold,
            long TheirGold,
            bool ImReady,
            bool TheyAreReady);
        public record ShopListing(string ListingId, string ItemId, string Name, int Quantity, long Price);
    }
}
