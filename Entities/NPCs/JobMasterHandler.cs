namespace Myria.ConsoleApp.Entities.NPCs
{
    // Mirrors Myria.Wpf's JobMasterPanel.
    internal static class JobMasterHandler
    {
        public static void JobMasterMenu(Character Character, string jobId)
        {
            var job   = JobManager.GetById(jobId);
            var entry = JobManager.GetOrAdd(Character, jobId);

            while (true)
            {
                string jobName = job is { } j ? Localization.T(j.Name) : jobId;
                string jobType = string.IsNullOrEmpty(job?.Type) ? "" : $" ({job.Type})";
                bool   isActive = Character.ActiveJobId == jobId;

                Console.WriteLine(Localization.T("console.npc.job_title", jobName, jobType));
                if (!string.IsNullOrEmpty(job?.Description))
                    Console.WriteLine(Localization.T(job.Description));
                Console.WriteLine();
                Console.WriteLine(Localization.T("console.npc.job_skill_line", JobXpService.GetLevel(entry.SkillXp), JobXpService.FormatProgress(entry.SkillXp)));
                Console.WriteLine(Localization.T("console.npc.job_knowledge_line", JobXpService.GetLevel(entry.KnowledgeXp), JobXpService.FormatProgress(entry.KnowledgeXp)));
                Console.WriteLine(Localization.T("console.npc.job_fame_line", JobXpService.GetLevel(entry.FameXp), JobXpService.FormatProgress(entry.FameXp)));
                Console.WriteLine();

                if (isActive)
                {
                    Console.WriteLine(Localization.T("console.npc.job_active_status"));
                    Console.WriteLine(Localization.T("console.npc.job_stop_option"));
                }
                else
                {
                    var cd = JobManager.GetCooldownRemaining(Character);
                    if (cd > TimeSpan.Zero)
                        Console.WriteLine(Localization.T("console.npc.job_cooldown_remaining", Printer.FormatCooldown(cd)));
                    Console.WriteLine(Localization.T("console.npc.job_work_option", jobName));
                }
                Console.WriteLine(Localization.T("console.npc.menu_leave"));
                Console.Write("> ");
                string input = Console.ReadLine()?.Trim() ?? "";
                if (input == "0") return;
                if (input != "1") { Printer.PrintError(Localization.T("ui_invalid_option")); continue; }

                if (isActive)
                {
                    JobManager.SetActiveJob(Character, null);
                    if (ConsoleHubClient.IsConnected)
                        _ = ConsoleHubClient.ToggleJobAsync(null);
                    Console.WriteLine(Localization.T("console.npc.job_stopped", jobName));
                }
                else
                {
                    if (JobManager.SetActiveJob(Character, jobId))
                    {
                        if (ConsoleHubClient.IsConnected)
                            _ = ConsoleHubClient.ToggleJobAsync(jobId);
                        Printer.PrintSuccess(Localization.T("console.npc.job_started", jobName));
                    }
                    else
                    {
                        var cd = JobManager.GetCooldownRemaining(Character);
                        Printer.PrintError(Localization.T("console.npc.job_switch_cooldown", Printer.FormatCooldown(cd)));
                    }
                }
            }
        }
    }
}
