using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Spellbook.Layout;
using Spellbook.Models;
using Spellbook.Pages;
using Spellbook.Services;

namespace Spellbook.Tests;

public sealed class DeckEditorTests
{
    [Fact]
    public void EditDecksAndCardsThroughTheRenderedInterface()
    {
        using var context = CreateContext();
        var layout = context.Render<MainLayout>(parameters => parameters.Add(p => p.Body,
            (RenderFragment)(builder => { builder.OpenComponent<Home>(0); builder.CloseComponent(); })));
        var page = layout.FindComponent<Home>();
        page.WaitForAssertion(() => Assert.Contains("No decks yet", page.Markup));
        Assert.Contains("Your decks stay in this browser", layout.Markup);

        // Empty names are ignored; surrounding whitespace is trimmed.
        page.Find("[aria-label='New deck name']").Change("   ");
        Button(page, "Create deck").Click();
        Assert.Empty(page.FindAll(".deck-choice"));
        page.Find("[aria-label='New deck name']").Change("  Burn  ");
        Button(page, "Create deck").Click();
        Assert.Equal("Burn", page.Find("h2").TextContent);

        page.Find(".editor label input").Change("Modern burn");
        page.Find("select").Change("Modern");
        var saved = Assert.Single(SavedDecks(context));
        Assert.Equal("Modern burn", saved.Name);
        Assert.Equal("Modern", saved.Format);

        Button(page, "Add").Click();
        Assert.Empty(SavedDecks(context)[0].Cards);
        page.Find("[aria-label='Card name']").Change("Lightning Bolt");
        page.Find("[aria-label='Quantity']").Change("0");
        Button(page, "Add").Click();
        Assert.Empty(SavedDecks(context)[0].Cards);

        page.Find("[aria-label='Quantity']").Change("4");
        page.Find("[aria-label='Card name']").KeyDown("Escape");
        Assert.Empty(page.FindAll(".card-row"));
        page.Find("[aria-label='Card name']").KeyDown("Enter");
        Assert.Equal(4, Assert.Single(SavedDecks(context)[0].Cards).Quantity);

        // Duplicate names merge, and both adding and incrementing honor the cap.
        page.Find("[aria-label='Card name']").Change(" lightning bolt ");
        page.Find("[aria-label='Quantity']").Change("249");
        Button(page, "Add").Click();
        page.Find("[aria-label='Increase quantity for Lightning Bolt']").Click();
        Assert.Equal(250, Assert.Single(SavedDecks(context)[0].Cards).Quantity);
        page.Find("[aria-label='Decrease quantity for Lightning Bolt']").Click();
        Assert.Equal(249, Assert.Single(SavedDecks(context)[0].Cards).Quantity);

        page.Find("[aria-label='Card name']").Change("Island");
        Button(page, "Add").Click();
        page.Find("[aria-label='Decrease quantity for Island']").Click();
        Assert.Single(SavedDecks(context)[0].Cards);
        page.Find("[aria-label='Remove Lightning Bolt']").Click();
        Assert.Equal(0, SavedDecks(context)[0].CardCount);

        page.Find("[aria-label='New deck name']").Change("Second deck");
        Button(page, "Create deck").Click();
        page.FindAll(".deck-choice")[0].Click();
        Assert.Equal("Modern burn", page.Find("h2").TextContent);
        Button(page, "Delete deck").Click();
        Assert.Equal("Second deck", page.Find("h2").TextContent);
        Button(page, "Delete deck").Click();
        Assert.Empty(SavedDecks(context));
        Assert.Contains("No decks yet", page.Markup);
    }

    [Fact]
    public void RestoreImportExportAndMediaSelectionPreserveLocalDeckData()
    {
        var existing = new Deck { Name = "Existing", Cards = [new DeckCard { Name = "Forest", Quantity = 3 }] };
        using var context = CreateContext(JsonSerializer.Serialize(new[] { existing }));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Equal("Existing", page.Find("h2").TextContent));
        Assert.Contains("Main deck · 3 cards", page.Markup);

        var importer = page.FindComponents<InputFile>()[0];
        importer.UploadFiles(InputFileContent.CreateFromText("not json", "invalid.json"));
        Assert.Contains("Import failed:", page.Find("[role='status']").TextContent);
        Assert.Single(page.FindAll(".deck-choice"));
        importer.UploadFiles(InputFileContent.CreateFromText(new string('x', 2_000_001), "large.json"));
        Assert.Contains("Import must be smaller than 2 MB", page.Find("[role='status']").TextContent);

