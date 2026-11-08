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
        context.JSInterop.Setup<bool>("spellbookTheme.reset", _ => true).SetResult(true);
        var page = context.Render<ThemePicker>();

        TestChecks.IsTrue(page.FindAll(".theme-option input")[1].HasAttribute("checked"));
        TestChecks.IsTrue(page.FindAll(".theme-option input")[3].HasAttribute("checked"));
        TestChecks.Contains("Current theme: Blue and Red", page.Markup);
        page.Find("button.secondary").Click();

        TestChecks.Single(context.JSInterop.Invocations["spellbookTheme.reset"]);
        TestChecks.IsFalse(page.FindAll(".theme-option input").Any(input => input.HasAttribute("checked")));
        TestChecks.Contains("Using the default Spellbook theme", page.Markup);
    }

    [TestMethod]
    public void UncheckingAColorAllowsAReplacementSelection()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
        context.JSInterop.Setup<bool>("spellbookTheme.save", _ => true).SetResult(true);
        var page = context.Render<ThemePicker>();

        page.FindAll(".theme-option input")[0].Change(true);
        page.FindAll(".theme-option input")[1].Change(true);
        page.FindAll(".theme-option input")[0].Change(false);
        page.FindAll(".theme-option input")[2].Change(true);
        page.Find("button:not(.secondary)").Click();

        var saved = context.JSInterop.Invocations["spellbookTheme.save"].Single();
        TestChecks.AreEqual(new[] { "blue", "black" }, (string[])saved.Arguments[0]!);
    }

    [TestMethod]
    public void BrowserLoadFailureIsReported()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("Storage unavailable"));

        var page = context.Render<ThemePicker>();

        TestChecks.Contains("Theme settings are unavailable in this browser", page.Markup);
    }

    [TestMethod]
    public void ThemeSaveFailureAndExceptionAreReported()
    {
        using (var context = new BunitContext())
        {
            context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
            context.JSInterop.Setup<bool>("spellbookTheme.save", _ => true).SetResult(false);
            var page = context.Render<ThemePicker>();
            page.FindAll(".theme-option input")[0].Change(true);
            page.FindAll(".theme-option input")[1].Change(true);
            page.Find("button:not(.secondary)").Click();
            TestChecks.Contains("Could not save the theme in this browser", page.Markup);
        }

        using (var context = new BunitContext())
        {
            context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
            context.JSInterop.Setup<bool>("spellbookTheme.save", _ => true)
                .SetException(new Microsoft.JSInterop.JSException("Storage unavailable"));
            var page = context.Render<ThemePicker>();
            page.FindAll(".theme-option input")[0].Change(true);
            page.FindAll(".theme-option input")[1].Change(true);
            page.Find("button:not(.secondary)").Click();
            TestChecks.Contains("Could not apply the theme in this browser", page.Markup);
        }
    }

    [TestMethod]
    public void ResetFailureKeepsDefaultThemeAndReportsStorageIssue()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(["blue", "red"]);
        context.JSInterop.Setup<bool>("spellbookTheme.reset", _ => true).SetResult(false);
        var page = context.Render<ThemePicker>();

        page.Find("button.secondary").Click();

        TestChecks.Contains("Using the default theme. Browser storage could not be cleared", page.Markup);
        TestChecks.IsFalse(page.FindAll(".theme-option input").Any(input => input.HasAttribute("checked")));
    }

    [TestMethod]
    public void ResetInteropExceptionIsReported()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(["blue", "red"]);
        context.JSInterop.Setup<bool>("spellbookTheme.reset", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("Theme interop unavailable"));
        var page = context.Render<ThemePicker>();

        page.Find("button.secondary").Click();

        TestChecks.Contains("Using the default theme. Browser storage could not be cleared", page.Markup);
        TestChecks.IsFalse(page.FindAll(".theme-option input").Any(input => input.HasAttribute("checked")));
    }
}
