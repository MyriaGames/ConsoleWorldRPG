using Myria.ConsoleApp.Commands;
using Myria.ConsoleApp.Systems;

namespace Myria.ConsoleApp
{
    public class Game
    {
        private bool _isRunning = true;
        private Character _player = new Character("Hero", new Stats());
        private CommandRouter _commandRouter;
        public static bool LogOut = false;

        public void Start()
        {
            Myria.Lib.Core.Services.GameService.SessionStarted += _ => DayCycleManager.StartInactivityTimer();
            Myria.Lib.Core.Services.GameService.SessionStarted += p => ClassManager.ApplyDailyPenalty(p);
            Myria.Lib.Core.Services.GameService.SessionStarted += p =>
            {
                // J18: knowledge gather bonus for already-rolled room limits
                int bonus = JobManager.GetGatherKnowledgeBonus(p);
                if (bonus > 0)
                    foreach (var room in RoomService.AllRooms.Where(r => r.GatheringSpots.Count > 0))
                        room.AddGatherBonus(bonus);

                // J11–J14: daily job ticks; J18: re-apply on each new day
                DayCycleManager.DayAdvanced += day =>
                {
                    JobManager.ApplyDailyTicks(p, day);
                    int b = JobManager.GetGatherKnowledgeBonus(p);
                    if (b > 0)
                        foreach (var r in RoomService.AllRooms.Where(r => r.GatheringSpots.Count > 0))
                            r.AddGatherBonus(b);
                };
            };

            // Hub events — subscribed once; fire from background threads while hub is connected
            ConsoleHubClient.ChatMessageReceived += (sender, msg, channel) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  [{channel}] {sender}: {msg}");
                Console.ResetColor();
            };
            ConsoleHubClient.CharacterEntered += name =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"\n  ** {name} entered the room.");
                Console.ResetColor();
            };
            ConsoleHubClient.CharacterLeft += name =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n  ** {name} left the room.");
                Console.ResetColor();
            };
            ConsoleHubClient.CharacterGathering += name =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n  ** {name} is gathering nearby.");
                Console.ResetColor();
            };
            ConsoleHubClient.CharacterCrafting += name =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n  ** {name} is crafting nearby.");
                Console.ResetColor();
            };
            ConsoleHubClient.CharacterUpgrading += name =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n  ** {name} is upgrading gear nearby.");
                Console.ResetColor();
            };
            ConsoleHubClient.CharacterInCombat += name =>
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"\n  ** {name} has engaged in combat nearby.");
                Console.ResetColor();
            };
            ConsoleHubClient.CharacterCombatEnded += name =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n  ** {name} has finished fighting.");
                Console.ResetColor();
            };
            ConsoleHubClient.RoomCharactersReceived += players =>
            {
                if (players.Count == 0) return;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n  ** Also here: {string.Join(", ", players)}");
                Console.ResetColor();
            };
            ConsoleHubClient.PartyInviteReceived += (from, partyId) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  ** {from} invited you to a party.  Type: party accept {partyId}");
                Console.ResetColor();
            };
            ConsoleHubClient.PartyUpdated += (members, leader) =>
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"\n  ** Party: [{string.Join(", ", members)}]  Leader: {leader ?? "(none)"}");
                Console.ResetColor();
            };
            ConsoleHubClient.PartyDisbanded += () =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n  ** Your party has been disbanded.");
                Console.ResetColor();
            };
            ConsoleHubClient.KickedFromParty += () =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n  ** You have been kicked from the party.");
                Console.ResetColor();
            };
            ConsoleHubClient.TradeProposed += (proposerName, tradeId) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n  ** {proposerName} wants to trade with you.  Type: trade accept {tradeId}");
                Console.ResetColor();
            };
            ConsoleHubClient.TradeStarted   += TradeCommands.OnTradeStarted;
            ConsoleHubClient.TradeUpdated   += TradeCommands.OnTradeUpdated;
            ConsoleHubClient.TradeCompleted += TradeCommands.OnTradeCompleted;
            ConsoleHubClient.TradeCancelled += TradeCommands.OnTradeCancelled;
            ConsoleHubClient.GuildMemberJoined += (name, rank) =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  ** {name} joined the guild as {rank}.");
                Console.ResetColor();
            };
            ConsoleHubClient.GuildMemberLeft += (name, kicked) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(kicked
                    ? $"\n  ** {name} was kicked from the guild."
                    : $"\n  ** {name} left the guild.");
                Console.ResetColor();
            };
            ConsoleHubClient.GuildRankChanged += (name, rank) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  ** {name} is now {rank}.");
                Console.ResetColor();
            };
            ConsoleHubClient.GuildLeaderChanged += (oldLeader, newLeader) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  ** {newLeader} is now the guild leader (was {oldLeader}).");
                Console.ResetColor();
            };
            ConsoleHubClient.GuildRookieAdded += name =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  ** {name} was hired as a rookie.");
                Console.ResetColor();
            };
            ConsoleHubClient.GuildDisbanded += () =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n  ** Your guild has been disbanded.");
                Console.ResetColor();
            };
            ConsoleHubClient.GuildLeft += () =>
                Console.WriteLine("\n  ** You left the guild.");
            ConsoleHubClient.GuildKicked += () =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n  ** You have been kicked from the guild.");
                Console.ResetColor();
            };
            ConsoleHubClient.GuildInviteReceived += (inviteId, guildId, guildName, guildTag, asRookie) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                string role = asRookie ? "as a rookie" : "as a member";
                Console.WriteLine($"\n  ** Guild invite from {guildName ?? guildTag ?? $"guild #{guildId}"} ({role}).  Type: guild accept {inviteId}");
                Console.ResetColor();
            };
            ConsoleHubClient.GuildError += msg =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  ** Guild error: {msg}");
                Console.ResetColor();
            };
            ConsoleHubClient.GuildMemberOnline += (name, room) =>
                Console.WriteLine($"\n  ** {name} is now online ({room}).");
            ConsoleHubClient.GuildMemberOffline += name =>
                Console.WriteLine($"\n  ** {name} went offline.");
            ConsoleHubClient.ShopOpened += ownerName =>
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"\n  ** {ownerName} opened a Character shop.  Use: shop visit {ownerName}");
                Console.ResetColor();
            };
            ConsoleHubClient.ShopClosed += ownerName =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n  ** {ownerName}'s shop is now closed.");
                Console.ResetColor();
            };
            ConsoleHubClient.ShopListUpdated += ShopCommands.OnShopListUpdated;
            ConsoleHubClient.ShopSale        += ShopCommands.OnShopSale;
            ConsoleHubClient.ShopBuyResult   += ShopCommands.OnShopBuyResult;
            ConsoleHubClient.GroupCombatStarted  += CombatCommands.OnGroupCombatStarted;
            ConsoleHubClient.GroupCombatUpdated  += CombatCommands.OnGroupCombatUpdated;
            ConsoleHubClient.GroupCombatFinished += CombatCommands.OnGroupCombatFinished;
            PvpCommands.Attach(_player);
            ConsoleHubClient.CharacterRespawned     += (roomId, roomName) =>
            {
                var respawnRoom = Myria.Lib.Core.Services.RoomService.AllRooms.FirstOrDefault(r => r.Id == roomId);
                if (respawnRoom is not null)
                {
                    _player.CurrentRoomId = roomId;
                    _player.CurrentRoom   = respawnRoom;
                }
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"\n  ** You were defeated and awaken in {roomName}.");
                Console.ResetColor();
            };

            if (GameService.InitializeGame(_player))
            {
                Console.WriteLine(Localization.T("app.startmessage"));

                while (_isRunning)
                {
                    LogOut = false;
                    _player = ShowLoginMenu();
                    if (_player == null)
                        _isRunning = false;

                    if (_isRunning)
                    {
                        Myria.Lib.Core.Services.GameService.StartSession(_player);
                        // Awaited (not fire-and-forget) so commands typed right after login never
                        // race the hub connection — IsConnected/CurrentPartyMembers/etc. are
                        // guaranteed accurate by the time the command loop below starts reading input.
                        TryConnectHubAsync(_player).GetAwaiter().GetResult();

                        _commandRouter = new CommandRouter(_player);
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(Localization.T("app.helpinfo"));
                        Console.ResetColor();
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  [Day {DayCycleManager.GameDay} — {DayCycleManager.CurrentTimeSegment}]");
                        Console.ResetColor();
                        _player.CurrentRoom.Describe();
                    }

                    while (_isRunning)
                    {
                        Console.Write("> ");
                        string? input = Console.ReadLine()?.Trim();
                        if (string.IsNullOrEmpty(input)) continue;

                        if (input == "exit")
                        {
                            SaveCharacter(_player);
                            DayCycleManager.StopInactivityTimer();
                            ConsoleHubClient.DisconnectAsync().GetAwaiter().GetResult();
                            _isRunning = false;
                            Console.WriteLine(Localization.T("app.goodbye"));
                            break;
                        }

                        if (LogOut)
                        {
                            DayCycleManager.StopInactivityTimer();
                            ConsoleHubClient.DisconnectAsync().GetAwaiter().GetResult();
                            break;
                        }
                        else if (!_commandRouter.HandleCommand(input))
                            Console.WriteLine(Localization.T("ui_unknown_command"));
                    }
                }
            }
        }

        private static void SaveCharacter(Character Character)
        {
            if (ConsoleHubClient.HasToken)
            {
                // SaveSession persists the server's own authoritative session character (the same
                // one every SlotSkill/UnslotSkill/etc. confirm actually mutates) - it used to be
                // followed by a separate SaveCharacterAsync REST call that saved the client's local
                // copy instead, immediately overwriting this correct save with a potentially-stale
                // one (e.g. if a just-sent fire-and-forget slot-assignment confirm hadn't been
                // reflected in Character's local mirror yet). Since SignalR processes invocations
                // from one connection in order, awaiting SaveSession here also naturally waits for
                // any earlier fire-and-forget hub call on this same connection to land first.
                bool ok = ConsoleHubClient.IsConnected && ConsoleHubClient.SaveSessionAsync().GetAwaiter().GetResult();
                if (!ok)
                {
                    Console.WriteLine("Warning: Server save failed. Saving locally...");
                    CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
                }
            }
            else
            {
                CharacterService.SaveCharacter(LoginManager.UserAccount, Character);
            }
        }

        private static async Task TryConnectHubAsync(Character Character)
        {
            if (!ConsoleHubClient.HasToken)
            {
                // REST login may have been skipped if server was offline during local auth — try once more
                var (ok, error) = await ConsoleHubClient.LoginAsync(
                    LoginManager.UserAccount.Username,
                    LoginManager.UserAccount.Password);
                if (!ok)
                {
                    if (error is not null) Printer.PrintWarning($"Multiplayer connect failed: {error}");
                    return;
                }
            }

            await ConsoleHubClient.ConnectAsync();

            if (ConsoleHubClient.IsConnected)
            {
                // The realm only accepts actions for a character this connection has loaded
                // (LoadCharacter checks ownership); without it every session-based hub call -
                // fights, PvP, skills, trades - was ignored. After an automatic reconnect it has to
                // happen again on the new connection (and rejoins a PvP fight in progress).
                async Task AttachAsync()
                {
                    await ConsoleHubClient.LoadCharacterOnServerAsync(Character.Name);
                    await ConsoleHubClient.SetCharacterNameAsync(Character.Name);
                    await ConsoleHubClient.JoinRoomAsync(Character.CurrentRoomId);
                }
                ConsoleHubClient.ReconnectedHandler = AttachAsync;
                await ConsoleHubClient.LoadCharacterOnServerAsync(Character.Name);
                await ConsoleHubClient.SetCharacterNameAsync(Character.Name);
                await ConsoleHubClient.JoinRoomAsync(Character.CurrentRoomId);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                string realmSuffix = LoginManager.SelectedRealmName is { } realm ? $" — {realm}" : "";
                Console.WriteLine($"  [Online{realmSuffix} — connected to multiplayer server]");
                Console.ResetColor();
            }
        }

        public static Character ShowLoginMenu()
        {
            while (true)
            {
                Console.WriteLine("\n==== " + Localization.T("ui_login_menu") + " ====");
                Console.WriteLine("0. " + Localization.T("ui_exit"));
                Console.WriteLine("1. " + Localization.T("ui_singleplayer"));
                Console.WriteLine("2. " + Localization.T("ui_login"));
                Console.WriteLine("3. " + Localization.T("ui_register"));
                Console.WriteLine("4. " + Localization.T("ui_settings"));
                Console.Write(Localization.T("ui_select_option"));

                string? input = Console.ReadLine()?.Trim();
                switch (input)
                {
                    case "0": return null;
                    case "1":
                        var single = LoginManager.PlaySingleplayer();
                        if (single != null) return single;
                        break;
                    case "2":
                        var user = LoginManager.Login();
                        if (user != null) return user;
                        break;
                    case "3":
                        LoginManager.Register();
                        break;
                    case "4":
                        ShowSettingsMenu();
                        break;
                    default:
                        Console.WriteLine(Localization.T("ui_invalid_option"));
                        break;
                }
            }
        }

        private static void ShowSettingsMenu()
        {
            string currentAddress = string.IsNullOrWhiteSpace(Settings.Current.ServerAddress)
                ? Localization.T("ui_server_address_default")
                : Settings.Current.ServerAddress;

            Console.WriteLine("\n== " + Localization.T("ui_settings") + " ==");
            Console.WriteLine("1. " + Localization.T("ui_language") + " (" + Settings.Current.LanguageSettings.Local + ")");
            Console.WriteLine("2. " + Localization.T("ui_server_address") + " (" + currentAddress + ")");
            Console.WriteLine("0. " + Localization.T("ui_back"));
            Console.Write("> ");
            var choice = Console.ReadLine();

            if (choice == "1")
            {
                Console.WriteLine(Localization.T("ui_select_language") + "1) En  2) De");
                Console.Write("> ");
                var l = Console.ReadLine();
                Settings.Current.LanguageSettings.Local = l switch
                {
                    "2" => GameLanguage.De,
                    _   => GameLanguage.En
                };
                SettingsService.Save();
                Localization.Load(Settings.Current.LanguageSettings.Local);
                Console.WriteLine(Localization.T("msg.language_applied", Settings.Current.LanguageSettings.Local));
            }
            else if (choice == "2")
            {
                // Only the auth server address is configurable here — matches Wpf's scope for
                // this field. Which realm (BaseUrl) to play on is still chosen via the
                // realm-selection menu at login. Takes effect on next login, not mid-session.
                Console.WriteLine(Localization.T("ui_server_address_prompt", currentAddress));
                Console.Write("> ");
                var input = Console.ReadLine()?.Trim() ?? "";
                if (input.Length > 0)
                {
                    Settings.Current.ServerAddress = ConsoleHubClient.NormalizeAddress(input);
                    SettingsService.Save();
                    ConsoleHubClient.AuthBaseUrl = Settings.Current.ServerAddress;
                    Console.WriteLine(Localization.T("msg.server_address_applied", Settings.Current.ServerAddress));
                }
            }
        }

    }

}
