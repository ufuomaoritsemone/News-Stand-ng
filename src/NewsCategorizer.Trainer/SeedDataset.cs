namespace NewsCategorizer.Trainer;

public static class SeedDataset
{
    public static readonly List<NewsArticleRecord> InitialSamples = new()
    {
        // ==========================================
        // 1. POLITICS (30 samples)
        // ==========================================
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Tinubu signs executive order on tax reform and fiscal governance in Abuja",
            Summary = "President Bola Ahmed Tinubu has signed a new executive order aimed at restructuring tax policies and fiscal responsibilities across federal ministries and agencies.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Senate passes amended electoral bill after intense debate at National Assembly plenary",
            Summary = "Federal lawmakers in the Senate approved major amendments to the Electoral Act governing electronic transmission of election results during Wednesday plenary session.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "APC governor presents 450 billion naira annual appropriation budget to State House of Assembly",
            Summary = "The executive governor presented the state fiscal budget proposal focusing on rural infrastructure, basic healthcare, and civil service pensions.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "INEC releases official timetable and guidelines for upcoming off-cycle governorship elections",
            Summary = "The Independent National Electoral Commission has scheduled dates and continuous voter registration for the gubernatorial polls in Edo and Ondo states.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "PDP National Working Committee convenes emergency meeting over party leadership crisis",
            Summary = "Opposition party stakeholders and governors met in Abuja to resolve ongoing factional disputes regarding national chairman succession and zoning formula.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Federal Government inaugurates tripartite committee on new national minimum wage implementation",
            Summary = "Minister of Labour and civil society representatives constitute federal monitoring council to ensure statutory salary compliance across all 36 states.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "House of Representatives committee summons ministers over capital budget implementation and expenditure",
            Summary = "House Committee on Public Accounts demanded immediate appearance of federal ministers to defend capital project allocations and procurement transparency.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Supreme Court upholds victory of state governor, dismisses opposition petition",
            Summary = "A seven-man panel of Supreme Court justices ruled that the petitioner failed to substantiate allegations of over-voting and non-compliance with the electoral guidelines.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "President Tinubu presides over Federal Executive Council meeting at State House Villa",
            Summary = "Ministers and presidential aides gathered at the Council Chamber in Aso Rock to deliberate on new economic initiatives and ministerial scorecards.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Labour Party factional leaders clash at national secretariat over convention delegates list",
            Summary = "Supporters of rival party executives engaged in physical altercation following disagreements over delegate accreditation ahead of national convention.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Governors Forum meets in Abuja to discuss local government financial autonomy and state policing",
            Summary = "The Nigeria Governors Forum held a closed-door summit to reach consensus on Supreme Court rulings granting direct federal allocation to local councils.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Senate confirms new ministerial nominees following screening at committee of the whole",
            Summary = "Red Chamber lawmakers concluded thorough screening of presidential nominees for federal ministerial and ambassadorial appointments.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Osun governorship election: Political parties sign peace accord ahead of Saturday poll",
            Summary = "Gubernatorial candidates from APC, PDP, LP and other political parties pledged commitment to non-violence and peaceful conduct at INEC peace accord signing ceremony.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Vice President Shettima leads high-level presidential delegation to regional diplomatic summit",
            Summary = "The Vice President represented Nigeria at multilateral talks focusing on regional security cooperation, democratic governance, and economic integration.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "National Assembly approves presidential request for supplementary statutory budget allocation",
            Summary = "Both chambers of parliament passed the revised expenditure framework to fund critical security operations and social welfare interventions.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Former presidential candidate Peter Obi calls for reduction in cost of public governance",
            Summary = "Opposition figure urged federal administration to eliminate redundant government agencies and prioritize education and healthcare spending.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Rivers State House of Assembly passes new legislative resolution amid executive standoff",
            Summary = "Lawmakers loyal to legislative leadership held plenary session to review governor policy decisions and local government transition committees.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Atiku Abubakar faults federal economic policies and calls for coalition of opposition parties",
            Summary = "Former Vice President and PDP presidential flagbearer addressed national press conference proposing strategic alliance to challenge ruling party.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Kano State Governor dissolves cabinet and appoints new special advisers in major reshuffle",
            Summary = "Executive governor announced restructuring of state ministries, departments, and agencies to accelerate development agenda in the state.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "National Chairman of APC welcomes defecting state lawmakers from opposition party",
            Summary = "Ruling party leadership hosted reception ceremony for opposition members of state assembly who formally decamped to the All Progressives Congress.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Senate President Godswill Akpabio assures Nigerians on speedy passage of national youth bill",
            Summary = "Presiding officer of the upper legislative chamber addressed youth leaders on parliamentary efforts to institutionalize affirmative representation in governance.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "INEC declares APC candidate winner of parliamentary by-election in northern senatorial district",
            Summary = "Returning officer announced final collated figures showing ruling party candidate defeated closest rival by twenty thousand valid votes.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Federal government appoints new ambassadors and high commissioners to overseas diplomatic missions",
            Summary = "Ministry of Foreign Affairs issued official list of career and political diplomats assigned to Nigerian embassies worldwide.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Lagos State Governor Babajide Sanwo-Olu signs bill creating new local council development authorities",
            Summary = "State chief executive signed executive enactment following unanimous passage by state parliamentarians at Alausa assembly.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Civil society groups stage peaceful demonstration at National Assembly gate demanding electoral reforms",
            Summary = "Coalition of pro-democracy activists submitted memorandum to lawmakers advocating for mandatory real-time electronic transmission of polling booth results.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Speaker Tajudeen Abbas inaugurates special parliamentary committee on constitutional review",
            Summary = "Green Chamber leader charged committee members to engage citizens across 360 federal constituencies on state creation, devolution of powers, and judicial reform.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "President Bola Tinubu reconstitutes governing boards of federal tertiary institutions and parastatals",
            Summary = "Secretary to the Government of the Federation released names of prominent technocrats and political figures appointed to chair university councils.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Ruling party holds national executive committee NEC meeting in Abuja to approve convention date",
            Summary = "Party chieftains, serving governors, and legislative caucuses met at the national secretariat to ratify proposed constitutional amendments.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Edo State Gubernatorial Election: Observers commend INEC on early arrival of sensitive election materials",
            Summary = "Domestic election observation monitors noted marked improvements in BVAS accreditation speed and security deployment across polling units.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Politics",
            Title = "Political parties finalize candidate primaries ahead of local government council elections",
            Summary = "State Independent Electoral Commission confirmed receipt of candidate nomination forms from registered political parties for chairmanship seats.",
            Source = "SeedData"
        },

