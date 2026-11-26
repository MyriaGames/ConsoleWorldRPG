using Myria.ConsoleApp.Utils;
using Myria.Lib.Core.Entities.Maps;
using Myria.Lib.Core.Systems;
using Xunit;

namespace Myria.Console.Tests;

/// <summary>
/// The console client renders almost everything by writing to Console directly, which leaves very
/// little that can be asserted. These two helpers are the exceptions — they return strings rather
/// than printing them — and both sit on the path every player sees constantly.
/// </summary>
[Collection("Localization")]
public class PresentationTests
{
    public PresentationTests(LocalizationFixture _) { }

    // ---- Cooldown formatting ---------------------------------------------------------------

    /// <summary>
    /// FormatCooldown picks one of three locale strings by magnitude. The boundaries are the
    /// interesting part: at exactly one hour it must switch from the minutes form to the hours
    /// form, and the minutes form has no hours component to fall back on — a wrong comparison
    /// shows a player "0m" while they still have an hour to wait.
    /// </summary>
    [Fact]
    public void FormatCooldown_UsesTheMinutesFormBelowAnHour()
    {
        Assert.Equal("45m", Printer.FormatCooldown(TimeSpan.FromMinutes(45)));
    }

    [Fact]
    public void FormatCooldown_SwitchesToHoursAtExactlyOneHour()
    {
        Assert.Equal("1h 0m", Printer.FormatCooldown(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void FormatCooldown_ShowsHoursAndRemainingMinutes()
    {
        Assert.Equal("2h 30m", Printer.FormatCooldown(new TimeSpan(2, 30, 0)));
    }

    [Fact]
    public void FormatCooldown_SwitchesToDaysAtExactlyOneDay()
    {
        Assert.Equal("1d 0h", Printer.FormatCooldown(TimeSpan.FromDays(1)));
    }

    [Fact]
    public void FormatCooldown_ShowsDaysAndRemainingHours()
    {
        Assert.Equal("3d 5h", Printer.FormatCooldown(new TimeSpan(3, 5, 0, 0)));
    }

    /// <summary>
    /// Just under each boundary must still use the smaller unit — this is the off-by-one guard
    /// for the >= comparisons.
    /// </summary>
    [Fact]
    public void FormatCooldown_StaysInTheSmallerUnitJustBelowEachBoundary()
    {
        Assert.Equal("59m", Printer.FormatCooldown(TimeSpan.FromMinutes(59)));
        Assert.Equal("23h 59m", Printer.FormatCooldown(new TimeSpan(23, 59, 0)));
    }

    [Fact]
    public void FormatCooldown_RendersZeroAsNoMinutesRemaining()
    {
        Assert.Equal("0m", Printer.FormatCooldown(TimeSpan.Zero));
    }

    // ---- Room display names ------------------------------------------------------------------

    /// <summary>
    /// Room.Name is normally a locale key ("room.royas.main"), but rooms built at runtime — guild
    /// houses and bases, whose names players choose — set IsLiteral so their name is shown as-is.
    /// Getting this backwards either prints a raw locale key to the player or tries to translate
    /// someone's guild-house name and prints that key instead.
    /// </summary>
    [Fact]
    public void DisplayName_TranslatesAKeyedRoomName()
    {
        var room = new Room("room.name.lumina_gate", "room.desc.lumina_gate") { IsLiteral = false };

        // Whatever the English file says, it must not still be the raw key.
        Assert.Equal(Localization.T("room.name.lumina_gate"), room.DisplayName());
    }

    [Fact]
    public void DisplayName_LeavesALiteralRoomNameUntouched()
    {
        var room = new Room("Sanctum of the Ninth", "A quiet hall.") { IsLiteral = true };

        Assert.Equal("Sanctum of the Ninth", room.DisplayName());
    }

    [Fact]
    public void DisplayDescription_LeavesALiteralDescriptionUntouched()
    {
        var room = new Room("Guild Hall", "A hall the guild built itself.") { IsLiteral = true };

        Assert.Equal("A hall the guild built itself.", room.DisplayDescription());
    }

    /// <summary>
    /// A literal name that happens to look like a locale key must still be shown verbatim —
    /// IsLiteral is the only thing that decides, never the shape of the string.
    /// </summary>
    [Fact]
    public void DisplayName_DoesNotTranslateALiteralNameThatResemblesAKey()
    {
        var room = new Room("room.name.lumina_gate", "room.desc.lumina_gate") { IsLiteral = true };

        Assert.Equal("room.name.lumina_gate", room.DisplayName());
    }
}
