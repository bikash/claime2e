using Dapper;

namespace JbAutoAi;

/// Synthetic history so the dashboards have something to be a dashboard *of*.
///
/// The seeded fixtures are twelve claims reported on one day — enough to exercise
/// the pipeline, useless for a trend line, an aging distribution or a cost-by-month
/// chart. This back-fills a few months of plausible motor claims, including a
/// cross-border cohort (Green Card / WAM art. 2 cases with a foreign party).
///
/// ponytail: deterministic Random(42) and an id prefix, so the set is reproducible
/// and removable with one DELETE. No fixture files, no factory framework.
public static class DemoData
{
    public const string IdPrefix = "dm";

    // Dutch claimants and the vehicles they drive.
    static readonly string[] Names =
    [
        "J. de Vries", "H. Mulder", "R. Peeters", "T. Aydin", "K. Bos", "M. Willemsen",
        "S. El Amrani", "L. de Groot", "A. Jansen", "P. van Dijk", "N. Bakker", "E. Visser",
        "D. Kooij", "F. Smit", "I. Hendriks", "O. Dekker", "W. Vermeulen", "C. Brouwer",
    ];
    static readonly string[] Cities =
    [
        "Utrecht", "Amsterdam", "Rotterdam", "Den Haag", "Eindhoven", "Groningen",
        "Tilburg", "Almere", "Breda", "Nijmegen", "Arnhem", "Haarlem",
    ];
    static readonly (string Kind, string Text)[] Circumstances =
    [
        ("rear_end", "Kop-staartaanrijding voor een verkeerslicht. Tegenpartij reed van achteren aan."),
        ("priority", "Voorrangsfout op een kruising; beide voertuigen beschadigd aan de linkerzijde."),
        ("parking", "Parkeerschade op een bedrijventerrein. Tegenpartij onbekend, geen getuigen."),
        ("chain", "Kettingbotsing op de A2 bij filevorming, drie voertuigen betrokken."),
        ("reversing", "Achteruitrijdend een paaltje geraakt bij het uitparkeren."),
        ("cyclist", "Aanrijding met een fietser; letsel gemeld, art. 185 WVW van toepassing."),
        ("hail", "Hagelschade aan motorkap en dak tijdens onweer, geen derden betrokken."),
        ("theft", "Poging tot diefstal; stuurslot en portier geforceerd op de openbare weg."),
    ];

    /// Cross-border cases: a foreign party on one side of the collision. These are the
    /// ones that route through the Green Card system rather than straight to a Dutch
    /// counterparty insurer, and they are exactly what a motor book gets wrong.
    static readonly (string Country, string Plate, string Insurer, string Note)[] Foreign =
    [
        ("DE", "M-XY 4821",  "HUK-Coburg",        "Duitse tegenpartij, groene kaart overgelegd; afwikkeling via het Nederlands Bureau."),
        ("BE", "1-ABC-234",  "AG Insurance",      "Belgische tegenpartij op de A16; schaderegeling via de Belgische WAM-verzekeraar."),
        ("FR", "AB-123-CD",  "AXA France",        "Aanrijding tijdens vakantie in Frankrijk; verzekerde is benadeelde partij."),
        ("PL", "WA 12345",   "PZU",               "Poolse bestelbus, groene kaart geldig; regres via schaderegelaar in Nederland."),
        ("ES", "1234 KLM",   "Mapfre",            "Schade in Spanje aan geparkeerd voertuig; art. 4 WAM-richtlijn vertegenwoordiger."),
        ("IT", "AB 123 CD",  "Generali Italia",   "Italiaanse huurauto betrokken; groene kaart en huurcontract in dossier."),
    ];

    static readonly string[] OpsHandlers = ["h_alex", "h_sam", "h_lin", "h_mira", "h_dara", "h_bas"];

    public static int Clear()
    {
        using var c = Db.Open();
        c.Execute($"DELETE FROM activity WHERE claim_id LIKE '{IdPrefix}%'");
        return c.Execute($"DELETE FROM claims WHERE id LIKE '{IdPrefix}%'");
    }

