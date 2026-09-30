using NewsScraperService.Services;
using Xunit;

namespace NewsApiClient.Tests;

public class CategorizerTests
{
    [Theory]
    // 1. SPORTS
    [InlineData("Super Eagles striker Victor Osimhen scores hat-trick in AFCON qualifier", "Sports")]
    [InlineData("Arsenal and Chelsea battle for Premier League supremacy in London derby", "Sports")]
    [InlineData("Tobi Amusan wins 100m hurdles gold medal in Diamond League final", "Sports")]
    [InlineData("British boxer Raven Chapman recovering after emergency surgery following brain bleed", "Sports")]
    [InlineData("NPFL: Enyimba defeat Rivers United in thrilling clash in Aba", "Sports")]

    // 2. POLITICS
    [InlineData("President Bola Tinubu signs new tax reform executive order at Aso Rock", "Politics")]
    [InlineData("Senate passes amended electoral bill after plenary debate at National Assembly", "Politics")]
    [InlineData("Supreme Court reserves judgment in Osun State governorship election appeal", "Politics")]
    [InlineData("INEC issues timetable for off-cycle gubernatorial election in Ondo state", "Politics")]
    [InlineData("APC and PDP chieftains trade words over delegate list ahead of primary election", "Politics")]

    // 3. BUSINESS
    [InlineData("Central Bank of Nigeria raises monetary policy rate by 50 basis points to curb inflation", "Business")]
    [InlineData("Naira appreciates against US dollar at official NAFEM foreign exchange market", "Business")]
    [InlineData("Nigerian Exchange NGX All-Share Index crosses 100,000 points landmark", "Business")]
    [InlineData("Dangote Petroleum Refinery commences nationwide distribution of PMS petrol and diesel", "Business")]
    [InlineData("World Bank approves 750 million dollar financing package for power infrastructure in Nigeria", "Business")]

    // 4. CRIME
    [InlineData("EFCC arrests 45 suspects over online financial fraud and cyber crime in Lagos", "Crime")]
    [InlineData("Police Command intercepts arms smuggling syndicate with AK-47 rifles and ammunition", "Crime")]
    [InlineData("NDLEA intercepts cocaine consignment at Murtala Muhammed International Airport", "Crime")]
    [InlineData("High Court sentences convicted kidnappers to life imprisonment without option of fine", "Crime")]
    [InlineData("Troops neutralize 20 terrorists and rescue abducted hostages in clearance operation", "Crime")]

    // 5. ENTERTAINMENT
    [InlineData("Burna Boy and Wizkid perform sold out historic concert at London O2 Arena", "Entertainment")]
    [InlineData("Funke Akindele blockbuster movie breaks Nigerian box office record with 1.5 billion naira", "Entertainment")]
    [InlineData("Grammy Awards nominate Nigerian stars Davido, Asake, and Tems for Best African Music Performance", "Entertainment")]
    [InlineData("Big Brother Naija BBNaija unveils new housemates and grand prize for season premiere", "Entertainment")]
    [InlineData("AMVCA: Nollywood stars celebrate at Africa Magic Viewers Choice Awards gala in Lagos", "Entertainment")]

    // 6. TECHNOLOGY
    [InlineData("Nigerian fintech Moniepoint raises 110 million dollars in Series C funding", "Technology")]
    [InlineData("NITDA launches national artificial intelligence strategy and tech developer training", "Technology")]
    [InlineData("Starlink expands high-speed satellite broadband coverage to rural communities in Nigeria", "Technology")]
    [InlineData("Paystack introduces automated cross-currency virtual account API for merchants", "Technology")]
    [InlineData("Flutterwave receives payment service provider licenses to expand in East Africa", "Technology")]

    // 7. GENERAL
    [InlineData("FRSC confirms 15 dead and 25 injured in tragic head-on highway collision in Niger State", "General")]
    [InlineData("Earth tremor in Abuja: Minister Dele Alake urges calm and orders seismic updates", "General")]
    [InlineData("Five US work visa categories and application requirements for skilled immigrants", "General")]
    [InlineData("NCDC issues public health advisory on preventive measures against cholera outbreak", "General")]
    [InlineData("JAMB announces official cut-off marks and registration dates for 2026 UTME exams", "General")]

    // 8. INTERNATIONAL
    [InlineData("United Nations Security Council votes on immediate ceasefire resolution in Gaza conflict", "International")]
    [InlineData("US Presidential election: Trump and Harris clash during televised debate in Philadelphia", "International")]
    [InlineData("Kremlin warns Western allies over missile supplies to Ukraine as Kyiv peace talks stall", "International")]
    [InlineData("British Prime Minister Keir Starmer unveils UK bilateral trade deal at European summit", "International")]
    [InlineData("South Africa Parliament approves government of national unity cabinet under Cyril Ramaphosa", "International")]
    [InlineData("Kenyan President William Ruto dissolves cabinet following youth demonstrations in Nairobi", "International")]
    public void CategorizeByLexicon_ClassifiesCorrectly(string headline, string expectedCategory)
    {
        var (category, confidence) = MlCategorizerEngine.CategorizeByLexicon(headline, string.Empty);
        Assert.Equal(expectedCategory, category);
        Assert.True(confidence >= 0.5f);
    }

    [Fact]
    public void SubstringTrap_WordsWithAiAndApp_AreNotWronglyClassifiedAsTechnology()
    {
        // Articles containing "ai" ("said", "against", "claim") and "app" ("appoints", "approves", "appeals")
        // must NOT be classified as Technology
        var politics1 = MlCategorizerEngine.CategorizeFallback("Governor appoints new commissioners and approves state budget amidst opposition claims");
        Assert.Equal("Politics", politics1);

        var politics2 = MlCategorizerEngine.CategorizeFallback("Supreme Court hears election appeal against victory of ruling party candidate");
        Assert.Equal("Politics", politics2);

        var general1 = MlCategorizerEngine.CategorizeFallback("Civil servants said they received salary arrears against expectations");
        Assert.Equal("General", general1);

        var general2 = MlCategorizerEngine.CategorizeFallback("Alake urges calm after earth tremor in Abuja and orders hourly seismic updates");
        Assert.Equal("General", general2);
    }

    [Fact]
    public void CleanNewsText_RemovesBoilerplateAndUrls()
    {
        string raw = "<p>Tinubu approves budget.</p>\n\nRead More: https://punchng.com/tinubu-approves-budget/?utm_source=rss.punchng.com&utm_medium=web [&#8230;]";
        string clean = MlCategorizerEngine.CleanNewsText(raw);

        Assert.DoesNotContain("<p>", clean);
        Assert.DoesNotContain("https://", clean);
        Assert.DoesNotContain("Read More:", clean);
        Assert.DoesNotContain("utm_source", clean);
        Assert.DoesNotContain("&#8230;", clean);
        Assert.Contains("Tinubu approves budget.", clean);
    }
}