        var imported = new Deck { Name = "Imported", Cards = [new DeckCard { Name = "Island", Quantity = 2 }] };
        importer.UploadFiles(InputFileContent.CreateFromText(JsonSerializer.Serialize(new[] { imported }), "decks.json"));
        page.WaitForAssertion(() => Assert.Equal("Imported", page.Find("h2").TextContent));
        Assert.Contains("Imported 1 deck(s).", page.Markup);
        var saved = SavedDecks(context);
        Assert.Equal(2, saved.Count);
        Assert.Equal(existing.Id, saved[0].Id);
        Assert.NotEqual(imported.Id, saved[1].Id);
        Assert.Equal(2, saved[1].CardCount);

        // Empty imports leave the selection and saved decks intact.
        importer.UploadFiles(InputFileContent.CreateFromText("[]", "empty.json"));
        Assert.Contains("Imported 0 deck(s).", page.Markup);
        Assert.Equal("Imported", page.Find("h2").TextContent);
        Assert.Equal(2, SavedDecks(context).Count);

        page.FindComponents<InputFile>()[1].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "sample.jpg", contentType: "image/jpeg"),
            InputFileContent.CreateFromBinary([3, 4], "sample.mov", contentType: "video/quicktime"));
        Assert.Contains("Selected: sample.jpg, sample.mov", page.Markup);
        page.FindAll(".deck-choice")[0].Click();
        Assert.Equal("Existing", page.Find("h2").TextContent);
        Assert.DoesNotContain("Selected:", page.Markup);
        Assert.DoesNotContain("Imported 0 deck(s).", page.Markup);

        Button(page, "Export JSON").Click();
        var download = Assert.Single(context.JSInterop.Invocations["spellbookStorage.download"]);
        Assert.Equal("spellbook-decks.json", download.Arguments[0]);
        var exported = ReadDecks(Assert.IsType<string>(download.Arguments[1]));
        Assert.Equal(saved.Select(d => d.Id), exported.Select(d => d.Id));
        Assert.Equal(5, exported.Sum(d => d.CardCount));
        Assert.All(context.JSInterop.Invocations,
            call => Assert.Contains(call.Identifier, new[] { "spellbookStorage.get", "spellbookStorage.set",
                "spellbookStorage.download", "Blazor._internal.InputFile.init" }));
    }

    [Fact]
    public void RecoverUnreadableStorageAfterKeepingRawCopyAndConfirmingReplacement()
    {
        using var context = CreateContext("not json");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Saved decks could not be loaded.", page.Markup));

        Button(page, "Download raw saved data").Click();
        var rawDownload = Assert.Single(context.JSInterop.Invocations["spellbookStorage.download"]);
        Assert.Equal("spellbook-recovery-raw.json", rawDownload.Arguments[0]);
        Assert.Equal("not json", rawDownload.Arguments[1]);

        var restore = page.FindComponents<InputFile>().Single();
        Assert.True(restore.Find("input").HasAttribute("disabled"));
        page.Find("input[type=checkbox]").Change(true);

        restore.UploadFiles(InputFileContent.CreateFromText("not json", "invalid.json"));
        Assert.Contains("Recovery failed; original saved data was not intentionally replaced", page.Markup);
        Assert.DoesNotContain(context.JSInterop.Invocations, call => call.Identifier == "spellbookStorage.set");

        var recovered = new Deck { Name = "Recovered", Cards = [new DeckCard { Name = "Island" }] };
        restore.UploadFiles(InputFileContent.CreateFromText(JsonSerializer.Serialize(new[] { recovered }), "backup.json"));

        Assert.Contains("Restored 1 deck(s).", page.Markup);
        Assert.Equal("Recovered", page.Find("h2").TextContent);
        Assert.Equal("Recovered", Assert.Single(SavedDecks(context)).Name);
    }

    [Fact]
    public void LocalPhotoPreviewRequiresUserConfirmationBeforeAddingToDeck()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("No decks yet", page.Markup));
        page.Find("[aria-label='New deck name']").Change("Photo deck");
        Button(page, "Create deck").Click();

        var media = page.FindComponents<InputFile>()[1];
        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3, 4], "card.jpg", contentType: "image/jpeg"));

        Assert.Contains("Selected: card.jpg", page.Markup);
        Assert.Equal("data:image/jpeg;base64,AQIDBA==", page.Find(".scan-preview").GetAttribute("src"));
        Assert.Empty(SavedDecks(context)[0].Cards);

        page.Find("[aria-label='Name from photo']").Change("Lightning Bolt");
        Button(page, "Add confirmed card").Click();
        Assert.Equal("Lightning Bolt", Assert.Single(SavedDecks(context)[0].Cards).Name);
        Assert.Equal(1, Assert.Single(SavedDecks(context)[0].Cards).Quantity);

        Button(page, "Clear photo").Click();
        Assert.Empty(page.FindAll(".scan-preview"));
        Assert.DoesNotContain("Selected: card.jpg", page.Markup);
    }

    [Fact]
    public void PhotoOcrRequiresReviewAndNeverAddsCardWithoutConfirmation()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.recognize", _ => true)
            .SetResult("Lightning Bolt\nInstant\nDeal 3 damage");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("No decks yet", page.Markup));
        page.Find("[aria-label='New deck name']").Change("OCR deck");
        Button(page, "Create deck").Click();
        page.FindComponents<InputFile>()[1].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "bolt.png", contentType: "image/png"));
        Button(page, "Recognize text on device").Click();
        page.WaitForAssertion(() => Assert.Equal("Lightning Bolt", page.Find("[aria-label='Name from photo']").GetAttribute("value")));
        Assert.Contains("Deal 3 damage", page.Markup);
        Assert.Empty(SavedDecks(context)[0].Cards);

        page.Find("[aria-label='Name from photo']").Change(" ");
        Button(page, "Add confirmed card").Click();
        Assert.Contains("Enter a valid card name", page.Markup);
        Assert.Empty(SavedDecks(context)[0].Cards);

        page.Find("[aria-label='Name from photo']").Change("Lightning Bolt");
        Button(page, "Add confirmed card").Click();
        Assert.Equal("Lightning Bolt", Assert.Single(SavedDecks(context)[0].Cards).Name);
    }

    [Fact]
    public void PhotoPickerRejectsUnsupportedOrOversizedMediaWithoutChangingDeck()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("No decks yet", page.Markup));
        page.Find("[aria-label='New deck name']").Change("Media deck");
        Button(page, "Create deck").Click();
        var media = page.FindComponents<InputFile>()[1];

        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "movie.mov", contentType: "video/quicktime"));
        Assert.Contains("No supported image selected", page.Markup);
        Assert.Empty(page.FindAll(".scan-preview"));

        media.UploadFiles(InputFileContent.CreateFromBinary(new byte[6_000_001], "big.jpg", contentType: "image/jpeg"));
        Assert.Contains("Photo exceeds 6 MB", page.Markup);
        Assert.Empty(page.FindAll(".scan-preview"));

        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "valid.webp", contentType: "image/webp"));
        Assert.Single(page.FindAll(".scan-preview"));
        media.UploadFiles(InputFileContent.CreateFromBinary([3, 4], "unsupported.heic", contentType: "image/heic"));
        Assert.Empty(page.FindAll(".scan-preview"));
        Assert.Empty(SavedDecks(context)[0].Cards);
    }

    [Fact]
    public void OcrWithNoUsableTitleAllowsManualCorrection()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.recognize", _ => true).SetResult("\n ");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("No decks yet", page.Markup));
        page.Find("[aria-label='New deck name']").Change("Manual correction");
        Button(page, "Create deck").Click();
        page.FindComponents<InputFile>()[1].UploadFiles(
            InputFileContent.CreateFromBinary([1], "blur.jpg", contentType: "image/jpeg"));
        Button(page, "Recognize text on device").Click();
        page.WaitForAssertion(() => Assert.Contains("Enter the card name yourself", page.Markup));
        Assert.Empty(SavedDecks(context)[0].Cards);
        page.Find("[aria-label='Name from photo']").Change("Island");
        Button(page, "Add confirmed card").Click();
        Assert.Equal("Island", Assert.Single(SavedDecks(context)[0].Cards).Name);
    }

    private static BunitContext CreateContext(string? storedJson = null)
    {
        var context = new BunitContext();
        context.Services.AddScoped<DeckStore>();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult(storedJson);
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        return context;
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<Home> page, string text) =>
        page.FindAll("button").Single(button => button.TextContent == text);

    private static List<Deck> SavedDecks(BunitContext context) =>
        ReadDecks(Assert.IsType<string>(context.JSInterop.Invocations["spellbookStorage.set"].Last().Arguments[1]));

    private static List<Deck> ReadDecks(string json) =>
        JsonSerializer.Deserialize<List<Deck>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
