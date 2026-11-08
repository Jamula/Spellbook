using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Spellbook.Components;
using Spellbook.Models;
using Spellbook.Pages;
using Spellbook.Services;

namespace Spellbook.Tests;

[TestClass]
public sealed class DeckAnalysisTests
{
    [TestMethod]
    public void QuantityWeightedAnalysisExplainsKnownUnknownAndSectionBoundaries()
    {
        var deck = new Deck { Cards = [
            new() { Name = "Land", Quantity = 24, Details = new() { TypeLine = "Basic Land — Forest", ManaValue = 0, Colors = "C", RolesReviewed = true } },
            new() { Name = "Spell", Quantity = 20, Details = new() { TypeLine = "Creature — Land Beast", ManaValue = 4.5, Colors = "UGG", RolesReviewed = true, Draw = true, Ramp = true, Removal = true } },
            new() { Name = "Early", Quantity = 4, Details = new() { TypeLine = "Sorcery", ManaValue = 0, Colors = "R", RolesReviewed = true } },
            new() { Name = "Late", Quantity = 12, Details = new() { TypeLine = "Artifact", ManaValue = 100, Colors = "C", RolesReviewed = true } },
            new() { Name = "Side", Quantity = 15, Section = "Sideboard" }
        ] };
        var stats = DeckAnalysis.Calculate(deck);
        TestChecks.AreEqual((60,15,24,0,0,0,0), (stats.Total,stats.OutsideMain,stats.Lands,stats.UnknownTypes,stats.UnknownMana,stats.UnknownColors,stats.UnknownRoles));
        TestChecks.AreEqual([4,0,0,0,20,0,0,12], stats.Curve);
        TestChecks.AreEqual((20,20,20), (stats.Draw,stats.Ramp,stats.Removal));
        TestChecks.AreEqual(20,stats.Colors['G']);
        TestChecks.Contains(stats.Advice, a => a.Contains("33–45%"));
        TestChecks.Contains(stats.Advice, a => a.Contains("4+ mana"));
        TestChecks.Contains(stats.Advice, a => a.Contains("20 draw"));
        deck.Cards[0].Quantity = 1;
        TestChecks.Contains(DeckAnalysis.Calculate(deck).Advice,a => a.Contains("below 33%"));
        deck.Cards[0].Quantity = 100;
        TestChecks.Contains(DeckAnalysis.Calculate(deck).Advice,a => a.Contains("above 45%"));
        deck.Format = "Two-Headed Giant";
        deck.Cards.Add(new() { Name = "Unknown", Quantity = 2 });
        deck.Cards.Add(new() { Name = "Bad mana", Quantity = 3, Details = new() { ManaValue = double.NaN, TypeLine = "Artifact", Colors = "CX" } });
        deck.Cards.Add(new() { Name = "Negative mana", Details = new() { ManaValue = -1, TypeLine = "Artifact", Colors = "" } });
        stats = DeckAnalysis.Calculate(deck);
        TestChecks.AreEqual((2,6,6,6), (stats.UnknownTypes,stats.UnknownMana,stats.UnknownColors,stats.UnknownRoles));
        TestChecks.Contains(stats.Advice,a => a.Contains("teammate"));
        TestChecks.Contains(stats.Advice,a => a.Contains("unknown"));
        TestChecks.Single(DeckAnalysis.Calculate(new()).Advice);
        var multiface = DeckAnalysis.Calculate(new() { Cards = [new() { Name = "Two faces", Details = new() { TypeLine = "Creature // Land", ManaValue = 3, Colors = "G" } }] });
        TestChecks.AreEqual((0,1,1), (multiface.Lands,multiface.UnknownTypes,multiface.UnknownMana));
    }

