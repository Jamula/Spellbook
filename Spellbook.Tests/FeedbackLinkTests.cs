using Bunit;
using Microsoft.AspNetCore.Components;
using Spellbook.Layout;

namespace Spellbook.Tests;

public sealed class FeedbackLinkTests
{
    [Fact]
    public void FeedbackOpensThePublicRepositoryFormWithAnEditableTemplate()
    {
        using var context = new BunitContext();
        var page = context.Render<FeedbackLink>();
        var link = page.Find("a");
        var destination = new Uri(link.GetAttribute("href")!);
        var query = destination.Query.TrimStart('?').Split('&')
            .Select(part => part.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]));

        Assert.Equal("https", destination.Scheme);
        Assert.Equal("github.com", destination.Host);
        Assert.Equal("/Jamula/Spellbook/issues/new", destination.AbsolutePath);
        Assert.Equal(2, query.Count);
        Assert.Equal("[Feedback] Describe your request or bug", query["title"]);
        var body = query["body"].ToString();
        Assert.Contains("## Steps to reproduce", body);
        Assert.Contains("## Expected result", body);
        Assert.Contains("## Actual result", body);
        Assert.Contains("Environment (optional — fill in manually)", body);
        Assert.Contains("This issue will be public", body);
        Assert.Contains("Do not include passwords, tokens, personal information, or private deck data", body);
        Assert.Contains('\n', body);
        Assert.DoesNotContain("\n", link.GetAttribute("href")!);
    }

    [Fact]
    public void LayoutExplainsVisibilityAndDoesNotCollectPageOrBrowserData()
    {
        using var context = new BunitContext();
        var layout = context.Render<MainLayout>(parameters => parameters.Add(p => p.Body,
            (RenderFragment)(builder => builder.AddContent(0, "Private deck: Confidential collection"))));
        var link = layout.Find(".feedback-link");
        var notice = layout.Find("#" + link.GetAttribute("aria-describedby"));

        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Contains("noopener", link.GetAttribute("rel")!);
        Assert.Contains("noreferrer", link.GetAttribute("rel")!);
        Assert.Contains("opens in a new tab", link.GetAttribute("aria-label")!);
        Assert.Contains("GitHub sign-in is required", notice.TextContent);
        Assert.Contains("issues are public", notice.TextContent);
        Assert.Contains("No deck data or device details are included automatically", notice.TextContent);
        Assert.DoesNotContain("Confidential", Uri.UnescapeDataString(link.GetAttribute("href")!));
        Assert.Empty(context.JSInterop.Invocations);
    }
}
