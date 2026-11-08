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
    public void DeckCollectionReportsPersistenceFailuresAndRejectsInvalidImports()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("[{\"name\":\"Alpha\",\"cards\":[]}]");
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true)
            .SetException(new JSException("Storage is unavailable."));
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => TestChecks.Contains("Alpha", page.Markup));

        page.Find("input[aria-label='Deck name for Alpha']").Change("Beta");
        TestChecks.Contains("Could not rename this deck", page.Markup);
        TestChecks.Contains("Deck name for Alpha", page.Markup);
        page.Find("button.danger").Click();
        TestChecks.Contains("Could not delete this deck", page.Markup);
        TestChecks.Contains("Alpha", page.Markup);

        using var importContext = new BunitContext();
        importContext.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("[]");
        importContext.Services.AddScoped<DeckStore>();
        var importPage = importContext.Render<Decks>();
        importPage.WaitForAssertion(() => TestChecks.Contains("No decks yet", importPage.Markup));
        importPage.FindComponents<InputFile>().Single().UploadFiles(
            InputFileContent.CreateFromText("not json", "backup.json"));
        TestChecks.Contains("Import failed", importPage.Markup);
    }

    [TestMethod]
    public void DeckCollectionRecoversDamagedStorageOnlyAfterConfirmation()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("not json");
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => TestChecks.Contains("Saved decks could not be loaded", page.Markup));

        page.Find("button").Click();
        TestChecks.AreEqual("spellbook-recovery-raw.json",
            context.JSInterop.Invocations["spellbookStorage.download"].Single().Arguments[0]);
        page.Find("input[type=checkbox]").Change(true);

        page.FindComponents<InputFile>().Single().UploadFiles(
            InputFileContent.CreateFromText("not json", "broken.json"));
        TestChecks.Contains("Recovery failed", page.Markup);

        var restored = new Deck { Name = "Recovered" };
        page.FindComponents<InputFile>().Single().UploadFiles(
            InputFileContent.CreateFromText(JsonSerializer.Serialize(new[] { restored }), "backup.json"));
        TestChecks.Contains("Restored 1 deck(s).", page.Markup);
        TestChecks.Contains("Recovered", page.Markup);
    }

    [TestMethod]
    public void DeckCollectionOpensAnExistingDeck()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("[{\"name\":\"Alpha\",\"cards\":[]}]");
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => TestChecks.Contains("Alpha", page.Markup));

        page.FindAll("button").Single(button => button.TextContent.Contains("Open deck")).Click();
        TestChecks.Contains("/deck/", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [TestMethod]
    public void DeckEditorCanDeleteTheSelectedDeck()
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
    public void DeckCollectionImportsAndExportsTheFullJsonBackup()
    {
        var original = new Deck { Name = "Alpha", Cards = [new DeckCard { Name = "Island", Quantity = 2 }] };
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true)
            .SetResult(JsonSerializer.Serialize(new[] { original }));
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => TestChecks.Contains("Alpha", page.Markup));

        page.Find("button.secondary").Click();
        var download = TestChecks.Single(context.JSInterop.Invocations["spellbookStorage.download"]);
        TestChecks.AreEqual("spellbook-decks.json", download.Arguments[0]);
        TestChecks.AreEqual("Alpha", JsonSerializer.Deserialize<List<Deck>>(
            TestChecks.IsType<string>(download.Arguments[1]), new JsonSerializerOptions(JsonSerializerDefaults.Web))![0].Name);

        var imported = new Deck { Name = "Imported", Cards = [new DeckCard { Name = "Forest", Quantity = 3 }] };
        page.FindComponents<InputFile>().Single().UploadFiles(
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
    public void SettingsPageExposesPrivacyControlsAndLocalBackupGuidance()
    {
        using var context = new BunitContext();
        var page = context.Render<Settings>();
        TestChecks.Contains("Settings", page.Markup);
        TestChecks.Contains("Privacy", page.Markup);
        TestChecks.Contains("backup", page.Markup);
    }
}
