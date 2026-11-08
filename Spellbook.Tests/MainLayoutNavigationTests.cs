using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spellbook.Layout;

namespace Spellbook.Tests;

[TestClass]
public sealed class MainLayoutNavigationTests
{
    [TestMethod]
    public void NavigationMenuOpensClosesAndContainsCurrentAppSections()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var page = context.Render<MainLayout>(p => p.Add(x => x.Body,
            (RenderFragment)(builder => builder.AddContent(0, "Current page"))));

        var toggle = page.Find(".navigation-toggle");
        var panel = page.Find("#app-navigation");
        TestChecks.AreEqual("false", toggle.GetAttribute("aria-expanded"));
        TestChecks.IsTrue(panel.HasAttribute("hidden"));
        TestChecks.AreEqual(2, panel.QuerySelectorAll("nav a").Length);
        TestChecks.AreEqual("Decks", panel.QuerySelectorAll("nav a")[0].TextContent.Trim());
        TestChecks.AreEqual("Settings", panel.QuerySelectorAll("nav a")[1].TextContent.Trim());

        toggle.Click();
        TestChecks.AreEqual("true", page.Find(".navigation-toggle").GetAttribute("aria-expanded"));
        TestChecks.IsFalse(page.Find("#app-navigation").HasAttribute("hidden"));
        TestChecks.AreEqual("Main navigation", page.Find("#app-navigation nav").GetAttribute("aria-label"));

        page.Find("#app-navigation nav a[href='/settings']").Click();
        TestChecks.AreEqual("false", page.Find(".navigation-toggle").GetAttribute("aria-expanded"));
        TestChecks.IsTrue(page.Find("#app-navigation").HasAttribute("hidden"));
    }

    [TestMethod]
    public void EscapeClosesNavigationDrawer()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var page = context.Render<MainLayout>(p => p.Add(x => x.Body,
            (RenderFragment)(builder => builder.AddContent(0, "Current page"))));

        page.Find(".navigation-toggle").Click();
        page.Find(".navigation-toggle").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        TestChecks.IsTrue(page.Find("#app-navigation").HasAttribute("hidden"));
        TestChecks.AreEqual("false", page.Find(".navigation-toggle").GetAttribute("aria-expanded"));
    }
}
