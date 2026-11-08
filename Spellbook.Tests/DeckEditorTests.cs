using Microsoft.VisualStudio.TestTools.UnitTesting;
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

[TestClass]
public sealed class DeckEditorTests
{
    [TestMethod]
    public void EditDecksAndCardsThroughTheRenderedInterface()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        TestChecks.Contains("Edit one deck", page.Markup);

        page.Find(".editor label input").Change("Modern burn");
        page.Find("select").Change("Modern");
        TestChecks.AreEqual("Modern burn", TestChecks.Single(SavedDecks(context)).Name);
        TestChecks.AreEqual("Modern", TestChecks.Single(SavedDecks(context)).Format);

        page.Find("[aria-label='Quantity']").Change("0");
        Button(page, "Add").Click();
        TestChecks.Empty(TestChecks.Single(SavedDecks(context)).Cards);

        page.Find("[aria-label='Card name']").Change("Lightning Bolt");
        page.Find("[aria-label='Quantity']").Change("4");
        page.Find("[aria-label='Card name']").KeyDown("Enter");
        TestChecks.AreEqual(4, TestChecks.Single(SavedDecks(context)[0].Cards).Quantity);

        page.Find("[aria-label='Card name']").Change(" lightning bolt ");
        page.Find("[aria-label='Quantity']").Change("249");
        Button(page, "Add").Click();
        page.Find("[aria-label='Increase quantity for Lightning Bolt']").Click();
        TestChecks.AreEqual(250, TestChecks.Single(SavedDecks(context)[0].Cards).Quantity);
        page.Find("[aria-label='Decrease quantity for Lightning Bolt']").Click();
        TestChecks.AreEqual(249, TestChecks.Single(SavedDecks(context)[0].Cards).Quantity);