    /// <param name="count">how many claims to fabricate</param>
    /// <param name="days">how far back to spread them</param>
    public static int Generate(int count = 140, int days = 120)
    {
        var rng = new Random(42);
        var today = DateTime.UtcNow.Date;
        using var c = Db.Open();
        var made = 0;

        for (var i = 0; i < count; i++)
        {
            // Weighted towards recent weeks, the way a real intake curve looks.
            var age = (int)(days * Math.Pow(rng.NextDouble(), 1.6));
            var created = today.AddDays(-age)
                               .AddHours(8 + rng.Next(0, 10))
                               .AddMinutes(rng.Next(0, 60));

            var crossBorder = rng.NextDouble() < 0.22;
            var (kind, text) = Circumstances[rng.Next(Circumstances.Length)];
            var name = Names[rng.Next(Names.Length)];
            var city = Cities[rng.Next(Cities.Length)];
            var id = IdPrefix + Db.NewId()[..10];

            string plate, description, policy;
            var thirdParty = crossBorder || kind is "rear_end" or "priority" or "chain" or "cyclist";
            var injuries = kind == "cyclist" || (thirdParty && rng.NextDouble() < 0.08);

            if (crossBorder)
            {
                var f = Foreign[rng.Next(Foreign.Length)];
                plate = DutchPlate(rng);
                city = rng.NextDouble() < 0.5 ? city : ForeignCity(f.Country, rng);
                description = $"{text} Tegenpartij: {f.Country}-kenteken {f.Plate}, verzekeraar {f.Insurer}. {f.Note}";
                policy = "POL-" + rng.Next(200000, 999999);
            }
            else
            {
                plate = DutchPlate(rng);
                description = text;
                policy = "POL-" + rng.Next(200000, 999999);
            }

            // Amounts: mostly small, a long tail of expensive ones.
            var amount = Math.Round(Math.Exp(6.4 + rng.NextDouble() * 1.9) * (injuries ? 3.2 : 1), 0);

            // Fraud: low for most, a tail above the referral threshold. Cross-border and
            // theft skew higher — that is where the real signal sits.
            var baseFraud = rng.NextDouble() * 0.35;
            if (kind == "theft") baseFraud += 0.25;
            if (kind == "parking") baseFraud += 0.12;
            if (crossBorder) baseFraud += 0.08;
            var fraud = Math.Round(Math.Min(0.97, baseFraud), 2);

            var fraudGateFailed = fraud >= 0.6;
            // Injury, a failed fraud gate or a cross-border liability question all stop STP.
            var outcome = fraudGateFailed || injuries ? "manual"
                        : crossBorder || amount > 12000 || rng.NextDouble() < 0.22 ? "assisted"
                        : "auto_approved";

            // Cycle time: minutes for STP, hours-to-days once a human is in the loop.
            var cycleHours = outcome switch
            {
                "auto_approved" => rng.NextDouble() * 0.6,
                "assisted" => 4 + rng.NextDouble() * 40,
                _ => 24 + rng.NextDouble() * 120,
            };
            var decidedAt = created.AddHours(cycleHours);
            var decided = decidedAt <= DateTime.UtcNow;

            // Older decided claims have been paid out; recent ones are still open.
            var settled = decided && age > 14 && outcome != "manual" && rng.NextDouble() < 0.75;
            var status = settled ? "settled" : decided ? "analyzed" : "submitted";

            var handler = rng.NextDouble() < 0.10
                ? null
                : OpsHandlers[WeightedHandler(rng)];

            var reasons = Json.Str(new object[]
            {
                new { code = "FNOL_COMPLETE", ok = true, message = "All required FNOL fields present." },
                new { code = "VEHICLE_ID_FORMAT", ok = true, message = "Plate/VIN format valid." },
                new { code = "NO_PERSONAL_INJURY", ok = !injuries,
                      message = injuries ? "Injury reported — manual handling required."
                                         : "No personal injury reported." },
                new { code = "FRAUD_SIGNALS_LOW", ok = !fraudGateFailed,
                      message = fraudGateFailed ? "Fraud indicators above referral threshold."
                                                : "Fraud indicators below threshold." },
                new { code = "AMOUNT_WITHIN_CAP", ok = amount <= 25000,
                      message = amount <= 25000 ? "Amount within automatic cap."
                                                : "Amount above the automatic cap." },
                new { code = "NO_THIRD_PARTY_DISPUTE", ok = !crossBorder,
                      message = crossBorder ? "Cross-border counterparty — liability confirmed with the foreign insurer."
                                            : "No third-party dispute on file." },
            });

            c.Execute("""
                INSERT INTO claims (id, created_at, status, policyholder_name, policy_number,
                                    license_plate, loss_date, loss_location, description,
                                    third_party_involved, injuries, estimated_amount_eur,
                                    extraction_confidence, fraud_score, summary,
                                    decision_outcome, decision_reasons, assigned_handler_id)
                VALUES (@id, @created, @status, @name, @policy, @plate, @lossDate, @city, @description,
                        @thirdParty, @injuries, @amount, @confidence, @fraud, @summary,
                        @outcome, @reasons::jsonb, @handler)
                """,
                new
                {
                    id, created, status, name, policy, plate,
                    lossDate = DateOnly.FromDateTime(created.AddDays(-rng.Next(0, 3))),
                    city, description,
                    thirdParty, injuries, amount,
                    confidence = Math.Round(0.72 + rng.NextDouble() * 0.27, 2),
                    fraud,
                    summary = description.Length > 160 ? description[..160] + "…" : description,
                    outcome = decided ? outcome : null,
                    reasons = decided ? reasons : "[]",
                    handler,
                });

            c.Execute("""
                INSERT INTO activity (claim_id, kind, actor_handler_id, body, created_at)
                VALUES (@id, 'created', NULL, @body, @created)
                """, new { id, body = $"FNOL received · {plate}", created });

            if (handler is not null)
                c.Execute("""
                    INSERT INTO activity (claim_id, kind, actor_handler_id, body, created_at)
                    VALUES (@id, 'assigned', @handler, @body, @at)
                    """, new { id, handler, body = "Assigned", at = created.AddMinutes(rng.Next(5, 240)) });

            if (decided)
                c.Execute("""
                    INSERT INTO activity (claim_id, kind, actor_handler_id, body, created_at)
                    VALUES (@id, 'decision', @handler, @body, @at)
                    """,
                    new { id, handler, body = $"Decision: {outcome}", at = decidedAt });

            if (settled)
                c.Execute("""
                    INSERT INTO activity (claim_id, kind, actor_handler_id, body, created_at)
                    VALUES (@id, 'decision', @handler, @body, @at)
                    """,
                    new
                    {
                        id, handler,
                        body = $"Settled · {I18n.Money(amount)}",
                        at = decidedAt.AddDays(rng.Next(1, 9)),
                    });

            made++;
        }
        return made;
    }

