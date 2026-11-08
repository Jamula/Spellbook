using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bunit;
using Microsoft.JSInterop;
using Spellbook.Layout;

namespace Spellbook.Tests;

[TestClass]
public sealed class PrivacyControlsTests
{
    [TestMethod]
    public void InitialDisclosureNeverCallsJavaScriptOrOffersUnavailableConsent()
    {
        using var context = new BunitContext();
        var panel = context.Render<PrivacyControls>();
        TestChecks.Empty(context.JSInterop.Invocations);
        TestChecks.AreEqual(2, panel.FindAll("input[disabled]").Count);
        TestChecks.Contains("does not load Google Analytics or Microsoft Clarity", panel.Markup);
        TestChecks.Contains("ask again", panel.Markup);
        TestChecks.Contains("IP address", panel.Markup);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void RefusalAndRevocationExplainWhetherLocalPreferenceWasSaved(bool saved)
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<bool>("spellbookPrivacy.revoke").SetResult(saved);
        var panel = context.Render<PrivacyControls>();
        panel.Find("button").Click();
        TestChecks.Contains(saved ? "choice was saved" : "could not save", panel.Find("[role=status]").TextContent);
        TestChecks.AreEqual("spellbookPrivacy.revoke", TestChecks.Single(context.JSInterop.Invocations).Identifier);
    }

    [TestMethod]
    public void MissingOrBlockedScriptDoesNotBreakThePage()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<bool>("spellbookPrivacy.revoke").SetException(new JSException("blocked"));
        var panel = context.Render<PrivacyControls>();
        panel.Find("button").Click();
        TestChecks.Contains("analytics are off", panel.Find("[role=status]").TextContent);
        TestChecks.Contains("storage is unavailable", panel.Find("[role=status]").TextContent);
    }
}
