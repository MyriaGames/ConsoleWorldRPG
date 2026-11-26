namespace Myria.ConsoleApp.Commands
{
    /// <summary>
    /// PvP (v0.3) in the console: "arena" lists who can be challenged in a PvP room,
    /// "challenge [party] &lt;n|name&gt;" starts a fight, and while one is running "attack [n]",
    /// "cast &lt;skill&gt; [n]", "use &lt;item&gt;", "flee" and "arena" (status) act in it. Event-driven
    /// like group combat: the server's PvpStarted/Updated/Finished broadcasts drive the display.
    /// Must be first in the command chain, so a fight's attack/cast/use win over the normal ones.
    /// </summary>
    public static class PvpCommands
    {
        private static readonly object Sync = new();
        private static PvpSnapshot? _snap;          // current fight, null when not in one
        private static int _mySide;                 // 1 = SideA, 2 = SideB
        private static Character? _player;
        private static Item? _pendingItem;          // item sent with "use", mirrored once the server confirms
        private static Timer? _stallTimer;
        private static List<PvpTargetInfo> _lastTargets = new();

        /// <summary>How often a waiting console asks the server to skip a stalled turn (the server
        /// only does so past PvpEncounter.TurnTimeout).</summary>
        private static readonly TimeSpan StallPollInterval = TimeSpan.FromSeconds(5);

        private static bool InFight { get { lock (Sync) return _snap != null; } }

        public static void Attach(Character player)
        {
            _player = player;
            // Idempotent: never double-subscribe if a session is started again.
            ConsoleHubClient.PvpStarted  -= OnPvpStarted;
            ConsoleHubClient.PvpUpdated  -= OnPvpUpdated;
            ConsoleHubClient.PvpFinished -= OnPvpFinished;
            ConsoleHubClient.PvpStarted  += OnPvpStarted;
            ConsoleHubClient.PvpUpdated  += OnPvpUpdated;
            ConsoleHubClient.PvpFinished += OnPvpFinished;
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public static bool Handle(string input, Character character)
        {
            if (InFight) return HandleInFight(input, character);

            if (input is "arena" or "pvp")
            {
                _ = ShowArenaAsync(character);
                return true;
            }
            if (input == "challenge" || input.StartsWith("challenge "))
            {
                var arg = input.Length > 10 ? input[10..].Trim() : "";
                bool party = arg.StartsWith("party ", StringComparison.OrdinalIgnoreCase);
                if (party) arg = arg[6..].Trim();
                _ = ChallengeAsync(arg, party);
                return true;
            }
            return false;
        }

        private static bool HandleInFight(string input, Character character)
        {
            if (input is "arena" or "pvp" or "status") { PrintState(); return true; }
            if (input == "flee") { _ = ConsoleHubClient.PvpFleeAsync(); return true; }

            if (input == "attack" || input.StartsWith("attack "))
            {
                int idx = input.Length > 7 && int.TryParse(input[7..].Trim(), out var n) ? n : FirstLivingOpponent();
                _ = SendAsync(ConsoleHubClient.PvpAttackAsync(idx));
                return true;
            }

            if (input.StartsWith("cast "))
            {
                // cast <skill name or id> [target number] - the number is an opponent for
                // enemy-targeted skills, an ally (your side) for SingleAlly skills.
                var parts = input[5..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                int? target = parts.Count > 1 && int.TryParse(parts[^1], out var t) ? t : null;
                if (target != null) parts.RemoveAt(parts.Count - 1);
                string skillArg = string.Join(' ', parts);
                var skill = character.Skills.FirstOrDefault(s =>
                    s.Id.Equals(skillArg, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Equals(skillArg, StringComparison.OrdinalIgnoreCase));
                if (skill == null) { Printer.PrintError(Localization.T("console.pvp.unknown_skill")); return true; }

                int idx = target ?? (skill.Target == SkillTarget.SingleAlly ? MyIndexOnMySide() : FirstLivingOpponent());
                _ = SendAsync(ConsoleHubClient.PvpCastSkillAsync(skill.Id, idx));
                return true;
            }

            if (input.StartsWith("use "))
            {
                var item = Myria.Lib.Core.Utils.InventoryUtils.ResolveInventoryItem(input[4..].Trim(), character);
                if (item is not ConsumableItem) { Printer.PrintError(Localization.T("console.combat.invalid_item")); return true; }
                lock (Sync) _pendingItem = item;
                _ = SendAsync(ConsoleHubClient.PvpUseItemAsync(item.Id));
                return true;
            }

            return false; // chat etc. still work mid-fight
        }

        private static async Task SendAsync(Task<PvpSnapshot?> call)
        {
            var result = await call;
            if (result == null) { Printer.PrintError(Localization.T("console.pvp.invalid_action")); return; }
            // A successful turn arrives as the PvpUpdated/Finished broadcast. A rejection with a
            // reason (no mana, cooldown) comes back only to us - still our turn - so print it here.
            if (!result.Finished && IsMe(result.CurrentTurnCharacterName))
                foreach (var msg in result.LogEntries) PrintLog(msg);
        }

        private static async Task ShowArenaAsync(Character character)
        {
            if (!ConsoleHubClient.IsConnected) { Console.WriteLine(Localization.T("console.combat.must_be_online")); return; }
            if (character.CurrentRoom is not { IsPvpRoom: true }) { Console.WriteLine(Localization.T("console.pvp.not_here")); return; }

            var targets = await ConsoleHubClient.GetPvpTargetsAsync();
            lock (Sync) _lastTargets = targets;

            Console.WriteLine(Localization.T("console.pvp.header", character.PvpRating, character.PvpCurrency, character.PvpWins, character.PvpLosses));
            if (targets.Count == 0) { Console.WriteLine(Localization.T("console.pvp.none")); return; }
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                string party = t.PartySizeHere > 1 ? Localization.T("console.pvp.party_suffix", t.PartySizeHere) : "";
                Console.WriteLine(Localization.T("console.pvp.target_line", i + 1, t.Name, t.Level, t.Class, party));
            }
            Console.WriteLine(Localization.T("console.pvp.hint"));
        }

        private static async Task ChallengeAsync(string arg, bool partyFight)
        {
            string? name;
            lock (Sync)
                name = int.TryParse(arg, out var n)
                    ? (n >= 1 && n <= _lastTargets.Count ? _lastTargets[n - 1].Name : null)
                    : (arg.Length > 0 ? arg : null);
            if (name == null) { Printer.PrintError(Localization.T("console.pvp.no_such_target")); return; }

            var result = await ConsoleHubClient.StartPvpFightAsync(name, partyFight);
            if (result?.Success == true) return; // PvpStarted prints the fight for every participant
            string reason = result?.Reason ?? "failed";
            string text = Localization.T("pvp.reason." + reason);
            Printer.PrintError(text.StartsWith("[") ? Localization.T("pvp.reason.failed") : text);
        }

        // ── Server events ─────────────────────────────────────────────────────

        private static void OnPvpStarted(PvpSnapshot snap)
        {
            lock (Sync)
            {
                _snap = snap;
                _mySide = snap.SideA.Any(c => IsMe(c.Name)) ? 1 : 2;
                _pendingItem = null;
                _stallTimer?.Dispose();
                _stallTimer = new Timer(_ =>
                {
                    if (InFight && !IsMyTurn()) _ = ConsoleHubClient.PvpSkipStalledTurnAsync();
                }, null, StallPollInterval, StallPollInterval);
            }
            Console.WriteLine(Localization.T("console.pvp.started"));
            foreach (var msg in snap.LogEntries) PrintLog(msg);
            PrintState();
        }

        private static void OnPvpUpdated(PvpSnapshot snap)
        {
            lock (Sync) { if (_snap == null) return; _snap = snap; }
            foreach (var msg in snap.LogEntries) PrintLog(msg);
            MirrorItemUse(snap);
            PrintTurn();
        }

        private static void OnPvpFinished(PvpSnapshot snap)
        {
            lock (Sync)
            {
                if (_snap == null) return;
                _snap = null;
                _stallTimer?.Dispose();
                _stallTimer = null;
            }
            foreach (var msg in snap.LogEntries) PrintLog(msg);
            MirrorItemUse(snap);

            // The console doesn't apply the server's CharacterUpdated pushes, so take the settled
            // result from here. HP/MP need nothing: the server restores the pre-fight values, which
            // is what the console's local copy still holds (it never applied fight damage).
            var mine = snap.Results?.FirstOrDefault(r => IsMe(r.Name));
            if (mine != null && _player != null)
            {
                _player.PvpRating = mine.NewRating;
                _player.PvpCurrency += mine.CurrencyDelta;
                if (mine.Won) _player.PvpWins++; else _player.PvpLosses++;
                Console.WriteLine(mine.Won
                    ? Localization.T("console.pvp.result_win", mine.NewRating, mine.RatingDelta, mine.CurrencyDelta)
                    : Localization.T("console.pvp.result_loss", mine.NewRating, mine.RatingDelta));
            }
            else
                Console.WriteLine(Localization.T("console.pvp.fight_over"));
        }

        /// <summary>Consumes the "use"d item locally once the server's log shows we used it.</summary>
        private static void MirrorItemUse(PvpSnapshot snap)
        {
            Item? item;
            lock (Sync) { item = _pendingItem; }
            if (item == null || _player == null) return;
            if (snap.LogEntries.Any(e => e.Key == "pg.fight.log.usedItem" && e.Args.FirstOrDefault() == _player.Name))
            {
                _player.Inventory.ConsumeOne(item);
                lock (Sync) _pendingItem = null;
            }
        }

        // ── Display helpers ───────────────────────────────────────────────────

        private static void PrintState()
        {
            PvpSnapshot? snap;
            lock (Sync) snap = _snap;
            if (snap == null) return;
            PrintSide(Localization.T("console.pvp.side_mine"), Mine(snap));
            PrintSide(Localization.T("console.pvp.side_theirs"), Theirs(snap));
            PrintTurn();
        }

        private static void PrintSide(string header, List<GroupCombatantState> side)
        {
            Console.WriteLine(header);
            for (int i = 0; i < side.Count; i++)
            {
                var c = side[i];
                Console.WriteLine(Localization.T("console.pvp.combatant_line", i, c.Name, c.Hp, c.MaxHp,
                    c.IsAlive ? "" : Localization.T("console.pvp.out_tag")));
            }
        }

        private static void PrintTurn()
        {
            PvpSnapshot? snap;
            lock (Sync) snap = _snap;
            if (snap == null) return;
            Console.WriteLine(IsMe(snap.CurrentTurnCharacterName)
                ? Localization.T("console.pvp.your_turn")
                : Localization.T("console.pvp.waiting", snap.CurrentTurnCharacterName));
        }

        private static void PrintLog(CombatLogMessage msg)
        {
            try { Console.WriteLine("  " + Localization.T(msg.Key, CombatCommands.LocalizeLogArgs(msg.Args))); }
            catch { Console.WriteLine("  " + msg.Key); }
        }

        private static bool IsMe(string? name) =>
            _player != null && string.Equals(name, _player.Name, StringComparison.OrdinalIgnoreCase);

        private static bool IsMyTurn() { lock (Sync) return _snap != null && IsMe(_snap.CurrentTurnCharacterName); }

        private static List<GroupCombatantState> Mine(PvpSnapshot s)   => _mySide == 1 ? s.SideA : s.SideB;
        private static List<GroupCombatantState> Theirs(PvpSnapshot s) => _mySide == 1 ? s.SideB : s.SideA;

        private static int FirstLivingOpponent()
        {
            lock (Sync) return _snap == null ? 0 : Math.Max(0, Theirs(_snap).FindIndex(c => c.IsAlive));
        }

        private static int MyIndexOnMySide()
        {
            lock (Sync) return _snap == null ? 0 : Math.Max(0, Mine(_snap).FindIndex(c => IsMe(c.Name)));
        }
    }
}
