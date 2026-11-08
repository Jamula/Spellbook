using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Spellbook.Components;
using Spellbook.Models;
using Spellbook.Pages;
using Spellbook.Services;

namespace Spellbook.Tests;

public sealed class DeckListTransferTests
{
    [Fact]
    public void ReviewedImportCreatesNewDeckAndPreservesZonesThroughEditorAndDownloads()
    {
        using var context = new BunitContext();
        context.Services.AddScoped<DeckStore>();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult(null);
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        var page = context.Render<Home>();
        var transfer = page.FindComponent<DeckListTransfer>();
        transfer.Find("textarea").Change("4 Lightning Bolt\nSideboard\n1 Lightning Bolt");
        transfer.Find("[aria-label='Imported deck name']").Change("From Moxfield");
        Button(transfer, "Review deck list").Click();
        Assert.True(Button(transfer, "Import reviewed deck").HasAttribute("disabled"));
        Assert.Equal(2, transfer.FindAll("li").Count);
        Assert.Empty(context.JSInterop.Invocations["spellbookStorage.set"]);
        transfer.Find("input[type=checkbox]").Change(true);
        Button(transfer, "Import reviewed deck").Click();
        var savedJson = Assert.IsType<string>(context.JSInterop.Invocations["spellbookStorage.set"].Last().Arguments[1]);
        var saved = JsonSerializer.Deserialize<List<Deck>>(savedJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("From Moxfield", Assert.Single(saved).Name);
        Assert.Equal(5, saved[0].CardCount);
        Assert.Contains(saved[0].Cards, c => c.Section == "Sideboard");
        page.Find("[aria-label='Increase quantity for Lightning Bolt (Sideboard)']").Click();
        page.Find("[aria-label='Card name']").Change("Lightning Bolt");
        page.FindAll("button").Single(b => b.TextContent == "Add").Click();
        Button(transfer, "Export deck TXT").Click();
        Button(transfer, "Export deck CSV").Click();
        var downloads = context.JSInterop.Invocations["spellbookStorage.download"];
        Assert.Equal("text/plain;charset=utf-8", downloads[0].Arguments[2]);
        Assert.Equal("text/csv;charset=utf-8", downloads[1].Arguments[2]);
        var roundTrip = DeckListCodec.Parse(Assert.IsType<string>(downloads[1].Arguments[1]), "Again");
        Assert.Equal(5, roundTrip.Deck.Cards.Single(c => c.Section == "Main").Quantity);
        Assert.Equal(2, roundTrip.Deck.Cards.Single(c => c.Section == "Sideboard").Quantity);
    }

    [Fact]
    public void FileReviewRejectsInvalidOrOversizedListsAndEditsInvalidateConfirmation()
    {
        using var context = new BunitContext();
        var imported = new List<Deck>();
        var component = context.Render<DeckListTransfer>(ps => ps.Add(p => p.OnImport, deck => imported.Add(deck)));
        Button(component, "Review deck list").Click();
        Assert.Contains("Paste a deck list", component.Markup);
        var file = component.FindComponent<InputFile>();
        file.UploadFiles(InputFileContent.CreateFromText("Quantity,Name,Board\n2,Opt,Sideboard", "archidekt.csv"));
        Assert.Contains("2 × Opt · Sideboard", component.Markup);
        component.Find("input[type=checkbox]").Change(true);
        component.Find("textarea").Change("1 Island");
        Assert.Empty(component.FindAll("li"));
        Button(component, "Review deck list").Click();
        Assert.True(Button(component, "Import reviewed deck").HasAttribute("disabled"));
        component.Find("input[type=checkbox]").Change(true);
        Button(component, "Import reviewed deck").Click();
        Assert.Equal("Island", Assert.Single(imported).Cards[0].Name);
        file.UploadFiles(InputFileContent.CreateFromText(new string('a', 2_000_001), "large.txt"));
        Assert.Contains("smaller than 2 MB", component.Markup);
        Assert.Single(imported);
    }

    [Fact]
    public void FailedImportCanBeRetriedWithoutLosingItsReview()
    {
        using var context = new BunitContext();
        var imported = new List<Deck>();
        var attempts = 0;
        var component = context.Render<DeckListTransfer>(ps => ps.Add(p => p.OnImport,
            EventCallback.Factory.Create<Deck>(this, deck =>
            {
                if (attempts++ == 0) throw new JSException("Quota exceeded");
                imported.Add(deck);
            })));
        component.Find("textarea").Change("1 Island");
        Button(component, "Review deck list").Click();
        component.Find("input[type=checkbox]").Change(true);

        Button(component, "Import reviewed deck").Click();

        Assert.Contains("Could not save the imported deck", component.Markup);
        Assert.Contains("Review: Imported deck", component.Markup);
        Assert.False(Button(component, "Import reviewed deck").HasAttribute("disabled"));
        Assert.Empty(imported);

        Button(component, "Import reviewed deck").Click();

        Assert.Single(imported);
        Assert.Contains("Deck list imported as a new deck.", component.Markup);
        Assert.Empty(component.FindAll("ul[aria-label='Deck list preview']"));
    }

    private static AngleSharp.Dom.IElement Button<T>(IRenderedComponent<T> component, string text) where T : Microsoft.AspNetCore.Components.IComponent =>
        component.FindAll("button").Single(b => b.TextContent == text);
}
