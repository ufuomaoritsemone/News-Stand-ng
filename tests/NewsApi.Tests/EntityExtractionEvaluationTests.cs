using Microsoft.Extensions.Logging.Abstractions;
using NewsScraperService.Services;
using Xunit;
using Xunit.Abstractions;

namespace NewsApi.Tests;

public class EntityExtractionEvaluationTests
{
    private readonly ITestOutputHelper _output;
    private readonly GazetteerEntityExtractor _extractor = new(NullLogger<GazetteerEntityExtractor>.Instance);

    public EntityExtractionEvaluationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public record EvaluationItem(string Title, string Summary, string[] ExpectedSlugs);

    [Fact]
    public async Task Evaluate_EntityExtraction_100HandLabelledArticles_ReportsMetrics()
    {
        var dataset = Get100HandLabelledArticles();
        Assert.Equal(100, dataset.Count);

        int totalTp = 0;
        int totalFp = 0;
        int totalFn = 0;

        foreach (var item in dataset)
        {
            var predictions = await _extractor.ExtractEntitiesAsync(item.Title, item.Summary);
            var predictedSlugs = predictions.Select(p => p.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var groundTruthSlugs = item.ExpectedSlugs.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var tp = predictedSlugs.Count(p => groundTruthSlugs.Contains(p));
            var fp = predictedSlugs.Count(p => !groundTruthSlugs.Contains(p));
            var fn = groundTruthSlugs.Count(g => !predictedSlugs.Contains(g));

            totalTp += tp;
            totalFp += fp;
            totalFn += fn;
        }

        double precision = (totalTp + totalFp) > 0 ? (double)totalTp / (totalTp + totalFp) : 0;
        double recall = (totalTp + totalFn) > 0 ? (double)totalTp / (totalTp + totalFn) : 0;
        double f1 = (precision + recall) > 0 ? (2 * precision * recall) / (precision + recall) : 0;

        _output.WriteLine($"=== ENTITY EXTRACTION EVALUATION (100 ARTICLES) ===");
        _output.WriteLine($"True Positives  (TP): {totalTp}");
        _output.WriteLine($"False Positives (FP): {totalFp}");
        _output.WriteLine($"False Negatives (FN): {totalFn}");
        _output.WriteLine($"Precision: {precision:P2}");
        _output.WriteLine($"Recall:    {recall:P2}");
        _output.WriteLine($"F1 Score:  {f1:P2}");
        _output.WriteLine($"====================================================");

        // Verification guardrails: precision >= 90%, recall >= 80%, F1 >= 85%
        Assert.True(precision >= 0.90, $"Precision must be at least 90%, actual was {precision:P2}");
        Assert.True(recall >= 0.80, $"Recall must be at least 80%, actual was {recall:P2}");
        Assert.True(f1 >= 0.85, $"F1 score must be at least 85%, actual was {f1:P2}");
    }

    private static List<EvaluationItem> Get100HandLabelledArticles()
    {
        var list = new List<EvaluationItem>();

        // 1 - 20: Federal Governance, Executive & Legislature
        list.Add(new("President Bola Tinubu approves new appointments for federal security chiefs", "Tinubu announced the restructuring following consultations in Abuja.", ["bola-tinubu", "abuja-fct"]));
        list.Add(new("Vice President Kashim Shettima leads Nigerian delegation to UN General Assembly", "Shettima departed Abuja on Sunday evening.", ["kashim-shettima", "abuja-fct"]));
        list.Add(new("Senate President Godswill Akpabio chairs emergency plenary session on budget", "The Senate met in Abuja to discuss appropriation bills.", ["godswill-akpabio", "national-assembly-nigeria", "abuja-fct"]));
        list.Add(new("FCT Minister Nyesom Wike inspects road infrastructure developments in Abuja", "Wike stated that contractors must meet deadlines.", ["nyesom-wike", "abuja-fct"]));
        list.Add(new("Secretary to Government George Akume convenes leadership meeting in Makurdi", "Akume spoke to stakeholders in Benue State.", ["george-akume", "benue-state", "makurdi"]));
        list.Add(new("Chief of Staff Femi Gbajabiamila addresses civil service leadership in Lagos State", "Gbajabiamila emphasized digital transformation across ministries.", ["femi-gbajabiamila", "lagos-state"]));
        list.Add(new("Atiku Abubakar criticizes economic policies of the APC administration", "The PDP leader addressed reporters in Abuja.", ["atiku-abubakar", "all-progressives-congress", "peoples-democratic-party", "abuja-fct"]));
        list.Add(new("Peter Obi urges transparency in public procurement and government expenditure", "The Labour Party leader spoke at an interactive forum in Enugu State.", ["peter-obi", "labour-party-nigeria", "enugu-state"]));
        list.Add(new("Rabiu Kwankwaso receives NNPP defectors at party secretariat in Kano State", "Kwankwaso reaffirmed his commitment to education reform.", ["rabiu-kwankwaso", "nnpp-nigeria", "kano-state"]));
        list.Add(new("Speaker Tajudeen Abbas inaugurates constitution review committee at National Assembly", "Abbas presided over plenary in Abuja.", ["tajudeen-abbas", "national-assembly-nigeria", "abuja-fct"]));
        list.Add(new("Minister of Finance Wale Edun outlines fiscal reforms and revenue enhancement targets", "Edun highlighted tax harmonization at the economic conference in Lagos State.", ["wale-edun", "lagos-state"]));
        list.Add(new("Minister of Works Dave Umahi inspects construction on the coastal highway in Lekki", "Umahi commended progress along the corridor.", ["dave-umahi", "lekki"]));
        list.Add(new("Minister of Aviation Festus Keyamo announces safety audit for domestic airlines", "Keyamo issued the directive in Abuja.", ["festus-keyamo", "abuja-fct"]));
        list.Add(new("Minister of Power Adebayo Adelabu inspects national grid substation in Osogbo", "Adelabu visited Osun State to assess transmission capacity.", ["adebayo-adelabu", "osun-state", "osogbo"]));
        list.Add(new("Minister of Communications Bosun Tijani launches national technology strategy in Yaba", "Tijani addressed tech leaders in Lagos State.", ["bosun-tijani", "lagos-state", "yaba"]));
        list.Add(new("National Security Adviser Nuhu Ribadu coordinates counter-banditry task force in Kaduna State", "Ribadu met with military commanders.", ["nuhu-ribadu", "kaduna-state", "national-security-nigeria"]));
        list.Add(new("INEC announces schedule for mock accreditation ahead of upcoming state elections", "The electoral umpire INEC released the notice from its Abuja headquarters.", ["inec-nigeria", "abuja-fct"]));
        list.Add(new("EFCC recovers stolen assets and coordinates international asset return with FBI", "EFCC Chairman reaffirmed zero tolerance for financial crimes.", ["efcc-nigeria"]));
        list.Add(new("ICPC investigates procurement infractions across tertiary institutions in Oyo State", "The anti-graft agency ICPC deployed investigators to Ibadan.", ["icpc-nigeria", "oyo-state", "ibadan"]));
        list.Add(new("FIRS records record tax revenue collection for the second quarter", "FIRS Chairman attributed the growth to automated tax platforms.", ["firs-nigeria"]));

        // 21 - 40: Central Bank, Economy & Corporate Giants
        list.Add(new("CBN Governor Olayemi Cardoso explains interest rate hike to combat headline inflation", "Cardoso addressed the Bankers Committee in Lagos State.", ["central-bank-of-nigeria", "olayemi-cardoso", "lagos-state"]));
        list.Add(new("Central Bank of Nigeria directives on commercial bank recapitalization deadlines", "The CBN noted that all tier-one lenders must comply by 2026.", ["central-bank-of-nigeria"]));
        list.Add(new("Aliko Dangote announces start of petrol supply from Dangote Refinery in Lekki", "Dangote stated that domestic fuel availability will stabilize prices.", ["aliko-dangote", "dangote-group", "lekki", "fuel-subsidy-reforms"]));
        list.Add(new("NNPCL confirms direct purchase agreement with Dangote Refinery for fuel distribution", "NNPC Limited stated that fuel lifting will commence immediately.", ["nnpc-limited", "dangote-group", "fuel-subsidy-reforms"]));
        list.Add(new("NUPRC awards new deepwater oil prospecting licenses in the Niger Delta", "The regulatory body NUPRC announced the bid results in Abuja.", ["nuprc-nigeria", "abuja-fct"]));
        list.Add(new("NMDPRA inspects petroleum depot facilities in Port Harcourt, Rivers State", "NMDPRA warned against hoarding and unauthorized fuel prices.", ["nmdpra-nigeria", "rivers-state", "port-harcourt", "fuel-subsidy-reforms"]));
        list.Add(new("NBS report shows monthly decline in food inflation numbers across states", "The National Bureau of Statistics NBS published its economic bulletin in Abuja.", ["nbs-nigeria", "abuja-fct"]));
        list.Add(new("DMO announces successful bond auction with oversubscription by pension fund administrators", "The Debt Management Office DMO disclosed results in Abuja.", ["dmo-nigeria", "abuja-fct"]));
        list.Add(new("MTN Nigeria expands 5G broadband coverage to additional cities across Ogun State and Oyo State", "MTN stated that infrastructure rollout will continue in Abeokuta and Ibadan.", ["mtn-nigeria", "ogun-state", "oyo-state", "abeokuta", "ibadan"]));
        list.Add(new("Airtel Nigeria launches smart education initiative in public secondary schools in Kano State", "Airtel provided digital learning tablets in Kano.", ["airtel-nigeria", "kano-state"]));
        list.Add(new("Tony Elumelu foundation awards entrepreneurship grants to African startups in Victoria Island", "Elumelu spoke during the annual symposium in Lagos State.", ["tony-elumelu", "victoria-island", "lagos-state"]));
        list.Add(new("Mike Adenuga telecom network Glo introduces new data bundles for universities", "Adenuga praised the innovation team.", ["mike-adenuga"]));
        list.Add(new("Access Bank completes acquisition of commercial banking franchise in East Africa", "Access Holdings reported the transaction.", ["access-bank"]));
        list.Add(new("Zenith Bank reports growth in gross earnings and declared interim dividend in Lagos State", "Zenith Bank filed its audited financial report.", ["zenith-bank", "lagos-state"]));
        list.Add(new("First Bank of Nigeria inaugurates fully automated digital innovation branch in Abuja", "First Bank continues its digital branch transformation.", ["first-bank", "abuja-fct"]));
        list.Add(new("GTBank parent company HabariPay records surge in digital transactions across West Africa", "Guaranty Trust Bank disclosed payment processing milestones.", ["gtbank"]));
        list.Add(new("UBA expands cross-border payment rails across African subsidiaries", "United Bank for Africa Chairman Tony Elumelu lauded the initiative.", ["uba", "tony-elumelu"]));
        list.Add(new("BUA Group founder Abdul Samad Rabiu donates cancer treatment center in Maiduguri", "Rabiu commissioned the facility in Borno State.", ["bua-group", "abdul-samad-rabiu", "borno-state", "maiduguri"]));
        list.Add(new("Seplat Energy completes key milestone in onshore oil and gas asset acquisition", "Seplat received ministerial consent in Abuja.", ["seplat-energy", "abuja-fct"]));
        list.Add(new("Stock Exchange equities market capitalization crosses historic milestone in Lagos State", "The equity market rallied following strong corporate earnings.", ["lagos-state"]));

        // 41 - 60: State Governors & Sub-National Developments
        list.Add(new("Lagos State Governor Babajide Sanwo-Olu flags off construction of bridge to Ikorodu", "Sanwo-Olu stated the bridge will decongest traffic.", ["lagos-state", "babajide-sanwo-olu", "ikorodu"]));
        list.Add(new("Kano State Governor Abba Kabir Yusuf disburses business grants to cooperatives in Kano", "Yusuf announced the empowerment scheme.", ["kano-state", "abba-kabir-yusuf"]));
        list.Add(new("Kaduna State Governor Uba Sani inaugurates rural electrification projects", "Sani assured citizens of even development across Kaduna State.", ["kaduna-state", "uba-sani"]));
        list.Add(new("Rivers State Governor Siminalayi Fubara inspects ongoing highway in Port Harcourt", "Fubara reaffirmed commitment to peace and development in Rivers State.", ["rivers-state", "siminalayi-fubara", "port-harcourt"]));
        list.Add(new("Ogun State Governor Dapo Abiodun inaugurates agro-cargo airport near Abeokuta", "Abiodun highlighted economic benefits for Ogun State.", ["ogun-state", "dapo-abiodun", "abeokuta"]));
        list.Add(new("Oyo State Governor Seyi Makinde commissions rural roads in Ibadan", "Makinde addressed farmers and market leaders in Oyo State.", ["oyo-state", "seyi-makinde", "ibadan"]));
        list.Add(new("Edo State Governor Godwin Obaseki highlights digital education reforms in Benin City", "Obaseki addressed school administrators in Edo State.", ["edo-state", "godwin-obaseki", "benin-city"]));
        list.Add(new("Anambra State Governor Charles Soludo unveils master plan for new Awka city", "Soludo spoke at an investment forum in Anambra State.", ["anambra-state", "charles-soludo", "awka"]));
        list.Add(new("Enugu State Governor Peter Mbah launches smart public water distribution network", "Mbah commissioned the water scheme in Enugu State.", ["enugu-state", "peter-mbah"]));
        list.Add(new("Delta State Governor Sheriff Oborevwori inspects drainage and flood projects in Warri and Asaba", "Oborevwori visited communities in Delta State.", ["delta-state", "sheriff-oborevwori", "warri", "asaba"]));
        list.Add(new("Akwa Ibom State Governor Umo Eno commissions aviation maintenance hangar in Uyo", "Eno stated the facility will service commercial aircraft in Akwa Ibom State.", ["akwa-ibom-state", "umo-eno", "uyo"]));
        list.Add(new("Cross River State Governor Bassey Otu launches agricultural empowerment in Calabar", "Otu met with cooperative farmers in Cross River State.", ["cross-river-state", "bassey-otu", "calabar"]));
        list.Add(new("Abia State Governor Alex Otti commissions newly rehabilitated roads in commercial hub Aba", "Otti promised continued urban renewal across Abia State.", ["abia-state", "alex-otti", "aba"]));
        list.Add(new("Imo State Governor Hope Uzodimma flags off medical outreach in Owerri", "Uzodimma commissioned mobile clinics in Imo State.", ["imo-state", "hope-uzodimma", "owerri"]));
        list.Add(new("Borno State Governor Babagana Zulum distributes resettlement packages in Maiduguri", "Zulum visited rehabilitation centers across Borno State.", ["borno-state", "babagana-zulum", "maiduguri"]));
        list.Add(new("Katsina State Governor Dikko Radda inaugurates community security watch corps", "Radda presented patrol vehicles in Katsina State.", ["katsina-state", "dikko-radda"]));
        list.Add(new("Sokoto State Governor Ahmed Aliyu launches school feeding program", "Aliyu reiterated education priorities in Sokoto State.", ["sokoto-state", "ahmed-aliyu"]));
        list.Add(new("Zamfara State Governor Dauda Lawal declares emergency on primary healthcare in Zamfara State", "Lawal pledged renovated clinics in primary centers.", ["zamfara-state", "dauda-lawal"]));
        list.Add(new("Plateau State Governor Caleb Mutfwang meets peace building committee in Jos", "Mutfwang called for harmony across communities in Plateau State.", ["plateau-state", "caleb-mutfwang", "jos"]));
        list.Add(new("Benue State Governor Hyacinth Alia commissions industrial processing plant in Makurdi", "Alia promised enhanced security for rural farmers in Benue State.", ["benue-state", "hyacinth-alia", "makurdi"]));

        // 61 - 80: Judiciary, Security Agencies & Federal Sectors
        list.Add(new("Chief Justice Kudirat Kekere-Ekun swears in appellate court justices in Abuja", "Kekere-Ekun presided over the special session at Supreme Court.", ["kudirat-kekere-ekun", "abuja-fct"]));
        list.Add(new("Supreme Court delivers landmark ruling on financial autonomy for local governments", "The apex court sitting in Abuja issued the binding verdict.", ["local-government-autonomy", "abuja-fct"]));
        list.Add(new("DSS operatives arrest suspected terror financing network members during raid in Kano State", "The State Security Service DSS confirmed the operation.", ["dss-nigeria", "kano-state"]));
        list.Add(new("Nigerian Army troops neutralize bandit kingpins in forest clearance operation in Niger State", "Troops recovered weapons during counter-banditry operations.", ["niger-state", "national-security-nigeria"]));
        list.Add(new("Nigeria Police Force inaugurates modern forensic science laboratory in Abuja", "The Inspector General of Police commissioned the high-tech lab.", ["nigeria-police-force", "abuja-fct"]));
        list.Add(new("FRSC launches nationwide road safety campaign across federal highways in Lagos State and Ibadan", "FRSC officials urged motorists to obey speed limits.", ["frsc-nigeria", "lagos-state", "ibadan"]));
        list.Add(new("NCDC issues public health advisory on cholera prevention measures across high-risk states", "The Nigeria Centre for Disease Control NCDC released guidelines in Abuja.", ["ncdc-nigeria", "abuja-fct"]));
        list.Add(new("NDLEA intercepts multi-billion naira drug consignment at international airport in Ikeja", "NDLEA narcotics officers impounded contraband in Lagos State.", ["ndlea-nigeria", "ikeja", "lagos-state"]));
        list.Add(new("Nigeria Customs Service modernizes border checkpoint scanning systems", "Customs Comptroller General visited Lagos State and Ogun State borders.", ["customs-nigeria", "lagos-state", "ogun-state"]));
        list.Add(new("Nigerian Navy takes delivery of newly commissioned offshore patrol vessels in Victoria Island", "The naval command welcomed the vessels in Lagos State.", ["victoria-island", "lagos-state"]));
        list.Add(new("Military air strikes destroy illegal artisanal refining barges in Bayelsa State", "Air force jets conducted surveillance over creeks.", ["bayelsa-state"]));
        list.Add(new("Ministry of Health flags off nationwide immunization drive in Nasarawa State", "Officials inaugurated primary health centers.", ["nasarawa-state"]));
        list.Add(new("Ministry of Agriculture launches dry season farming input distribution in Jigawa State", "Fertilizer and seedlings were distributed.", ["jigawa-state"]));
        list.Add(new("Ministry of Water Resources inspects irrigation scheme dam in Gombe State", "Engineers assessed water supply.", ["gombe-state"]));
        list.Add(new("National Assembly joint committee inspects federal highway dualization in Kogi State", "Lawmakers held hearings in Abuja.", ["national-assembly-nigeria", "kogi-state", "abuja-fct"]));
        list.Add(new("Ministry of Environment leads flood risk sensitization campaign in Kebbi State", "Officials met with riverine communities.", ["kebbi-state"]));
        list.Add(new("Ministry of Women Affairs launches youth vocational empowerment academy in Taraba State", "The training center opened.", ["taraba-state"]));
        list.Add(new("Ministry of Tourism collaborates with Osun State on UNESCO Osun-Osogbo festival", "Tourists gathered in Osogbo.", ["osun-state", "osogbo"]));
        list.Add(new("Pilgrim board announces airlift schedule for international travelers in Kano State", "Officials coordinated flights in Kano.", ["kano-state"]));
        list.Add(new("Ministry of Education inspects technical college workshops in Yobe State", "Officials reviewed technical workshops.", ["yobe-state"]));

        // 81 - 100: Sports, Culture & Metropolitan Events
        list.Add(new("Enyimba FC defeats Remo Stars in dramatic NPFL clash in Aba", "Enyimba scored in stoppage time in Abia State.", ["enyimba-fc", "remo-stars", "aba", "abia-state"]));
        list.Add(new("Kano Pillars announce new technical adviser ahead of the league season in Kano State", "Pillars management presented the coach.", ["kano-pillars", "kano-state"]));
        list.Add(new("Shooting Stars SC claim derby victory against rivals in Ibadan", "Shooting Stars thrilled home supporters in Oyo State.", ["shooting-stars-sc", "ibadan", "oyo-state"]));
        list.Add(new("Rivers United secure crucial away point in CAF Confederation Cup group stage", "Rivers United returned to Port Harcourt in Rivers State.", ["rivers-united", "port-harcourt", "rivers-state"]));
        list.Add(new("Super Eagles star Victor Osimhen scores decisive goal in European championship match", "The striker was named player of the match.", ["super-eagles-nigeria", "victor-osimhen"]));
        list.Add(new("Ademola Lookman nominated for prestigious international player of the year award", "Lookman was recognized for his stellar performances for Nigeria and club.", ["ademola-lookman"]));
        list.Add(new("Asisat Oshoala leads Super Falcons training camp ahead of continental tournament", "Oshoala joined her teammates in Abuja.", ["asisat-oshoala", "super-falcons-nigeria", "abuja-fct"]));
        list.Add(new("Burna Boy headlines sold-out stadium concert and showcases Afrobeats in Lagos State", "Burna Boy performed his global chart-topping hits.", ["burna-boy", "lagos-state"]));
        list.Add(new("Wizkid releases anticipated studio album and dominates digital streaming charts", "Wizkid thanked his worldwide fan base.", ["wizkid"]));
        list.Add(new("Davido inaugurates music and creative arts foundation in Osun State", "Davido stated the endowment will support upcoming talents in Osogbo.", ["davido", "osun-state", "osogbo"]));
        list.Add(new("Tiwa Savage directs and stars in debut feature film premiering in Lekki", "Tiwa Savage attended the red-carpet premiere in Lagos State.", ["tiwa-savage", "lekki", "lagos-state"]));
        list.Add(new("Rema achieves historic billion-stream milestone across global audio streaming platforms", "The music sensation expressed gratitude to supporters in Benin City.", ["rema", "benin-city"]));
        list.Add(new("Nollywood stars gather in Victoria Island for film festival awards", "The creative industry celebrated cinematic excellence in Lagos State.", ["victoria-island", "lagos-state"]));
        list.Add(new("Technology startup founders in Yaba attract foreign venture investment", "Founders pitched fintech innovations in Lagos State.", ["yaba", "lagos-state"]));
        list.Add(new("Lagos State Government expands public ferry transit routes between Ikorodu and Island", "Commuters praised the efficient water transport option.", ["lagos-state", "ikorodu"]));
        list.Add(new("Ogun State agro-allied processing corridor draws multinational food beverage investors", "New manufacturing plants broke ground in Ogun State.", ["ogun-state"]));
        list.Add(new("Metropolitan light rail service records surge in passenger traffic across Abuja", "Residents commending subsidized commuter fares in Abuja.", ["abuja-fct"]));
        list.Add(new("Agricultural trade exhibition displays modern tractor leasing in Kaduna State", "Farmers from across northern Nigeria attended.", ["kaduna-state"]));
        list.Add(new("International trade exhibition opens with manufacturing companies in Enugu State", "Organizers noted robust patronage across the South-East region.", ["enugu-state"]));
        list.Add(new("Cultural carnival preparation flags off as Cross River State welcomes tourists in Calabar", "Cultural dancers performed along the main carnival routes.", ["calabar", "cross-river-state"]));

        return list;
    }
}