    /// Weighted so the workload chart has an overloaded handler and a quiet one —
    /// otherwise the rebalancing widget has nothing to say.
    static int WeightedHandler(Random rng)
    {
        var r = rng.NextDouble();
        return r < 0.30 ? 0 : r < 0.52 ? 1 : r < 0.70 ? 2 : r < 0.85 ? 3 : r < 0.95 ? 4 : 5;
    }

    static string DutchPlate(Random rng)
    {
        const string L = "BDFGHJKLNPRSTVXZ";
        return $"{L[rng.Next(L.Length)]}{L[rng.Next(L.Length)]}-{rng.Next(100, 999)}-{L[rng.Next(L.Length)]}";
    }

    static string ForeignCity(string country, Random rng) => country switch
    {
        "DE" => new[] { "Düsseldorf", "Köln", "Emmerich" }[rng.Next(3)],
        "BE" => new[] { "Antwerpen", "Gent", "Hasselt" }[rng.Next(3)],
        "FR" => new[] { "Lille", "Reims", "Lyon" }[rng.Next(3)],
        "PL" => new[] { "Wrocław", "Poznań" }[rng.Next(2)],
        "ES" => new[] { "Barcelona", "Valencia" }[rng.Next(2)],
        _ => new[] { "Milaan", "Bologna" }[rng.Next(2)],
    };
}