        // ==========================================
        // 2. BUSINESS (30 samples)
        // ==========================================
        new NewsArticleRecord {
            Category = "Business",
            Title = "Central Bank of Nigeria raises monetary policy rate by 50 basis points to curb inflation",
            Summary = "The Monetary Policy Committee MPC of the CBN announced an increase in the benchmark interest rate to 27.25 percent to anchor inflation expectations and stabilize the foreign exchange market.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Naira appreciates against US dollar at official autonomous foreign exchange market NAFEM",
            Summary = "The Nigerian domestic currency recorded significant gains at the official FX window following sustained liquidity interventions and clearing of verified forex backlogs by the apex bank.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Nigerian Exchange NGX All-Share Index crosses 100,000 points landmark on banking equities rally",
            Summary = "Stock market capitalization surged by over 400 billion naira as investors aggressively purchased shares of Tier-1 banks, oil marketing firms, and industrial conglomerates.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Dangote Petroleum Refinery commences nationwide supply of petrol and diesel to domestic marketers",
            Summary = "The 650,000 barrels per day mega refinery in Lekki began direct loading of refined petroleum motor spirit to major and independent oil marketers associations across Nigeria.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "NNPC Limited reports crude oil production surge to 1.8 million barrels per day",
            Summary = "State-owned energy corporation announced increased upstream crude extraction following improved pipeline security collaboration and new offshore well completions in the Niger Delta.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Commercial banks record surge in half-year profit before tax driven by FX revaluation and net interest income",
            Summary = "Financial earnings reports submitted to the Nigerian Exchange show leading tier-one banks posting record revenues from electronic payment channels, retail lending, and treasury securities.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "National Bureau of Statistics NBS reports headline inflation decelerates to 28 percent",
            Summary = "Consumer Price Index data published by the statistical bureau revealed moderation in month-on-month core and food inflation indices following harvest season output.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "World Bank approves 750 million dollar financing package for power sector infrastructure in Nigeria",
            Summary = "The multilateral development institution authorized concessional credit facility to strengthen electricity transmission grid reliability and scale distributed renewable energy networks.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Federal Government raises 350 billion naira through monthly domestic sovereign bond auction",
            Summary = "The Debt Management Office DMO disclosed that long-tenor FGN bonds were heavily oversubscribed by institutional investors, pension fund administrators, and insurance firms.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Manufacturers Association of Nigeria MAN urges government to lower interest rates on industrial loans",
            Summary = "Industrial lobby group highlighted high energy costs, port congestion tariffs, and monetary tightening as major factors constraining local factory output and job creation.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Seplat Energy completes landmark acquisition of ExxonMobil shallow water oil assets in Nigeria",
            Summary = "Indigenous energy company finalized multi-million dollar transaction following regulatory clearance from the Nigerian Upstream Petroleum Regulatory Commission NUPRC.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Pension Fund Administrators total assets under management hit 20 trillion naira milestone",
            Summary = "The National Pension Commission PenCom reported robust expansion in pension industry funds driven by investments in federal government securities and corporate equities.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "MTN Nigeria reports revenue growth from data services and mobile financial fintech expansion",
            Summary = "Telecommunications giant posted double-digit quarterly revenue gains supported by surge in broadband traffic, 5G enterprise subscriptions, and MoMo PSB transaction volume.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "OPEC agrees on crude oil production quotas at ministerial meeting in Vienna",
            Summary = "Petroleum exporting countries decided to extend voluntary oil output reductions to maintain price stability across global Brent crude benchmark markets.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Zenith Bank announces interim dividend payout to shareholders following strong Q2 financial earnings",
            Summary = "Board of directors approved cash dividend distribution after the financial institution recorded substantial growth in customer deposits and gross operating revenue.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "AfCFTA Secretariat partners with Nigerian exporters to boost intra-African agricultural trade",
            Summary = "Pan-African trade pact initiative launched zero-tariff shipment corridor for non-oil exports including cocoa, sesame seeds, and processed cashew nuts to East and North Africa.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Nigeria customs revenue collection surpasses annual target with four trillion naira generated",
            Summary = "Comptroller General of Customs credited modern e-customs trade portals, container scanning automation, and anti-smuggling tariff enforcement for the record fiscal collection.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "International Monetary Fund IMF projects 3.1 percent economic GDP growth for Nigeria",
            Summary = "Global financial body released its regional economic outlook praising fiscal reforms and revenue mobilization while recommending targeted social safety nets for vulnerable households.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Geregu Power records 80 percent rise in quarterly profit after tax on expanded electricity generation",
            Summary = "Independent power producer listed on the NGX reported increased generation capacity and improved gas supply reliability following maintenance overhaul of gas turbines.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Fidelity Bank successfully concludes capital raising rights issue and public share subscription",
            Summary = "Tier-two lender raised over 120 billion naira in fresh equity capital from local and international institutional investors to meet CBN recapitalization directives.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Federal Inland Revenue Service FIRS surpasses non-oil tax revenue collection milestone",
            Summary = "Tax authorities achieved historic revenue figures from Company Income Tax CIT, Value Added Tax VAT, and electronic money transfer levies.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Dangote Sugar and Flour Mills announce strategic merger to create integrated agro-allied conglomerate",
            Summary = "Agribusiness giants announced board approvals for corporate restructuring to streamline supply chain logistics and expand food processing capacity across West Africa.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Nigerian ports authority NPA records 20 percent increase in container cargo throughput",
            Summary = "Managing Director of NPA attributed port performance gains to round-the-clock berthing operations, electronic call-up truck system, and dredging of deep sea channels.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Treasury bills stop rates drop across all tenors as liquidity surges in interbank money market",
            Summary = "Central Bank auction results revealed strong investor demand for 364-day sovereign bills with total subscription exceeding initial offer by 300 percent.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Aviation fuel price hike threatens domestic airline operators with fare increases",
            Summary = "Airline Operators of Nigeria warned that escalating Jet A1 fuel costs and foreign exchange scarcity could force carriers to adjust ticket prices across domestic routes.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "BUA Cement commissions new 3 million metric tonnes production line in Sokoto plant",
            Summary = "Industrial group expanded total manufacturing capacity to meet rising domestic infrastructure construction demand while increasing clinker export shipments.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Bank recapitalization: CBN sets minimum capital thresholds for national and international commercial banks",
            Summary = "Apex monetary regulator issued guidelines directing financial institutions to bolster balance sheet buffers through rights issues, private placements, or mergers.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Cocoa farmers in Ondo and Cross River record bumper harvest profits amid global price surge",
            Summary = "Commodity exporters in southern cocoa belt enjoyed record farmgate pricing as European bean shortages drove global ICE futures to multi-year highs.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Access Holdings receives regulatory approval for acquisition of banking subsidiaries in Southern Africa",
            Summary = "Financial services group expanded pan-African banking franchise following successful closing of acquisition deals in Kenya, South Africa, and Mozambique.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Business",
            Title = "Federal government establishes 100 billion naira consumer credit fund to stimulate retail economy",
            Summary = "National Credit Corporation began disbursement of low-interest asset financing loans to working civil servants and private sector employees.",
            Source = "SeedData"
        },

