namespace Myria.ConsoleApp.Commands
{
    public static class CombatCommands
    {
        // ── Group combat state ────────────────────────────────────────────────

        private static GroupCombatSnapshot? _groupSnap;
        private static bool _isGroupCombat;

        public static void OnGroupCombatStarted(StartGroupCombatResult result)
        {
            _isGroupCombat = true;
            _groupSnap = new GroupCombatSnapshot(
                result.Characters, result.Monsters, result.CurrentTurnCharacterName, false, false, new());
            Console.WriteLine();
            Console.WriteLine(Localization.T("console.combat.group_started"));
            PrintGroupState(_groupSnap);
        }

        public static void OnGroupCombatUpdated(GroupCombatSnapshot snap)
        {
            _groupSnap = snap;
            foreach (var msg in snap.LogEntries)
                PrintCombatLog(msg);
            Console.Write(Localization.T("console.combat.turn_prefix", snap.CurrentTurnCharacterName));
            Console.WriteLine(Localization.T("console.combat.turn_hint"));
        }

        public static void OnGroupCombatFinished(GroupCombatSnapshot snap)
        {
            _isGroupCombat = false;
            _groupSnap = null;
            foreach (var msg in snap.LogEntries)
                PrintCombatLog(msg);
            if (snap.CharactersWon)
                Console.WriteLine(Localization.T("console.combat.victory"));
            else
                Console.WriteLine(Localization.T("console.combat.defeat"));
        }

        // ── Command handler ───────────────────────────────────────────────────

        public static bool Handle(string input, Character Character)
        {
            // Group fight commands (prefix "attack" / "cast" when in group combat)
            if (_isGroupCombat)
            {
                if (input.StartsWith("attack"))
                {
                    int idx = 0;
                    var parts = input.Split(' ');
                    if (parts.Length >= 2) int.TryParse(parts[1], out idx);
                    _ = DoGroupAttackAsync(idx);
                    return true;
                }

                if (input.StartsWith("cast "))
                {
                    // cast <skill> [targetIndex]
                    var parts = input.Split(' ');
                    if (parts.Length < 2) return false;
                    string skillId  = parts[1];
                    int    targetIdx = 0;
                    if (parts.Length >= 3) int.TryParse(parts[2], out targetIdx);
                    _ = DoGroupCastAsync(skillId, targetIdx);
                    return true;
                }

                if (input is "group status" or "group")
                {
                    if (_groupSnap != null) PrintGroupState(_groupSnap);
                    return true;
                }
            }

            if (input == "group fight")
            {
                if (!ConsoleHubClient.IsConnected)
                {
                    Console.WriteLine(Localization.T("console.combat.must_be_online"));
                    return true;
                }
                if (ConsoleHubClient.CurrentPartyMembers.Count == 0)
                {
                    Console.WriteLine(Localization.T("console.party.not_in_party"));
                    return true;
                }
                _ = StartGroupFightAsync(Character);
                return true;
            }

            if (input == "fight")
            {
                if (Character.CurrentRoom.Monsters.Any())
                    EncounterRunner.StartEncounter(Character);
                else
                    Console.WriteLine(Localization.T("console.combat.nothing_to_fight"));
                return true;
            }

            return false;
        }

        // ── Async helpers ─────────────────────────────────────────────────────

        private static async Task StartGroupFightAsync(Character Character)
        {
            var result = await ConsoleHubClient.StartGroupCombatAsync(Character.CurrentRoom.Id);
            if (result == null || !result.Success)
                Console.WriteLine(Localization.T("console.combat.group_fight_failed", result?.Reason ?? Localization.T("console.combat.no_response")));
            // GroupCombatStarted event fires on all members — OnGroupCombatStarted handles display
        }

        private static async Task DoGroupAttackAsync(int targetIndex)
        {
            var snap = await ConsoleHubClient.GroupCharacterAttackAsync(targetIndex);
            if (snap == null || snap.Characters.Count == 0)
                Console.WriteLine(Localization.T("console.combat.invalid_attack"));
            // GroupCombatUpdated/Finished events handle display
        }

        private static async Task DoGroupCastAsync(string skillId, int targetIndex)
        {
            var snap = await ConsoleHubClient.GroupCharacterCastSkillAsync(skillId, targetIndex);
            if (snap == null || snap.Characters.Count == 0)
                Console.WriteLine(Localization.T("console.combat.invalid_cast"));
        }

        // ── Display helpers ───────────────────────────────────────────────────

        private static void PrintGroupState(GroupCombatSnapshot snap)
        {
            Console.WriteLine();
            Console.WriteLine(Localization.T("console.combat.party_header"));
            foreach (var p in snap.Characters)
                Console.WriteLine(Localization.T("console.combat.combatant_line", p.Name.PadRight(16), p.Hp, p.MaxHp, p.IsAlive ? "" : Localization.T("console.combat.dead_tag")));
            Console.WriteLine(Localization.T("console.combat.monsters_header"));
            for (int i = 0; i < snap.Monsters.Count; i++)
            {
                var m = snap.Monsters[i];
                Console.WriteLine(Localization.T("console.combat.monster_line", i, Localization.T(m.Name).PadRight(16), m.Hp, m.MaxHp, m.IsAlive ? "" : Localization.T("console.combat.dead_tag")));
            }
            if (!snap.Finished)
                Console.WriteLine(Localization.T("console.combat.current_turn", snap.CurrentTurnCharacterName));
        }

        private static void PrintCombatLog(CombatLogMessage msg)
        {
            try
            {
                var text = Localization.T(msg.Key, msg.Args.Cast<object>().ToArray());
                Console.WriteLine(text);
            }
            catch
            {
                Console.WriteLine(msg.Key);
            }
        }
    }
}
