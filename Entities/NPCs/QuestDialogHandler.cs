using Myria.Lib.Core.Services;
using Myria.Lib.Core.Systems.Enums;
using Myria.Lib.Core.Systems.World;

namespace Myria.ConsoleApp.Entities.NPCs
{
    // Mirrors Myria.Wpf's QuestDialogPanel/DialogPanel split — quest offer/return flow and the
    // shared dialog-line playback used by both.
    internal static class QuestDialogHandler
    {
        // ── CN3: Quest return flow ───────────────────────────────────────────────

        public static void OfferQuestReturns(Character Character, Npc npc)
        {
            // Filter: quests where ReturnNpcIdResolved matches the current NPC.
            // Currently falls back to all completed quests since GiverNpcId is not
            // set in quests.json yet. Once quest data is updated, switch to
            // QuestManager.GetReturnableForNpc(Character, npc.Id).
            // Online the server keeps the counters (combat runs there) - fetch them first.
            if (ConsoleHubClient.IsConnected) ConsoleHubClient.SyncQuestProgress(Character);
            else QuestManager.UpdateItemProgress(Character);

            // Generated quests are strict about where they go: a supply request back to that NPC in
            // that city, a bounty to any guild house.
            var dynamicHere = QuestManager.GetReturnableForNpc(Character, npc.Id).Where(q => q.IsDynamic).ToHashSet();
            var returnable = Character.ActiveQuests
                .Where(q => q.Status == QuestStatus.Completed && (!q.IsDynamic || dynamicHere.Contains(q)))
                .ToList();

            if (returnable.Count == 0) return;

            Console.WriteLine(Localization.T("console.quest.returns_header"));
            for (int i = 0; i < returnable.Count; i++)
                Console.WriteLine(Localization.T("console.quest.numbered_line", i + 1, QuestText.Name(returnable[i])));

            Console.Write(Localization.T("console.quest.return_prompt"));
            if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 1 || choice > returnable.Count)
                return;

            var quest = returnable[choice - 1];

            if (quest.IsDynamic)
            {
                if (!PlayDialog(quest, Character, npc, isReturn: true))
                {
                    Console.WriteLine(Localization.T("console.quest.return_cancelled"));
                    return;
                }
                if (DynamicQuestHandler.HandIn(Character, quest))
                    Console.WriteLine(Localization.T("console.quest.returned", QuestText.Name(quest)));
                return;
            }

            // CN8: play return dialog before completing
            if (!PlayDialog(quest, Character, npc, isReturn: true))
            {
                Console.WriteLine(Localization.T("console.quest.return_cancelled"));
                return;
            }

            quest.GrantRewards(Character);
            if (ConsoleHubClient.IsConnected)
                _ = ConsoleHubClient.QuestActionAsync(quest.Id, isReturn: true);

            if (quest.IsRepeatable)
            {
                if (!Character.RepeatableQuestRecords.TryGetValue(quest.Id, out var rec))
                {
                    rec = new RepeatRecord();
                    Character.RepeatableQuestRecords[quest.Id] = rec;
                }
                if (rec.LastCompletionDate?.Date != DateTime.Today)
                    rec.CompletionsToday = 0;
                rec.TimesCompleted++;
                rec.CompletionsToday++;
                rec.LastCompletionDate = DateTime.Now;
                Character.ActiveQuests.Remove(quest);
            }
            else
            {
                quest.Status = QuestStatus.Returned;
                Character.CompletedQuests.Add(quest);
                Character.ActiveQuests.Remove(quest);
            }

            Console.WriteLine(Localization.T("console.quest.returned", QuestText.Name(quest)));
        }

        // ── Quest acceptance ─────────────────────────────────────────────────────

