using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Spellbook.Models;
using Spellbook.Pages;
using Spellbook.Services;

namespace Spellbook.Tests;

[TestClass]
public sealed class DeckNavigationTests
{
    [TestMethod]
    public void DeckCollectionCreatesDeckAndNavigatesToEditor()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("[]");
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();

        page.WaitForAssertion(() => TestChecks.Contains("No decks yet", page.Markup));
        page.Find("input[aria-label='New deck name']").Change("Alpha");
        page.Find("button").Click();
        TestChecks.Contains("/deck/", context.Services.GetRequiredService<NavigationManager>().Uri);

    }

    [TestMethod]
    public void DeckCollectionKeepsSettingsRecoveryLinkWhenSavedDataIsDamaged()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("not json");
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();

        page.WaitForAssertion(() => TestChecks.Contains("Saved decks could not be loaded.", page.Markup));
        TestChecks.AreEqual("/settings", page.Find("a").GetAttribute("href"));
        TestChecks.Contains("No edits were saved.", page.Markup);
    }

    [TestMethod]
    public void DeckCollectionRenamesAndDeletesDecks()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("[{\"name\":\"Alpha\",\"cards\":[]}]");
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => TestChecks.Contains("Alpha", page.Markup));
        page.Find("input[aria-label='Deck name for Alpha']").Change("Beta");
        TestChecks.Contains("Deck name saved", page.Markup);
        page.Find("button.danger").Click();
        TestChecks.Contains("No decks yet", page.Markup);
    }

    [TestMethod]
    public void DeckCollectionCanOpenAnExistingDeckAndRollBackFailedEdits()
    {
        var deck = new Deck { Name = "Alpha" };
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true)
            .SetResult(JsonSerializer.Serialize(new[] { deck }));
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true)
            .SetException(new JSException("Quota exceeded"));
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => TestChecks.Contains("Alpha", page.Markup));

        page.Find(".deck-tile button:not(.danger)").Click();
        TestChecks.Contains(deck.Id.ToString(), context.Services.GetRequiredService<NavigationManager>().Uri);

        var secondContext = new BunitContext();
        using (secondContext)
        {
            secondContext.JSInterop.Setup<string?>("spellbookStorage.get", _ => true)
                .SetResult(JsonSerializer.Serialize(new[] { deck }));
            secondContext.JSInterop.SetupVoid("spellbookStorage.set", _ => true)
                .SetException(new JSException("Quota exceeded"));
            secondContext.Services.AddScoped<DeckStore>();
            var pageWithFailedWrites = secondContext.Render<Decks>();
            pageWithFailedWrites.WaitForAssertion(() => TestChecks.Contains("Alpha", pageWithFailedWrites.Markup));
            pageWithFailedWrites.Find("input[aria-label='Deck name for Alpha']").Change("Beta");
            TestChecks.Contains("Could not rename this deck", pageWithFailedWrites.Markup);
            TestChecks.AreEqual("Alpha", pageWithFailedWrites.Find("input[aria-label='Deck name for Alpha']").GetAttribute("value"));
            pageWithFailedWrites.Find("button.danger").Click();
            TestChecks.Contains("Could not delete this deck", pageWithFailedWrites.Markup);
            TestChecks.Single(pageWithFailedWrites.FindAll(".deck-tile"));
        }
    }

    [TestMethod]
    public void SettingsRecoversDamagedDataOnlyAfterConfirmingAndValidatingBackup()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("not json");
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Settings>();
        page.WaitForAssertion(() => TestChecks.Contains("Saved deck data could not be loaded", page.Markup));

        page.Find("button").Click();
        TestChecks.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
            TestChecks.IsType<string>(context.JSInterop.Invocations["spellbookStorage.download"].Last().Arguments[0]),
            @"^\d{4}-\d{2}-\d{2}-local-decks-[A-Za-z0-9._-]+\.json$"));
        var restore = page.FindComponents<InputFile>().Single();
        TestChecks.IsTrue(restore.Find("input").HasAttribute("disabled"));
        page.Find("input[type=checkbox]").Change(true);
        restore.UploadFiles(InputFileContent.CreateFromText("not json", "invalid.json"));
        TestChecks.Contains("Recovery failed", page.Markup);
        TestChecks.Empty(context.JSInterop.Invocations["spellbookStorage.set"]);

        restore.UploadFiles(InputFileContent.CreateFromText("[]", "empty.json"));
        TestChecks.Contains("Restored 0 deck(s).", page.Markup);
        TestChecks.Contains("Deck backup and restore", page.Markup);
    }

    [TestMethod]
    public void SettingsImportsAndExportsTheFullJsonBackup()
    {
        var original = new Deck { Name = "Alpha", Cards = [new DeckCard { Name = "Island", Quantity = 2 }] };
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true)
            .SetResult(JsonSerializer.Serialize(new[] { original }));
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Settings>();
        page.WaitForAssertion(() => TestChecks.Contains("Export JSON backup", page.Markup));

        var importer = page.FindComponents<InputFile>().Single(input =>
            input.Find("input").GetAttribute("accept") == ".json,application/json");
        importer.UploadFiles(InputFileContent.CreateFromText("not json", "invalid.json"));
        TestChecks.Contains("Import failed", page.Markup);

        page.Find("button.secondary").Click();
        var download = TestChecks.Single(context.JSInterop.Invocations["spellbookStorage.download"]);
        TestChecks.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
            TestChecks.IsType<string>(download.Arguments[0]),
            @"^\d{4}-\d{2}-\d{2}-local-decks-[A-Za-z0-9._-]+\.json$"));
        TestChecks.AreEqual("Alpha", JsonSerializer.Deserialize<List<Deck>>(
            TestChecks.IsType<string>(download.Arguments[1]), new JsonSerializerOptions(JsonSerializerDefaults.Web))![0].Name);

        var imported = new Deck { Name = "Imported", Cards = [new DeckCard { Name = "Forest", Quantity = 3 }] };
        page.FindComponents<InputFile>().Single(input =>
            input.Find("input").GetAttribute("accept") == ".json,application/json").UploadFiles(
            InputFileContent.CreateFromText(JsonSerializer.Serialize(new[] { imported }), "backup.json"));
        TestChecks.Contains("Imported 1 deck(s).", page.Markup);
        var saved = JsonSerializer.Deserialize<List<Deck>>(
            TestChecks.IsType<string>(context.JSInterop.Invocations["spellbookStorage.set"].Last().Arguments[1]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        TestChecks.AreEqual(2, saved.Count);
        TestChecks.AreEqual("Imported", saved[1].Name);
        TestChecks.AreEqual(3, saved[1].CardCount);
    }

    [TestMethod]
    public void SettingsKeepsBackupAvailableWhenBrowserDownloadFails()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("[]");
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true)
            .SetException(new JSException("download denied"));
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Settings>();
        page.WaitForAssertion(() => TestChecks.Contains("Export JSON backup", page.Markup));

        page.Find("button.secondary").Click();

        TestChecks.Contains("Could not download the backup", page.Markup);
    }

    [TestMethod]
    public void SettingsReportsRawBackupDownloadFailureForDamagedStorage()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("not json");
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true)
            .SetException(new JSException("download denied"));
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Settings>();
        page.WaitForAssertion(() => TestChecks.Contains("Download raw saved data", page.Markup));

        page.Find("button.secondary").Click();

        TestChecks.Contains("Could not download the raw saved data", page.Markup);
    }

    [TestMethod]
    public void SettingsReportsImportSaveFailureAndPreservesExistingData()
    {
        var original = new Deck { Name = "Alpha" };
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true)
            .SetResult(JsonSerializer.Serialize(new[] { original }));
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true)
            .SetException(new JSException("Quota exceeded"));
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Settings>();
        page.WaitForAssertion(() => TestChecks.Contains("Import JSON backup", page.Markup));

        page.FindComponents<InputFile>().Single().UploadFiles(
            InputFileContent.CreateFromText("[{\"name\":\"Imported\"}]", "backup.json"));

        TestChecks.Contains("Import failed", page.Markup);
        TestChecks.AreEqual(1, context.JSInterop.Invocations["spellbookStorage.set"].Count);
    }

    [TestMethod]
    public void DeckEditorCanDeleteTheSelectedDeckAndReturnToCollection()
    {
        var deck = new Deck { Name = "Alpha" };
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true)
            .SetResult(JsonSerializer.Serialize(new[] { deck }));
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Home>(parameters => parameters.Add(component => component.DeckId, deck.Id));
        page.WaitForAssertion(() => TestChecks.Contains("Alpha", page.Markup));

        page.Find("button.danger").Click();
        TestChecks.AreEqual("/", new Uri(context.Services.GetRequiredService<NavigationManager>().Uri).AbsolutePath);
    }

    [TestMethod]
    public void SettingsPageExposesPrivacyControlsAndLocalBackupGuidance()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string[]?>("spellbookTheme.load", _ => true).SetResult(null);
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("[]");
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Settings>();
        page.WaitForAssertion(() => TestChecks.Contains("Export JSON backup", page.Markup));
        TestChecks.Contains("Settings", page.Markup);
        TestChecks.Contains("Privacy", page.Markup);
        TestChecks.Contains("backup", page.Markup);
        TestChecks.Contains("Export JSON backup", page.Markup);
        TestChecks.Contains("Import JSON backup", page.Markup);
    }
}
