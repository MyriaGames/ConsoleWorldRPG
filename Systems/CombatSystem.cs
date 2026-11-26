namespace Myria.ConsoleApp.Systems
{
    public static class EncounterRunner
    {
        private static readonly Random _random = Random.Shared;

        public static void StartEncounter(Character Character)
        {
            if (Character.CurrentRoom.EncounterableMonsters is not { Count: > 0 })
            {
                Console.WriteLine("But nothing stirs in the darkness...");
                return;
            }

            if (ConsoleHubClient.IsConnected)
            {
                RunOnlineCombat(Character);
                return;
            }

            // v0.3 dungeons: offline, a dungeon room fights the local run's pack, one at a time.
            Myria.Lib.Core.Systems.CombatEncounter? encounter = null;
            var run = Myria.Lib.Core.Systems.Dungeons.LocalDungeonRuns.For(Character.CurrentRoom);
            Monster monster;
            if (run is not null)
            {
                var pack = run.NextPack(Character.CurrentRoom, 1, () => encounter is null || encounter.Phase != CombatPhase.Finished);
                if (pack.Count == 0)
                {
                    Console.WriteLine(Localization.T("dungeon.room_cleared"));
                    return;
                }
                monster = pack[0];
            }
            else monster = SelectMonster(Character);

            Console.WriteLine($"\nA wild {Localization.T(monster.Name)} appears!");
            Console.WriteLine(Localization.T(monster.Description));

            encounter = new Myria.Lib.Core.Systems.CombatEncounter(Character, monster);
            encounter.MonsterKilled += (_, e) => PrintQuestProgress(Character, monster);
            if (run is not null)
            {
                int roomId = Character.CurrentRoom.Id;
                encounter.MonsterKilled += (_, e) => { if (e.Monster is not null) run.RecordKill(roomId, e.Monster); };
            }

            int logIndex = 1; // skip the "pg.fight.log.start" entry added by the constructor
            RunCombatLoop(encounter, Character, monster, ref logIndex);

            if (Character.IsAlive)
            {
                Console.WriteLine($"\n✅ You defeated the {Localization.T(monster.Name)}!");
                PrintDrops(encounter);
            }
            else
            {
                HandleDeath(Character);
            }
        }

        // ── Online combat ────────────────────────────────────────────────────────

        private static void RunOnlineCombat(Character Character)
        {
            var start = ConsoleHubClient.StartCombatAsync(Character.CurrentRoom.Id)
                            .GetAwaiter().GetResult();
            if (start?.Reason == "room_cleared")
            {
                Console.WriteLine(Localization.T("dungeon.room_cleared"));
                return;
            }
            if (start is null || !start.Success)
            {
                Console.WriteLine($"❌ Could not start encounter: {start?.Reason ?? "server unavailable"}");
                return;
            }

            string monsterName = start.MonsterName != null ? Localization.T(start.MonsterName) : "???";
            Console.WriteLine($"\nA wild {monsterName} appears!");

            string phase     = "CharacterTurn";
            int monsterHp    = start.MonsterHp;
            int monsterMaxHp = start.MonsterMaxHp;

            while (true)
            {
                if (phase is "Casting" or "Recovery")
                {
                    Console.WriteLine($"\n[{phase}...] Press Enter to continue.");
                    Console.ReadLine();
                    var adv = ConsoleHubClient.CharacterAttackAsync().GetAwaiter().GetResult();
                    if (adv is null) break;
                    FlushOnlineLog(adv.LogEntries);
                    Character.CurrentHealth = adv.CharacterHp;
                    monsterHp = adv.MonsterHp;
                    phase     = adv.Phase;
                    if (!adv.Finished) continue;
                    if (adv.CharacterWon) OnlineWin(Character, monsterName, adv);
                    else               OnlineDeath(Character);
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n[HP: {Character.CurrentHealth}/{Character.MaxHealth} | MP: {Character.CurrentMana}/{Character.MaxMana}]");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{monsterName} — HP: {monsterHp}/{monsterMaxHp}");
                Console.ResetColor();
                Console.WriteLine("What will you do? (attack / cast <skill> / use <item> / list)");
                Console.Write("> ");

                var input = Console.ReadLine()?.Trim().ToLower() ?? "";
                CombatTurnResult? result = null;

                if (input == "attack")
                {
                    result = ConsoleHubClient.CharacterAttackAsync().GetAwaiter().GetResult();
                }
                else if (input.StartsWith("cast "))
                {
                    string skillName = input[5..].Trim();
                    var skill = Character.Skills.FirstOrDefault(s =>
                        s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
                    if (skill == null) { Console.WriteLine("❌ You don't know that skill."); continue; }
                    result = ConsoleHubClient.CharacterCastSkillAsync(skill.Id).GetAwaiter().GetResult();
                }
                else if (input is "cast" or "cast ?")
                {
                    PrintSkillList(Character);
                    continue;
                }
                else if (input.StartsWith("use "))
                {
                    var item = InventoryUtils.ResolveInventoryItem(input[4..].Trim(), Character);
                    if (item is not ConsumableItem)
                    {
                        Console.WriteLine(item != null
                            ? "You can't use that item in battle!"
                            : "You don't have that item in your inventory!");
                        continue;
                    }
                    result = ConsoleHubClient.CharacterUseItemAsync(item.Id).GetAwaiter().GetResult();
                    if (result is { Success: true })
                        Commands.CombatCommands.MirrorItemUse(Character, item, result.LogEntries);
                }
                else if (input == "list")
                {
                    Character.Inventory.ListItems();
                    continue;
                }
                else
                {
                    Console.WriteLine("❓ Use 'attack', 'cast <skill>', 'use <item>', or 'list'.");
                    continue;
                }

                if (result is null || !result.Success)
                {
                    Console.WriteLine("❌ Action failed.");
                    continue;
                }

                FlushOnlineLog(result.LogEntries);
                Character.CurrentHealth = result.CharacterHp;
                monsterHp = result.MonsterHp;
                phase     = result.Phase;

                if (!result.Finished) continue;
                if (result.CharacterWon) OnlineWin(Character, monsterName, result);
                else                  OnlineDeath(Character);
                return;
            }
        }

        private static void FlushOnlineLog(List<CombatLogMessage> entries)
        {
            foreach (var entry in entries)
                Console.WriteLine("  " + Localization.T(entry.Key, Commands.CombatCommands.LocalizeLogArgs(entry.Args)));
        }

        private static void OnlineWin(Character Character, string monsterName, CombatTurnResult result)
        {
            Console.WriteLine($"\n✅ You defeated the {monsterName}!");
            if (result.XpGained > 0)
            {
                Character.GainXp(result.XpGained);
                Console.WriteLine($"⭐ +{result.XpGained} XP");
            }
            PrintSyncedQuestCompletions(Character);
            var drops = new List<(string name, int count)>();
            foreach (var id in result.LootItemIds)
                if (ItemFactory.TryCreateItem(id, out var item) && Character.Inventory.AddItem(item, Character))
                    drops.Add((Localization.T(item.Name), item.StackSize));
            if (drops.Count > 0)
            {
                Console.WriteLine("You found:");
                foreach (var (name, count) in drops)
                    Console.WriteLine($"  🪶 {count}x {name}");
            }
        }

        /// <summary>Online the kill happened on the server: pull its quest counters and report
        /// quests this fight finished (bounties included).</summary>
        private static void PrintSyncedQuestCompletions(Character Character)
        {
            var before = Character.ActiveQuests.Where(q => q.Status == QuestStatus.Completed).Select(q => q.Id).ToHashSet();
            ConsoleHubClient.SyncQuestProgress(Character);
            foreach (var quest in Character.ActiveQuests.Where(q => q.Status == QuestStatus.Completed && !before.Contains(q.Id)))
                Console.WriteLine($"✅ Quest '{Myria.Lib.Core.Systems.World.QuestText.Name(quest)}' complete! Return to an NPC to claim your reward.");
        }

        private static void OnlineDeath(Character Character)
        {
            Console.WriteLine("\n💀 You were defeated...");
            int respawnRoomId   = Character.LastHealerRoomId ?? 1;
            var respawnRoom     = GameService.Rooms[respawnRoomId];
            Character.CurrentRoom  = respawnRoom;
            Character.CurrentRoomId = respawnRoomId;
            Character.CurrentHealth = Character.MaxHealth;
            Character.CurrentMana   = Character.MaxMana;
            Console.WriteLine($"🌀 You awaken in {respawnRoom.DisplayName()}.");
        }

        // ── Monster selection ────────────────────────────────────────────────────

        private static Monster SelectMonster(Character Character)
        {
            var room = Character.CurrentRoom;

            return room.Monsters[_random.Next(0, room.Monsters.Count)].Clone();
        }

        // ── Combat loop ──────────────────────────────────────────────────────────

        private static void RunCombatLoop(
            Myria.Lib.Core.Systems.CombatEncounter encounter,
            Character Character,
            Monster monster,
            ref int logIndex)
        {
            while (encounter.Phase != CombatPhase.Finished)
            {
                switch (encounter.Phase)
                {
                    case CombatPhase.Casting:
                        Console.WriteLine($"\nCasting... ({encounter.TurnsUntilActionExecutes} turn(s) remaining)");
                        Console.WriteLine("[Press Enter to continue]");
                        Console.ReadLine();
                        encounter.Tick();
                        FlushLog(encounter, ref logIndex);
                        break;

                    case CombatPhase.Recovery:
                        Console.WriteLine($"\nRecovering... ({encounter.RecoveryTurnsRemaining} turn(s) remaining)");
                        Console.WriteLine("[Press Enter to continue]");
                        Console.ReadLine();
                        encounter.Tick();
                        FlushLog(encounter, ref logIndex);
                        break;

                    case CombatPhase.CharacterTurn:
                        PrintStatus(Character, monster);
                        if (!HandleCharacterTurn(encounter, Character, monster, ref logIndex))
                            continue; // unknown or list command — re-prompt without advancing
                        break;
                }
            }
        }

        private static void PrintStatus(Character Character, Monster monster)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[HP: {Character.CurrentHealth}/{Character.MaxHealth} | MP: {Character.CurrentMana}/{Character.MaxMana}]");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{Localization.T(monster.Name)} — HP: {monster.CurrentHealth}/{monster.MaxHealth}");
            Console.ResetColor();
            Console.WriteLine("What will you do? (attack / cast <skill> / use <item> / list)");
            Console.Write("> ");
        }

        private static bool HandleCharacterTurn(
            Myria.Lib.Core.Systems.CombatEncounter encounter,
            Character Character,
            Monster monster,
            ref int logIndex)
        {
            var input = Console.ReadLine()?.Trim().ToLower() ?? "";

            if (input == "attack")
            {
                encounter.CharacterAttack();
                FlushLog(encounter, ref logIndex);
                return true;
            }

            if (input is "cast" or "cast ?")
            {
                PrintSkillList(Character);
                return false;
            }

            if (input.StartsWith("cast "))
            {
                return TryCast(encounter, Character, input[5..].Trim(), ref logIndex);
            }

            if (input.StartsWith("use "))
            {
                return TryUseItem(encounter, Character, input[4..].Trim(), ref logIndex);
            }

            if (input == "list")
            {
                Character.Inventory.ListItems();
                return false;
            }

            Console.WriteLine("❓ Use 'attack', 'cast <skill>', 'use <item>', or 'list'.");
            return false;
        }

        private static bool TryCast(
            Myria.Lib.Core.Systems.CombatEncounter encounter,
            Character Character,
            string skillName,
            ref int logIndex)
        {
            var skill = Character.Skills.FirstOrDefault(s =>
                s.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

            if (skill == null) { Console.WriteLine("❌ You don't know that skill."); return false; }
            if (Character.CurrentMana < skill.ManaCost) { Console.WriteLine("❌ Not enough mana."); return false; }

            bool ok = encounter.CharacterBeginCast(skill);
            if (!ok) return false;

            FlushLog(encounter, ref logIndex);
            return true;
        }

        private static bool TryUseItem(
            Myria.Lib.Core.Systems.CombatEncounter encounter,
            Character Character,
            string itemName,
            ref int logIndex)
        {
            var item = InventoryUtils.ResolveInventoryItem(itemName, Character);

            if (item is ConsumableItem consumable)
            {
                encounter.CharacterUseItem(consumable);
                FlushLog(encounter, ref logIndex);
                return true;
            }

            if (item != null)
                Console.WriteLine("You can't use that item in battle!");
            else
                Console.WriteLine("You don't have that item in your inventory!");
            return false;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static void FlushLog(Myria.Lib.Core.Systems.CombatEncounter encounter, ref int logIndex)
        {
            while (logIndex < encounter.Log.Count)
            {
                var entry = encounter.Log[logIndex++];
                Console.WriteLine("  " + Localization.T(entry.Key, Commands.CombatCommands.LocalizeLogArgs(entry.Args)));
            }
        }

        private static void PrintSkillList(Character Character)
        {
            Console.WriteLine("Your skills:");
            if (Character.Skills.Count == 0) { Console.WriteLine("  (none)"); return; }
            foreach (var s in Character.Skills)
                Console.WriteLine($"  {s.Name} — MP: {s.ManaCost}, Cast: {s.CastTime}t");
        }

        private static void PrintQuestProgress(Character Character, Monster monster)
        {
            foreach (var quest in Character.ActiveQuests
                         .Where(q => q.Status == QuestStatus.InProgress
                                  && q.RequiredKills.ContainsKey(monster.Id)))
            {
                int required = quest.RequiredKills[monster.Id];
                int current  = quest.KillProgress.GetValueOrDefault(monster.Id, 0);
                Console.WriteLine($"📜 Quest '{Myria.Lib.Core.Systems.World.QuestText.Name(quest)}': {Localization.T(monster.Name)} ({current}/{required})");
            }

            foreach (var quest in Character.ActiveQuests
                         .Where(q => q.Status == QuestStatus.Completed
                                  && q.RequiredKills.ContainsKey(monster.Id)))
            {
                Console.WriteLine($"✅ Quest '{Myria.Lib.Core.Systems.World.QuestText.Name(quest)}' complete! Return to an NPC to claim your reward.");
            }
        }

        private static void PrintDrops(Myria.Lib.Core.Systems.CombatEncounter encounter)
        {
            var drops = encounter.GetDropNames();
            if (drops.Count > 0)
            {
                Console.WriteLine("You found:");
                foreach (var (name, count) in drops)
                    Console.WriteLine($"  🪶 {count}x {Localization.T(name)}");
            }
            if (encounter.InventoryFull)
                Console.WriteLine("❌ Inventory was full; some items could not be picked up.");
        }

        private static void HandleDeath(Character Character)
        {
            Console.WriteLine("\n💀 You were defeated...");

            int respawnRoomId   = Character.LastHealerRoomId ?? 1;
            var respawnRoom     = GameService.Rooms[respawnRoomId];
            Character.CurrentRoom  = respawnRoom;
            Character.CurrentRoomId = respawnRoomId;
            Character.CurrentHealth = Character.MaxHealth;
            Character.CurrentMana   = Character.MaxMana;

            Console.WriteLine($"🌀 You awaken in {respawnRoom.DisplayName()}.");
            Myria.Lib.Core.Systems.Dungeons.LocalDungeonRuns.NoteLocation(respawnRoom);
            Character.ApplyDeathXpPenalty();
        }
    }
}
