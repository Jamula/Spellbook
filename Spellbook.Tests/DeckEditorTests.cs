using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Spellbook.Layout;
using Spellbook.Models;
using Spellbook.Pages;
using Spellbook.Services;

namespace Spellbook.Tests;

public sealed class DeckEditorTests
{
    [Fact]
    public void EditDeckAndCardsThroughTheRenderedInterface()
    {
        using var context = CreateContext();
        var layout = context.Render<MainLayout>(parameters => parameters.Add(p => p.Body,
            (RenderFragment)(builder => { builder.OpenComponent<Home>(0); builder.CloseComponent(); })));
        var page = layout.FindComponent<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        Assert.Contains("Your decks stay in this browser", layout.Markup);

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

        Button(page, "Delete deck").Click();
        Assert.Empty(SavedDecks(context));
        Assert.Equal("/", new Uri(context.Services.GetRequiredService<NavigationManager>().Uri).AbsolutePath);
    }

    [Fact]
    public void RestoreImportExportAndMediaSelectionPreserveLocalDeckData()
    {
        var existing = new Deck { Name = "Existing", Cards = [new DeckCard { Name = "Forest", Quantity = 3 }] };
        using var context = CreateContext(JsonSerializer.Serialize(new[] { existing }));
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => Assert.Contains("Existing", page.Markup));

        var importer = page.FindComponent<InputFile>();
        importer.UploadFiles(InputFileContent.CreateFromText("not json", "invalid.json"));
        Assert.Contains("Import failed:", page.Find("[role='status']").TextContent);
        Assert.Single(page.FindAll(".deck-tile"));
        importer.UploadFiles(InputFileContent.CreateFromText(new string('x', 2_000_001), "large.json"));
        Assert.Contains("Import must be smaller than 2 MB", page.Find("[role='status']").TextContent);

        var imported = new Deck { Name = "Imported", Cards = [new DeckCard { Name = "Island", Quantity = 2 }] };
        importer.UploadFiles(InputFileContent.CreateFromText(JsonSerializer.Serialize(new[] { imported }), "decks.json"));
        page.WaitForAssertion(() => Assert.Contains("Imported", page.Markup));
        Assert.Contains("Imported 1 deck(s).", page.Markup);
        var saved = SavedDecks(context);
        Assert.Equal(2, saved.Count);
        Assert.Equal(existing.Id, saved[0].Id);
        Assert.NotEqual(imported.Id, saved[1].Id);
        Assert.Equal(2, saved[1].CardCount);

        importer.UploadFiles(InputFileContent.CreateFromText("[]", "empty.json"));
        Assert.Contains("Imported 0 deck(s).", page.Markup);
        Assert.Equal(2, SavedDecks(context).Count);

