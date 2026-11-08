using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Spellbook.Layout;

namespace Spellbook.Tests;

public sealed class FeedbackLinkTests
{
    private static BunitContext Context(bool configured = true)
    {
        var context = new BunitContext();
        context.JSInterop.Setup<bool>("spellbookFeedback.open", _ => true).SetResult(configured);
        context.JSInterop.SetupVoid("spellbookFeedback.close", _ => true).SetVoidResult();
        return context;
    }

    [Fact]
    public void FeedbackOpensInAppDialogWithoutReadingDecksOrNavigatingToGitHub()
    {
        using var context = Context();
        var page = context.Render<MainLayout>(p => p.Add(x => x.Body,
            (RenderFragment)(builder => builder.AddContent(0, "Private deck: Confidential collection"))));
        page.Find(".feedback-link").Click();
        Assert.Equal("dialog", page.Find(".feedback-dialog").TagName.ToLowerInvariant());
        Assert.Contains("issues are public", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(page.FindAll("a[href*='/issues/new']"));
        Assert.Single(context.JSInterop.Invocations["spellbookFeedback.open"]);
        Assert.DoesNotContain("Confidential collection", page.Find(".feedback-dialog").TextContent);
        var version = page.Find(".app-version").TextContent;
        Assert.Matches(@"^Version \d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", version);
        Assert.DoesNotContain("+", version);
        page.Find(".feedback-actions .secondary").Click();
        Assert.Single(context.JSInterop.Invocations["spellbookFeedback.close"]);
    }

    [Fact]
    public void UnconfiguredServiceDisablesSubmitAndProvidesDirectIssueFallback()
    {
        using var context = Context(configured: false);
        var page = context.Render<FeedbackLink>();
        page.Find(".feedback-link").Click();
        Assert.Contains("In-app feedback is unavailable", page.Markup);
        Assert.Equal("https://github.com/Jamula/Spellbook/issues/new", page.Find("a[href*=\"/issues/new\"]").GetAttribute("href"));
        var submit = page.Find(".feedback-actions button:last-child");
        Assert.True(submit.HasAttribute("disabled"));
        page.Find("#feedback-subject").Input("Hi");
        page.Find("#feedback-details").Input("short");
        Assert.True(page.Find(".feedback-actions button:last-child").HasAttribute("disabled"));
        Assert.Empty(context.JSInterop.Invocations["spellbookFeedback.submit"]);
    }

    [Theory]
    [InlineData("https://github.com/Jamula/Spellbook/issues/123", true)]
    [InlineData("https://example.net/looks-like-an-issue", false)]
    public void FeedbackSubmissionConfirmsOnlyExpectedGitHubIssueLinks(string returnedUrl, bool valid)
    {
        using var context = Context();
        context.JSInterop.Setup<string>("spellbookFeedback.submit", _ => true).SetResult(returnedUrl);
        var page = context.Render<FeedbackLink>();
        page.Find(".feedback-link").Click();
        page.Find("#feedback-subject").Input("Missing card ability");
        page.Find("#feedback-details").Input("Opening a card does not display its abilities yet.");
        Assert.True(page.Find(".feedback-actions button:last-child").HasAttribute("disabled"));
        page.Find(".feedback-consent input").Change(true);
        Assert.False(page.Find(".feedback-actions button:last-child").HasAttribute("disabled"));
        page.Find(".feedback-actions button:last-child").Click();
        if (valid)
        {
            page.WaitForAssertion(() => Assert.Contains("feedback was submitted", page.Markup));
            Assert.Equal(returnedUrl, page.Find("a[href*='/issues/123']").GetAttribute("href"));
        }
        else
        {
            page.WaitForAssertion(() => Assert.Contains("could not be submitted", page.Markup));
            Assert.Empty(page.FindAll("a[href*='/issues/123']"));
        }
        var invocation = Assert.Single(context.JSInterop.Invocations["spellbookFeedback.submit"]);
        Assert.Equal("Bug", invocation.Arguments[0]);
        Assert.Equal("Missing card ability", invocation.Arguments[1]);
        Assert.Equal("Opening a card does not display its abilities yet.", invocation.Arguments[2]);
    }

    [Fact]
    public void FailedBackendKeepsTextAvailableForRetry()
    {
        using var context = Context();
        context.JSInterop.Setup<string>("spellbookFeedback.submit", _ => true)
            .SetException(new JSException("backend unavailable"));
        var page = context.Render<FeedbackLink>();
        page.Find(".feedback-link").Click();
        page.Find("#feedback-subject").Input("Card scan incorrect");
        page.Find("#feedback-details").Input("The printed mana cost was appended to the card title.");
        page.Find(".feedback-consent input").Change(true);
        page.Find(".feedback-actions button:last-child").Click();
        page.WaitForAssertion(() => Assert.Contains("could not be submitted", page.Markup));
        Assert.Equal("Card scan incorrect", page.Find("#feedback-subject").GetAttribute("value"));
        var prior = Assert.Single(context.JSInterop.Invocations["spellbookFeedback.submit"]);
        Assert.Contains("printed mana cost", Assert.IsType<string>(prior.Arguments[2]));
        // Retry with no new typing: the component must retain the edited text.
        page.Find(".feedback-actions button:last-child").Click();
        Assert.Equal(2, context.JSInterop.Invocations["spellbookFeedback.submit"].Count);
        Assert.Equal(prior.Arguments[2], context.JSInterop.Invocations["spellbookFeedback.submit"].Last().Arguments[2]);
    }
}