        page.Find("[aria-label='Card name']").Change("Island");
        Button(page, "Add").Click();
        page.Find("[aria-label='Remove Lightning Bolt']").Click();
        TestChecks.AreEqual(1, TestChecks.Single(SavedDecks(context)).CardCount);
    }

    [TestMethod]
    public void FailedDeckListSaveDoesNotChangeTheLiveCollection()
    {
        var existing = new Deck { Name = "Existing" };
        using var context = CreateContext(JsonSerializer.Serialize(new[] { existing }));
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true)
            .SetException(new JSException("Quota exceeded"));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.AreEqual("Existing", page.Find("h2").TextContent));
        var transfer = page.FindComponent<Spellbook.Components.DeckListTransfer>();
        transfer.Find("textarea").Change("1 Island");
        transfer.FindAll("button").Single(button => button.TextContent == "Review deck list").Click();
        transfer.Find("input[type=checkbox]").Change(true);

        transfer.FindAll("button").Single(button => button.TextContent == "Import reviewed deck").Click();

        TestChecks.Contains("Could not save the imported deck", transfer.Markup);
        TestChecks.Contains("Review: Imported deck", transfer.Markup);
        TestChecks.IsFalse(transfer.FindAll("button").Single(button => button.TextContent == "Import reviewed deck").HasAttribute("disabled"));
        TestChecks.AreEqual("Existing", page.Find("h2").TextContent);
        var attemptedSave = JsonSerializer.Deserialize<List<Deck>>(
            TestChecks.IsType<string>(TestChecks.Single(context.JSInterop.Invocations["spellbookStorage.set"]).Arguments[1]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        TestChecks.AreEqual(["Existing", "Imported deck"], attemptedSave.Select(deck => deck.Name));
    }

    [TestMethod]
    public void EmptyCollectionCanImportDeckListAndOpensImportedDeck()
    {
        using var context = CreateContext("[]");
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => TestChecks.Contains("No decks yet", page.Markup));

        var transfer = page.FindComponent<Spellbook.Components.DeckListTransfer>();
        transfer.Find("textarea").Change("4 Lightning Bolt");
        transfer.FindAll("button").Single(button => button.TextContent == "Review deck list").Click();
        transfer.Find("input[type=checkbox]").Change(true);
        transfer.FindAll("button").Single(button => button.TextContent == "Import reviewed deck").Click();

        var imported = TestChecks.Single(SavedDecks(context));
        TestChecks.AreEqual("Imported deck", imported.Name);
        TestChecks.AreEqual(4, TestChecks.Single(imported.Cards).Quantity);
        TestChecks.Contains($"/deck/{imported.Id}", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [TestMethod]
    public void RecoverUnreadableStorageAfterKeepingRawCopyAndConfirmingReplacement()
    {
        using var context = CreateContext("not json");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Saved decks could not be loaded.", page.Markup));

        Button(page, "Download raw saved data").Click();
        var rawDownload = TestChecks.Single(context.JSInterop.Invocations["spellbookStorage.download"]);
        TestChecks.AreEqual("spellbook-recovery-raw.json", rawDownload.Arguments[0]);
        TestChecks.AreEqual("not json", rawDownload.Arguments[1]);

        var restore = page.FindComponents<InputFile>().Single();
        TestChecks.IsTrue(restore.Find("input").HasAttribute("disabled"));
        page.Find("input[type=checkbox]").Change(true);

        restore.UploadFiles(InputFileContent.CreateFromText("not json", "invalid.json"));
        TestChecks.Contains("Recovery failed; original saved data was not intentionally replaced", page.Markup);
        TestChecks.DoesNotContain(context.JSInterop.Invocations.Select(call => call.Identifier), "spellbookStorage.set");

        var recovered = new Deck { Name = "Recovered", Cards = [new DeckCard { Name = "Island" }] };
        restore.UploadFiles(InputFileContent.CreateFromText(JsonSerializer.Serialize(new[] { recovered }), "backup.json"));

        TestChecks.Contains("Restored 1 deck(s).", page.Markup);
        TestChecks.AreEqual("Recovered", page.Find("h2").TextContent);
        TestChecks.AreEqual("Recovered", TestChecks.Single(SavedDecks(context)).Name);
    }

    [TestMethod]
    public void LocalPhotoPreviewRequiresUserConfirmationBeforeAddingToDeck()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));

        var media = page.FindComponents<InputFile>()[0];
        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3, 4], "card.jpg", contentType: "image/jpeg"));

        TestChecks.Contains("Selected: card.jpg", page.Markup);
        TestChecks.AreEqual("data:image/jpeg;base64,AQIDBA==", page.Find(".scan-preview").GetAttribute("src"));
        TestChecks.Empty(SavedDecks(context)[0].Cards);

        page.Find("[aria-label='Name from photo']").Change("Lightning Bolt");
        Button(page, "Add confirmed card").Click();
        TestChecks.AreEqual("Lightning Bolt", TestChecks.Single(SavedDecks(context)[0].Cards).Name);
        TestChecks.AreEqual(1, TestChecks.Single(SavedDecks(context)[0].Cards).Quantity);

        Button(page, "Clear photo").Click();
        TestChecks.Empty(page.FindAll(".scan-preview"));
        TestChecks.DoesNotContain("Selected: card.jpg", page.Markup);
    }

    [TestMethod]
    public void PhotoOcrRequiresReviewAndNeverAddsCardWithoutConfirmation()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.recognize", _ => true)
            .SetResult("Lightning Bolt\nInstant\nDeal 3 damage");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "bolt.png", contentType: "image/png"));
        Button(page, "Recognize text on device").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual("Lightning Bolt", page.Find("[aria-label='Name from photo']").GetAttribute("value")));
        TestChecks.Contains("Deal 3 damage", page.Markup);
        TestChecks.Empty(SavedDecks(context)[0].Cards);

        page.Find("[aria-label='Name from photo']").Change(" ");
        Button(page, "Add confirmed card").Click();
        TestChecks.Contains("Enter a valid card name", page.Markup);
        TestChecks.Empty(SavedDecks(context)[0].Cards);

        page.Find("[aria-label='Name from photo']").Change("Lightning Bolt");
        Button(page, "Add confirmed card").Click();
        TestChecks.AreEqual("Lightning Bolt", TestChecks.Single(SavedDecks(context)[0].Cards).Name);
    }

    [TestMethod]
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
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        var media = page.FindComponents<InputFile>()[0];

        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "movie.mov", contentType: "video/quicktime"));
        page.WaitForAssertion(() => TestChecks.AreEqual("blob:local-video",
            page.Find("#card-video-preview").GetAttribute("src")));
        TestChecks.Empty(page.FindAll(".scan-preview"));
        Button(page, "Capture selected video frame").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual("data:image/jpeg;base64,AQID",
            page.Find(".scan-preview").GetAttribute("src")));
        TestChecks.Empty(SavedDecks(context)[0].Cards);

        media.UploadFiles(InputFileContent.CreateFromBinary([3, 4], "photo.heic", contentType: "image/heic"));
        page.WaitForAssertion(() => TestChecks.AreEqual("data:image/jpeg;base64,BAUG",
            page.Find(".scan-preview").GetAttribute("src")));
        TestChecks.Empty(page.FindAll("#card-video-preview"));
        TestChecks.Empty(SavedDecks(context)[0].Cards);
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Add confirmed card").Click();
        TestChecks.AreEqual("Avacyn, Angel of Hope", TestChecks.Single(SavedDecks(context)[0].Cards).Name);
    }

    [TestMethod]
    public void AutomaticVideoScanDeduplicatesAdjacentFramesAndRequiresReviewBeforeAdding()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.prepareVideo", _ => true).SetResult("blob:local-video");
        context.JSInterop.Setup<System.Text.Json.JsonElement>("spellbookMedia.scanVideo", _ => true)
            .SetResult(JsonDocument.Parse("""{"frames":[{"time":0,"text":"Lightning Bolt\n{R}"},{"time":0.75,"text":"Lightning Bolt\n{R}"},{"time":1.5,"text":"Island\nBasic Land"}],"cancelled":false}""").RootElement);
        context.JSInterop.SetupVoid("spellbookMedia.releaseVideo", _ => true).SetVoidResult();

        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("No decks yet", page.Markup));
        page.Find("[aria-label='New deck name']").Change("Video scan");
        Button(page, "Create deck").Click();
        page.FindComponents<InputFile>()[1].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "clip.mov", contentType: "video/quicktime"));
        page.WaitForAssertion(() => TestChecks.AreEqual(1, page.FindAll("#card-video-preview").Count));

        Button(page, "Scan video for cards").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual(2, page.FindAll(".video-candidate").Count));
        TestChecks.Contains("Lightning Bolt", page.Markup);
        TestChecks.Contains("Stack estimate: unavailable", page.Markup);
        TestChecks.Empty(SavedDecks(context)[0].Cards);

        page.FindAll(".video-candidate input[type=number]")[0].Change("3");
        Button(page, "Add reviewed cards").Click();
        TestChecks.AreEqual(2, SavedDecks(context)[0].Cards.Count);
        TestChecks.AreEqual(3, SavedDecks(context)[0].Cards.Single(c => c.Name == "Lightning Bolt").Quantity);
    }

    [TestMethod]
    public void PhotoPickerRejectsUnsupportedOrOversizedMediaWithoutChangingDeck()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        var media = page.FindComponents<InputFile>()[0];

        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "unsupported.xyz", contentType: "application/octet-stream"));
        TestChecks.Contains("Select a JPEG, PNG", page.Markup);
        TestChecks.Empty(page.FindAll(".scan-preview"));

        media.UploadFiles(InputFileContent.CreateFromBinary(new byte[6_000_001], "big.jpg", contentType: "image/jpeg"));
        TestChecks.Contains("Photo exceeds 6 MB", page.Markup);
        TestChecks.Empty(page.FindAll(".scan-preview"));

        media.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "valid.webp", contentType: "image/webp"));
        TestChecks.Single(page.FindAll(".scan-preview"));
        media.UploadFiles(InputFileContent.CreateFromBinary(new byte[20_000_001], "oversize.heic", contentType: "image/heic"));
        TestChecks.Contains("HEIC exceeds the 20 MB", page.Markup);
        TestChecks.Empty(page.FindAll(".scan-preview"));

        // Unused oversized HEIC must not reject a separately selected valid JPEG.
        media.UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "valid.jpg", contentType: "image/jpeg"),
            InputFileContent.CreateFromBinary(new byte[20_000_001], "unused.heic", contentType: "image/heic"));
        page.WaitForAssertion(() => TestChecks.AreEqual("data:image/jpeg;base64,AQI=",
            page.Find(".scan-preview").GetAttribute("src")));
        TestChecks.Empty(SavedDecks(context)[0].Cards);
    }

    [TestMethod]
    public void InvalidHeicConversionAndVideoPrepareLeaveLocalDeckUnchanged()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.convertHeic", _ => true).SetResult("not-jpeg");
        context.JSInterop.Setup<string>("spellbookMedia.prepareVideo", _ => true).SetResult("not-blob");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        var picker = page.FindComponents<InputFile>()[0];

        picker.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "card.heic", contentType: "image/heic"));
        page.WaitForAssertion(() => TestChecks.Contains("Media could not be processed locally", page.Markup));
        TestChecks.Empty(page.FindAll(".scan-preview"));

        picker.UploadFiles(InputFileContent.CreateFromBinary([1, 2], "movie.mov", contentType: "video/quicktime"));
        page.WaitForAssertion(() => TestChecks.Contains("Media could not be processed locally", page.Markup));
        TestChecks.Empty(page.FindAll("#card-video-preview"));
        TestChecks.Empty(SavedDecks(context)[0].Cards);
    }

    [TestMethod]
    public void InvalidVideoFrameCaptureCanBeRetriedWithoutAddingCard()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.prepareVideo", _ => true).SetResult("blob:review");
        context.JSInterop.Setup<string>("spellbookMedia.captureFrame", _ => true).SetResult("not-jpeg");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "movie.mov", contentType: "video/quicktime"));
        page.WaitForAssertion(() => TestChecks.Single(page.FindAll("#card-video-preview")));
        Button(page, "Capture selected video frame").Click();
        page.WaitForAssertion(() => TestChecks.Contains("Could not capture this frame", page.Markup));
        TestChecks.Empty(page.FindAll(".scan-preview"));
        TestChecks.Empty(SavedDecks(context)[0].Cards);
    }

    [TestMethod]
    public void OcrWithNoUsableTitleAllowsManualCorrection()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.recognize", _ => true).SetResult("\n ");
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1], "blur.jpg", contentType: "image/jpeg"));
        Button(page, "Recognize text on device").Click();
        page.WaitForAssertion(() => TestChecks.Contains("Enter the card name yourself", page.Markup));
        TestChecks.Empty(SavedDecks(context)[0].Cards);
        page.Find("[aria-label='Name from photo']").Change("Island");
        Button(page, "Add confirmed card").Click();
        TestChecks.AreEqual("Island", TestChecks.Single(SavedDecks(context)[0].Cards).Name);
    }

    [TestMethod]
    public void SavedCardTitleCanBeEditedAndInvalidNamesAreRejected()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.Find("[aria-label='Card name']").Change("Old title");
        Button(page, "Add").Click();

        var row = page.Find(".card-row");
        row.QuerySelector("input[aria-label='Card title for Old title']")!.Change("  New title  ");
        TestChecks.Contains("Card title saved", page.Markup);
        TestChecks.AreEqual("New title", row.QuerySelector(".visual-card-info a")!.TextContent);
        TestChecks.AreEqual("New title", TestChecks.Single(SavedDecks(context)[0].Cards).Name);

        row.QuerySelector("input[aria-label='Card title for New title']")!.Change("   ");
        TestChecks.Contains("Enter a card title", page.Markup);
        TestChecks.AreEqual("New title", TestChecks.Single(SavedDecks(context)[0].Cards).Name);
    }

    [TestMethod]
    public void SelectedDeckKeepsPendingPhotoUntilUserClearsIt()
    {
        using var context = CreateContext();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1], "initial.jpg", contentType: "image/jpeg"));
        TestChecks.Single(page.FindAll(".scan-preview"));
        Button(page, "Clear photo").Click();
        TestChecks.Empty(page.FindAll(".scan-preview"));
    }

    [TestMethod]
    public async Task ScryfallAutocompleteProvidesSuggestionsAndManualEntryRemainsPossible()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string[]>("spellbookScryfall.autocomplete", _ => true)
            .SetResult(["Lightning Bolt", "Lightning Helix"]);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));

        await page.InvokeAsync(() => page.Find("[aria-label='Card name']").Input("Lightn"));
        page.WaitForAssertion(() => TestChecks.Contains("Lightning Bolt", page.Find(".card-suggestions").TextContent),
            timeout: TimeSpan.FromSeconds(4));
        TestChecks.Empty(SavedDecks(context)[0].Cards);
        await page.InvokeAsync(() => page.Find("[aria-label='Card name']").Change("Lightning Bolt"));
        page.WaitForAssertion(() => TestChecks.AreEqual("Lightning Bolt", page.Find("[aria-label='Card name']").GetAttribute("value")));
        await page.InvokeAsync(() => Button(page, "Add").Click());
        page.WaitForAssertion(() => TestChecks.AreEqual("Lightning Bolt", TestChecks.Single(SavedDecks(context)[0].Cards).Name));

        await page.InvokeAsync(() => page.Find("[aria-label='Card name']").Change("My custom offline card"));
        Button(page, "Add").Click();
        page.WaitForAssertion(() => TestChecks.Contains(SavedDecks(context)[0].Cards, card => card.Name == "My custom offline card"));
    }


    [TestMethod]
    public void ScryfallLookupFailureRetainsManualEntry()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string[]>("spellbookScryfall.autocomplete", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("Network unavailable"));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.Find("[aria-label='Card name']").Input("Lightning");
        page.WaitForAssertion(() => TestChecks.Contains("Scryfall is unavailable", page.Markup),
            timeout: TimeSpan.FromSeconds(4));
        TestChecks.Empty(page.FindAll(".card-suggestions"));

        page.Find("[aria-label='Card name']").Change("Custom card");
        Button(page, "Add").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual("Custom card", TestChecks.Single(SavedDecks(context)[0].Cards).Name));
    }

    [TestMethod]
    public void OcrFailureCanBeRecoveredByManualCardName()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string>("spellbookMedia.recognize", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("OCR worker failed"));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "ocr-failure.jpg", contentType: "image/jpeg"));
        Button(page, "Recognize text on device").Click();
        page.WaitForAssertion(() => TestChecks.Contains("On-device OCR is unavailable", page.Markup));
        TestChecks.Empty(SavedDecks(context)[0].Cards);

        page.Find("[aria-label='Name from photo']").Change("Forest");
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual("Forest", TestChecks.Single(SavedDecks(context)[0].Cards).Name));
    }

    [TestMethod]
    public void ExactScryfallLookupWithoutMatchNeverAutomaticallyAddsCard()
    {
        using var context = CreateContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "unknown-card.jpg", contentType: "image/jpeg"));
        page.Find("[aria-label='Name from photo']").Change("Not a known MTG card");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => TestChecks.Contains("No exact Scryfall match", page.Markup));
        TestChecks.Empty(SavedDecks(context)[0].Cards);
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual("Not a known MTG card", TestChecks.Single(SavedDecks(context)[0].Cards).Name));
    }

    [TestMethod]
    public void InvalidDeckRenameCannotOverwriteSavedName()
    {
        using var context = CreateContext(JsonSerializer.Serialize(new[] { new Deck { Name = "Original" } }));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.Find(".editor label input").Change(" ");
        TestChecks.Contains("previous name was restored", page.Markup);
        TestChecks.AreEqual("Original", TestChecks.Single(SavedDecks(context)).Name);

        page.Find(".editor label input").Change(new string('X', 161));
        TestChecks.AreEqual("Original", TestChecks.Single(SavedDecks(context)).Name);
    }


    [TestMethod]
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
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1, 2], "avacyn.jpg", contentType: "image/jpeg"));
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => TestChecks.Contains("Found on Scryfall", page.Markup));
        TestChecks.Contains("mana value 8", page.Markup);
        TestChecks.Contains("Legendary Creature", page.Markup);
        TestChecks.Single(page.FindAll(".verified-card img"));
        TestChecks.Empty(SavedDecks(context)[0].Cards);
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual(
            "Avacyn, Angel of Hope", TestChecks.Single(SavedDecks(context)[0].Cards).Name));
    }

    [TestMethod]
    public void ScryfallVerificationExceptionAllowsManualConfirmationWithoutAutoSaving()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<ScryfallCard?>("spellbookScryfall.exact", _ => true)
            .SetException(new Microsoft.JSInterop.JSException("Scryfall offline"));
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1], "offline.jpg", contentType: "image/jpeg"));
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => TestChecks.Contains("Card lookup unavailable", page.Markup));
        TestChecks.Empty(SavedDecks(context)[0].Cards);
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual(
            "Avacyn, Angel of Hope", TestChecks.Single(SavedDecks(context)[0].Cards).Name));
    }

    [TestMethod]
    public void EditingScannedCardNameInvalidatesPreviousVerification()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<ScryfallCard?>("spellbookScryfall.exact", _ => true)
            .SetResult(new ScryfallCard { Name = "Avacyn, Angel of Hope", ManaValue = 8 });
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1], "card.png", contentType: "image/png"));
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => TestChecks.Contains("Found on Scryfall", page.Markup));
        page.Find("[aria-label='Name from photo']").Input("Island");
        page.WaitForAssertion(() => TestChecks.Empty(page.FindAll(".verified-card")));
        page.Find("[aria-label='Name from photo']").Change("Island");
        Button(page, "Add confirmed card").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual("Island", TestChecks.Single(SavedDecks(context)[0].Cards).Name));
    }


    [TestMethod]
    public void ShortScryfallQueriesAreNotSentAndEmptySearchesKeepManualEntry()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string[]>("spellbookScryfall.autocomplete", _ => true)
            .SetResult([]);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        page.Find("[aria-label='Card name']").Input("A");
        TestChecks.DoesNotContain(context.JSInterop.Invocations.Select(i => i.Identifier),
            "spellbookScryfall.autocomplete");

        page.Find("[aria-label='Card name']").Input("Avacy");
        page.WaitForAssertion(() => TestChecks.Contains("No suggestions", page.Markup),
            timeout: TimeSpan.FromSeconds(4));
        TestChecks.Empty(page.FindAll(".card-suggestions"));

        page.Find("[aria-label='Card name']").Change("Avacyn, Angel of Hope");
        Button(page, "Add").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual(
            "Avacyn, Angel of Hope", TestChecks.Single(SavedDecks(context)[0].Cards).Name));
    }


    [TestMethod]
    public async Task ScryfallSuggestionClickChoosesCanonicalName()
    {
        using var context = CreateContext();
        context.JSInterop.Setup<string[]>("spellbookScryfall.autocomplete", _ => true)
            .SetResult(["Avacyn, Angel of Hope"]);
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        await page.InvokeAsync(() => page.Find("[aria-label='Card name']").Input("Avacy"));
        page.WaitForAssertion(() => TestChecks.Contains("Avacyn, Angel of Hope", page.Find(".card-suggestions").TextContent),
            timeout: TimeSpan.FromSeconds(4));

        await page.InvokeAsync(() => Button(page, "Avacyn, Angel of Hope").Click());
        page.WaitForAssertion(() =>
        {
            TestChecks.Empty(page.FindAll(".card-suggestions"));
            TestChecks.AreEqual("Avacyn, Angel of Hope", page.Find("[aria-label='Card name']").GetAttribute("value"));
        });
        await page.InvokeAsync(() => Button(page, "Add").Click());
        page.WaitForAssertion(() => TestChecks.AreEqual("Avacyn, Angel of Hope", TestChecks.Single(SavedDecks(context)[0].Cards).Name));
    }

    [TestMethod]
    [DataRow("Avacyn, Angel of Hope 5WWW\\nLegendary Creature", "Avacyn, Angel of Hope")]
    [DataRow("Lightning Bolt {R}\\nInstant", "Lightning Bolt")]
    [DataRow("Niv-Mizzet, Parun UURRR", "Niv-Mizzet, Parun")]
    [DataRow("Seven Dwarves", "Seven Dwarves")]
    [DataRow("2020 Vision", "2020 Vision")]
    public void OcrTitleExtractorRemovesRecognizableManaSuffixOnly(string recognized, string expected)
    {
        TestChecks.AreEqual(expected, CardTitleExtractor.FromOcr(recognized.Replace("\\n", "\n")));
    }

    [TestMethod]
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
        page.WaitForAssertion(() => TestChecks.Contains("Add a card", page.Markup));
        TestChecks.Single(page.FindAll("section.card-scan-panel[aria-label='Scan a card']"));
        page.FindComponents<InputFile>()[0].UploadFiles(
            InputFileContent.CreateFromBinary([1], "card.png", contentType: "image/png"));
        page.Find("[aria-label='Name from photo']").Change("Avacyn, Angel of Hope");
        Button(page, "Verify against Scryfall").Click();
        page.WaitForAssertion(() => TestChecks.AreEqual(
            "https://scryfall.com/card/avr/6/avacyn-angel-of-hope",
            page.Find(".verified-card a").GetAttribute("href")));
        Button(page, "Add confirmed card").Click();
        TestChecks.Contains("scryfall.com/search?q=", page.Find(".card-row a").GetAttribute("href")!);
    }

    private static BunitContext CreateContext(string? storedJson = null)
    {
        var context = new BunitContext();
        context.Services.AddScoped<DeckStore>();
        storedJson ??= JsonSerializer.Serialize(new[] { new Deck { Name = "Test deck" } });
        context.Services.AddSingleton(new InitialDeckStorage(storedJson));
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult(storedJson);
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        return context;
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<Home> page, string text) =>
        page.FindAll("button").Single(button => button.TextContent == text);

    private static List<Deck> SavedDecks(BunitContext context)
    {
        var writes = context.JSInterop.Invocations["spellbookStorage.set"];
        return writes.Count > 0
            ? ReadDecks(TestChecks.IsType<string>(writes.Last().Arguments[1]))
            : ReadDecks(context.Services.GetRequiredService<InitialDeckStorage>().Json);
    }

    private sealed record InitialDeckStorage(string Json);

    private static List<Deck> ReadDecks(string json) =>
        JsonSerializer.Deserialize<List<Deck>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