        page.FindAll("button").Single(button => button.TextContent == "Export JSON").Click();
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
    public void FailedDeckListSaveDoesNotChangeTheLiveCollection()
    {
        var existing = new Deck { Name = "Existing" };
        using var context = CreateContext(JsonSerializer.Serialize(new[] { existing }));
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true)
            .SetException(new JSException("Quota exceeded"));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Equal("Existing", page.Find("h2").TextContent));
        var transfer = page.FindComponent<Spellbook.Components.DeckListTransfer>();
        transfer.Find("textarea").Change("1 Island");
        transfer.FindAll("button").Single(button => button.TextContent == "Review deck list").Click();
        transfer.Find("input[type=checkbox]").Change(true);

        transfer.FindAll("button").Single(button => button.TextContent == "Import reviewed deck").Click();

        Assert.Contains("Could not save the imported deck", transfer.Markup);
        Assert.Contains("Review: Imported deck", transfer.Markup);
        Assert.False(transfer.FindAll("button").Single(button => button.TextContent == "Import reviewed deck").HasAttribute("disabled"));
        Assert.Equal("Existing", page.Find("h2").TextContent);
        Assert.Single(page.FindAll(".editor"));
        var attemptedSave = JsonSerializer.Deserialize<List<Deck>>(
            Assert.IsType<string>(Assert.Single(context.JSInterop.Invocations["spellbookStorage.set"]).Arguments[1]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(["Existing", "Imported deck"], attemptedSave.Select(deck => deck.Name));
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
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));

        var media = page.FindComponents<InputFile>()[0];
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
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
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
    public void NativeVideoFrameAndHeicFlowRequireExplicitConfirmation()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.prepareVideo", _ => true).SetResult("blob:local-video");
        context.JSInterop.Setup<string>("spellbookMedia.captureFrame", _ => true)
            .SetResult("data:image/jpeg;base64,AQID");
        context.JSInterop.Setup<string>("spellbookMedia.convertHeic", _ => true)
            .SetResult("data:image/jpeg;base64,BAUG");
        context.JSInterop.SetupVoid("spellbookMedia.releaseVideo", _ => true).SetVoidResult();

        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        var media = page.FindComponents<InputFile>()[0];

        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "movie.mov", contentType: "video/quicktime"));
        page.WaitForAssertion(() => Assert.Equal("blob:local-video",
            page.Find("#card-video-preview").GetAttribute("src")));
        Assert.Empty(page.FindAll(".scan-preview"));
        Button(page, "Capture selected video frame").Click();
        page.WaitForAssertion(() => Assert.Equal("data:image/jpeg;base64,AQID",
            page.Find(".scan-preview").GetAttribute("src")));
        Assert.Empty(SavedDecks(context)[0].Cards);

        media.UploadFiles(InputFileContent.CreateFromBinary([3, 4], "photo.heic", contentType: "image/heic"));
        page.WaitForAssertion(() => Assert.Equal("data:image/jpeg;base64,BAUG",
            page.Find(".scan-preview").GetAttribute("src")));
        Assert.Empty(page.FindAll("#card-video-preview"));
        Assert.Empty(SavedDecks(context)[0].Cards);
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Add confirmed card").Click();
        Assert.Equal("Avacyn, Angel of Hope", Assert.Single(SavedDecks(context)[0].Cards).Name);
    }

    [Fact]
    public void PhotoPickerRejectsUnsupportedOrOversizedMediaWithoutChangingDeck()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        var media = page.FindComponents<InputFile>()[0];

        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "unsupported.xyz", contentType: "application/octet-stream"));
        Assert.Contains("Select a JPEG, PNG", page.Markup);
        Assert.Empty(page.FindAll(".scan-preview"));

        media.UploadFiles(InputFileContent.CreateFromBinary(new byte[6_000_001], "big.jpg", contentType: "image/jpeg"));
        Assert.Contains("Photo exceeds 6 MB", page.Markup);
        Assert.Empty(page.FindAll(".scan-preview"));

        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "valid.webp", contentType: "image/webp"));
        Assert.Single(page.FindAll(".scan-preview"));
        media.UploadFiles(InputFileContent.CreateFromBinary(new byte[20_000_001], "oversize.heic", contentType: "image/heic"));
        Assert.Contains("HEIC exceeds the 20 MB", page.Markup);
        Assert.Empty(page.FindAll(".scan-preview"));

        // Unused oversized HEIC must not reject a separately selected valid JPEG.
        media.UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "valid.jpg", contentType: "image/jpeg"),
            InputFileContent.CreateFromBinary(new byte[20_000_001], "unused.heic", contentType: "image/heic"));
        page.WaitForAssertion(() => Assert.Equal("data:image/jpeg;base64,AQI=",
            page.Find(".scan-preview").GetAttribute("src")));
        Assert.Empty(SavedDecks(context)[0].Cards);
    }

    [Fact]
    public void InvalidHeicConversionAndVideoPrepareLeaveLocalDeckUnchanged()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.convertHeic", _ => true).SetResult("not-jpeg");
        context.JSInterop.Setup<string>("spellbookMedia.prepareVideo", _ => true).SetResult("not-blob");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        var picker = page.FindComponents<InputFile>()[0];

        picker.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "card.heic", contentType: "image/heic"));
        page.WaitForAssertion(() => Assert.Contains("Media could not be processed locally", page.Markup));
        Assert.Empty(page.FindAll(".scan-preview"));

        picker.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "movie.mov", contentType: "video/quicktime"));
        page.WaitForAssertion(() => Assert.Contains("Media could not be processed locally", page.Markup));
        Assert.Empty(page.FindAll("#card-video-preview"));
        Assert.Empty(SavedDecks(context)[0].Cards);
    }

    [Fact]
    public void InvalidVideoFrameCaptureCanBeRetriedWithoutAddingCard()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.prepareVideo", _ => true).SetResult("blob:review");
        context.JSInterop.Setup<string>("spellbookMedia.captureFrame", _ => true).SetResult("not-jpeg");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "movie.mov", contentType: "video/quicktime"));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#card-video-preview")));
        Button(page, "Capture selected video frame").Click();
        page.WaitForAssertion(() => Assert.Contains("Could not capture this frame", page.Markup));
        Assert.Empty(page.FindAll(".scan-preview"));
        Assert.Empty(SavedDecks(context)[0].Cards);
    }

    [Fact]
    public void OcrWithNoUsableTitleAllowsManualCorrection()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.recognize", _ => true).SetResult("\n ");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1], "blur.jpg", contentType: "image/jpeg"));
        Button(page, "Recognize text on device").Click();
        page.WaitForAssertion(() => Assert.Contains("Enter the card name yourself", page.Markup));
        Assert.Empty(SavedDecks(context)[0].Cards);
        page.Find("[aria-label='Name from photo']").Change("Island");
        Button(page, "Add confirmed card").Click();
        Assert.Equal("Island", Assert.Single(SavedDecks(context)[0].Cards).Name);
    }

    [Fact]
    public void SwitchingDecksByCreationDeletionOrImportDiscardsPendingPhoto()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        var picker = page.FindComponents<InputFile>()[0];
        picker.UploadFiles(InputFileContent.CreateFromBinary([1], "initial.jpg", contentType: "image/jpeg"));
        Assert.Single(page.FindAll(".scan-preview"));

        Button(page, "Delete deck").Click();
        Assert.Empty(page.FindAll(".scan-preview"));
        Assert.Empty(SavedDecks(context));
    }

    [Fact]
    public async Task ScryfallAutocompleteProvidesSuggestionsAndManualEntryRemainsPossible()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string[]>("spellbookScryfall.autocomplete", _ => true)
            .SetResult(["Lightning Bolt", "Lightning Helix"]);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));

        await page.InvokeAsync(() => page.Find("[aria-label='Card name']").Input("Lightn"));
        page.WaitForAssertion(() => Assert.Contains("Lightning Bolt", page.Find(".card-suggestions").TextContent),
            timeout: TimeSpan.FromSeconds(4));
        Assert.Empty(SavedDecks(context)[0].Cards);
        await page.InvokeAsync(() => page.Find("[aria-label='Card name']").Change("Lightning Bolt"));
        page.WaitForAssertion(() => Assert.Equal("Lightning Bolt", page.Find("[aria-label='Card name']").GetAttribute("value")));
        await page.InvokeAsync(() => Button(page, "Add").Click());
        page.WaitForAssertion(() => Assert.Equal("Lightning Bolt", Assert.Single(SavedDecks(context)[0].Cards).Name));

        await page.InvokeAsync(() => page.Find("[aria-label='Card name']").Change("My custom offline card"));
        Button(page, "Add").Click();
        page.WaitForAssertion(() => Assert.Contains(SavedDecks(context)[0].Cards, card => card.Name == "My custom offline card"));
    }


    [Fact]
    public void ScryfallLookupFailureRetainsManualEntry()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string[]>("spellbookScryfall.autocomplete", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("Network unavailable"));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.Find("[aria-label='Card name']").Input("Lightning");
        page.WaitForAssertion(() => Assert.Contains("Scryfall is unavailable", page.Markup),
            timeout: TimeSpan.FromSeconds(4));
        Assert.Empty(page.FindAll(".card-suggestions"));

        page.Find("[aria-label='Card name']").Change("Custom card");
        Button(page, "Add").Click();
        page.WaitForAssertion(() => Assert.Equal("Custom card", Assert.Single(SavedDecks(context)[0].Cards).Name));
    }

    [Fact]
    public void OcrFailureCanBeRecoveredByManualCardName()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.recognize", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("OCR worker failed"));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "ocr-failure.jpg", contentType: "image/jpeg"));
        Button(page, "Recognize text on device").Click();
        page.WaitForAssertion(() => Assert.Contains("On-device OCR is unavailable", page.Markup));
        Assert.Empty(SavedDecks(context)[0].Cards);

        page.Find("[aria-label='Name from photo']").Change("Forest");
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => Assert.Equal("Forest", Assert.Single(SavedDecks(context)[0].Cards).Name));
    }

    [Fact]
    public void ExactScryfallLookupWithoutMatchNeverAutomaticallyAddsCard()
    {
        using var context = CreateContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "unknown-card.jpg", contentType: "image/jpeg"));
        page.Find("[aria-label='Name from photo']").Change("Not a known MTG card");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => Assert.Contains("No exact Scryfall match", page.Markup));
        Assert.Empty(SavedDecks(context)[0].Cards);
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => Assert.Equal("Not a known MTG card", Assert.Single(SavedDecks(context)[0].Cards).Name));
    }

    [Fact]
    public void InvalidDeckRenameCannotOverwriteSavedName()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.Find(".editor label input").Change("Original");
        page.Find(".editor label input").Change(" ");
        Assert.Contains("previous name was restored", page.Markup);
        Assert.Equal("Original", Assert.Single(SavedDecks(context)).Name);

        page.Find(".editor label input").Change(new string('X', 161));
        Assert.Equal("Original", Assert.Single(SavedDecks(context)).Name);
    }


    [Fact]
    public void CanonicalAvacynLookupRequiresExplicitConfirmationBeforeSaving()
    {
        // Canonical reference supplied for MVP acceptance:
        // https://scryfall.com/card/avr/6/avacyn-angel-of-hope
        using var context = CreateContext();
        context.JSInterop.Setup<ScryfallCard?>("spellbookScryfall.exact", _ => true)
            .SetResult(new ScryfallCard
            {
                Name = "Avacyn, Angel of Hope",
                ManaValue = 8,
                TypeLine = "Legendary Creature — Angel",
                ImageUrl = "https://cards.scryfall.io/normal/front/a/v/avacyn-reference.jpg"
            });
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "avacyn.jpg", contentType: "image/jpeg"));
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => Assert.Contains("Found on Scryfall", page.Markup));
        Assert.Contains("mana value 8", page.Markup);
        Assert.Contains("Legendary Creature", page.Markup);
        Assert.Single(page.FindAll(".verified-card img"));
        Assert.Empty(SavedDecks(context)[0].Cards);
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => Assert.Equal(
            "Avacyn, Angel of Hope", Assert.Single(SavedDecks(context)[0].Cards).Name));
    }

    [Fact]
    public void ScryfallVerificationExceptionAllowsManualConfirmationWithoutAutoSaving()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<ScryfallCard?>("spellbookScryfall.exact", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("Scryfall offline"));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1], "offline.jpg", contentType: "image/jpeg"));
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => Assert.Contains("Card lookup unavailable", page.Markup));
        Assert.Empty(SavedDecks(context)[0].Cards);
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => Assert.Equal(
            "Avacyn, Angel of Hope", Assert.Single(SavedDecks(context)[0].Cards).Name));
    }

    [Fact]
    public void EditingScannedCardNameInvalidatesPreviousVerification()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<ScryfallCard?>("spellbookScryfall.exact", _ => true)
            .SetResult(new ScryfallCard { Name = "Avacyn, Angel of Hope", ManaValue = 8 });
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1], "card.png", contentType: "image/png"));
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => Assert.Contains("Found on Scryfall", page.Markup));
        page.Find("[aria-label='Name from photo']").Input("Island");
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".verified-card")));
        page.Find("[aria-label='Name from photo']").Change("Island");
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => Assert.Equal("Island", Assert.Single(SavedDecks(context)[0].Cards).Name));
    }


    [Fact]
    public void ShortScryfallQueriesAreNotSentAndEmptySearchesKeepManualEntry()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string[]>("spellbookScryfall.autocomplete", _ => true)
            .SetResult([]);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        page.Find("[aria-label='Card name']").Input("A");
        Assert.DoesNotContain(context.JSInterop.Invocations,
            i => i.Identifier == "spellbookScryfall.autocomplete");

        page.Find("[aria-label='Card name']").Input("Avacy");
        page.WaitForAssertion(() => Assert.Contains("No suggestions", page.Markup),
            timeout: TimeSpan.FromSeconds(4));
        Assert.Empty(page.FindAll(".card-suggestions"));

        page.Find("[aria-label='Card name']").Change("Avacyn, Angel of Hope");
        Button(page, "Add").Click();
        page.WaitForAssertion(() => Assert.Equal(
            "Avacyn, Angel of Hope", Assert.Single(SavedDecks(context)[0].Cards).Name));
    }


    [Fact]
    public async Task ScryfallSuggestionClickChoosesCanonicalName()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string[]>("spellbookScryfall.autocomplete", _ => true)
            .SetResult(["Avacyn, Angel of Hope"]);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        await page.InvokeAsync(() => page.Find("[aria-label='Card name']").Input("Avacy"));
        page.WaitForAssertion(() => Assert.Contains("Avacyn, Angel of Hope", page.Find(".card-suggestions").TextContent),
            timeout: TimeSpan.FromSeconds(4));

        await page.InvokeAsync(() => Button(page, "Avacyn, Angel of Hope").Click());
        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindAll(".card-suggestions"));
            Assert.Equal("Avacyn, Angel of Hope", page.Find("[aria-label='Card name']").GetAttribute("value"));
        });
        await page.InvokeAsync(() => Button(page, "Add").Click());
        page.WaitForAssertion(() => Assert.Equal("Avacyn, Angel of Hope", Assert.Single(SavedDecks(context)[0].Cards).Name));
    }

    [Theory]
    [InlineData("Avacyn, Angel of Hope 5WWW\\nLegendary Creature", "Avacyn, Angel of Hope")]
    [InlineData("Lightning Bolt {R}\\nInstant", "Lightning Bolt")]
    [InlineData("Niv-Mizzet, Parun UURRR", "Niv-Mizzet, Parun")]
    [InlineData("Seven Dwarves", "Seven Dwarves")]
    [InlineData("2020 Vision", "2020 Vision")]
    public void OcrTitleExtractorRemovesRecognizableManaSuffixOnly(string recognized, string expected)
    {
        Assert.Equal(expected, CardTitleExtractor.FromOcr(recognized.Replace("\\n", "\n")));
    }

    [Fact]
    public void ScanIsDistinctAndVerifiedCardLinksToCanonicalScryfall()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<ScryfallCard?>("spellbookScryfall.exact", _ => true)
            .SetResult(new ScryfallCard
            {
                Name = "Avacyn, Angel of Hope",
                ScryfallUrl = "https://scryfall.com/card/avr/6/avacyn-angel-of-hope",
                ManaValue = 8
            });
        var page = context.Render<Home>();
        page.WaitForAssertion(() => Assert.Contains("Scan a card", page.Markup));
        Assert.Single(page.FindAll("section.card-scan-panel[aria-label='Scan a card']"));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1], "card.png", contentType: "image/png"));
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => Assert.Equal(
            "https://scryfall.com/card/avr/6/avacyn-angel-of-hope",
            page.Find(".verified-card a").GetAttribute("href")));
        Button(page, "Add confirmed card").Click();
        Assert.Contains("scryfall.com/search?q=", page.Find(".card-row a").GetAttribute("href"));
    }

    private static BunitContext CreateContext(string? storedJson = null)
    {
        var context = new BunitContext();
        context.Services.AddScoped<DeckStore>();
        storedJson ??= JsonSerializer.Serialize(new[] { new Deck { Name = "Test deck" } });
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult(storedJson);
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        return context;
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<Home> page, string text) =>
        page.FindAll("button").Single(button => button.TextContent == text);

    private static List<Deck> SavedDecks(BunitContext context)
    {
        var saves = context.JSInterop.Invocations["spellbookStorage.set"];
        return saves.Count == 0
            ? [new Deck { Name = "Test deck" }]
            : ReadDecks(Assert.IsType<string>(saves.Last().Arguments[1]));
    }

    private static List<Deck> ReadDecks(string json) =>
        JsonSerializer.Deserialize<List<Deck>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
