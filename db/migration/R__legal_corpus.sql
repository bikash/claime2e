-- R__ repeatable migration: the Dutch motor-law corpus (FR-11).
--
-- Flyway re-runs this whenever the checksum changes, so editing a passage below
-- is the whole "corpus update" workflow:
--     flyway migrate            -- upserts docs + chunks, clears stale embeddings
--     dotnet run -- --embed     -- re-embeds only the chunks whose text changed
--
-- REVIEW GATE. Everything here ships with review_status = 'draft': machine-
-- authored renderings of publicly available statute text, written to be faithful
-- but NOT authoritative. The corpus owner promotes a row to 'curated' after legal
-- sign-off. See docs/LEGAL-CORPUS.md for sourcing, licensing and cadence.
--
-- LICENSING. Statute text (BW, WAM, WVW, RVV) is public and reproduced here as
-- `summary`. OVS, the Verbond bedrijfsregelingen, PIFI and insurer polisvoorwaarden
-- are third-party licensed material: they appear as `licensed_summary` — scope and
-- effect only, no verbatim text — until an ingestion licence is in place.

INSERT INTO legal_corpus_version (id, label, is_active, notes)
VALUES ('v1.0.0', 'NL motor — baseline statutes, market agreements, doctrine', true,
        'Draft corpus. Statutes summarised from public sources; licensed material described only.')
ON CONFLICT (id) DO UPDATE SET label = excluded.label, notes = excluded.notes;

-- --- documents ----------------------------------------------------------------

INSERT INTO legal_doc (id, corpus_version, citation, source, doc_class, title,
                       insurer, valid_from, valid_to, passage_kind, review_status, url)
VALUES
-- Burgerlijk Wetboek
('bw-6-162','v1.0.0','BW 6:162','Burgerlijk Wetboek Boek 6','statute',
 'Onrechtmatige daad', NULL, '1992-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0005289/#Boek6_Titeldeel3_Afdeling1_Artikel162'),
('bw-6-98','v1.0.0','BW 6:98','Burgerlijk Wetboek Boek 6','statute',
 'Toerekening van schade — causaal verband', NULL, '1992-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0005289/#Boek6_Titeldeel1_Afdeling10_Artikel98'),
('bw-6-101','v1.0.0','BW 6:101','Burgerlijk Wetboek Boek 6','statute',
 'Eigen schuld en billijkheidscorrectie', NULL, '1992-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0005289/#Boek6_Titeldeel1_Afdeling10_Artikel101'),
('bw-6-170','v1.0.0','BW 6:170','Burgerlijk Wetboek Boek 6','statute',
 'Aansprakelijkheid voor ondergeschikten', NULL, '1992-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0005289/#Boek6_Titeldeel3_Afdeling2_Artikel170'),
('bw-7-942','v1.0.0','BW 7:942','Burgerlijk Wetboek Boek 7','statute',
 'Verjaring van de vordering op de verzekeraar', NULL, '2006-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0005290/#Boek7_Titeldeel17_Afdeling2_Artikel942'),
('wam-10','v1.0.0','WAM 10','Wet aansprakelijkheidsverzekering motorrijtuigen','statute',
 'Verjaring van de eigen rechtsvordering van de benadeelde', NULL, '1965-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0002415/#Artikel10'),
('bw-3-310','v1.0.0','BW 3:310','Burgerlijk Wetboek Boek 3','statute',
 'Verjaring van de vordering tot schadevergoeding', NULL, '1992-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0005291/#Boek3_Titeldeel11_Artikel310'),
('bw-7-928','v1.0.0','BW 7:928','Burgerlijk Wetboek Boek 7','statute',
 'Mededelingsplicht bij het aangaan van de verzekering', NULL, '2006-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0005290/#Boek7_Titeldeel17_Afdeling1_Artikel928'),
('bw-7-941','v1.0.0','BW 7:941','Burgerlijk Wetboek Boek 7','statute',
 'Meldingsplicht bij verwezenlijking van het risico', NULL, '2006-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0005290/#Boek7_Titeldeel17_Afdeling2_Artikel941'),
('bw-7-952','v1.0.0','BW 7:952','Burgerlijk Wetboek Boek 7','statute',
 'Geen dekking bij opzet of roekeloosheid', NULL, '2006-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0005290/#Boek7_Titeldeel17_Afdeling2_Artikel952'),

-- Wet aansprakelijkheidsverzekering motorrijtuigen
('wam-2','v1.0.0','WAM 2','Wet aansprakelijkheidsverzekering motorrijtuigen','statute',
 'Verzekeringsplicht voor motorrijtuigen', NULL, '1965-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0002415/#Artikel2'),
