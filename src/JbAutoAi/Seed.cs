using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace JbAutoAi;

/// Deterministic demo data: twelve claims chosen to exercise every decision path
/// and every fraud signal. Documents are written straight to the DB with their
/// extraction payload already filled in, so the seed runs offline; the analysis
/// stage then runs for real, exactly as it would after a live upload.
public static class Seed
{
    record Scenario(
        string Name, string Policy, string Plate, string Vin, int LossDaysAgo, string Location,
        string Description, bool ThirdParty, bool Injuries, string? PoliceNumber,
        int PhotoSeed, byte[] Palette, string[] DamageAreas, string Severity,
        double Estimate, double Confidence, bool Police = false, bool NoDocuments = false,
        bool EmailOnly = false, int? SharePhotoFrom = null, int? ExifDaysAgo = null);

    static DateOnly Days(int ago) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-ago);

    static readonly Scenario[] Scenarios =
    [
        new("Jan de Vries", "NL-1001", "12-ABC-3", "WVWZZZ1KZAW123456", 2, "Amsterdam Zuidas",
            "Lichte kras op de achterbumper op de parkeerplaats.", false, false, null,
            1, [70, 90, 120], ["rear_bumper"], "minor", 850, 0.92),

        new("Anna Bakker", "NL-1002", "34-DEF-5", "", 5, "Utrecht Centrum",
            "Spatbord geschaafd langs een paaltje.", false, false, null,
            2, [100, 100, 130], ["front_fender"], "minor", 1200, 0.89),

        new("Ruben Peters", "NL-1003", "56-GHI-7", "", 3, "Rotterdam Kralingen",
            "Zijdelingse aanrijding door ander voertuig op kruising; tegenpartij kwam van rechts.",
            true, false, "PV-2026-88123",
            3, [90, 120, 100], ["driver_door", "front_fender"], "moderate", 1950, 0.88, Police: true),

        new("Sofie Jansen", "NL-1004", "78-JKL-9", "", 1, "Den Haag Bezuidenhout",
            "Flankaanrijding, passagier meldt nekklachten.", true, true, "PV-2026-88410",
            4, [140, 90, 90], ["passenger_door", "b_pillar", "rear_quarter"], "severe", 4400, 0.90,
            Police: true),

        new("Marc de Boer", "NL-1005", "90-MNO-1", "WBAAA1305C1234567", 7, "Eindhoven",
            "Frontale botsing tegen betonnen paal.", false, false, null,
            5, [60, 60, 90], ["hood", "front_bumper", "grille", "headlight_left"], "severe", 5800, 0.91),

        new("Piet van Dijk", "NL-1006", "11-PQR-2", "", 4, "Groningen",
            "Achterschade — zie bijgevoegde foto.", false, false, null,
            1, [70, 90, 120], ["rear_bumper"], "minor", 1100, 0.85, SharePhotoFrom: 0),

        new("Emma Visser", "NL-1007", "22-STU-3", "", 120, "Nijmegen",
            "Hagelschade pas opgemerkt na een lange rit.", false, false, null,
            7, [110, 110, 110], ["roof", "hood"], "moderate", 1600, 0.86),

        new("Lars Smit", "NL-1008", "44-VWX-5", "", 3, "Tilburg",
            "Voertuig total loss verklaard na aanrijding.", true, false, "PV-2026-90001",
            8, [50, 40, 40],
            ["hood", "front_bumper", "engine_bay", "windshield", "roof", "a_pillar"],
            "total_loss", 12500, 0.93, Police: true),

        new("Femke Aarts", "NL-1009", "55-YZA-6", "", 1, "Haarlem",
            "Kleine parkeerdeuk door winkelwagen.", false, false, null,
            9, [95, 105, 125], ["driver_door"], "minor", 620, 0.94),

        new("Kees Nijhof", "NL-1010", "66-BCD-7", "", 260, "Enschede",
            "Oude schade nu pas gemeld.", false, false, null,
            10, [130, 60, 60], [], "unknown", 900, 0.40, NoDocuments: true),

        new("Yasmin Ozturk", "NL-1011", "77-EFG-8", "", 4, "Almere",
            "Scheur in de voorbumper, offerte volgt nog.", false, false, null,
            11, [100, 130, 110], ["front_bumper"], "minor", 1050, 0.82, EmailOnly: true),

        // Fietser + EXIF-mismatch: art. 185 WVW hard gate and a forensics signal in one.
        new("Tom Aalders", "NL-1012", "88-HIJ-9", "", 6, "Leiden Breestraat",
            "Aanrijding met een fietser die van rechts kwam; fietser is 12 jaar oud.",
            true, true, "PV-2026-91188",
            12, [80, 100, 90], ["front_bumper", "headlight_right"], "moderate", 2100, 0.87,
            Police: true, ExifDaysAgo: 400),
    ];

    /// The demo policyholder account. Owns the first three seeded claims, so the
    /// portal has something to show on first login.
    public const string DemoPortalEmail = "jan.devries@example.nl";
    const int DemoPortalClaimCount = 3;

    static string EnsureDemoPortalUser()
    {
        var existing = Db.GetPortalUserByEmail(DemoPortalEmail);
        if (existing is not null) return existing.Id;
        var created = Db.CreatePortalUser(DemoPortalEmail, "Jan de Vries", "+31 6 1234 5678",
                                          Auth.HashPassword(Db.DemoPassword));
        return created?.Id ?? Db.GetPortalUserByEmail(DemoPortalEmail)!.Id;
    }

    /// (palette, seed) of every fixture photo, in scenario order. Smoke asserts
    /// these stay perceptually distinct — see Smoke.MediaChecks.
    internal static IEnumerable<(byte[] Palette, int Seed, int Index)> PhotoFixtures() =>
        Scenarios.Select((s, i) => (s.Palette, s.PhotoSeed, i))
                 .Where(t => !Scenarios[t.i].NoDocuments && !Scenarios[t.i].EmailOnly);

    public static async Task RunAsync()
    {
        Directory.CreateDirectory(Pipeline.UploadsRoot);
        var portalUserId = EnsureDemoPortalUser();
        var created = new List<string>();
        var photoBytesByIndex = new Dictionary<int, byte[]>();

        for (var i = 0; i < Scenarios.Length; i++)
        {
            var s = Scenarios[i];
            var claimId = Db.CreateClaim(new Claim
            {
                PolicyholderName = s.Name,
                PolicyNumber = s.Policy,
                LicensePlate = s.Plate,
                Vin = s.Vin,
                LossDate = Days(s.LossDaysAgo),
                LossLocation = s.Location,
                Description = s.Description,
                ThirdPartyInvolved = s.ThirdParty,
                Injuries = s.Injuries,
                PoliceReportNumber = s.PoliceNumber,
            });
            created.Add(claimId);

            // Give the demo portal account a few claims to look at.
            if (i < DemoPortalClaimCount)
            {
                Db.LinkClaimToPortalUser(claimId, portalUserId);
                Db.AddCustomerActivity(claimId, "created", portalUserId, null);
            }

            var dir = Path.Combine(Pipeline.UploadsRoot, claimId);
            Directory.CreateDirectory(dir);

            if (s.NoDocuments)
            {
                Console.WriteLine($"[{i + 1,2}] {claimId} — {s.Name} (no documents)");
                continue;
            }

            if (s.EmailOnly)
            {
                AddEmail(claimId, dir, s);
                Console.WriteLine($"[{i + 1,2}] {claimId} — {s.Name} (email only)");
                continue;
            }

            var shared = s.SharePhotoFrom is { } from ? photoBytesByIndex.GetValueOrDefault(from) : null;
            var raw = AddPhoto(claimId, dir, $"damage_{i + 1}.jpg", s, shared);
            photoBytesByIndex[i] = raw;

            AddEstimate(claimId, dir, s);
            if (s.Police) AddPoliceReport(claimId, dir, s);
            AddEmail(claimId, dir, s);

            Console.WriteLine($"[{i + 1,2}] {claimId} — {s.Name}");
        }

        Console.WriteLine("\nrunning analysis…\n");
        foreach (var id in created)
        {
            await Pipeline.AnalyseAsync(id);
            var c = Db.GetClaim(id)!;
            Console.WriteLine($"  {c.ClaimNumber}  {c.DecisionOutcome,-14} "
                            + $"€{c.EstimatedAmountEur,8:F0}  fraud {c.FraudScore:F2}  "
                            + $"citations {c.CitationIntegrity:P0}");
        }

        var health = Db.GetCitationHealth();
        Console.WriteLine($"\nseed done — {created.Count} claims, corpus {health.CorpusVersion}, "
                        + $"{health.CorpusEmbedded}/{health.CorpusChunks} chunks embedded, "
                        + $"citation integrity {health.Integrity:P0}.");
    }

    /// Deterministic synthetic damage photo — same seed produces byte-identical
    /// output, which is what makes the recycled-photo scenario reproducible.
    /// Deterministic synthetic damage photo.
    ///
    /// The variation has to live in LOW spatial frequencies. A DCT perceptual hash
    /// keeps only the top-left 8×8 coefficients, so per-pixel noise — which is where
    /// the obvious approach puts the entropy — is discarded wholesale and unrelated
    /// fixtures land a handful of bits apart, false-positiving the recycled-photo
    /// check. Real photos differ in their broad structure; so must these.
    internal static byte[] RenderPhoto(byte[] palette, int seed, DateOnly? exifDate = null)
    {
        const int w = 320, h = 240;
        using var img = new Image<Rgb24>(w, h);

        var rng = new Random(seed * 7919);
        // Per-seed low-frequency field: a tilted gradient plus two sinusoids whose
        // orientation and period come from the seed.
        double gx = (seed * 37 % 100) / 100.0 - 0.5, gy = (seed * 61 % 100) / 100.0 - 0.5;
        double f1 = 1 + seed % 4, f2 = 1 + (seed * 3) % 5, phase = seed * 0.7;

        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var u = (double)x / w;
                var v = (double)y / h;
                var field = 70 * (gx * u + gy * v)
                          + 34 * Math.Sin(Math.PI * f1 * u + phase)
                          + 28 * Math.Cos(Math.PI * f2 * v + phase)
                          + rng.Next(-10, 11);
                img[x, y] = new Rgb24(
                    (byte)Math.Clamp(palette[0] + field, 0, 255),
                    (byte)Math.Clamp(palette[1] + field, 0, 255),
                    (byte)Math.Clamp(palette[2] + field, 0, 255));
            }

        // Dark "damage" blocks — count, size and position all seed-dependent.
        var blocks = 3 + seed % 6;
        for (var i = 0; i < blocks; i++)
        {
            var bw = 26 + (seed * 11 + i * 17) % 60;
            var bh = 18 + (seed * 7 + i * 13) % 44;
            var x0 = (seed * 17 + i * 53) % (w - bw);
            var y0 = (seed * 13 + i * 41) % (h - bh);
            for (var y = y0; y < y0 + bh; y++)
                for (var x = x0; x < x0 + bw; x++)
                    img[x, y] = new Rgb24(18, 18, 20);
        }

        if (exifDate is { } taken)
        {
            var profile = new ExifProfile();
            profile.SetValue(ExifTag.DateTimeOriginal,
                taken.ToDateTime(TimeOnly.MinValue).ToString("yyyy:MM:dd HH:mm:ss"));
            img.Metadata.ExifProfile = profile;
        }

        using var ms = new MemoryStream();
        img.Save(ms, new JpegEncoder { Quality = 85 });
        return ms.ToArray();
    }

    static byte[] AddPhoto(string claimId, string dir, string name, Scenario s, byte[]? shared)
    {
        var raw = shared ?? RenderPhoto(s.Palette, s.PhotoSeed,
                                        s.ExifDaysAgo is { } d ? Days(d) : null);
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, raw);

        var extracted = Json.Str(new Dictionary<string, object?>
        {
            ["damage_areas"] = s.DamageAreas,
            ["severity"] = s.Severity,
            ["confidence"] = 0.9,
            ["photo_quality"] = "good",
            ["notes"] = "seeded synthetic damage photo",
            ["photo_signals"] = Media.PhotoExifSignals(raw, Days(s.LossDaysAgo)),
        });

        Db.AddDocument(claimId, name, $"{claimId}/{name}", "photo",
                       Media.Sha256Hex(raw), Media.PerceptualHash(raw), extracted);
        return raw;
    }

    static void AddEstimate(string claimId, string dir, Scenario s)
    {
        var body = $"Reparatiecalculatie — onderdelen + arbeidsloon: EUR {s.Estimate:F2}\n";
        var raw = WriteFile(dir, "repair_estimate.pdf", body);
        var extracted = Json.Str(new Dictionary<string, object?>
        {
            ["estimated_amount_eur"] = new { value = s.Estimate, confidence = s.Confidence },
            ["labour_hours"] = new { value = 4.0, confidence = s.Confidence },
            ["overall_confidence"] = s.Confidence,
        });
        Db.AddDocument(claimId, "repair_estimate.pdf", $"{claimId}/repair_estimate.pdf",
                       "repair_estimate", Media.Sha256Hex(raw), null, extracted);
    }

    static void AddPoliceReport(string claimId, string dir, Scenario s)
    {
        var number = s.PoliceNumber ?? "PV-ONBEKEND";
        var raw = WriteFile(dir, "police_report.pdf",
            $"Politie proces-verbaal {number}\nBetrokken voertuigen: 2\n{s.Description}\n");
        var extracted = Json.Str(new Dictionary<string, object?>
        {
            ["police_report_number"] = new { value = number, confidence = 0.95 },
            ["overall_confidence"] = 0.9,
        });
        Db.AddDocument(claimId, "police_report.pdf", $"{claimId}/police_report.pdf",
                       "police_report", Media.Sha256Hex(raw), null, extracted);
    }

    static void AddEmail(string claimId, string dir, Scenario s)
    {
        var raw = WriteFile(dir, "customer_email.txt",
            $"Onderwerp: Schademelding {s.Plate}\n\n{s.Description}\n");
        Db.AddDocument(claimId, "customer_email.txt", $"{claimId}/customer_email.txt",
                       "email", Media.Sha256Hex(raw), null,
                       Json.Str(new Dictionary<string, object?> { ["overall_confidence"] = 0.7 }));
    }

    static byte[] WriteFile(string dir, string name, string body)
    {
        var raw = System.Text.Encoding.UTF8.GetBytes(body);
        File.WriteAllBytes(Path.Combine(dir, name), raw);
        return raw;
    }
}
