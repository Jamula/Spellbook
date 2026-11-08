using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
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
