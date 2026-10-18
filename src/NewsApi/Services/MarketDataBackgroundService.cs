using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Services;

public class MarketDataBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketDataBackgroundService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(24);

    public MarketDataBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MarketDataBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MarketDataBackgroundService starting with 24-hour daily synchronization interval.");

        // Grace startup delay to ensure DbInitializer and port bindings settle cleanly
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MarketDataBackgroundService stopped during initial startup delay.");
            return;
        }

        // Initial seed and sync on startup
        try
        {
            await SyncMarketDataAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MarketDataBackgroundService initial sync canceled due to application shutdown.");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial market data sync failed.");
        }

        using var timer = new PeriodicTimer(_syncInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;

                await SyncMarketDataAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("MarketDataBackgroundService periodic sync canceled due to application shutdown.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily market data sync failed.");
            }
        }
    }

    public async Task SyncMarketDataAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NewsDbContext>();
        var fxScraper = scope.ServiceProvider.GetRequiredService<IFxRatesScraperService>();

        await SeedOrUpdateNairaFxAsync(db, fxScraper, ct);
        ct.ThrowIfCancellationRequested();

        await SeedOrUpdateFuelPricesAsync(db, ct);
        ct.ThrowIfCancellationRequested();

        await SeedOrUpdateJambWaecAsync(db, ct);
        ct.ThrowIfCancellationRequested();

        await SeedOrUpdateElectionsAsync(db, ct);
        ct.ThrowIfCancellationRequested();

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Market data sync successfully executed for all evergreen trackers.");
    }

    private static async Task SeedOrUpdateNairaFxAsync(NewsDbContext db, IFxRatesScraperService fxScraper, CancellationToken ct)
    {
        var record = await db.MarketDataRecords.FirstOrDefaultAsync(r => r.MetricKey == "naira-fx", ct);
        var now = DateTime.UtcNow;

        FxScrapeResult fxResult;
        try
        {
            fxResult = await fxScraper.ScrapeLatestFxRatesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            fxResult = new FxScrapeResult
            {
                PrimaryValue = "₦0.00 / $1",
                SecondaryValue = "₦0.00 Official NAFEM",
                Change24h = "0.00%",
                SourceAttribution = "Central Bank of Nigeria (NAFEM) & Lagos Parallel Market Desk",
                ScrapedAt = now
            };
        }

        var faqs = new List<FaqItemDto>
        {
            new("What is the difference between the CBN official and parallel market exchange rates?",
                "The official rate is determined on the Nigerian Autonomous Foreign Exchange Market (NAFEM) managed by the Central Bank of Nigeria based on verified banking transactions. The parallel (black market) rate is determined by street currency dealers and Bureau de Change operators based on immediate cash demand and supply."),
            new("How does the Central Bank of Nigeria determine the official NAFEM rate?",
                "The CBN operates a willing-buyer, willing-seller model where commercial banks and authorized forex dealers submit transaction quotes transparently, creating an order-book weighted average price each business day."),
            new("Why does the exchange rate fluctuate frequently in Nigeria?",
                "Forex volatility is influenced by crude oil export revenues, foreign reserve levels, import demand for industrial inputs and refined fuel, sovereign debt obligations, and seasonal diaspora remittance inflows."),
            new("Where can individuals and businesses legally buy foreign currency in Nigeria?",
                "Under current financial guidelines, legal forex acquisitions are conducted through authorized commercial deposit banks via the Form A (education/medical) and Form Q (SME) windows, or through CBN-licensed Bureau de Change operators.")
        };

        if (record == null)
        {
            record = new MarketDataRecord
            {
                MetricKey = "naira-fx",
                CreatedAt = now
            };
            db.MarketDataRecords.Add(record);
        }

        record.Title = "Naira Exchange Rate (USD/NGN)";
        record.PrimaryValue = fxResult.PrimaryValue;
        record.SecondaryValue = fxResult.SecondaryValue;
        record.Change24h = fxResult.Change24h;
        record.Unit = "NGN per USD";
        record.SourceAttribution = fxResult.SourceAttribution;
        record.SummaryMarkdown = "Authoritative real-time tracker for the Nigerian Naira against the United States Dollar, Euro, British Pound, and Japanese Yen. Compares the Central Bank of Nigeria official NAFEM window with the open street parallel market, backed by historical volatility charts and two-way conversion calculators.";
        record.HistoricalDataJson = JsonSerializer.Serialize(new
        {
            points = fxResult.HistoricalPoints,
            currencies = fxResult.Currencies
        });
        record.FaqsJson = JsonSerializer.Serialize(faqs);
        record.LastUpdated = fxResult.ScrapedAt;
    }

    private static async Task SeedOrUpdateFuelPricesAsync(NewsDbContext db, CancellationToken ct)
    {
        var record = await db.MarketDataRecords.FirstOrDefaultAsync(r => r.MetricKey == "fuel-prices", ct);
        var now = DateTime.UtcNow;

        var statePoints = new List<MarketDataPointDto>
        {
            new() { Label = "Lagos", Date = "South West", PrimaryValue = 998.00, SecondaryValue = 1010.00, Note = "NNPCL Retail / Major Marketers" },
            new() { Label = "Abuja FCT", Date = "North Central", PrimaryValue = 1030.00, SecondaryValue = 1050.00, Note = "Federal Capital Outlets" },
            new() { Label = "Rivers (Port Harcourt)", Date = "South South", PrimaryValue = 1025.00, SecondaryValue = 1040.00, Note = "Coastal Marketers" },
            new() { Label = "Kano", Date = "North West", PrimaryValue = 1045.00, SecondaryValue = 1060.00, Note = "Commercial Centers" },
            new() { Label = "Oyo (Ibadan)", Date = "South West", PrimaryValue = 1010.00, SecondaryValue = 1025.00, Note = "South West Distribution" },
            new() { Label = "Enugu", Date = "South East", PrimaryValue = 1035.00, SecondaryValue = 1055.00, Note = "South East Hub" },
            new() { Label = "Delta (Warri / Asaba)", Date = "South South", PrimaryValue = 1020.00, SecondaryValue = 1035.00, Note = "Niger Delta Hub" },
            new() { Label = "Kaduna", Date = "North West", PrimaryValue = 1040.00, SecondaryValue = 1055.00, Note = "North West Axis" },
            new() { Label = "Borno (Maiduguri)", Date = "North East", PrimaryValue = 1060.00, SecondaryValue = 1080.00, Note = "North East Axis" },
            new() { Label = "Plateau (Jos)", Date = "North Central", PrimaryValue = 1035.00, SecondaryValue = 1050.00, Note = "Highland Hub" }
        };

        var faqs = new List<FaqItemDto>
        {
            new("Why do petrol pump prices differ between Lagos, Abuja, and other states?",
                "Price differentials are largely driven by transport logistics, haulage costs from coastal sea depots, and local distributor margins following full downstream deregulation by the Federal Government."),
            new("What impact has the Dangote Petroleum Refinery had on local petrol supply?",
                "Direct commercial supply from the Dangote Refinery to independent marketers has eased maritime demurrage costs and reduced foreign exchange exposure for fuel import vessels, promoting product availability across national terminals."),
            new("Who regulates fuel quality and pump metering in Nigeria?",
                "The Nigerian Midstream and Downstream Petroleum Regulatory Authority (NMDPRA) inspects retail filling stations nationwide to enforce safety, standard cubic metering accuracy, and fuel quality compliance."),
            new("What is the current national average petrol price?",
                "The current national average pump price hovers around ₦1,025 per litre at major marketer outlets, with variations between ₦998 in coastal depots and ₦1,060 in northern inland states.")
        };

        if (record == null)
        {
            record = new MarketDataRecord
            {
                MetricKey = "fuel-prices",
                CreatedAt = now
            };
            db.MarketDataRecords.Add(record);
        }

        record.Title = "PMS Petrol Pump Price in Nigeria";
        record.PrimaryValue = "₦1,025 / Litre";
        record.SecondaryValue = "₦998 – ₦1,060 Range";
        record.Change24h = "0.0%";
        record.Unit = "NGN per Litre";
        record.SourceAttribution = "Major Energies Marketers Association of Nigeria (MEMAN) & NNPCL Retail";
        record.SummaryMarkdown = "State-by-state price monitor for Premium Motor Spirit (PMS) fuel in Nigeria. Features live retail ranges across major Nigerian metropolitan centers and geopolitical zones.";
        record.HistoricalDataJson = JsonSerializer.Serialize(statePoints);
        record.FaqsJson = JsonSerializer.Serialize(faqs);
        record.LastUpdated = now;
    }

    private static async Task SeedOrUpdateJambWaecAsync(NewsDbContext db, CancellationToken ct)
    {
        var record = await db.MarketDataRecords.FirstOrDefaultAsync(r => r.MetricKey == "jamb-waec", ct);
        var now = DateTime.UtcNow;

        var timelinePoints = new List<MarketDataPointDto>
        {
            new() { Label = "UTME Profile Creation", Date = "January – February", PrimaryValue = 7700, Note = "Includes Reading Text & CBT fee" },
            new() { Label = "UTME Mock Examination", Date = "March", PrimaryValue = 0, Note = "Optional practice exam for registered candidates" },
            new() { Label = "Main UTME CBT Window", Date = "April – May", PrimaryValue = 0, Note = "Nationwide CBT testing across accredited centers" },
            new() { Label = "WAEC WASSCE Examination", Date = "May – June", PrimaryValue = 27000, Note = "Senior School Certificate Examination (School candidates)" },
            new() { Label = "NECO SSCE Examination", Date = "June – July", PrimaryValue = 22000, Note = "National Examinations Council annual school diet" }
        };

        var faqs = new List<FaqItemDto>
        {
            new("How do candidates generate a National Identification Number (NIN) profile code for JAMB?",
                "Candidates send an SMS with the word 'NIN' followed by a space and their 11-digit NIN number to either 55019 or 66019 from their registered mobile phone number to receive their 10-character profile code."),
            new("What is the official registration fee for the JAMB UTME examination?",
                "The standard UTME registration fee is ₦7,700 (including mock examination) or ₦6,200 without mock, payable exclusively via accredited CBT centers and approved banking channels."),
            new("When are WAEC and JAMB results typically released?",
                "JAMB releases UTME results within 5 to 7 days after candidate completion of CBT sessions. WAEC releases its May/June WASSCE results approximately 45 days after the conclusion of the final paper."),
            new("Where can students verify accredited CBT registration centers?",
                "JAMB maintains an updated list of certified CBT centers on its official portal (jamb.gov.ng). Registering at non-accredited cybercafés is strictly prohibited to avoid profile invalidation.")
        };

        if (record == null)
        {
            record = new MarketDataRecord
            {
                MetricKey = "jamb-waec",
                CreatedAt = now
            };
            db.MarketDataRecords.Add(record);
        }

        record.Title = "JAMB UTME & WAEC Examination Portal Updates";
        record.PrimaryValue = "Registration Active";
        record.SecondaryValue = "UTME Exam: April 2027";
        record.Change24h = "Verified";
        record.Unit = "Academic Cycle";
        record.SourceAttribution = "Joint Admissions and Matriculation Board (JAMB) & West African Examinations Council (WAEC)";
        record.SummaryMarkdown = "Comprehensive guide and calendar tracker for Nigerian tertiary matriculation and secondary certification exams. Covers official deadlines, fee caps, and accreditation updates.";
        record.HistoricalDataJson = JsonSerializer.Serialize(timelinePoints);
        record.FaqsJson = JsonSerializer.Serialize(faqs);
        record.LastUpdated = now;
    }

    private static async Task SeedOrUpdateElectionsAsync(NewsDbContext db, CancellationToken ct)
    {
        var record = await db.MarketDataRecords.FirstOrDefaultAsync(r => r.MetricKey == "elections", ct);
        var now = DateTime.UtcNow;

        var electionPoints = new List<MarketDataPointDto>
        {
            new() { Label = "Anambra Governorship Election", Date = "November 2025", PrimaryValue = 1, Note = "Off-cycle state executive election" },
            new() { Label = "Ekiti Governorship Election", Date = "June 2026", PrimaryValue = 1, Note = "Off-cycle state executive election" },
            new() { Label = "Osun Governorship Election", Date = "July 2026", PrimaryValue = 1, Note = "Off-cycle state executive election" },
            new() { Label = "Continuous Voter Registration (CVR)", Date = "Ongoing", PrimaryValue = 0, Note = "Online pre-registration & physical biometric capture" },
            new() { Label = "2027 General Elections", Date = "February 2027", PrimaryValue = 1, Note = "Presidential, National Assembly, and State elections" }
        };

        var faqs = new List<FaqItemDto>
        {
            new("What are off-cycle governorship elections in Nigeria?",
                "Off-cycle elections occur when the four-year tenure of an elected state governor concludes outside the regular general election cycle, typically resulting from judicial rulings that altered swearing-in dates (e.g., Anambra, Edo, Ondo, Ekiti, Osun, Bayelsa, Kogi, Imo)."),
            new("How does the Bimodal Voter Accreditation System (BVAS) work?",
                "BVAS is an electronic device deployed by INEC that verifies voters' identity using either fingerprint or facial biometrics before voting, and photographs/transmits signed polling unit result sheets directly to the IReV portal."),
            new("What is the INEC Result Viewing Portal (IReV)?",
                "IReV (inecelectionresults.ng) is a public web portal where scanned Form EC8A polling unit results are uploaded directly from polling units on election day for public auditing and transparency."),
            new("How can citizens check their voter registration status?",
                "Voters can verify their registration status, polling unit location, and delimitation details by visiting voters.inecnigeria.org and entering their state, surname, and date of birth.")
        };

        if (record == null)
        {
            record = new MarketDataRecord
            {
                MetricKey = "elections",
                CreatedAt = now
            };
            db.MarketDataRecords.Add(record);
        }

        record.Title = "Nigeria Elections & INEC Timetable Tracker";
        record.PrimaryValue = "Off-Cycle Governorship Prep";
        record.SecondaryValue = "General Polls: Feb 2027";
        record.Change24h = "Active";
        record.Unit = "Electoral Cycle";
        record.SourceAttribution = "Independent National Electoral Commission (INEC Nigeria)";
        record.SummaryMarkdown = "Authoritative tracker for upcoming off-cycle state governorship elections, voter registration timetables, BVAS accreditation procedures, and democratic governance news.";
        record.HistoricalDataJson = JsonSerializer.Serialize(electionPoints);
        record.FaqsJson = JsonSerializer.Serialize(faqs);
        record.LastUpdated = now;
    }
}