    [TestMethod]
    public void LocalMetadataWorkflowPersistsRecalculatesAndSurvivesBackupRoundtrip()
    {
        using var context = new BunitContext();
        context.JSInterop.Setup<string?>("spellbookStorage.get", _ => true).SetResult(JsonSerializer.Serialize(new[] { new Deck { Cards = [new() { Name = "Example", Quantity = 4 }] } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        context.JSInterop.SetupVoid("spellbookStorage.set", _ => true).SetVoidResult();
        context.Services.AddScoped<DeckStore>();
        var page = context.Render<Home>();
        page.WaitForAssertion(() => TestChecks.Contains("4 unknown card types", page.Markup));
        page.Find("[aria-label='Card to review']").Change("-1");
        page.Find("[aria-label='Card to review']").Change("0");
        page.Find("[aria-label='Card mana value']").Change("-1");
        Save(page);
        TestChecks.Contains("Use a mana value",page.Markup);
        page.Find("[aria-label='Card mana value']").Change("2");
        page.Find("[aria-label='Card type line']").Change("Sorcery");
        page.Find("[aria-label='Card colors']").Change("RX");
        Save(page);
        TestChecks.Contains("Use a mana value",page.Markup);
        page.Find("[aria-label='Card colors']").Change(" r ");
        page.Find("[aria-label='Roles reviewed']").Change(true);
        page.Find("[aria-label='Draw role']").Change(true);
        page.Find("[aria-label='Ramp role']").Change(true);
        page.Find("[aria-label='Removal role']").Change(true);
        Save(page);
        TestChecks.Contains("4 / 4 / 4 user-confirmed", page.Markup);
        var saved = (string)context.JSInterop.Invocations.Last(i => i.Identifier == "spellbookStorage.set").Arguments[1]!;
        var store = context.Services.GetRequiredService<DeckStore>();
        TestChecks.AreEqual("R",TestChecks.Single(TestChecks.Single(store.ParseImport(saved)).Cards).Details!.Colors);
        page.Find("[aria-label='Increase quantity for Example']").Click();
        TestChecks.Contains("5 / 5 / 5 user-confirmed",page.Markup);
        page.Find(".editor > label select").Change("Two-Headed Giant");
        TestChecks.Contains("compare these counts with your teammate",page.Markup);
        page.Find("[aria-label='Card to review']").Change("0");
        TestChecks.AreEqual("R",page.Find("[aria-label='Card colors']").GetAttribute("value"));
        page.Find("[aria-label='Card mana value']").Change("");
        page.Find("[aria-label='Card colors']").Change("");
        page.Find("[aria-label='Card type line']").Change("");
        page.Find("[aria-label='Roles reviewed']").Change(false);
        Save(page);
        TestChecks.Contains("5 unknown card types",page.Markup);
        page.Find("[aria-label='Remove Example']").Click();
        TestChecks.Contains("Add cards to the main deck",page.Markup);
    }

    [TestMethod]
    public void LegacyMetadataAndMalformedBackupsKeepImportBoundaryExplicit()
    {
        using var context = new BunitContext();
        var store = new DeckStore(context.JSInterop.JSRuntime);
        TestChecks.IsNull(store.ParseImport("[{\"name\":\"Legacy\",\"cards\":[{\"name\":\"Card\"}]}]")[0].Cards[0].Details);
        foreach (var details in new[] { "{\"manaValue\":-1}", "{\"manaValue\":1001}", "{\"colors\":\"XC\"}", "{\"colors\":\"CW\"}", "{\"colors\":\"WWWWWW\"}", "{\"typeLine\":\"" + new string('x',501) + "\"}" })
            TestChecks.Throws<System.IO.InvalidDataException>(() => store.ParseImport("[{\"name\":\"Bad\",\"cards\":[{\"name\":\"Card\",\"details\":"+details+"}]}]"));
        var deck = new Deck { Cards = [new() { Name = "Side", Section = "Sideboard", Details = new() { TypeLine = "Land" } }] };
        var component = context.Render<DeckStatistics>(p => p.Add(c => c.Deck,deck));
        var statistics = component.Find("details.deck-statistics");
        Assert.AreEqual("Deck statistics", component.Find("details.deck-statistics > summary").TextContent.Trim());
        Assert.IsFalse(statistics.HasAttribute("open"), "Statistics should be collapsed until the user opens the section.");
        TestChecks.Contains("1 copies in other sections",component.Markup);
        deck.Cards[0].Section = "Main";
        component.Render(p => p.Add(c => c.Deck,deck));
        TestChecks.Contains("1 known",component.Markup);
        component.Find("[aria-label='Card to review']").Change("0");
        component.Render(p => p.Add(c => c.Deck,new Deck()));
        TestChecks.Empty(component.FindAll("[aria-label='Card mana value']"));
    }

    [TestMethod]
    public void ReviewingLocalCardDetailsPreservesManaCostAndImageUrl()
    {
        using var context = new BunitContext();
        var card = new DeckCard
        {
            Name = "Mana-rich creature",
            Details = new CardDetails
            {
                ManaValue = 4,
                ManaCost = "{2}{G}{G}",
                TypeLine = "Creature",
                Colors = "G",
                ImageUrl = "https://cards.scryfall.io/small/front/a/b/card.jpg"
            }
        };
        var deck = new Deck { Cards = [card] };
        var component = context.Render<DeckStatistics>(p => p.Add(c => c.Deck, deck));

        component.Find("[aria-label='Card to review']").Change("0");
        component.Find("[aria-label='Card mana value']").Change("5");
        component.FindAll("button").Single(button => button.TextContent == "Save card details").Click();

        Assert.AreEqual(5, card.Details!.ManaValue);
        Assert.AreEqual("{2}{G}{G}", card.Details.ManaCost);
        Assert.AreEqual("https://cards.scryfall.io/small/front/a/b/card.jpg", card.Details.ImageUrl);
    }

    private static void Save(IRenderedComponent<Home> page) => page.FindAll("button").Single(b => b.TextContent == "Save card details").Click();
}
