using Myria.ConsoleApp.Commands;
using Myria.Lib.Core.Entities.Effects;
using Myria.Lib.Core.Entities.Skills;
using Xunit;

namespace Myria.Console.Tests;

/// <summary>
/// The status tag next to each upgrade in the console's "skill &lt;n&gt;" menu. Three states, and
/// the cap shown must be the *effective* one: an effect-granting upgrade with MaxPurchases 0 is
/// capped at 1 (SkillLevelingService.GetEffectiveMaxPurchases), not repeatable.
/// </summary>
[Collection("Localization")]
public class SkillUpgradeStatusTests
{
    public SkillUpgradeStatusTests(LocalizationFixture _) { }

    private static SkillProgress Progress(int level, params string[] purchased) =>
        new() { SkillId = "s", Level = level, PurchasedUpgradeIds = purchased.ToList() };

    [Fact]
    public void BelowRequiredLevel_ShowsWhenItUnlocks()
    {
        var option = new SkillUpgradeOption { Id = "s_power_2", RequiredLevel = 11 };
        Assert.Equal("unlocks at level 11", SkillCommands.UpgradeStatus(Progress(10), option));
    }

    [Fact]
    public void CappedUpgrade_ShowsBoughtOverCap()
    {
        var option = new SkillUpgradeOption { Id = "s_mastery", RequiredLevel = 6, MaxPurchases = 2 };
        Assert.Equal("1/2", SkillCommands.UpgradeStatus(Progress(6, "s_mastery"), option));
    }

    [Fact]
    public void UncappedNumericUpgrade_ShowsRepeatable()
    {
        var option = new SkillUpgradeOption { Id = "s_power", ScalingFactorDelta = 0.2f };
        Assert.Equal("bought 3x, repeatable", SkillCommands.UpgradeStatus(Progress(4, "s_power", "s_power", "s_power"), option));
    }

    [Fact]
    public void EffectUpgradeWithoutExplicitCap_IsShownAsCappedAtOne()
    {
        var option = new SkillUpgradeOption
        {
            Id = "s_sig",
            AddedEffects = { new SkillEffectEntry { EffectId = "stun_short", ApplyTo = EffectTarget.Target } },
        };
        Assert.Equal("0/1", SkillCommands.UpgradeStatus(Progress(1), option));
    }
}
