using Bunit;
using Microsoft.JSInterop;
using Spellbook.Layout;

namespace Spellbook.Tests;

public sealed class PrivacyControlsTests
{
    [Fact]
    public void InitialDisclosureNeverCallsJavaScriptOrOffersUnavailableConsent()
    {
        using var context = new BunitContext();
        var panel = context.Render<PrivacyControls>();
        Assert.Empty(context.JSInterop.Invocations);
        Assert.Equal(2, panel.FindAll("input[disabled]").Count);
        Assert.Contains("does not load Google Analytics or Microsoft Clarity", panel.Markup);
        Assert.Contains("ask again", panel.Markup);
        Assert.Contains("IP address", panel.Markup);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RefusalAndRevocationExplainWhetherLocalPreferenceWasSaved(bool saved)
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<bool>("spellbookPrivacy.revoke").SetResult(saved);
        var panel = context.Render<PrivacyControls>();
        panel.Find("button").Click();
        Assert.Contains(saved ? "choice was saved" : "could not save", panel.Find("[role=status]").TextContent);
        Assert.Equal("spellbookPrivacy.revoke", Assert.Single(context.JSInterop.Invocations).Identifier);
    }

    [Fact]
    public void MissingOrBlockedScriptDoesNotBreakThePage()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<bool>("spellbookPrivacy.revoke").SetException(new JSException("blocked"));
        var panel = context.Render<PrivacyControls>();
        panel.Find("button").Click();
        Assert.Contains("analytics are off", panel.Find("[role=status]").TextContent);
        Assert.Contains("storage is unavailable", panel.Find("[role=status]").TextContent);
    }
}
