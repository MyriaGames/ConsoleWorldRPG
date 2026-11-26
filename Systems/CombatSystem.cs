namespace Myria.ConsoleApp.Systems
{
    public static class EncounterRunner
    {
        private static readonly Random _random = Random.Shared;

        public static void StartEncounter(Character Character)
        {
            if (Character.CurrentRoom.EncounterableMonsters is not { Count: > 0 }
                || Character.CurrentRoom.IsCleared)
            {
                Console.WriteLine("But nothing stirs in the darkness...");
                return;
            }

            if (ConsoleHubClient.IsConnected)
            {
                RunOnlineCombat(Character);
                return;
            }

            var monster = SelectMonster(Character);
            Console.WriteLine($"\nA wild {Localization.T(monster.Name)} appears!");
            Console.WriteLine(Localization.T(monster.Description));

            var encounter = new Myria.Lib.Core.Systems.CombatEncounter(Character, monster);
            encounter.MonsterKilled += (_, e) => PrintQuestProgress(Character, monster);

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
                Console.WriteLine("What will you do? (attack / cast <skill> / list)");
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
                else if (input == "list")
                {
                    Character.Inventory.ListItems();
                    continue;
                }
                else
                {
                    Console.WriteLine("❓ Use 'attack', 'cast <skill>', or 'list'.");
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
                Console.WriteLine("  " + Localization.T(entry.Key, entry.Args.Cast<object>().ToArray()));
        }

        private static void OnlineWin(Character Character, string monsterName, CombatTurnResult result)
        {
            Console.WriteLine($"\n✅ You defeated the {monsterName}!");
            if (result.XpGained > 0)
            {
                Character.GainXp(result.XpGained);
                Console.WriteLine($"⭐ +{result.XpGained} XP");
            }
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

        private static void OnlineDeath(Character Character)
        {
            // The server already respawned the character (before answering the last turn) and sent
            // CharacterRespawned, whose handler in Game.cs moved us to the server's respawn room and
            // says where we woke up. No guessing here: a guessed room that differed from the server's
            // left every next move refused (moves must use an exit of the server's current room).
            Console.WriteLine("\n💀 You were defeated...");
            Character.CurrentHealth = Character.MaxHealth;
            Character.CurrentMana   = Character.MaxMana;
        }

        // ── Monster selection ────────────────────────────────────────────────────

        private static Monster SelectMonster(Character Character)
        {
            var room = Character.CurrentRoom;

            if (room.IsDungeonRoom && room.CurrentMonsters.Count < 1)
            {
                if (room.IsBossRoom && room.Monsters.Count > 0)
                {
                    room.CurrentMonsters.Add(room.Monsters[0].Clone());
                }
                else
                {
                    int count = _random.Next(1, 11);
                    for (int i = 0; i < count; i++)
                        room.CurrentMonsters.Add(room.Monsters[_random.Next(0, room.Monsters.Count)].Clone());
                }
                return room.CurrentMonsters[_random.Next(0, room.CurrentMonsters.Count)];
            }

            if (room.IsDungeonRoom)
                return room.CurrentMonsters[_random.Next(0, room.CurrentMonsters.Count)];

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
                Console.WriteLine("  " + Localization.T(entry.Key, entry.Args));
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
                Console.WriteLine($"📜 Quest '{Localization.T(quest.Name)}': {Localization.T(monster.Name)} ({current}/{required})");
            }

            foreach (var quest in Character.ActiveQuests
                         .Where(q => q.Status == QuestStatus.Completed
                                  && q.RequiredKills.ContainsKey(monster.Id)))
            {
                Console.WriteLine($"✅ Quest '{Localization.T(quest.Name)}' complete! Return to an NPC to claim your reward.");
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
            Character.ApplyDeathXpPenalty();
        }
    }
}
