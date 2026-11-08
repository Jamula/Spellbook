using Microsoft.VisualStudio.TestTools.UnitTesting;
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

[TestClass]
public sealed class DeckListTransferTests
{
    [TestMethod]
    public void ReviewedImportCreatesNewDeckAndPreservesZonesThroughEditorAndDownloads()
    {
        using var context = new BunitContext();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        var decks = new List<Deck> { new() { Name = "Existing" } };
        var transfer = context.Render<DeckListTransfer>(ps => ps
            .Add(p => p.Decks, decks)
            .Add(p => p.OnImport, deck => decks.Add(deck)));
        transfer.Find("textarea").Change("4 Lightning Bolt\nSideboard\n1 Lightning Bolt");
        transfer.Find("[aria-label='Imported deck name']").Change("From Moxfield");
        Button(transfer, "Review deck list").Click();
        TestChecks.IsTrue(Button(transfer, "Import reviewed deck").HasAttribute("disabled"));
        TestChecks.AreEqual(2, transfer.FindAll("li").Count);
        TestChecks.Empty(context.JSInterop.Invocations["spellbookStorage.set"]);
        transfer.Find("input[type=checkbox]").Change(true);
        Button(transfer, "Import reviewed deck").Click();
        TestChecks.AreEqual(2, decks.Count);
        TestChecks.AreEqual("From Moxfield", decks[1].Name);
        TestChecks.AreEqual(5, decks[1].CardCount);
        TestChecks.Contains(decks[1].Cards, c => c.Section == "Sideboard");
        transfer.Find("[aria-label='Deck to export']").Change("1");
        Button(transfer, "Export deck TXT").Click();
        Button(transfer, "Export deck CSV").Click();
        var downloads = context.JSInterop.Invocations["spellbookStorage.download"];
        TestChecks.AreEqual("text/plain;charset=utf-8", downloads[0].Arguments[2]);
        TestChecks.AreEqual("text/csv;charset=utf-8", downloads[1].Arguments[2]);
        var roundTrip = DeckListCodec.Parse(TestChecks.IsType<string>(downloads[1].Arguments[1]), "Again");
        TestChecks.AreEqual(4, roundTrip.Deck.Cards.Single(c => c.Section == "Main").Quantity);
        TestChecks.AreEqual(1, roundTrip.Deck.Cards.Single(c => c.Section == "Sideboard").Quantity);
    }

    [TestMethod]
    public void FileReviewRejectsInvalidOrOversizedListsAndEditsInvalidateConfirmation()
    {
        using var context = new BunitContext();
        var imported = new List<Deck>();
        var component = context.Render<DeckListTransfer>(ps => ps
            .Add(p => p.Decks, [])
            .Add(p => p.OnImport, deck => imported.Add(deck)));
        Button(component, "Review deck list").Click();
        TestChecks.Contains("Paste a deck list", component.Markup);
        var file = component.FindComponent<InputFile>();
        file.UploadFiles(InputFileContent.CreateFromText("Quantity,Name,Board\n2,Opt,Sideboard", "archidekt.csv"));
        TestChecks.Contains("2 × Opt · Sideboard", component.Markup);
        component.Find("input[type=checkbox]").Change(true);
        component.Find("textarea").Change("1 Island");
        TestChecks.Empty(component.FindAll("li"));
        Button(component, "Review deck list").Click();
        TestChecks.IsTrue(Button(component, "Import reviewed deck").HasAttribute("disabled"));
        component.Find("input[type=checkbox]").Change(true);
        Button(component, "Import reviewed deck").Click();
        TestChecks.AreEqual("Island", TestChecks.Single(imported).Cards[0].Name);
        file.UploadFiles(InputFileContent.CreateFromText(new string('a', 2_000_001), "large.txt"));
        TestChecks.Contains("smaller than 2 MB", component.Markup);
        TestChecks.Single(imported);
    }

    [TestMethod]
    public void FailedImportCanBeRetriedWithoutLosingItsReview()
    {
        using var context = new BunitContext();
        var imported = new List<Deck>();
        var attempts = 0;
        var component = context.Render<DeckListTransfer>(ps => ps
            .Add(p => p.Decks, [])
            .Add(p => p.OnImport, EventCallback.Factory.Create<Deck>(this, deck =>
            {
                if (attempts++ == 0) throw new JSException("Quota exceeded");
                imported.Add(deck);
            })));
        component.Find("textarea").Change("1 Island");
        Button(component, "Review deck list").Click();
        component.Find("input[type=checkbox]").Change(true);

        Button(component, "Import reviewed deck").Click();

        TestChecks.Contains("Could not save the imported deck", component.Markup);
        TestChecks.Contains("Review: Imported deck", component.Markup);
        TestChecks.IsFalse(Button(component, "Import reviewed deck").HasAttribute("disabled"));
        TestChecks.Empty(imported);

        Button(component, "Import reviewed deck").Click();

        TestChecks.Single(imported);
        TestChecks.Contains("Deck list imported as a new deck.", component.Markup);
        TestChecks.Empty(component.FindAll("ul[aria-label='Deck list preview']"));
    }

    private static AngleSharp.Dom.IElement Button<T>(IRenderedComponent<T> component, string text) where T : Microsoft.AspNetCore.Components.IComponent =>
        component.FindAll("button").Single(b => b.TextContent == text);
}