        // ==========================================
        // 3. SPORTS (30 samples)
        // ==========================================
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Super Eagles striker Victor Osimhen scores stunning hat-trick in AFCON qualifier match",
            Summary = "Nigeria national football team secured an emphatic 4-1 victory over their Group opponent with a masterclass attacking display from the Galatasaray star striker.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Super Falcons qualify for FIFA Women World Cup knockout stage after historic victory",
            Summary = "The Nigerian women national football team advanced to the tournament round of 16 following a disciplined 2-0 win over South Africa in Rabat.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Premier League: Arsenal defeat Manchester City 2-1 in dramatic title race clash at Emirates",
            Summary = "Bukayo Saka scored the decisive winning goal in stoppage time as Mikel Arteta side earned vital three points in the English Premier League table.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "NPFL Champions Enyimba FC unveil new European head coach ahead of CAF Champions League tournament",
            Summary = "The Aba-based Nigerian Premier Football League champions signed experienced manager to lead their continental campaign in Africa premier club competition.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Nigerian world record holder Tobi Amusan clinches gold medal in 100m hurdles at Diamond League",
            Summary = "World champion sprint hurdler delivered a season-best timing of 12.33 seconds to finish first ahead of international rivals in Zurich track final.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Real Madrid and Chelsea agree 85 million euro transfer fee for international winger",
            Summary = "The European champions finalized summer transfer window agreement for the attacking playmaker on a five-year contract with medical scheduled in Madrid.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Nigeria Football Federation NFF appoints new technical director for grassroots youth academies",
            Summary = "The football governing body unveiled strategic development roadmap to scout emerging talent across national under-17 and under-20 squads.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Ademola Lookman nominated for Ballon d'Or award after stellar season with Atalanta",
            Summary = "Nigerian winger made the 30-man shortlist for football most prestigious individual award following his Europa League final hat-trick heroics.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Manchester United announce signing of Bayer Leverkusen striker Victor Boniface in blockbuster deal",
            Summary = "The Premier League giants completed the signing of the Nigerian international forward to bolster their attacking options for the new European football season.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "D'Tigress qualify for Olympic Games Women Basketball tournament quarter-finals in Paris",
            Summary = "The reigning African basketball champions made history by defeating world top-ranked opponents with outstanding performances from Ezinne Kalu and Amy Okonkwo.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Remo Stars defeat Rivers United 2-0 to climb top of Nigeria Premier Football League standings",
            Summary = "Ikenne-based club secured home victory with two second-half goals from their top goalscorer in a thrilling Southwest derby encounter.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "CAF announces host countries for upcoming Africa Cup of Nations tournaments",
            Summary = "Confederation of African Football executive committee confirmed official host nations for the 2027 and 2029 continental football tournaments.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Anthony Joshua knocks out opponent in fifth round of heavyweight championship boxing bout",
            Summary = "Former two-time unified world heavyweight champion delivered a devastating right hand knockout at Wembley Stadium to earn title shot.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "UEFA Champions League: Real Madrid defeat Bayern Munich in semi-final thriller to reach final",
            Summary = "Vinicius Junior and Jude Bellingham combined to score crucial goals in a pulsating European clash at the Santiago Bernabeu.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "FIFA releases latest men world rankings: Super Eagles climb five spots in global standings",
            Summary = "International football governing body published revised national team rankings following recent World Cup qualifying matches and international friendlies.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Alex Iwobi and Calvin Bassey star as Fulham secure 3-1 victory over Tottenham in London derby",
            Summary = "Nigerian international duo produced commanding midfield and defensive displays to help their club collect maximum points in Premier League fixture.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Kano Pillars secure hard-fought 1-0 away victory over Rangers International in NPFL clash",
            Summary = "Veteran midfielder Rabiu Ali scored a sublime free-kick in the 88th minute to seal a famous away win in Enugu.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Stanley Nwabali signs contract extension with Chippa United amid European transfer interest",
            Summary = "Super Eagles first-choice goalkeeper agreed fresh multi-year terms with South African Premier Soccer League club following impressive AFCON tournament.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Liverpool appoint new head coach to succeed Jurgen Klopp on three-year contract",
            Summary = "Anfield club hierarchy officially introduced Dutch manager to lead the squad ahead of the upcoming Premier League and Champions League seasons.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Nigerian table tennis legend Quadri Aruna advances to World Table Tennis WTT tournament semi-finals",
            Summary = "Africa top-ranked table tennis star produced stunning forehand winners to defeat European champion in five thrilling sets.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Kylian Mbappe scores twice on home debut as Real Madrid win 3-0 in La Liga opener",
            Summary = "French superstar forward delighted eighty thousand spectators with a sensational brace at the Santiago Bernabeu stadium.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Flying Eagles defeat Ghana 2-1 to lift WAFU Zone B Under-20 Championship trophy",
            Summary = "Nigeria junior national team secured regional glory with disciplined defensive display and clinical finishing in the final at Stade Municipal.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Inter Milan win Italian Serie A title after victory in Milan derby against AC Milan",
            Summary = "Simone Inzaghi side celebrated their twentieth Scudetto championship with five matches to spare after commanding performance at San Siro.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Nigerian weightlifter Rafiatu Lawal wins gold medal at Commonwealth Weightlifting Championship",
            Summary = "National champion lifted a combined total of 206kg in snatch and clean and jerk to break previous African championship record.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Chelsea complete 60 million pound signing of Portugal international midfielder from Sporting Lisbon",
            Summary = "Stamford Bridge club announced long-term contract for the central midfielder following successful medical examinations in London.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Super Eagles coach names 23-man squad for crucial 2026 FIFA World Cup qualifying fixtures",
            Summary = "National team manager included Osimhen, Boniface, Chukwueze, and Ndidi for back-to-back qualifiers against South Africa and Benin Republic.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Spanish Grand Slam: Carlos Alcaraz defeats Novak Djokovic in Wimbledon tennis final epic",
            Summary = "Young tennis prodigy captured his third major grand slam championship after a five-set masterclass on Centre Court.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Rivers United qualify for CAF Confederation Cup group stage after 3-0 aggregate victory",
            Summary = "Port Harcourt side sealed continental progression with disciplined away performance against Burkinabe opponents in Ouagadougou.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "Asisat Oshoala scores twice as Bay FC defeat Portland Thorns in NWSL women soccer clash",
            Summary = "Six-time African Women Footballer of the Year led her American club to crucial victory with clinical finishing inside the penalty box.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Sports",
            Title = "British boxer Raven Chapman recovering after emergency surgery following training bout injury",
            Summary = "The female featherweight fighter underwent emergency medical procedure in hospital following a head injury sustained during sparring camp in Denmark.",
            Source = "SeedData"
        },

        // ==========================================
        // 4. ENTERTAINMENT (30 samples)
        // ==========================================
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Burna Boy and Wizkid perform sold-out historic concert at London O2 Arena",
            Summary = "Afrobeats superstars captivated over twenty thousand cheering international fans during their joint world tour performance featuring live orchestral bands.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Funke Akindele blockbuster movie breaks Nigerian box office record with 1.5 billion naira gross",
            Summary = "Cinema blockbuster directed by the Nollywood icon achieved the highest-grossing film milestone in Nigerian cinematic history across nationwide cinemas.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Grammy Awards nominate Nigerian stars Davido, Asake, and Tems for Best African Music Performance",
            Summary = "The Recording Academy unveiled official nominations recognizing Afrobeats and Nigerian artists ahead of the annual Grammy Awards gala in Los Angeles.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Big Brother Naija BBNaija unveils new housemates and grand prize for season premiere",
            Summary = "Popular reality television show kicked off with celebrity hosts, brand new theme house, and one hundred million naira grand prize for the eventual winner.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Rema new single reaches number one on Billboard and global Spotify streaming charts",
            Summary = "Chart-topping single from the Mavin Records superstar recorded over 50 million digital streams within two weeks of official release worldwide.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "AMVCA 2026: Nollywood stars celebrate at Africa Magic Viewers Choice Awards gala night in Lagos",
            Summary = "Top cinematic talents, filmmakers, and costume designers were honored with prestigious trophies at the star-studded ceremony held at Eko Hotel.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Tiwa Savage releases debut feature film 'Water and Garri' on Prime Video streaming platform",
            Summary = "Afrobeats queen made her acting and executive producer debut in a critically acclaimed drama exploring romance, culture, and music in Cape Coast.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Olamide signs sensational young indigenous rapper to YBNL Nation record label",
            Summary = "Music executive and hip hop legend announced the latest addition to his influential imprint with an introductory EP and music video.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Kunle Afolayan premieres epic indigenous historical film 'Anikulapo: Rise of the Spectre'",
            Summary = "Celebrated Nigerian film director launched multi-part visual masterpiece on Netflix featuring veteran Yoruba actors and stunning cinematography.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Ayra Starr releases sophomore studio album 'The Year I Turned 21' to international acclaim",
            Summary = "Grammy-nominated songstress delivered sixteen-track project featuring collaborations with international pop icons and Nigerian hitmakers.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Headies Awards announce host city and special recognition award recipients for 17th edition",
            Summary = "Organizers of Nigeria prestigious music awards revealed plans for a vibrant ceremony celebrating achievements in Afrobeats, street-pop, and highlife.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Veteran Nollywood actor Richard Mofe-Damijo RMD celebrates 40 years in acting career",
            Summary = "Movie industry luminaries and cultural icons gathered in Lagos to celebrate the legendary actor contributions to theatre, television, and cinema.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Shallipopi and Odumodublvck announce joint North American concert tour across ten cities",
            Summary = "Street-hop and drill sensations will headline concerts in New York, Toronto, Atlanta, and Houston to perform their viral charting anthems.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Femi Adebayo epic movie 'Jagun Jagun' wins Best Indigenous Film at international festival",
            Summary = "Yoruba language cinematic spectacle received international honors for outstanding set design, stunt choreography, and cultural storytelling.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Kizz Daniel sells out 15,000-capacity arena in London during 'Vado at 30' tour",
            Summary = "Flyboy I.N.C frontliner serenaded enthusiastic crowd with non-stop catalogue of hits including 'Buga', 'Cough', and 'Twe Twe'.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Genevieve Nnaji returns to cinema with international co-production adaptation 'I Do Not Come To You By Chance'",
            Summary = "Iconic Nollywood director and actress unveiled book adaptation at Toronto International Film Festival TIFF to glowing reviews.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Afrobeats festival Afronation gathers fifty thousand fans on beach in Portugal",
            Summary = "The world largest Afrobeats celebration featured headline performances from Asake, Wizkid, Davido, and Diamond Platnumz across three days.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Mercy Chinwo and Nathaniel Bassey headline international gospel music concert in Lagos",
            Summary = "Over eighty thousand worshipers filled the national stadium for an all-night praise festival and live gospel album recording session.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "BBNaija housemates engage in emotional eviction night drama as double eviction rocks house",
            Summary = "Reality TV viewers voted to save their favorites while two contestants were sent packing from the Big Brother house during Sunday live show.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Lagos Fashion Week showcases vibrant African designs and sustainable textile innovations",
            Summary = "Leading fashion designers from Nigeria, Ghana, Kenya, and South Africa presented runway collections blending traditional adire with modern silhouettes.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Fireboy DML releases soulful new acoustic album exploring vulnerability and love",
            Summary = "YBNL singer-songwriter showcased vocal dexterity and introspective lyricism in a critically lauded thirteen-track body of work.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Toyin Abraham new comedy film 'Ijakumo 2' set for nationwide cinema release in December",
            Summary = "Popular Nollywood producer announced official trailer and cinema release dates for anticipated sequel across Nigerian film exhibitors.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Tems wins Best R&B Song at BET Awards for chart-topping single 'Love Me JeJe'",
            Summary = "Oscar-nominated and Grammy-winning Nigerian singer added another international trophy to her shelf during the live broadcast in Los Angeles.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Phyno and Flavour collaborate on cultural highlife single celebrating Eastern Nigerian heritage",
            Summary = "Indigenous music heavyweights delivered rhythmic masterpiece accompanied by visually rich music video shot in Enugu and Asaba.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Simi and Adekunle Gold release romantic duet album exploring marital journey and family",
            Summary = "Music power couple celebrated their wedding anniversary with an intimate collaborative EP that quickly climbed streaming charts.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Netflix Nigeria acquires exclusive worldwide streaming rights to new crime thriller series",
            Summary = "Streaming entertainment giant commissioned six-part Lagos underworld drama created by young Nigerian screenwriters and showrunners.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Sola Sobowale stars in political drama series 'King of Boys 3' set for production",
            Summary = "Kemi Adetiba confirmed script completion and cast auditions for the third installment of the groundbreaking crime and politics saga.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Wizkid album 'Morayo' breaks first-day streaming record on Apple Music Nigeria",
            Summary = "Starboy dedicated heartfelt album to his late mother featuring collaborations with international jazz musicians and French producers.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Chike releases heartfelt highlife ballad 'Egwu' featuring late Mohbad as tribute",
            Summary = "Emotional collaboration resonated deeply with millions of fans across TikTok and YouTube, becoming one of the most played songs of the year.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Entertainment",
            Title = "Davido announces 30 Billion World Tour with grand stadium concert at Madison Square Garden",
            Summary = "DMW boss confirmed dates for his arena world tour across North America, Europe, and Africa to celebrate decade of musical excellence.",
            Source = "SeedData"
        },

        // ==========================================
        // 5. TECHNOLOGY (30 samples)
        // ==========================================
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Nigerian fintech Moniepoint raises 110 million dollars Series C funding to expand digital banking",
            Summary = "The business payment platform achieved unicorn status following a financing round led by global venture capital investors to deepen retail banking tools.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "NITDA and Federal Ministry of Communications launch 3MTT three million tech talent training initiative",
            Summary = "Government tech agency began training youth nationwide in software engineering, artificial intelligence, cloud computing, and data analysis.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Starlink satellite internet expands high-speed broadband coverage to rural communities in Nigeria",
            Summary = "Low Earth orbit satellite provider announced affordable hardware kits to bridge digital divide across underserved northern and coastal zones.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Nigerian Communications Commission NCC orders telcos MTN and Airtel to block unlinked SIM lines",
            Summary = "Telecommunications regulatory authority enforced mandatory National Identity Number NIN linkage deadline to improve national identity database integrity.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Flutterwave receives payment service provider licenses in Uganda, Mozambique, and Ghana",
            Summary = "Fintech infrastructure company expanded its cross-border remittance gateway and enterprise payment processing footprint across sub-Saharan Africa.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Google launches AI developer hub and 350 million dollar startup accelerator in Yaba Lagos",
            Summary = "Global tech giant committed grants, cloud compute credits, and mentorship support to West African artificial intelligence founders.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Cybersecurity researchers warn against rising malware attacks targeting mobile banking apps",
            Summary = "Tech analysts discovered fraudulent Android APK clones mimicking Nigerian commercial bank applications to steal two-factor authentication OTP tokens.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "MainOne and Equinix unveil 300 million dollar data center facility in Lekki Lagos",
            Summary = "Hyperscale data center infrastructure launched to host cloud workloads, submarine cable connectivity, and local internet exchange peering points.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "OpenAI partners with African universities to train large language models in Yoruba, Hausa, and Igbo",
            Summary = "Artificial intelligence research lab launched multilingual benchmark datasets to improve generative AI accessibility for indigenous African languages.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Paystack introduces automated cross-currency virtual account API for West African merchants",
            Summary = "Payment gateway platform announced API integration allowing e-commerce businesses to accept payments in multiple fiat currencies and settle locally.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Nigeria 5G network subscriptions surpass 10 million milestone as telcos expand base stations",
            Summary = "Broadband rollout accelerated in Lagos, Abuja, Port Harcourt, and Kano as telecom operators deployed thousands of new fifth-generation radio towers.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Microsoft Azure and AWS expand availability zones to support Nigerian digital cloud transformation",
            Summary = "Cloud infrastructure providers partnered with local telecommunications carriers to provide low-latency cloud hosting for fintechs and enterprises.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Nigerian healthtech startup Reliance Health secures 40 million dollars in Series B funding",
            Summary = "Digital health insurance platform plans to scale telemedicine mobile app features and partner with diagnostic clinics across West Africa.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "NCS and CPN host annual cybersecurity conference on protecting national critical IT infrastructure",
            Summary = "Nigeria Computer Society brought together software engineers, ethical hackers, and IT directors to address ransomware defense and zero-trust architectures.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Central Bank of Nigeria issues regulatory sandbox guidelines for cryptocurrency and blockchain firms",
            Summary = "Banking regulator established structured compliance framework allowing licensed virtual asset service providers to test crypto remittance services.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Andela launches talent marketplace connecting African software engineers with Silicon Valley employers",
            Summary = "Remote tech workforce company expanded platform to place senior backend, full-stack, and DevOps engineers with global Fortune 500 enterprises.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Techstars Lagos accelerator cohort graduates 12 African startups after demo day pitch",
            Summary = "Early-stage tech founders presented innovative SaaS, logistics, and B2B marketplace solutions to international angel investors and VC partners.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Apple and Samsung launch official smartphone trade-in and certified refurbishment program in Nigeria",
            Summary = "Consumer electronics makers partnered with authorized local retail partners to offer structured device financing and battery replacement warranties.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Nigerian agritech startup ThriveAgric wins international prize for agricultural AI technology",
            Summary = "Digital agriculture company developed machine learning algorithms predicting crop yields and soil moisture for smallholder grain farmers.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Telecom operators demand tariff increase citing escalating fiber optic cable maintenance costs",
            Summary = "Association of Licensed Telecommunications Operators ALTON petitioned regulator to approve price adjustments due to diesel inflation and road construction fiber cuts.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Piggyvest reaches 5 million registered users on automated savings and investment mobile app",
            Summary = "Wealthtech platform disclosed that Nigerian retail savers processed over 1 trillion naira in micro-savings and locked treasury fund yields.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Ministry of Communications signs memorandum with Meta to train 50,000 youth in augmented reality AR",
            Summary = "Federal tech initiative targets equipping digital creators with immersive design skills for metaverse gaming and architectural simulation.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Lagos State launches unified digital land registry powered by blockchain technology",
            Summary = "State government unveiled automated GIS portal enabling property buyers to verify title deeds and electronic Certificates of Occupancy in minutes.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Chowdeck expands on-demand food delivery app service to Ibadan, Port Harcourt, and Abuja",
            Summary = "Logistics startup doubled its motorcycle fleet and restaurant integrations to deliver hot meals with average delivery time under thirty minutes.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Kuda Bank launches cross-border remittance corridor for Nigerians living in UK and Canada",
            Summary = "Digital neobank added multi-currency wallet functionality allowing diaspora users to send funds to Nigerian local bank accounts with zero transfer fees.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Google Play Store removes fraudulent instant lending apps violating user privacy guidelines",
            Summary = "Android app store operator purged rogue loan applications that harassed borrowers through illegal contact list scraping and predatory interest rates.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "NITDA issues AI code of ethics for software developers and data processing entities in Nigeria",
            Summary = "Technology standards bureau mandated transparency, algorithmic bias testing, and data protection compliance for automated decision systems.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Moove mobility fintech closes 100 million dollar debt financing to expand electric vehicle EV fleet",
            Summary = "Ride-hailing vehicle financing platform partnered with Uber and major lenders to deploy clean energy EV sedans across Lagos and Cairo.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "GitHub reports 40 percent surge in open-source contributions from Nigerian software developers",
            Summary = "Developer platform recognized Nigeria as one of the fastest-growing software engineering ecosystems globally for Python, JavaScript, and Rust projects.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Technology",
            Title = "Interswitch launches biometric payment POS terminals for contactless retail transactions",
            Summary = "Digital payments pioneer introduced palm-vein and facial recognition point-of-sale hardware to eliminate debit card cloning at supermarkets.",
            Source = "SeedData"
        },

        // ==========================================
        // 6. CRIME & SECURITY (30 samples)
        // ==========================================
        new NewsArticleRecord {
            Category = "Crime",
            Title = "EFCC arrests 45 suspects over alleged online financial fraud and cyber crime in Lagos",
            Summary = "Operatives of the Economic and Financial Crimes Commission recovered luxury vehicles, counterfeit documents, and electronic devices during raid on cybercrime syndicate.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "State Police Command intercepts arms smuggling syndicate along highway border checkpoint",
            Summary = "Commissioner of Police confirmed arrest of suspects conveying concealed AK-47 assault rifles and over one thousand rounds of live ammunition in commercial bus.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "High Court sentences convicted kidnapper to life imprisonment without option of fine",
            Summary = "Presiding judge delivered guilty verdict following overwhelming forensic ballistics and eyewitness testimony presented by state prosecution team.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "NDLEA intercepts multi-billion naira cocaine consignment at Murtala Muhammed International Airport Lagos",
            Summary = "Anti-narcotics operatives seized pure cocaine concealed inside cargo freight arriving from South America and arrested three clearing agents.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Military troops neutralize 20 terrorists and rescue abducted hostages in clearance operation",
            Summary = "Operation Hadin Kai troops supported by Nigerian Air Force air strikes dislodged ISWAP terrorist camp in Sambisa forest corridor.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Police arrest notorious armed robbery gang terrorizing commercial banks and jewelry stores",
            Summary = "Special Anti-Robbery squad apprehended four armed gang members who confessed to multiple violent robberies in Benin City and Warri.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "EFCC arraigns former government official over alleged 5 billion naira money laundering charges",
            Summary = "Anti-graft agency docked the defendant at Federal High Court on twelve counts of diverting public funds into private offshore corporate accounts.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Security operatives rescue 176 abducted women and children from bandit hideouts in Kwara forest",
            Summary = "Joint security forces successfully liberated captive villagers who had spent six months in forest encampments following intensive cordon-and-search mission.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Lagos State Police Command impounds 50 stolen vehicles and bursts car snatching ring",
            Summary = "Rapid Response Squad tracked stolen SUVs across state boundaries and arrested syndicate members specializing in forging chassis identification numbers.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "ICPC recovers 1.2 billion naira in cash and seized landed properties from corrupt contractor",
            Summary = "Independent Corrupt Practices Commission secured interim forfeiture order after investigations revealed fraudulent inflation of rural electrification contracts.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Cult clash leaves three dead in rivalry battle: Police arrest seven suspects with machetes and guns",
            Summary = "Divisional police officers restored calm after rival cult groups clashed over territory control in commercial motor park area.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Nigerian Navy foils crude oil bunkering operation and destroys five illegal refining sites",
            Summary = "Naval gunboats intercepted wooden barge laden with two hundred thousand liters of stolen crude oil in the creeks of Rivers State.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "NDLEA destroys 25 hectares of cannabis sativa plantation in Ondo forest reserve",
            Summary = "Drug law enforcement agency launched helicopter eradication assault destroying tons of harvested weed and arresting farm operators.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Police arrest two suspects for ritual killing and human body parts trafficking in Ogun State",
            Summary = "State police command detained herbalist and accomplice caught with fresh human skull during routine highway stop-and-search operation.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Federal High Court freezes 18 bank accounts linked to alleged billion-naira oil subsidy fraud",
            Summary = "Presiding judge granted ex-parte application submitted by federal prosecutors investigating forged bills of lading and non-existent fuel vessel discharges.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Gunmen attack rural community: Vigilante group and police repulse assailants, killing four",
            Summary = "Local hunters collaborated with armed police patrol teams to defend village from midnight assault, recovering automatic rifles and motorcycles.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "EFCC secures conviction of 35 internet fraudsters in Ibadan and Enugu courts",
            Summary = "Specialized cyber crime courts handed down prison sentences and ordered restitution of victims funds following guilty pleas by the convicted youth.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Customs intercepts illegal wildlife shipment containing elephant tusks and pangolin scales",
            Summary = "Federal operations unit intercepted shipping container declared as timber at export terminal, preventing illegal trafficking of endangered species parts.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Police bust fake currency printing syndicate and seize 100 million counterfeit naira notes",
            Summary = "Undercover detectives raided clandestine apartment equipped with industrial printing presses, plates, and special security paper in Kano.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Kidnappers release abducted university students after joint tactical police pressure",
            Summary = "State government confirmed students were freed unharmed without payment of ransom following intensive aerial drone surveillance over forest corridors.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Naval patrol arrests 10 foreign pirates attempting to hijack oil tanker off Gulf of Guinea",
            Summary = "Maritime special forces boarded hijacked vessel in daring night assault, rescuing international crew members and detaining sea pirates.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Police arrest fake medical doctor running illegal surgical clinic in residential estate",
            Summary = "State medical board and law enforcement sealed unregistered hospital after investigations revealed suspect possessed forged medical degree certificates.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "EFCC recovers 8 million dollars in cash hidden in safe house during intelligence-led raid",
            Summary = "Zonal director confirmed recovery of undeclared foreign currency stash following whistle-blower tip-off on politically exposed person.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Immigration service intercepts human trafficking syndicate transporting 15 teenage girls abroad",
            Summary = "Border patrol officers at international airport detected forged travel visas and arrested two traffickers attempting to fly victims to Middle East.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Magistrate court remands four suspects in correctional custody over fatal bullion van robbery",
            Summary = "Chief Magistrate ordered defendants detained at maximum security custodial centre pending legal advice from Director of Public Prosecutions DPP.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Bandits ambush passenger bus on Kaduna highway: Security forces mobilize to rescue victims",
            Summary = "Joint military and police rapid response units deployed tactical armored vehicles to pursue armed bandits into the Birnin Gwari forest belt.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "NDLEA arrests wanted drug baroness heading cross-border heroin distribution cartel",
            Summary = "Anti-narcotics agency apprehended prime suspect after months of surveillance on luxury mansions in Victoria Island and Abuja.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Police arraign civil servant for forging governor signature to sell government land parcels",
            Summary = "State ministry of justice filed ten charges of forgery, perjury, and official corruption against senior surveyor in state lands bureau.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Troops destroy 15 bomb-making factories and seize suicide vests in Borno clearance sweep",
            Summary = "Army counter-improvised explosive device team defused landmines and neutralized insurgent bomb technicians during Sambisa sweep.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "Crime",
            Title = "Police dismantle interstate child-stealing syndicate and rescue 8 abducted infants in Abia",
            Summary = "Detectives from state criminal investigation department arrested ringleader operating illegal maternity home and selling babies to buyers.",
            Source = "SeedData"
        },

        // ==========================================
        // 7. GENERAL (30 samples)
        // ==========================================
        new NewsArticleRecord {
            Category = "General",
            Title = "FRSC confirms 15 dead and 25 injured in tragic head-on highway collision in Niger State",
            Summary = "Federal Road Safety Corps confirmed multi-vehicle crash involving commercial passenger buses following tire burst and excessive speeding on rainy highway.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "NCDC issues public health advisory on preventive measures against cholera outbreak in 12 states",
            Summary = "Nigeria Centre for Disease Control urged citizens to practice clean water hygiene, boil drinking water, and report sudden dehydration symptoms to local health centres.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Federal Ministry of Education announces new nationwide curriculum for primary and secondary schools",
            Summary = "Education authorities unveiled modern curriculum incorporating vocational training, digital literacy, and civic education across public schools.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "NEMA coordinates relief material distribution to thousands of flood victims across riverine communities",
            Summary = "National Emergency Management Agency delivered food rations, blankets, temporary shelters, and medical kits to displaced families in Benue and Kogi.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Earth tremor in Abuja: Solid Minerals Minister Alake urges calm and orders continuous seismic monitoring",
            Summary = "Geological survey agency confirmed minor earth tremors in parts of the Federal Capital Territory, assuring residents that no structural faults were breached.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Five US work visa categories and application requirements for foreign skilled professionals",
            Summary = "Comprehensive immigration guide explains employment visa options EB-1 to EB-5, consular interview procedures, and green card pathways.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Nigeria Labour Congress NLC suspends nationwide warning strike following tripartite salary consensus",
            Summary = "Organized labour leadership agreed to suspend planned industrial action after federal government approved payment of wage award arrears.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "NiMet predicts three days of heavy rainfall and flash flooding in southern and north-central states",
            Summary = "Nigerian Meteorological Agency advised municipal authorities to clear blocked drainage canals and motorists to avoid driving through high flood waters.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Lagos State Government begins demolition of distressed buildings to prevent structural collapse",
            Summary = "Building Control Agency marked unsafe multi-story structures across Lagos Island following non-compliance with structural integrity tests.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "JAMB announces official cut-off marks and registration dates for 2026 UTME tertiary admissions",
            Summary = "Joint Admissions and Matriculation Board released policy guidelines for university, polytechnic, and college of education entrance examinations.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Federal Road Safety Corps launches nationwide ember months road safety awareness campaign",
            Summary = "Corps Marshal instructed sector commanders to deploy speed enforcement radar guns, breathalyzers, and emergency ambulances along major corridors.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Christian Association of Nigeria CAN and Islamic Council NSCIA hold interfaith peace dialogue",
            Summary = "Religious leaders from Christian and Muslim communities met to promote harmonious coexistence, mutual tolerance, and community peacebuilding.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Teaching hospital doctors perform first successful robotic heart surgery in West Africa",
            Summary = "Medical surgeons at University College Hospital achieved historic healthcare breakthrough with minimally invasive surgical robot equipment.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "National Youth Service Corps NYSC opens orientation camps for 2026 Batch B prospective corps members",
            Summary = "Director General of NYSC welcomed thousands of fresh university graduates to orientation grounds across all 36 states and the FCT.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Fire breaks out at commercial market in Ibadan: Firefighters battle inferno to save property",
            Summary = "State fire service deployed multiple water tankers to contain midnight blaze that engulfed textile shops at popular goods market.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Federal Ministry of Health flags off national immunization campaign against yellow fever and measles",
            Summary = "Health workers and primary care teams deployed to rural wards to administer vaccines to over five million infants and children.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "WAEC releases 2026 WASSCE secondary school exam results with 78 percent pass rate in English and Math",
            Summary = "West African Examinations Council announced release of certificate examination results with improved performance metrics across registered candidates.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Ooni of Ife and Sultan of Sokoto attend cultural carnival celebrating Nigerian historic heritage",
            Summary = "Royal monarchs and traditional rulers showcased rich Yoruba, Hausa, and Fulani royal arts, costumes, and music in vibrant cultural festival.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "National Identity Management Commission NIMC upgrades self-service mobile portal for citizen data modification",
            Summary = "Identity management agency launched web portal allowing citizens to update address, phone number, and correct misspelled names remotely.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Nigerian student loan scheme NELFUND disburses tuition fees to over 50,000 undergraduate students",
            Summary = "Fund administrator confirmed direct electronic transfers to accounts of federal universities to cover registered students academic fees.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Federal government declares public holiday for national independence day celebration",
            Summary = "Minister of Interior congratulated citizens on national anniversary and called for patriotism, unity, and dedication to nation-building.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "United Nations launches 20 million dollar humanitarian support fund for climate adaptation in Sahel",
            Summary = "UN agencies partnered with West African governments to support agro-forestry, solar borehole water access, and desertification containment.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Ailing veteran filmmaker Ola Balogun directs body donation for medical education and research in will",
            Summary = "Renowned Nigerian cinematic pioneer and cultural historian announced public declaration directing that his remains be used to train young medical doctors.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "UK Home Office announces revised global talent and skilled worker visa sponsorship guidelines",
            Summary = "British immigration department updated minimum salary thresholds and eligible occupation codes for overseas healthcare and research professionals.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Lagos Blue Line light rail train carries two million passengers in first six months of commercial operations",
            Summary = "Metropolitan transport authority reported growing commuter ridership on electric passenger train service operating along Marina-Mile 2 axis.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "WHO issues new guidelines on preventive nutrition and physical activity to reduce cardiovascular diseases",
            Summary = "World Health Organization report emphasized reducing dietary salt intake and engaging in regular exercise to combat hypertension.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "ASUU and Federal Government sign memorandum of action on university funding and revitalization",
            Summary = "Academic Staff Union of Universities reached agreement with education ministry officials regarding payment of earned academic allowances.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "National Orientation Agency NOA launches nationwide civic campaign on national anthem and symbols",
            Summary = "Government agency distributed educational handbooks to schools and community centers to promote civic values and respect for national flag.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Traffic gridlock on Lagos-Ibadan expressway eases following completion of bridge maintenance repairs",
            Summary = "Federal Ministry of Works reopened completed carriageway lanes, restoring free vehicular movement for inter-state travelers.",
            Source = "SeedData"
        },
        new NewsArticleRecord {
            Category = "General",
            Title = "Hundreds gather for burial ceremony of prominent community elder and philanthropist in Ibadan",
            Summary = "Dignitaries from academia, commerce, and civil service paid their final respects to beloved community leader known for building free rural schools.",
            Source = "SeedData"
        }
    };
}
