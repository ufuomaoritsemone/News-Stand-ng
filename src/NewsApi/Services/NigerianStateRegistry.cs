namespace NewsApi.Services;

public record StateInfo(
    string Name,
    string Slug,
    string Capital,
    string GeopoliticalZone,
    string Slogan,
    string[] MajorCities,
    string Intro
);

public static class NigerianStateRegistry
{
    private static readonly List<StateInfo> _states =
    [
        new("Abia", "abia", "Umuahia", "South East", "God's Own State", ["Umuahia", "Aba", "Ohafia", "Arochukwu"],
            "Abia State is a prominent commercial and manufacturing hub in southeastern Nigeria, anchored by the vibrant industrial city of Aba and the administrative capital of Umuahia."),
        
        new("Adamawa", "adamawa", "Yola", "North East", "Land of Beauty", ["Yola", "Mubi", "Jimeta", "Numan"],
            "Adamawa State is an agrarian and educational center in northeastern Nigeria, situated along the scenic Benue River valley bordering Cameroon."),
        
        new("Akwa Ibom", "akwa-ibom", "Uyo", "South South", "Land of Promise", ["Uyo", "Eket", "Ikot Ekpene", "Oron"],
            "Akwa Ibom State is a premier hydrocarbon-producing and infrastructural beacon in Nigeria's Niger Delta, known for clean urban planning and maritime commerce."),
        
        new("Anambra", "anambra", "Awka", "South East", "Light of the Nation", ["Awka", "Onitsha", "Nnewi", "Ekwulobia"],
            "Anambra State boasts West Africa's largest open-air commercial market in Onitsha and a booming indigenous automotive and manufacturing cluster in Nnewi."),
        
        new("Bauchi", "bauchi", "Bauchi", "North East", "Pearl of Tourism", ["Bauchi", "Azare", "Misau", "Jama'are"],
            "Bauchi State is rich in cultural heritage and natural biodiversity, home to the renowned Yankari National Park and vast agricultural plains."),
        
        new("Bayelsa", "bayelsa", "Yenagoa", "South South", "Glory of All Lands", ["Yenagoa", "Brass", "Ogbia", "Nembe"],
            "Bayelsa State is the historic cradle of crude oil discovery in Nigeria, endowed with expansive mangrove waterways, coastal fisheries, and energy reserves."),
        
        new("Benue", "benue", "Makurdi", "North Central", "Food Basket of the Nation", ["Makurdi", "Gboko", "Otukpo", "Katsina-Ala"],
            "Benue State is Nigeria's agrarian powerhouse along the fertile banks of the Benue River, leading the federation in yam, cassava, and grain production."),
        
        new("Borno", "borno", "Maiduguri", "North East", "Home of Peace", ["Maiduguri", "Biu", "Bama", "Monguno"],
            "Borno State is a historic crossroads of trans-Saharan trade and Lake Chad basin commerce, demonstrating remarkable resilience and post-conflict recovery."),
        
        new("Cross River", "cross-river", "Calabar", "South South", "The People's Paradise", ["Calabar", "Ikom", "Ogoja", "Obudu"],
            "Cross River State is celebrated for its lush rainforest conservation, the annual Calabar Carnival, and the high-altitude Obudu Mountain Resort."),
        
        new("Delta", "delta", "Asaba", "South South", "The Big Heart", ["Asaba", "Warri", "Sapele", "Ughelli", "Agbor"],
            "Delta State is a vital economic pillar producing petroleum, natural gas, and maritime trade through the strategic commercial ports of Warri and Sapele."),
        
        new("Ebonyi", "ebonyi", "Abakaliki", "South East", "Salt of the Nation", ["Abakaliki", "Afikpo", "Onueke", "Ishielu"],
            "Ebonyi State is a key agricultural center renowned for Abakaliki rice cultivation, rich solid mineral deposits, and rapid infrastructural development."),
        
        new("Edo", "edo", "Benin City", "South South", "Heartbeat of the Nation", ["Benin City", "Auchi", "Ekpoma", "Uromi"],
            "Edo State is an ancient cultural civilization world-famous for the Benin Kingdom bronzes, rubber plantations, and growing petrochemical corridors."),
        
        new("Ekiti", "ekiti", "Ado-Ekiti", "South West", "Land of Honour and Integrity", ["Ado-Ekiti", "Ikere", "Ijero", "Oye"],
            "Ekiti State is acclaimed as Nigeria's intellectual capital with the highest concentration of academic professors, alongside picturesque granite hills."),
        
        new("Enugu", "enugu", "Enugu", "South East", "Coal City State", ["Enugu", "Nsukka", "Awgu", "Udi"],
            "Enugu State is the historic political and cultural capital of eastern Nigeria, celebrated for coal mining heritage, tertiary education, and lush escarpments."),
        
        new("Federal Capital Territory", "fct", "Abuja", "North Central", "Centre of Unity", ["Abuja", "Garki", "Wuse", "Maitama", "Gwarinpa", "Kuje", "Bwari"],
            "The Federal Capital Territory (FCT) is the seat of the Nigerian Federal Government, home to Aso Rock Villa, the National Assembly, and diplomatic missions."),
        
        new("Gombe", "gombe", "Gombe", "North East", "Jewel in the Savannah", ["Gombe", "Kaltungo", "Bajoga", "Billiri"],
            "Gombe State is a tranquil commercial hub and agricultural trading junction linking the northeastern states of the federation."),
        
        new("Imo", "imo", "Owerri", "South East", "Eastern Heartland", ["Owerri", "Orlu", "Okigwe", "Oguta"],
            "Imo State is renowned for hospitality, vibrant urban nightlife in Owerri, high educational literacy, and natural palm oil and gas resources."),
        
        new("Jigawa", "jigawa", "Dutse", "North West", "The New World", ["Dutse", "Hadejia", "Kazaure", "Gumel"],
            "Jigawa State features expansive savannah plains driving commercial sesame, hibiscus, and rice farming, supported by modern digital governance initiatives."),
        
        new("Kaduna", "kaduna", "Kaduna", "North West", "Centre of Learning", ["Kaduna", "Zaria", "Kafanchan", "Saminaka"],
            "Kaduna State is an iconic industrial, military, and educational gateway to northern Nigeria, hosting Ahmadu Bello University and historic railway junctions."),
        
        new("Kano", "kano", "Kano", "North West", "Centre of Commerce", ["Kano", "Wudil", "Bichi", "Rano"],
            "Kano State is Nigeria's most populous commercial powerhouse with over a millennium of trans-Saharan textile, groundnut, and retail trade."),
        
        new("Katsina", "katsina", "Katsina", "North West", "Home of Hospitality", ["Katsina", "Daura", "Funtua", "Malumfashi"],
            "Katsina State is an ancient center of Islamic scholarship and commerce, known for the historic Gobarau Minaret and deep agricultural trade."),
        
        new("Kebbi", "kebbi", "Birnin Kebbi", "North West", "Land of Equity", ["Birnin Kebbi", "Argungu", "Yauri", "Zuru"],
            "Kebbi State leads Nigeria in mechanized wet-season and dry-season rice production, famed internationally for the annual Argungu International Fishing Festival."),
        
        new("Kogi", "kogi", "Lokoja", "North Central", "The Confluence State", ["Lokoja", "Okene", "Kabba", "Idah", "Ajaokuta"],
            "Kogi State is uniquely situated at the confluence of the Niger and Benue rivers, serving as a transit bridge between northern and southern Nigeria."),
        
        new("Kwara", "kwara", "Ilorin", "North Central", "State of Harmony", ["Ilorin", "Offa", "Omu-Aran", "Lafiagi"],
            "Kwara State bridges Yoruba and northern cultural spheres, thriving on agriculture, educational institutions, and traditional pottery and textile crafts."),
        
        new("Lagos", "lagos", "Ikeja", "South West", "Centre of Excellence", ["Lagos", "Ikeja", "Lekki", "Victoria Island", "Ikorodu", "Epe", "Badagry", "Yaba"],
            "Lagos State is the economic, financial, and cultural powerhouse of Nigeria and West Africa, driving innovation, creative industries, and international logistics."),
        
        new("Nasarawa", "nasarawa", "Lafia", "North Central", "Home of Solid Minerals", ["Lafia", "Keffi", "Akwanga", "Karu"],
            "Nasarawa State borders the federal capital and possesses Nigeria's richest deposits of solid minerals, alongside vast commercial sesame and melon farming."),
        
        new("Niger", "niger", "Minna", "North Central", "The Power State", ["Minna", "Bida", "Suleja", "Kontagora"],
            "Niger State is Nigeria's largest state by landmass, generating the bulk of the nation's hydroelectricity from Kainji, Jebba, and Shiroro dams."),
        
        new("Ogun", "ogun", "Abeokuta", "South West", "Gateway State", ["Abeokuta", "Ijebu Ode", "Sagamu", "Ota"],
            "Ogun State is the foremost industrial manufacturing corridor in Nigeria, hosting the highest density of commercial factories, educational academies, and historical Olumo Rock."),
        
        new("Ondo", "ondo", "Akure", "South West", "Sunshine State", ["Akure", "Ondo Town", "Owo", "Ikare"],
            "Ondo State is Nigeria's largest cocoa-producing territory, endowed with extensive bitumen fields, coastal oil terminals, and the scenic Idanre Hills."),
        
        new("Osun", "osun", "Osogbo", "South West", "Land of Virtue", ["Osogbo", "Ile-Ife", "Ilesa", "Ede"],
            "Osun State is the ancestral cradle of Yoruba civilization at Ile-Ife, renowned globally for the UNESCO World Heritage Osun-Osogbo Sacred Grove."),
        
        new("Oyo", "oyo", "Ibadan", "South West", "Pace Setter State", ["Ibadan", "Ogbomoso", "Oyo", "Iseyin"],
            "Oyo State is a historic bastion of African governance, home to Ibadan—West Africa's largest indigenous city—and Nigeria's premier University of Ibadan."),
        
        new("Plateau", "plateau", "Jos", "North Central", "Home of Peace and Tourism", ["Jos", "Bukuru", "Pankshin", "Shendam"],
            "Plateau State enjoys a cool temperate highland climate on the Jos Plateau, producing exotic vegetables, fruits, and historic tin mining monuments."),
        
        new("Rivers", "rivers", "Port Harcourt", "South South", "Treasure Base of the Nation", ["Port Harcourt", "Bonny", "Eleme", "Ahoada"],
            "Rivers State is a global energy nerve-center with extensive petrochemical refineries, liquefied natural gas (NLNG) trains on Bonny Island, and vibrant seaports."),
        
        new("Sokoto", "sokoto", "Sokoto", "North West", "Seat of the Caliphate", ["Sokoto", "Tambuwal", "Wamakko", "Gwadabawa"],
            "Sokoto State is the spiritual headquarters of Nigerian Muslims as the seat of the historic Sokoto Caliphate, celebrated for leather tanning and livestock trade."),
        
        new("Taraba", "taraba", "Jalingo", "North East", "Nature's Gift to the Nation", ["Jalingo", "Wukari", "Gembu", "Bali"],
            "Taraba State encompasses the breathtaking high-altitude Mambilla Plateau, tea plantations, and rich livestock grazing lands."),
        
        new("Yobe", "yobe", "Damaturu", "North East", "Pride of the Sahel", ["Damaturu", "Potiskum", "Gashua", "Nguru"],
            "Yobe State is home to one of Africa's largest cattle markets in Potiskum, vibrant gum arabic trade, and traditional fishing wetlands."),
        
        new("Zamfara", "zamfara", "Gusau", "North West", "Farming is Our Pride", ["Gusau", "Kaura Namoda", "Talata Mafara", "Anka"],
            "Zamfara State is an agricultural and artisanal solid mineral territory in northwestern Nigeria, with vast gold deposits and grain reserves.")
    ];

    public static IReadOnlyList<StateInfo> AllStates => _states;

    public static StateInfo? FindBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var clean = slug.Trim().ToLowerInvariant();
        if (clean.EndsWith("-state"))
        {
            clean = clean[..^6];
        }

        if (clean == "abuja" || clean == "abuja-fct" || clean == "fct-abuja")
        {
            clean = "fct";
        }

        return _states.FirstOrDefault(s => s.Slug == clean || s.Name.Equals(clean, StringComparison.OrdinalIgnoreCase));
    }
}
