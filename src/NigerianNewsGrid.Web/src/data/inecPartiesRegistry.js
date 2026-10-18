/**
 * Official Statutory Register of Recognized Political Parties & Candidates
 * Source: Independent National Electoral Commission (INEC Nigeria)
 * https://www.inecnigeria.org/parties
 *
 * Ephemeral Election Tracker Registry (2026/2027)
 */

export const INEC_PARTIES = [
  {
    id: 'apc',
    acronym: 'APC',
    name: 'All Progressives Congress',
    topicSlug: 'all-progressives-congress',
    chairman: 'Prof. Nentawe Yilwatda Goshwe',
    secretary: 'Sen. Surajudeen Ajibola Basiru PhD, BL',
    headquarters: 'No. 40 Blantyre Street, Wuse II, Abuja, Nigeria',
    color: '#008751',
    accentColor: '#0099FF',
    status: 'Ruling Party',
    candidates: [
      {
        name: 'Bola Ahmed Tinubu',
        topicSlug: 'bola-tinubu',
        office: 'Presidential',
        role: 'Incumbent President & Flagbearer',
        state: 'Lagos'
      },
      {
        name: 'Kashim Shettima',
        topicSlug: 'kashim-shettima',
        office: 'Vice-Presidential',
        role: 'Incumbent Vice President',
        state: 'Borno'
      }
    ]
  },
  {
    id: 'pdp',
    acronym: 'PDP',
    name: 'Peoples Democratic Party',
    topicSlug: 'peoples-democratic-party',
    chairman: 'Hon. Abdulrahman Mohammed',
    secretary: 'Senator Samuel Anyanwu',
    headquarters: 'Wadata Plaza, Plot 1970 Michael Okpara Street, Wuse Zone 5, Abuja',
    color: '#D32F2F',
    accentColor: '#388E3C',
    status: 'Main Opposition',
    candidates: [
      {
        name: 'Atiku Abubakar',
        topicSlug: 'atiku-abubakar',
        office: 'Presidential',
        role: 'Presidential Candidate / Flagbearer',
        state: 'Adamawa'
      }
    ]
  },
  {
    id: 'lp',
    acronym: 'LP',
    name: 'Labour Party',
    topicSlug: 'labour-party-nigeria',
    chairman: 'Sen. Nenadi E. Usman',
    secretary: 'Hon. Iheanacho Obioma',
    headquarters: 'Number 2, IBM Haruna Street, Utako, Abuja',
    color: '#E65100',
    accentColor: '#2E7D32',
    status: 'Recognized Statutory Party',
    candidates: [
      {
        name: 'Peter Obi',
        topicSlug: 'peter-obi',
        office: 'Presidential',
        role: 'Presidential Candidate / Flagbearer',
        state: 'Anambra'
      }
    ]
  },
  {
    id: 'nnpp',
    acronym: 'NNPP',
    name: 'New Nigeria Peoples Party',
    topicSlug: 'nnpp-nigeria',
    chairman: 'Dr. Agbo Gilbert Major',
    secretary: 'Com. Olaposi Sunday Oginni',
    headquarters: '11 Mahatma Gandhi Street, Area 11, Garki, Abuja, Nigeria',
    color: '#00796B',
    accentColor: '#D32F2F',
    status: 'Recognized Statutory Party',
    candidates: [
      {
        name: 'Rabiu Musa Kwankwaso',
        topicSlug: 'rabiu-kwankwaso',
        office: 'Presidential',
        role: 'Presidential Candidate / National Leader',
        state: 'Kano'
      }
    ]
  },
  {
    id: 'aac',
    acronym: 'AAC',
    name: 'African Action Congress',
    topicSlug: 'african-action-congress',
    chairman: 'Omoyele Sowore',
    secretary: 'Oshiokhue Philip Ikpeminoghena',
    headquarters: 'Office 011, Bolingo Hotel & Towers, Plot 777 Independent Avenue, CBD, Abuja',
    color: '#E64A19',
    accentColor: '#212121',
    status: 'Recognized Statutory Party',
    candidates: [
      {
        name: 'Omoyele Sowore',
        topicSlug: 'omoyele-sowore',
        office: 'Presidential',
        role: 'Presidential Candidate / Flagbearer',
        state: 'Ondo'
      }
    ]
  },
  {
    id: 'adc',
    acronym: 'ADC',
    name: 'African Democratic Congress',
    topicSlug: 'african-democratic-congress',
    chairman: 'Sen. David Mark',
    secretary: 'Ogbeni Rauf Aregbesola',
    headquarters: 'No. 2 Adetokunbo Ademola Crescent, Wuse II, Abuja',
    color: '#303F9F',
    accentColor: '#FFA000',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'apga',
    acronym: 'APGA',
    name: 'All Progressives Grand Alliance',
    topicSlug: 'all-progressives-grand-alliance',
    chairman: 'Barrister Sylvester Ezeokenwa',
    secretary: 'Ibrahim Mani',
    headquarters: 'Plot 1160 Cadastral Zone B07, Katampe District, Abuja',
    color: '#FBC02D',
    accentColor: '#388E3C',
    status: 'Recognized Statutory Party',
    candidates: [
      {
        name: 'Charles Chukwuma Soludo',
        topicSlug: 'charles-soludo',
        office: 'Gubernatorial',
        role: 'Executive Governor (Anambra State)',
        state: 'Anambra'
      }
    ]
  },
  {
    id: 'sdp',
    acronym: 'SDP',
    name: 'Social Democratic Party',
    topicSlug: 'social-democratic-party',
    chairman: 'Prof. Sadiq Umar Abubakar Gombe',
    secretary: 'Dr. Olu Agunloye',
    headquarters: 'Number 16, Nairobi Street, off Parakou Crescent, Wuse 2, Abuja',
    color: '#0288D1',
    accentColor: '#F57C00',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'ypp',
    acronym: 'YPP',
    name: 'Young Progressives Party',
    topicSlug: 'young-progressives-party',
    chairman: 'Comrade Bishop Amakiri',
    secretary: 'Barr. Vidiyeno Bamaiyi',
    headquarters: 'Block 10, Flat No. 1 Benue Crescent, Area 1, Garki, Abuja',
    color: '#7B1FA2',
    accentColor: '#388E3C',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'accord',
    acronym: 'A',
    name: 'Accord',
    topicSlug: 'accord-party',
    chairman: 'Bar. Maxwell Mgbudem',
    secretary: 'Hon. Adebukola Abiola Ajaja',
    headquarters: 'No. 15 Jos Street, Area 3 Garki, Abuja',
    color: '#5D4037',
    accentColor: '#10B981',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'aa',
    acronym: 'AA',
    name: 'Action Alliance',
    topicSlug: 'action-alliance',
    chairman: 'Hon. Adekunle Rufai Omoaje',
    secretary: 'Miller C. Orgwu',
    headquarters: 'Plot 955, No. 17 Femi Otedola Crescent, 7th Avenue, Gwarinpa Estate, FCT Abuja',
    color: '#0097A7',
    accentColor: '#E65100',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'adp',
    acronym: 'ADP',
    name: 'Action Democratic Party',
    topicSlug: 'action-democratic-party',
    chairman: 'Engr. Yabagi Yusuf Sani',
    secretary: 'Victor Fingesi',
    headquarters: 'Plot 3379A, Mungo Park Close, Off Jesse Jackson Asokoro New Extension, Abuja',
    color: '#512DA8',
    accentColor: '#FFB300',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'app',
    acronym: 'APP',
    name: 'Action Peoples Party',
    topicSlug: 'action-peoples-party',
    chairman: 'Uchenna Nnadi',
    secretary: 'Abu Ibrahim Sossan',
    headquarters: 'No. 6 Alexander Crescent, Behind Banex Plaza, Wuse II, Abuja',
    color: '#C2185B',
    accentColor: '#00897B',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'apm',
    acronym: 'APM',
    name: 'Allied Peoples Movement',
    topicSlug: 'allied-peoples-movement',
    chairman: 'Yusuf Mamman Dantalle',
    secretary: 'Oyadeyi Ayodele Adebayo',
    headquarters: 'Plot 232, No. 2 Leventis Building, Samuel Adesujo Ademulegun Street, CBD, Abuja',
    color: '#388E3C',
    accentColor: '#FBC02D',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'bp',
    acronym: 'BP',
    name: 'Boot Party',
    topicSlug: 'boot-party',
    chairman: 'Adenuga Sunday',
    secretary: 'Egwuatu Maryann .C',
    headquarters: 'House 11 Road C1, F.H.A Karu, Abuja',
    color: '#F57C00',
    accentColor: '#1976D2',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'dla',
    acronym: 'DLA',
    name: 'Democratic Leadership Alliance',
    topicSlug: 'democratic-leadership-alliance',
    chairman: 'Barr. Samuel M. Memeh',
    secretary: 'Grace E. Obekpa',
    headquarters: 'No. 25 Niger Street, Sun City Estate, Galadimawa, Abuja',
    color: '#455A64',
    accentColor: '#00ACC1',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'ndp',
    acronym: 'NDP',
    name: 'National Democratic Party',
    topicSlug: 'national-democratic-party',
    chairman: 'Hon. Ada Elizabeth Fredrick Okwori',
    secretary: 'Mr. Silva Opusunju',
    headquarters: 'No. 3 Ontario Crescent, Suncity, Galadimawa, Abuja',
    color: '#1976D2',
    accentColor: '#388E3C',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'nrm',
    acronym: 'NRM',
    name: 'National Rescue Movement',
    topicSlug: 'national-rescue-movement',
    chairman: 'Prince (Dr.) Chinedu Obi',
    secretary: 'Alh. Hassan Aminu Ibrahim',
    headquarters: 'Number 16, Benue Crescent, Area 1, Garki, Abuja',
    color: '#689F38',
    accentColor: '#D32F2F',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'ndc',
    acronym: 'NDC',
    name: 'Nigeria Democratic Congress',
    topicSlug: 'nigeria-democratic-congress',
    chairman: 'Sen. Cleopas Moses Zuwoghe',
    secretary: 'Barr. Ikenna Morgan Enekweizu',
    headquarters: 'No. 4 Odenna Close, Off Libreville Street, Wuse II, Abuja',
    color: '#AFB42B',
    accentColor: '#0288D1',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'prp',
    acronym: 'PRP',
    name: 'Peoples Redemption Party',
    topicSlug: 'peoples-redemption-party',
    chairman: 'Dr. Hakeem Baba-Ahmed',
    secretary: 'Mr. Kanu Sunday Uchenna',
    headquarters: 'No. 8, Ogbabi Street, Adjacent Military Police Headquarter, Garki 2, Abuja',
    color: '#D81B60',
    accentColor: '#FBC02D',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'yp',
    acronym: 'YP',
    name: 'Youth Party',
    topicSlug: 'youth-party',
    chairman: 'Mr. Abdulraham Abubakar',
    secretary: 'Mrs. Helen Adoh',
    headquarters: 'Suite 207, MKK Plaza, Gudu, Abuja',
    color: '#00ACC1',
    accentColor: '#303F9F',
    status: 'Recognized Statutory Party',
    candidates: []
  },
  {
    id: 'zlp',
    acronym: 'ZLP',
    name: 'Zenith Labour Party',
    topicSlug: 'zenith-labour-party',
    chairman: 'Chief Dan Nwanyanwu, OFR',
    secretary: 'Yahaya Makama',
    headquarters: 'Plot 73, Ladoke Akintola, Suite 206 Dabo Plaza, Garki 2, Abuja',
    color: '#8E24AA',
    accentColor: '#00897B',
    status: 'Recognized Statutory Party',
    candidates: []
  }
];

export function getAllCandidates() {
  const list = [];
  for (const party of INEC_PARTIES) {
    for (const c of party.candidates) {
      list.push({
        ...c,
        partyAcronym: party.acronym,
        partyName: party.name,
        partyColor: party.color,
        partySlug: party.topicSlug
      });
    }
  }
  return list;
}
