using Xunit;

// Both Myria.Console and the Myria.Lib.Core it sits on are built around static, process-wide
// state (ConsoleHubClient's connection and token, Localization's loaded strings, GameService's
// content registries). Running test classes in parallel would let them interfere
// nondeterministically, so parallelization is disabled assembly-wide — the same decision
// Myria.Lib.Tests already made for the same reason.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