('wam-3','v1.0.0','WAM 3','Wet aansprakelijkheidsverzekering motorrijtuigen','statute',
 'Omvang van de verplichte dekking', NULL, '1965-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0002415/#Artikel3'),
('wam-6','v1.0.0','WAM 6','Wet aansprakelijkheidsverzekering motorrijtuigen','statute',
 'Eigen recht van de benadeelde jegens de verzekeraar', NULL, '1965-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0002415/#Artikel6'),
('wam-11','v1.0.0','WAM 11','Wet aansprakelijkheidsverzekering motorrijtuigen','statute',
 'Verweermiddelen niet tegenwerpbaar aan de benadeelde', NULL, '1965-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0002415/#Artikel11'),
('wam-22','v1.0.0','WAM 22','Wet aansprakelijkheidsverzekering motorrijtuigen','statute',
 'Minimum verzekerde bedragen', NULL, '2023-12-23', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0002415/#Artikel22'),

-- Wegenverkeerswet 1994 / RVV 1990
('wvw-185','v1.0.0','WVW 185','Wegenverkeerswet 1994','statute',
 'Aansprakelijkheid motorrijtuig jegens niet-vervoerde personen en zaken',
 NULL, '1995-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0006622/#Hoofdstuk12_Artikel185'),
('wvw-5','v1.0.0','WVW 5','Wegenverkeerswet 1994','statute',
 'Verbod op gevaarzetting en hinder', NULL, '1995-01-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0006622/#Hoofdstuk2_Paragraaf1_Artikel5'),
('rvv-15','v1.0.0','RVV 15','Reglement verkeersregels en verkeerstekens 1990','statute',
 'Voorrang op kruispunten', NULL, '1991-11-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0004825/#Hoofdstuk2_Paragraaf10_Artikel15'),
('rvv-18','v1.0.0','RVV 18','Reglement verkeersregels en verkeerstekens 1990','statute',
 'Afslaan — voorrang verlenen aan overige weggebruikers', NULL, '1991-11-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0004825/#Hoofdstuk2_Paragraaf11_Artikel18'),
('rvv-19','v1.0.0','RVV 19','Reglement verkeersregels en verkeerstekens 1990','statute',
 'Stopafstand — grondslag achteropaanrijding', NULL, '1991-11-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0004825/#Hoofdstuk2_Paragraaf12_Artikel19'),
('rvv-54','v1.0.0','RVV 54','Reglement verkeersregels en verkeerstekens 1990','statute',
 'Bijzondere manoeuvres', NULL, '1991-11-01', NULL, 'summary','draft',
 'https://wetten.overheid.nl/BWBR0004825/#Hoofdstuk2_Paragraaf25_Artikel54'),

-- Rechtspraak (doctrine — ECLI/NJ references to be completed by the corpus owner)
('hr-iza-vrerink','v1.0.0','HR 28-02-1992 (IZA/Vrerink)','Hoge Raad','case_law',
 '50%-regel: minimum aansprakelijkheid jegens volwassen niet-gemotoriseerden',
 NULL, '1992-02-28', NULL, 'summary','draft',
 'https://uitspraken.rechtspraak.nl/'),
('hr-kolkman','v1.0.0','HR 01-06-1990 (Ingrid Kolkman)','Hoge Raad','case_law',
 '100%-regel bij kinderen jonger dan 14 jaar', NULL, '1990-06-01', NULL, 'summary','draft',
 'https://uitspraken.rechtspraak.nl/'),
('hr-van-uitregt','v1.0.0','HR 31-05-1991 (Marbeth van Uitregt)','Hoge Raad','case_law',
 'Bevestiging kindregel onder art. 185 WVW', NULL, '1991-05-31', NULL, 'summary','draft',
 'https://uitspraken.rechtspraak.nl/'),
('reflexwerking','v1.0.0','Reflexwerking art. 185 WVW','Hoge Raad','case_law',
 'Reflexwerking: art. 185 WVW bij schade van de motorrijtuigbezitter zelf',
 NULL, '1992-02-28', NULL, 'summary','draft',
 'https://uitspraken.rechtspraak.nl/'),

-- Marktafspraken en protocollen (licensed — description only)
('ovs','v1.0.0','OVS','Verbond van Verzekeraars','market_agreement',
 'Overeenkomst Vereenvoudigde Schaderegeling — aanrijdingscategorieën',
 NULL, '2000-01-01', NULL, 'licensed_summary','draft',
 'https://www.verzekeraars.nl/'),
('br-7','v1.0.0','Bedrijfsregeling 7','Verbond van Verzekeraars','market_agreement',
 'Schaderegeling schuldloze derde', NULL, '2000-01-01', NULL, 'licensed_summary','draft',
 'https://www.verzekeraars.nl/'),
('pifi','v1.0.0','PIFI','Verbond van Verzekeraars','protocol',
 'Protocol Incidentenwaarschuwingssysteem Financiële Instellingen — EVR',
 NULL, '2021-01-01', NULL, 'licensed_summary','draft',
 'https://www.verzekeraars.nl/'),
('gbl','v1.0.0','GBL','De Letselschade Raad','protocol',
 'Gedragscode Behandeling Letselschade', NULL, '2012-01-01', NULL, 'licensed_summary','draft',
 'https://deletselschaderaad.nl/'),
('kifid','v1.0.0','Kifid','Kifid','kifid',
 'Klachteninstituut Financiële Dienstverlening — geschilbeslechting',
 NULL, '2007-04-01', NULL, 'licensed_summary','draft',
 'https://www.kifid.nl/'),

-- Toezicht en AI-governance
('avg-22','v1.0.0','AVG 22','Algemene verordening gegevensbescherming','statute',
 'Geautomatiseerde individuele besluitvorming', NULL, '2018-05-25', NULL, 'summary','draft',
 'https://eur-lex.europa.eu/eli/reg/2016/679/oj'),
('ai-act-annex-iii','v1.0.0','AI Act Annex III 5(c)','Verordening (EU) 2024/1689','statute',
 'Hoog-risico AI: risicobeoordeling en prijsstelling bij levens- en zorgverzekering',
 NULL, '2024-08-01', NULL, 'summary','draft',
 'https://eur-lex.europa.eu/eli/reg/2024/1689/oj'),

-- Dekkingsstructuur (marktconventie)
('dekking-tiers','v1.0.0','Dekkingsniveaus NL motor','Marktconventie','policy_wording',
 'WA, WA + beperkt casco, WA + volledig casco (allrisk)', NULL, '2000-01-01', NULL,
 'licensed_summary','draft', NULL),
('wok-total-loss','v1.0.0','WOK / total loss','RDW','protocol',
 'Total loss, WOK-status en registratie bij de RDW', NULL, '2000-01-01', NULL,
 'licensed_summary','draft', 'https://www.rdw.nl/')
ON CONFLICT (id) DO UPDATE SET
    citation = excluded.citation, source = excluded.source, doc_class = excluded.doc_class,
    title = excluded.title, insurer = excluded.insurer, valid_from = excluded.valid_from,
    valid_to = excluded.valid_to, passage_kind = excluded.passage_kind,
    review_status = excluded.review_status, url = excluded.url;

-- --- chunks -------------------------------------------------------------------
-- Editing a passage clears its embedding, so `--embed` re-embeds only what moved.

INSERT INTO legal_chunk (id, doc_id, ordinal, passage, tags) VALUES

('bw-6-162#1','bw-6-162',1, $t$Wie jegens een ander een onrechtmatige daad pleegt die hem kan worden toegerekend, is verplicht de schade die de ander daardoor lijdt te vergoeden. Als onrechtmatig geldt een inbreuk op een recht, een doen of nalaten in strijd met een wettelijke plicht, of in strijd met hetgeen volgens ongeschreven recht in het maatschappelijk verkeer betaamt. Toerekening volgt uit schuld of uit een oorzaak die krachtens wet of verkeersopvatting voor rekening van de dader komt. Dit is de algemene grondslag voor aansprakelijkheid bij een aanrijding tussen twee gemotoriseerde partijen.$t$,
 'onrechtmatige daad aansprakelijkheid schuld toerekening aanrijding liability tort'),

('bw-6-98#1','bw-6-98',1, $t$Voor vergoeding komt slechts de schade in aanmerking die in zodanig verband staat met de gebeurtenis waarop de aansprakelijkheid berust, dat zij de aansprakelijke, mede gezien de aard van de aansprakelijkheid en van de schade, als gevolg van die gebeurtenis kan worden toegerekend. Praktisch: schade aan een paneel dat niet in de botsrichting ligt vraagt om onderbouwing van het causaal verband voordat zij wordt vergoed.$t$,
 'causaal verband toerekening schade causation botsrichting impact direction'),

('bw-6-101#1','bw-6-101',1, $t$Wanneer de schade mede een gevolg is van een omstandigheid die aan de benadeelde kan worden toegerekend, wordt de vergoedingsplicht verminderd naar de mate waarin ieders omstandigheden aan de schade hebben bijgedragen. Een andere verdeling volgt wanneer de billijkheid dit wegens de uiteenlopende ernst van de gemaakte fouten of andere omstandigheden eist (billijkheidscorrectie). Dit is de wettelijke basis onder elke schuldverdeling van 0/50/100 procent.$t$,
 'eigen schuld schuldverdeling billijkheidscorrectie liability split 50 100 contributory'),

('bw-6-170#1','bw-6-170',1, $t$Voor schade veroorzaakt door een fout van een ondergeschikte is degene in wiens dienst de ondergeschikte zijn taak vervult aansprakelijk, indien de kans op de fout door de opdracht is vergroot en de werkgever zeggenschap had over de gedragingen. Relevant bij zakelijk gebruik van het voertuig en bij lease- en bedrijfswagenparken.$t$,
 'werkgever ondergeschikte zakelijk gebruik lease bedrijfswagen commercial use employer'),

('bw-3-310#1','bw-3-310',1, $t$Een rechtsvordering tot vergoeding van schade verjaart door verloop van vijf jaren na de aanvang van de dag volgende op die waarop de benadeelde zowel met de schade als met de daarvoor aansprakelijke persoon bekend is geworden, en in ieder geval door verloop van twintig jaren na de gebeurtenis waardoor de schade is veroorzaakt. Let op de reikwijdte: dit artikel betreft de vordering van de benadeelde op de aansprakelijke persoon uit onrechtmatige daad. Voor de vordering van de verzekerde op de eigen verzekeraar geldt de kortere termijn van BW 7:942, en voor de directe actie van de benadeelde tegen de WAM-verzekeraar die van WAM 10.$t$,
 'verjaring vijf jaar twintig jaar limitation prescription termijn onrechtmatige daad'),

('bw-7-942#1','bw-7-942',1, $t$De rechtsvordering van de verzekerde tegen de verzekeraar tot het doen van een uitkering verjaart door verloop van drie jaren na de aanvang van de dag volgende op die waarop de tot uitkering gerechtigde met de opeisbaarheid daarvan bekend is geworden. De verjaring wordt gestuit door een schriftelijke mededeling waarbij op uitkering aanspraak wordt gemaakt; na afwijzing gaat een nieuwe termijn lopen. Dit is de termijn die geldt tussen verzekerde en de eigen verzekeraar — niet de vijfjaarstermijn van BW 3:310.$t$,
 'verjaring drie jaar verzekeraar uitkering stuiting afwijzing limitation insurer'),

('wam-10#1','wam-10',1, $t$De eigen rechtsvordering van de benadeelde tegen de verzekeraar op grond van deze wet verjaart door verloop van drie jaren te rekenen vanaf het feit waaruit de schade is ontstaan. Deze termijn staat naast de verjaring van de vordering op de aansprakelijke persoon zelf; een dossier kan dus tegen de verzekeraar verjaard zijn terwijl de vordering op de veroorzaker nog loopt.$t$,
 'verjaring drie jaar WAM benadeelde directe actie verzekeraar limitation'),

('bw-7-928#1','bw-7-928',1, $t$De verzekeringnemer is verplicht voor het sluiten van de overeenkomst aan de verzekeraar alle feiten mede te delen die hij kent of behoort te kennen en waarvan hij weet of behoort te begrijpen dat de beslissing van de verzekeraar ervan afhangt of kan afhangen. Schending kan gevolgen hebben voor de dekking; bij een claim kort na het sluiten van de polis is dit een aandachtspunt.$t$,
 'mededelingsplicht verzwijging polis inception coverage upgrade non-disclosure'),

('bw-7-941#1','bw-7-941',1, $t$Zodra de verzekeringnemer of de tot uitkering gerechtigde van de verwezenlijking van het risico op de hoogte is, of behoort te zijn, is hij verplicht aan de verzekeraar de melding daarvan te doen zo spoedig als redelijkerwijs mogelijk is. De verzekeraar kan de uitkering verminderen met de schade die hij door de te late melding lijdt; verval van recht vereist een beroep op benadeling of op opzet tot misleiding.$t$,
 'meldingsplicht melding zo spoedig mogelijk late notification termijn 30 dagen'),

('bw-7-952#1','bw-7-952',1, $t$De verzekeraar vergoedt geen schade aan de verzekerde die de schade met opzet of door roekeloosheid heeft veroorzaakt. Dit is de wettelijke basis onder polisuitsluitingen zoals rijden onder invloed, rijden zonder geldig rijbewijs en opzettelijk toegebrachte schade.$t$,
 'opzet roekeloosheid uitsluiting alcohol rijbewijs exclusion intent recklessness'),

('wam-2#1','wam-2',1, $t$De bezitter van een motorrijtuig en degene op wiens naam het kenteken is gesteld, zijn verplicht voor het motorrijtuig een verzekering te sluiten en in stand te houden die aan de eisen van deze wet voldoet. Zonder geldige WAM-dekking op de verliesdatum is er geen aansprakelijkheidsdekking jegens derden.$t$,
 'verzekeringsplicht WAM kenteken dekking mandatory insurance'),

('wam-3#1','wam-3',1, $t$De verzekering moet de burgerrechtelijke aansprakelijkheid dekken waartoe het motorrijtuig in het verkeer aanleiding kan geven, van de bezitter, de houder en van iedere bestuurder en passagier. De dekking geldt in Nederland en in de overige aangewezen landen. Dit bepaalt of een derde-partijschade onder WA valt of onder cascodekking.$t$,
 'WA dekking derden bestuurder passagier aansprakelijkheid third party cover'),

('wam-6#1','wam-6',1, $t$De benadeelde heeft jegens de verzekeraar die de aansprakelijkheid dekt een eigen recht op schadevergoeding. Hij kan de verzekeraar rechtstreeks aanspreken; betaling aan een ander dan de benadeelde bevrijdt de verzekeraar niet. Grondslag voor directe schaderegeling met een tegenpartij zonder tussenkomst van de verzekerde.$t$,
 'eigen recht benadeelde directe actie tegenpartij direct action victim'),

('wam-11#1','wam-11',1, $t$Geen uit de wettelijke bepalingen omtrent de verzekeringsovereenkomst of uit die overeenkomst zelf voortvloeiende nietigheid, verweer of verval kan door de verzekeraar aan de benadeelde worden tegengeworpen. De verzekeraar die betaalt kan wel verhaal nemen op de verzekerde. Reden waarom een dekkingsuitsluiting de derde niet raakt, maar wel een regresvordering oplevert.$t$,
 'verweermiddelen nietigheid regres verhaal benadeelde recourse subrogation'),

('wam-22#1','wam-22',1, $t$De verzekering moet dekking bieden tot ten minste de bij of krachtens deze wet vastgestelde bedragen, die de minima van de Europese motorrijtuigenrichtlijn volgen. De structuur is één minimum per gebeurtenis voor personenschade, ongeacht het aantal benadeelden, en één minimum per gebeurtenis voor zaakschade. De richtlijn biedt daarnaast een alternatief per slachtoffer voor personenschade; Nederland heeft die variant niet gekozen. De bedragen worden periodiek geïndexeerd — controleer de actuele bedragen in het Staatsblad voordat een dekkingsplafond wordt toegepast.$t$,
 'minimum verzekerd bedrag dekkingsplafond personenschade zaakschade minimum sums'),

('wvw-185#1','wvw-185',1, $t$Indien een motorrijtuig waarmee op de weg wordt gereden betrokken is bij een verkeersongeval waardoor schade wordt toegebracht aan een niet door dat motorrijtuig vervoerde persoon of zaak, is de eigenaar of houder verplicht die schade te vergoeden, tenzij aannemelijk is dat het ongeval te wijten is aan overmacht. De reikwijdte is breder dan alleen voetgangers en fietsers: het artikel ziet op iedere persoon en zaak die niet door dat motorrijtuig wordt vervoerd. Lid 3 zondert daarvan uit de schade aan een ander motorrijtuig dat in beweging op de weg wordt gebruikt, en aan de daarmee vervoerde personen en zaken — botsingen tussen twee rijdende motorrijtuigen vallen dus buiten dit artikel en worden beoordeeld onder BW 6:162. Het beroep op overmacht slaagt vrijwel alleen bij een voor de bestuurder volstrekt onvoorzienbare fout van het slachtoffer.$t$,
 'artikel 185 risicoaansprakelijkheid overmacht fietser voetganger kwetsbare verkeersdeelnemer vulnerable road user'),

('wvw-185#2','wvw-185',2, $t$Uitwerking in de rechtspraak, en uitsluitend voor het geval dat het beroep op overmacht van artikel 185 lid 1 niet slaagt: slaagt dat beroep wél, dan is er in het geheel geen aansprakelijkheid op grond van dit artikel en komen de onderstaande minima niet aan de orde. Slaagt het niet, dan draagt de motorrijtuigzijde bij een volwassen niet-gemotoriseerd slachtoffer ten minste vijftig procent van de schade, ook wanneer het slachtoffer een verkeersfout maakte, behoudens opzet of aan opzet grenzende roekeloosheid. Bij kinderen jonger dan veertien jaar geldt volledige vergoeding, met dezelfde uitzondering. Deze categorie is nooit geschikt voor automatische afhandeling en gaat altijd naar een behandelaar.$t$,
 '50%-regel 100%-regel kind minderjarige fietser voetganger hard gate human review'),

('wvw-5#1','wvw-5',1, $t$Het is een ieder verboden zich zodanig te gedragen dat gevaar op de weg wordt veroorzaakt of kan worden veroorzaakt of dat het verkeer op de weg wordt gehinderd of kan worden gehinderd. Vaak ingeroepen als onderbouwing van onrechtmatig gedrag naast BW 6:162.$t$,
 'gevaarzetting hinder verkeersgedrag endangerment'),

('rvv-15#1','rvv-15',1, $t$Op kruispunten verlenen bestuurders voorrang aan voor hen van rechts komende bestuurders. Op deze hoofdregel kent artikel 15 twee uitzonderingen: bestuurders op een onverharde weg verlenen voorrang aan bestuurders op een verharde weg, en alle bestuurders verlenen voorrang aan een tram. De plicht van een bestuurder die van een uitrit de weg oprijdt volgt niet uit dit artikel maar uit de regeling van de bijzondere manoeuvres (RVV 54). Kernregel bij voorrangsscenario''s op de aanrijdingsformulier-vakjes over kruisend verkeer.$t$,
 'voorrang kruispunt van rechts right of way intersection kruisend verkeer'),

('rvv-18#1','rvv-18',1, $t$Bestuurders die afslaan moeten het verkeer dat hen op dezelfde weg tegemoetkomt of op dezelfde weg rechtdoor gaat of dat hen rechts inhaalt voor laten gaan. Grondslag bij afslaan-scenario''s en bij aanrijdingen met rechtdoorgaand of inhalend verkeer.$t$,
 'afslaan voorrang tegemoetkomend rechtdoor inhalen turning lane change'),

('rvv-19#1','rvv-19',1, $t$De bestuurder moet in staat zijn zijn voertuig tot stilstand te brengen binnen de afstand waarover hij de weg kan overzien en waarover deze vrij is. Dit is de standaardgrondslag voor aansprakelijkheid van de achterste bestuurder bij een kop-staartaanrijding; de bewijslast om daarvan af te wijken ligt bij de achterste partij.$t$,
 'stopafstand kop-staart achteropaanrijding rear-end volgafstand stopping distance'),

('rvv-54#1','rvv-54',1, $t$Bestuurders die een bijzondere manoeuvre uitvoeren — zoals wegrijden, achteruitrijden, keren, van een uitrit de weg oprijden, van rijstrook wisselen of invoegen — moeten het overige verkeer voor laten gaan. Dekt de aanrijdingsformulier-vakjes over wegrijden vanaf een parkeerplaats, achteruitrijden en van rijstrook wisselen.$t$,
 'bijzondere manoeuvre achteruitrijden wegrijden keren invoegen rijstrook parkeren special manoeuvre reversing'),

('hr-iza-vrerink#1','hr-iza-vrerink',1, $t$De Hoge Raad oordeelde op 28 februari 1992 dat bij een aanrijding tussen een motorrijtuig en een volwassen niet-gemotoriseerde verkeersdeelnemer, waarbij geen sprake is van overmacht aan de zijde van de bestuurder, ten minste vijftig procent van de schade voor rekening van de motorrijtuigzijde blijft, ongeacht de mate van eigen schuld — behoudens opzet of aan opzet grenzende roekeloosheid van het slachtoffer. NB: ECLI- en NJ-vindplaats moeten door de corpusbeheerder worden aangevuld voordat deze passage als curated wordt vrijgegeven.$t$,
 '50%-regel volwassene eigen schuld artikel 185 rechtspraak case law'),

('hr-kolkman#1','hr-kolkman',1, $t$In het arrest Ingrid Kolkman van 1 juni 1990 aanvaardde de Hoge Raad dat bij kinderen de billijkheidscorrectie van BW 6:101 ertoe leidt dat de schade in beginsel volledig door de motorrijtuigzijde wordt gedragen, behoudens opzet of aan opzet grenzende roekeloosheid. Deze lijn is later toegespitst op kinderen jonger dan veertien jaar. NB: ECLI- en NJ-vindplaats aanvullen vóór curatie.$t$,
 '100%-regel kind minderjarige veertien jaar artikel 185 rechtspraak child'),

('hr-van-uitregt#1','hr-van-uitregt',1, $t$Het arrest Marbeth van Uitregt van 31 mei 1991 bevestigde en verfijnde de kindregel onder artikel 185 WVW: bij jonge kinderen slaagt een beroep op eigen schuld vrijwel nooit, gelet op hun beperkte vermogen het verkeersrisico te overzien. NB: ECLI- en NJ-vindplaats aanvullen vóór curatie.$t$,
 'kindregel jong kind eigen schuld artikel 185 rechtspraak'),

('reflexwerking#1','reflexwerking',1, $t$Reflexwerking houdt in dat de beschermingsgedachte van artikel 185 WVW doorwerkt wanneer de bezitter van het motorrijtuig zélf schade lijdt door toedoen van een niet-gemotoriseerde: de eigen schuld van de motorrijtuigzijde wordt dan verhoudingsgewijs zwaarder gewogen, zodat de fietser of voetganger niet volledig aansprakelijk is voor de schade aan het motorrijtuig. NB: vindplaats aanvullen vóór curatie.$t$,
 'reflexwerking artikel 185 fietser voetganger schade motorrijtuig'),

('ovs#1','ovs',1, $t$De Overeenkomst Vereenvoudigde Schaderegeling is een afspraak tussen deelnemende Nederlandse verzekeraars die de schuldvraag bij veelvoorkomende aanrijdingen standaardiseert in vaste aanrijdingscategorieën met een vooraf afgesproken schuldverdeling, zodat de schade tussen verzekeraars onderling snel kan worden verrekend zonder individuele aansprakelijkheidsdiscussie. De OVS bindt alleen deelnemers en laat de wettelijke aansprakelijkheid jegens de benadeelde onverlet. De categorie-indeling en de bijbehorende verdeelsleutels zijn auteursrechtelijk beschermd materiaal van het Verbond van Verzekeraars en zijn hier niet opgenomen; integrale opname vereist een licentie.$t$,
 'OVS aanrijdingscategorie schuldverdeling verzekeraars onderling verrekening collision category'),

('br-7#1','br-7',1, $t$Bedrijfsregeling 7 regelt de behandeling van de schuldloze derde: wanneer een benadeelde zonder eigen schuld betrokken raakt bij een ongeval waarbij de aansprakelijkheid tussen meerdere betrokken verzekeraars niet direct kan worden vastgesteld, wordt zijn schade toch vlot geregeld door de betrokken verzekeraars, die de verdeling onderling uitvechten. Doel is dat het onschuldige slachtoffer niet de dupe wordt van een aansprakelijkheidsdiscussie. Volledige tekst is materiaal van het Verbond van Verzekeraars en vereist een licentie voor integrale opname.$t$,
 'bedrijfsregeling 7 schuldloze derde blameless third party regeling'),

('pifi#1','pifi',1, $t$Het Protocol Incidentenwaarschuwingssysteem Financiële Instellingen regelt onder welke voorwaarden een financiële instelling persoonsgegevens mag opnemen in het Incidentenregister en het Extern Verwijzingsregister. Opname vereist een concrete, onderbouwde verdenking, een proportionaliteitsafweging en een besluit door een daartoe aangewezen functionaris. Fraudesignalen zijn onderzoeksindicaties: zij mogen nooit leiden tot automatische afwijzing van een claim, en EVR-registratie blijft volledig een menselijk besluit. Volledige protocoltekst is licentiemateriaal.$t$,
 'PIFI EVR incidentenregister fraude verdenking proportionaliteit menselijk besluit fraud governance'),

('gbl#1','gbl',1, $t$De Gedragscode Behandeling Letselschade beschrijft hoe verzekeraars en belangenbehartigers letselschadedossiers behandelen: tijdige erkenning van aansprakelijkheid, bevoorschotting, medisch traject en doorlooptijden. Letselschade valt buiten de scope van automatische afhandeling: elk signaal van persoonlijk letsel routeert het dossier naar de letselschadeafdeling. Volledige tekst is materiaal van De Letselschade Raad.$t$,
 'GBL letselschade personal injury gedragscode routering injury department'),

('kifid#1','kifid',1, $t$Kifid behandelt klachten van consumenten over financiële dienstverleners, waaronder verzekeraars, nadat de interne klachtprocedure is doorlopen. Uitspraken zijn richtinggevend voor de uitleg van polisvoorwaarden en voor de zorgvuldigheid van het schadebehandelingsproces. Een klachtgevoelige beslissing — met name een afwijzing — vraagt om een schriftelijk gemotiveerde onderbouwing die een Kifid-toets doorstaat.$t$,
 'Kifid klacht consument polisvoorwaarden afwijzing motivering complaint'),

('avg-22#1','avg-22',1, $t$De betrokkene heeft het recht niet te worden onderworpen aan een uitsluitend op geautomatiseerde verwerking gebaseerd besluit waaraan voor hem rechtsgevolgen zijn verbonden of dat hem anderszins in aanmerkelijke mate treft, behoudens de in de verordening genoemde uitzonderingen. Waar een uitzondering geldt, moeten passende maatregelen worden getroffen, waaronder ten minste het recht op menselijke tussenkomst, het recht om het standpunt kenbaar te maken en het recht het besluit aan te vechten. Het verbod is dus niet absoluut: waar een uitzondering van lid 2 geldt — noodzakelijk voor het sluiten of uitvoeren van een overeenkomst, wettelijke grondslag, of uitdrukkelijke toestemming — is een uitsluitend geautomatiseerd besluit toegestaan, mits de waarborgen van lid 3 worden geboden. Dit platform hanteert daarbovenop een striktere interne regel: een afwijzing wordt nooit automatisch genomen, ongeacht of een uitzondering van toepassing zou zijn.$t$,
 'AVG GDPR artikel 22 geautomatiseerde besluitvorming menselijke tussenkomst automated decision'),

('ai-act-annex-iii#1','ai-act-annex-iii',1, $t$Bijlage III bij de AI-verordening merkt AI-systemen die bedoeld zijn voor risicobeoordeling en prijsstelling ten aanzien van natuurlijke personen bij levens- en zorgverzekeringen aan als hoog risico. Schadeafhandeling bij motorrijtuigverzekering is in die opsomming niet uitdrukkelijk genoemd. Omdat de kwalificatie afhangt van het concrete gebruik en van nadere richtsnoeren, hanteert dit platform hoog-risicogereedheid als ontwerpuitgangspunt: menselijk toezicht, logging, traceerbaarheid en technische documentatie zijn standaard aanwezig. Juridische toetsing is vereist vóór livegang van straight-through processing.$t$,
 'AI Act hoog risico annex III levensverzekering zorgverzekering menselijk toezicht logging high-risk'),

('dekking-tiers#1','dekking-tiers',1, $t$De Nederlandse motorrijtuigenmarkt kent drie dekkingsniveaus. WA dekt uitsluitend de wettelijke aansprakelijkheid jegens derden en is verplicht. WA + beperkt casco voegt een limitatieve lijst van eigen schadeoorzaken toe, zoals diefstal, brand, storm, ruitschade en aanrijding met loslopende dieren, maar dekt geen eigen schuld bij een aanrijding. WA + volledig casco (allrisk) dekt daarnaast eigen schade door aanrijding, ongeacht schuld. De dekkingsbepaling toetst de geëxtraheerde schadeoorzaak tegen het niveau op de polis en past het eigen risico toe. Polisvoorwaarden verschillen per verzekeraar en zijn auteursrechtelijk beschermd.$t$,
 'WA beperkt casco allrisk volledig casco eigen risico dekking coverage tier deductible'),

('wok-total-loss#1','wok-total-loss',1, $t$Bij total loss weegt de verzekeraar de reparatiekosten tegen de dagwaarde minus restwaarde. Technische total loss leidt tot uitboeking van het voertuig; de RDW kan een WOK-status (Wachten Op Keuren) registreren, waarna het voertuig pas na herkeuring weer de weg op mag. Een als total loss geclassificeerd dossier vraagt om taxatie en een restwaardetraject en is daarmee uitgesloten van automatische goedkeuring.$t$,
 'total loss WOK RDW dagwaarde restwaarde taxatie salvage write-off')

ON CONFLICT (id) DO UPDATE SET
    passage = excluded.passage,
    tags    = excluded.tags,
    -- A changed passage invalidates its vector: `--embed` picks it up next run.
    embedding = CASE WHEN legal_chunk.passage IS DISTINCT FROM excluded.passage
                     THEN NULL ELSE legal_chunk.embedding END;
