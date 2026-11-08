using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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
        page.Find("input[aria-label='New deck name']").Change("Alpha");
        page.Find("button").Click();
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
}
