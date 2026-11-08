using Bunit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spellbook.Layout;

namespace Spellbook.Tests;

[TestClass]
public sealed class ThemePickerTests
{
    [TestMethod]
    public void ThemeRequiresTwoDistinctColorsAndSavesThePair()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
        context.JSInterop.Setup<bool>("spellbookTheme.save", _ => true).SetResult(true);
        var page = context.Render<ThemePicker>();

        var choices = page.FindAll(".theme-option input");
        TestChecks.AreEqual(5, choices.Count);
        TestChecks.IsTrue(page.Find("button:not(.secondary)").HasAttribute("disabled"));
        choices[0].Change(true);
        page.FindAll(".theme-option input")[1].Change(true);
        TestChecks.IsFalse(page.Find("button:not(.secondary)").HasAttribute("disabled"));
        TestChecks.IsTrue(page.FindAll(".theme-option input")[2].HasAttribute("disabled"));

        page.Find("button:not(.secondary)").Click();

        var saved = context.JSInterop.Invocations["spellbookTheme.save"].Single();
        TestChecks.AreEqual(new[] { "white", "blue" }, (string[])saved.Arguments[0]!);
        TestChecks.Contains("Theme applied", page.Markup);
    }

    [TestMethod]
    public void StoredThemeIsShownAndCanBeResetToDefault()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(["blue", "red"]);
        context.JSInterop.SetupVoid("spellbookTheme.reset", _ => true).SetVoidResult();
        var page = context.Render<ThemePicker>();

        TestChecks.IsTrue(page.FindAll(".theme-option input")[1].HasAttribute("checked"));
        TestChecks.IsTrue(page.FindAll(".theme-option input")[3].HasAttribute("checked"));
        TestChecks.Contains("Current theme: Blue and Red", page.Markup);
        page.Find("button.secondary").Click();

        TestChecks.Single(context.JSInterop.Invocations["spellbookTheme.reset"]);
        TestChecks.IsFalse(page.FindAll(".theme-option input").Any(input => input.HasAttribute("checked")));
        TestChecks.Contains("Using the default Spellbook theme", page.Markup);
    }
}