        public static void OfferQuestAcceptance(Character Character, Npc npc)
        {
            // When GiverNpcId is set in quests.json, switch to:
            // var available = QuestManager.GetAcceptableForNpc(Character, npc.Id);
            // CurrentPartyMembers is the full party list (includes self); 0 means solo.
            int partySize = Math.Max(1, ConsoleHubClient.CurrentPartyMembers.Count);
            var available = QuestManager.GetAvailableForCharacter(Character, partySize)
                .Where(q => !Character.ActiveQuests.Any(a => a.Id == q.Id))
                .ToList();

            if (available.Count == 0) return;

            Console.WriteLine(Localization.T("console.quest.available_header"));
            for (int i = 0; i < available.Count; i++)
                Console.WriteLine(Localization.T("console.quest.available_line", i + 1, available[i].RequiredLevel, Localization.T(available[i].Name), Localization.T(available[i].Description)));

            Console.Write(Localization.T("console.quest.accept_prompt"));
            if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 1 || choice > available.Count)
                return;

            var template = available[choice - 1];

            // CN8: play accept dialog; Character can still decline at the end
            if (!PlayDialog(template, Character, npc, isReturn: false))
            {
                Console.WriteLine(Localization.T("console.quest.declined"));
                return;
            }

            var quest = template.Clone();
            quest.Status = quest.IsTalkOnly ? QuestStatus.Completed : QuestStatus.InProgress;
            foreach (var key in quest.RequiredKills.Keys) quest.KillProgress[key] = 0;
            foreach (var key in quest.RequiredItems.Keys) quest.ItemProgress[key] = 0;
            quest.GrantAcceptItems(Character);
            Character.ActiveQuests.Add(quest);
            if (ConsoleHubClient.IsConnected)
                _ = ConsoleHubClient.QuestActionAsync(quest.Id, isReturn: false);

            Console.WriteLine(quest.IsTalkOnly
                ? Localization.T("console.quest.accepted_completed", Localization.T(quest.Name))
                : Localization.T("console.quest.accepted", Localization.T(quest.Name)));
        }

        // ── CN8: Quest dialog ─────────────────────────────────────────────────────

        private static bool PlayDialog(Quest quest, Character Character, Npc npc, bool isReturn)
        {
            var lines = isReturn ? quest.ReturnDialog : quest.AcceptDialog;
            // Authored dialog lines are locale keys (e.g. "quest.co1_first_day.accept.1"); the
            // fallback below is literal display text, not a key, so it must not be translated.
            bool isFallback = lines == null || lines.Count == 0;
            if (isFallback) // generated quests have no authored dialog - their description is the line
                lines = new List<DialogLine> { new() { Speaker = "npc", Text = quest.IsDynamic ? QuestText.Description(quest) : "..." } };

            Console.WriteLine();
            string header = $" {QuestText.Name(quest)} [Lv {quest.RequiredLevel}] ";
            int padLen = Math.Max(0, 60 - header.Length);
            Console.WriteLine("─" + header + new string('─', padLen));

            for (int i = 0; i < lines.Count; i++)
            {
                string speaker = ResolveSpeakerConsole(lines[i].Speaker, Character, npc);
                string text    = isFallback ? lines[i].Text : Localization.T(lines[i].Text);
                Console.WriteLine(Localization.T("console.quest.dialog_line", speaker, text));

                if (i < lines.Count - 1)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(Localization.T("console.quest.press_enter"));
                    Console.ResetColor();
                    Console.ReadLine();
                }
            }

            Console.WriteLine("\n" + new string('─', 61));
            string prompt = isReturn
                ? Localization.T("console.quest.return_confirm_prompt")
                : Localization.T("console.quest.accept_confirm_prompt");
            Console.Write(prompt);
            return (Console.ReadLine()?.Trim().ToLower() ?? "") == "y";
        }

        private static string ResolveSpeakerConsole(string speaker, Character Character, Npc npc)
        {
            if (speaker == "Character") return Character.Name;
            if (speaker == "npc")    return npc.Type.ToString();
            if (speaker.StartsWith("npc:"))
            {
                string id = speaker[4..];
                return NpcService.TryGet(id, out var other) && other != null
                    ? other.Type.ToString()
                    : id;
            }
            return speaker;
        }
    }
}
