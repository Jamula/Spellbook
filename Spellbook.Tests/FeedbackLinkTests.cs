using Microsoft.VisualStudio.TestTools.UnitTesting;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Spellbook.Layout;

namespace Spellbook.Tests;

[TestClass]
public sealed class FeedbackLinkTests
{
    private static BunitContext Context(bool configured = true)
    {
        var context = new BunitContext();
        context.JSInterop.Setup<bool>("spellbookFeedback.open", _ => true).SetResult(configured);
        context.JSInterop.SetupVoid("spellbookFeedback.close", _ => true).SetVoidResult();
        return context;
    }

    [TestMethod]
    public void FeedbackOpensInAppDialogWithoutReadingDecksOrNavigatingToGitHub()
    {
        using var context = Context();
        var page = context.Render<MainLayout>(p => p.Add(x => x.Body,
            (RenderFragment)(builder => builder.AddContent(0, "Private deck: Confidential collection"))));
        page.Find(".feedback-link").Click();
        TestChecks.AreEqual("dialog", page.Find(".feedback-dialog").TagName.ToLowerInvariant());
        TestChecks.Contains("issues are public", page.Markup, StringComparison.OrdinalIgnoreCase);
        TestChecks.Empty(page.FindAll("a[href*='/issues/new']"));
        TestChecks.Single(context.JSInterop.Invocations["spellbookFeedback.open"]);
        TestChecks.DoesNotContain("Confidential collection", page.Find(".feedback-dialog").TextContent);
        var version = page.Find(".brand-version").TextContent;
        TestChecks.Matches(@"^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", version);
        TestChecks.DoesNotContain("+", version);
        page.Find(".feedback-actions .secondary").Click();
        TestChecks.Single(context.JSInterop.Invocations["spellbookFeedback.close"]);
    }

    [TestMethod]
    public void UnconfiguredServiceDisablesSubmitAndProvidesDirectIssueFallback()
    {
        using var context = Context(configured: false);
        var page = context.Render<FeedbackLink>();
        page.Find(".feedback-link").Click();
        TestChecks.Contains("In-app feedback is unavailable", page.Markup);
        TestChecks.AreEqual("https://github.com/Jamula/Spellbook/issues/new", page.Find("a[href*=\"/issues/new\"]").GetAttribute("href"));
        var submit = page.Find(".feedback-actions button:last-child");
        TestChecks.IsTrue(submit.HasAttribute("disabled"));
        page.Find("#feedback-subject").Input("Hi");
        page.Find("#feedback-details").Input("short");
        TestChecks.IsTrue(page.Find(".feedback-actions button:last-child").HasAttribute("disabled"));
        TestChecks.Empty(context.JSInterop.Invocations["spellbookFeedback.submit"]);
    }

    [TestMethod]
    [DataRow("https://github.com/Jamula/Spellbook/issues/123", true)]
    [DataRow("https://example.net/looks-like-an-issue", false)]
    public void FeedbackSubmissionConfirmsOnlyExpectedGitHubIssueLinks(string returnedUrl, bool valid)
    {
        using var context = Context();
        context.JSInterop.Setup<string>("spellbookFeedback.submit", _ => true).SetResult(returnedUrl);
        var page = context.Render<FeedbackLink>();
        page.Find(".feedback-link").Click();
        page.Find("#feedback-subject").Input("Missing card ability");
        page.Find("#feedback-details").Input("Opening a card does not display its abilities yet.");
        TestChecks.IsTrue(page.Find(".feedback-actions button:last-child").HasAttribute("disabled"));
        page.Find(".feedback-consent input").Change(true);
        TestChecks.IsFalse(page.Find(".feedback-actions button:last-child").HasAttribute("disabled"));
        page.Find(".feedback-actions button:last-child").Click();
        if (valid)
        {
            page.WaitForAssertion(() => TestChecks.Contains("feedback was submitted", page.Markup));
            TestChecks.AreEqual(returnedUrl, page.Find("a[href*='/issues/123']").GetAttribute("href"));
        }
        else
        {
            page.WaitForAssertion(() => TestChecks.Contains("could not be submitted", page.Markup));
            TestChecks.Empty(page.FindAll("a[href*='/issues/123']"));
        }
        var invocation = TestChecks.Single(context.JSInterop.Invocations["spellbookFeedback.submit"]);
        TestChecks.AreEqual("Bug", invocation.Arguments[0]);
        TestChecks.AreEqual("Missing card ability", invocation.Arguments[1]);
        TestChecks.AreEqual("Opening a card does not display its abilities yet.", invocation.Arguments[2]);
    }

    [TestMethod]
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
        page.WaitForAssertion(() => TestChecks.Contains("could not be submitted", page.Markup));
        TestChecks.AreEqual("Card scan incorrect", page.Find("#feedback-subject").GetAttribute("value"));
        var prior = TestChecks.Single(context.JSInterop.Invocations["spellbookFeedback.submit"]);
        TestChecks.Contains("printed mana cost", TestChecks.IsType<string>(prior.Arguments[2]));
        // Retry with no new typing: the component must retain the edited text.
        page.Find(".feedback-actions button:last-child").Click();
        TestChecks.AreEqual(2, context.JSInterop.Invocations["spellbookFeedback.submit"].Count);
        TestChecks.AreEqual(prior.Arguments[2], context.JSInterop.Invocations["spellbookFeedback.submit"].Last().Arguments[2]);
    }
}
