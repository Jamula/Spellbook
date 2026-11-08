using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using Spellbook.Models;
using Spellbook.Pages;
using Spellbook.Services;

namespace Spellbook.Tests;

[TestClass]
public sealed class VideoScanTests
{
    [TestMethod]
    public void NearbySightingsDeduplicateAndStackEstimateRemainsEditable()
    {
        var candidates = VideoCardCandidateBuilder.Build([
            new(0, "Lightning Bolt\n{R}", null),
            new(.75, "Lightning Bolt\n{R}", 5),
            new(1.5, "Island\nBasic Land", null),
            new(4, "Lightning Bolt\n{R}", 300)
        ]);

        Assert.AreEqual(3, candidates.Count);
        Assert.AreEqual("Lightning Bolt", candidates[0].Name);
        Assert.AreEqual(5, candidates[0].Quantity);
        StringAssert.Contains(candidates[0].EstimateLabel, "low confidence");
        candidates[0].Quantity = 3;
        Assert.AreEqual(3, candidates[0].Quantity);
        Assert.IsFalse(candidates[1].HasStackEstimate);
        StringAssert.Contains(candidates[1].EstimateLabel, "enter quantity manually");
        Assert.AreEqual("Lightning Bolt", candidates[2].Name);
        Assert.AreEqual(1, candidates[2].Quantity);
    }

    [TestMethod]
    public void VideoReviewShowsEstimateAndRequiresExplicitAdd()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<DeckStore>();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult(null);
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.Setup<string>("spellbookMedia.prepareVideo", _ => true).SetResult("blob:video");
        using var scan = JsonDocument.Parse("""{"frames":[{"time":0,"text":"Lightning Bolt\n{R}","stackCountEstimate":4},{"time":0.75,"text":"Lightning Bolt\n{R}","stackCountEstimate":4},{"time":1.5,"text":"Island\nBasic Land","stackCountEstimate":null}],"cancelled":false}""");
        context.JSInterop.Setup<JsonElement>("spellbookMedia.scanVideo", _ => true).SetResult(scan.RootElement);

        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.IsTrue(page.Markup.Contains("No decks yet")));
        page.Find("[aria-label='New deck name']").Change("Video deck");
        page.FindAll("button").Single(b => b.TextContent == "Create deck").Click();
        page.FindComponents<InputFile>()[1].UploadFiles(InputFileContent.CreateFromBinary([1], "scan.mp4", contentType: "video/mp4"));
        page.WaitForAssertion(() => Assert.AreEqual(1, page.FindAll("#card-video-preview").Count));
        var savesBeforeScan = context.JSInterop.Invocations.Count(i => i.Identifier == "spellbookStorage.set");

        page.FindAll("button").Single(b => b.TextContent == "Scan video for cards").Click();
        page.WaitForAssertion(() => Assert.AreEqual(2, page.FindAll(".video-candidate").Count));
        Assert.IsTrue(page.Markup.Contains("Estimated from visible edges: 4"));
        Assert.AreEqual(savesBeforeScan, context.JSInterop.Invocations.Count(i => i.Identifier == "spellbookStorage.set"));

        page.FindAll(".video-candidate input[type=number]")[0].Change("3");
        page.FindAll("button").Single(b => b.TextContent == "Add reviewed cards").Click();
        var saved = JsonSerializer.Deserialize<List<Deck>>(
            (string)context.JSInterop.Invocations.Last(i => i.Identifier == "spellbookStorage.set").Arguments[1]!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.AreEqual(3, saved.Single().Cards.Single(c => c.Name == "Lightning Bolt").Quantity);
        Assert.IsTrue(saved.Single().Cards.Any(c => c.Name == "Island"));
    }
}
