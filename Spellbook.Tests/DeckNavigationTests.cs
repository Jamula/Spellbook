using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Spellbook.Models;
using Spellbook.Pages;
using Spellbook.Services;

namespace Spellbook.Tests;

public sealed class DeckNavigationTests
{
    [Fact]
    public void DeckCollectionCreatesDeckAndNavigatesToEditor()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("[]");
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();

        page.WaitForAssertion(() => Assert.Contains("No decks yet", page.Markup));
        var name = page.Find("input[maxlength='160']");
        name.Change("   ");
        page.Find("button").Click();
        Assert.Contains("Deck name must be 1–160 characters", page.Markup);
        Assert.Empty(page.FindAll(".deck-tile"));

        name.Change(" Alpha ");
        page.Find("button").Click();
        var saved = JsonSerializer.Deserialize<List<Deck>>(
            Assert.IsType<string>(context.JSInterop.Invocations["spellbookStorage.set"].Single().Arguments[1]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Alpha", Assert.Single(saved!).Name);
        Assert.Contains("/deck/", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void DeckCollectionRenamesAndDeletesDecks()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("[{\"name\":\"Alpha\",\"cards\":[]}]");
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => Assert.Contains("Alpha", page.Markup));
        page.Find("input[aria-label='Deck name for Alpha']").Change("Beta");
        Assert.Contains("Deck name saved", page.Markup);
        page.Find("button.danger").Click();
        Assert.Contains("No decks yet", page.Markup);
    }

    [Fact]
    public void SettingsPageExposesPrivacyControlsAndLocalBackupGuidance()
    {
        using var context = new BunitContext();
        var page = context.Render<Settings>();
        Assert.Contains("Settings", page.Markup);
        Assert.Contains("Privacy", page.Markup);
        Assert.Contains("backup", page.Markup);
    }

    [Fact]
    public void DeckCollectionRecoversUnreadableStorageOnlyAfterConfirmation()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult("not json");
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("spellbookStorage.download", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Decks>();
        page.WaitForAssertion(() => Assert.Contains("Saved decks could not be loaded", page.Markup));

        page.Find("button").Click();
        Assert.Equal("spellbook-recovery-raw.json",
            context.JSInterop.Invocations["spellbookStorage.download"].Single().Arguments[0]);
        var restore = page.FindComponent<InputFile>();
        Assert.True(restore.Find("input").HasAttribute("disabled"));
        page.Find("input[type=checkbox]").Change(true);
        Assert.False(restore.Find("input").HasAttribute("disabled"));

        restore.UploadFiles(InputFileContent.CreateFromText("not json", "invalid.json"));
        page.WaitForAssertion(() => Assert.Contains("Recovery failed:", page.Markup));

        var recovered = new Deck { Name = "Recovered" };
        restore.UploadFiles(InputFileContent.CreateFromText(JsonSerializer.Serialize(new[] { recovered }), "backup.json"));

        Assert.Contains("Restored 1 deck(s).", page.Markup);
        Assert.Contains("Recovered", page.Markup);
        Assert.Single(context.JSInterop.Invocations["spellbookStorage.set"]);
    }
}
