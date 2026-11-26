using Myria.Lib.Core.Systems;
using Xunit;

namespace Myria.Console.Tests;

/// <summary>
/// Loads English strings once for the whole test run, the same way
/// Myria.ConsoleApp.Services.GameService.InitializeGame does: register the console-only locale
/// directory first, then load the language so those additions are merged on top of the shared
/// base strings from Myria.Lib.Core.
///
/// Localization is static, process-wide state, so this must happen exactly once and be shared —
/// never re-loaded per test class. English is pinned deliberately: assertions read real
/// user-facing text, and a different default language would change every expected string.
/// </summary>
public sealed class LocalizationFixture
{
    public LocalizationFixture()
    {
        Localization.RegisterAdditionalLocaleDirectory("Data/misc/locales");
        Localization.Load("en");
    }
}

/// <summary>
/// Apply <c>[Collection("Localization")]</c> to any test class that asserts on translated text.
/// Tests over pure string/URL logic don't need it.
/// </summary>
[CollectionDefinition("Localization")]
public sealed class LocalizationCollection : ICollectionFixture<LocalizationFixture>
{
}
