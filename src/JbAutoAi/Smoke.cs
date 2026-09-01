using Npgsql;

namespace JbAutoAi;

/// One runnable check for the whole decision path: `./start.sh --smoke`.
///
/// Runs against a throwaway `smoke_test` schema in the same database, applies the
/// real Flyway migration files to it, and needs no Azure credentials — the LLM
/// layer stubs out and legal retrieval falls back to its lexical arm, which is
/// exactly the degraded mode worth having a test for.
public static class Smoke
{
    static int _checks;

    static void Check(bool condition, string what)
    {
        _checks++;
        if (!condition) throw new Exception($"FAILED: {what}");
        Console.WriteLine($"  ok  {what}");
    }

    public static async Task<int> RunAsync(string baseConnectionString, string repoRoot)
    {
        const string schema = "smoke_test";

        // Deterministic and offline by construction: even with real credentials in
        // .env, the suite runs the stubbed model path and the lexical-only
        // retrieval arm — the degraded mode most worth having covered.
        Environment.SetEnvironmentVariable("AZURE_OPENAI_KEY", "");

        await using (var c = new NpgsqlConnection(baseConnectionString))
        {
            await c.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS {schema} CASCADE; CREATE SCHEMA {schema};", c);
            await cmd.ExecuteNonQueryAsync();
        }

        // Appended rather than rebuilt through NpgsqlConnectionStringBuilder, whose
        // ConnectionString drops the password unless Persist Security Info is set.
        // public stays on the path so the `vector` type resolves.
        Db.Configure($"{baseConnectionString.TrimEnd(';')};Search Path={schema},public");

        var migrations = Directory.GetFiles(Path.Combine(repoRoot, "db", "migration"), "*.sql");
        foreach (var file in migrations.Where(f => Path.GetFileName(f).StartsWith('V')).Order()
                            .Concat(migrations.Where(f => Path.GetFileName(f).StartsWith('R')).Order()))
            Db.Exec(await File.ReadAllTextAsync(file));

        Db.SeedReferenceData();

        try
        {
            RulesChecks();
            FraudChecks();
            MediaChecks();
            await LegalChecks();
            MetricsChecks();
            await PipelineChecks();

            Console.WriteLine($"\nsmoke: {_checks} checks passed");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"\nsmoke: {e.Message}");
            return 1;
        }
        finally
        {
            Db.Configure(baseConnectionString);
            Db.Exec($"DROP SCHEMA IF EXISTS {schema} CASCADE");
        }
    }

    static Claim NewClaim(string name, string plate, bool thirdParty = false, bool injuries = false,
                          int lossDaysAgo = 3)
    {
        var id = Db.CreateClaim(new Claim
        {
            PolicyholderName = name,
            PolicyNumber = "P-" + plate,
            LicensePlate = plate,
            Vin = "",
            LossDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-lossDaysAgo),
            LossLocation = "Amsterdam A10",
            Description = "Aanrijding met schade aan de bumper.",
            ThirdPartyInvolved = thirdParty,
            Injuries = injuries,
        });
        return Db.GetClaim(id)!;
    }

    /// The copilot's drag-and-drop path: a dropped file is ingested, analysis runs,
    /// and a decision lands on the claim for the settle button to act on.
    static async Task PipelineChecks()
    {
        Console.WriteLine("\ncopilot end-to-end (ingest → analyse → decision)");
        Pipeline.UploadsRoot = Path.Combine(Path.GetTempPath(), "jb_smoke_uploads");

        var claim = NewClaim("Lotte Visser", "11-QRS-2");
        await Pipeline.IngestFileAsync(claim, "melding.eml", "Aanrijding, schade linksvoor."u8.ToArray());
        Check(Db.GetDocuments(claim.Id).Count == 1, "dropped file ingested as claim document");

        var outcome = await Pipeline.AnalyseAsync(claim.Id);
        var after = Db.GetClaim(claim.Id)!;
        Check(!string.IsNullOrEmpty(outcome) && after.DecisionOutcome == outcome,
            "analysis records the decision the copilot narrates");
        Check(after.Status == "decided", "claim status advanced to decided");
    }

    static void RulesChecks()
    {
        Console.WriteLine("\nFNOL + rules");

        var claim = NewClaim("Jan de Vries", "12-ABC-3");
        Check(claim.ClaimNumber.StartsWith("NL-"), "claim number allocated from sequence");
        Check(claim.LicensePlate == "12ABC3", "license plate normalised");

        claim.EstimatedAmountEur = 1200;
        claim.ExtractionConfidence = 0.9;
        var noEvidence = Rules.Evaluate(claim, [], 0.0);
        Check(noEvidence.Outcome == "assisted", "no evidence → assisted (soft gate only)");
        Check(noEvidence.Reasons.Single(r => r.Code == "EVIDENCE_MINIMUM").Ok == false,
            "EVIDENCE_MINIMUM fails without documents");
        Check(noEvidence.Reasons.Single(r => r.Code == "NO_PERSONAL_INJURY").Ok,
            "NO_PERSONAL_INJURY passes without injury");

        var happy = NewClaim("Anna Bakker", "34-DEF-5");
        happy.EstimatedAmountEur = 800;
        happy.ExtractionConfidence = 0.92;
        List<Document> docs =
        [
            new() { DocType = "photo" },
            new() { DocType = "repair_estimate" },
        ];
        Check(Rules.Evaluate(happy, docs, 0.1).Outcome == "auto_approved", "clean claim → auto_approved");

        var injured = NewClaim("Sofie Jansen", "99-XYZ-9", thirdParty: true, injuries: true);
        injured.EstimatedAmountEur = 500;
        injured.ExtractionConfidence = 0.95;
        Check(Rules.Evaluate(injured, docs, 0.0).Outcome == "manual", "personal injury → manual (hard gate)");

        var overCap = NewClaim("Marc de Boer", "90-MNO-1");
        overCap.EstimatedAmountEur = 99_000;
        overCap.ExtractionConfidence = 0.95;
        Check(Rules.Evaluate(overCap, docs, 0.0).Outcome == "assisted", "over cap → assisted, not denied");

        // FR-11: an unresolvable citation must block straight-through processing.
        var broken = Rules.Evaluate(happy, docs, 0.1, citationIntegrity: 0.5);
        Check(broken.Outcome == "manual", "unresolvable citation → manual (blocks STP)");
        Check(broken.Reasons.Single(r => r.Code == "CITATIONS_RESOLVABLE").Ok == false,
            "CITATIONS_RESOLVABLE gate fires on broken citations");

        // NFR-6: an AI outage degrades to assisted, it does not deny and does not throw.
        var down = Rules.Evaluate(happy, docs, 0.1, aiServicesHealthy: false);
        Check(down.Outcome == "assisted", "AI outage → assisted, never auto-approved, never denied");
        Check(down.Reasons.Single(r => r.Code == "AI_SERVICES_HEALTHY").Ok == false,
            "AI_SERVICES_HEALTHY gate fires when a stage fell back");
    }

    static void FraudChecks()
    {
        Console.WriteLine("\nfraud signals");

        var claim = new Claim
        {
            LossDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3),
            EstimatedAmountEur = 500,
            ExtractionConfidence = 0.9,
        };

        var recycled = Rules.ComputeFraud(claim, [new Document { DocType = "photo" }], 1);
        Check(recycled.Score >= 0.6, "recycled photo scores ≥ 0.60");
        Check(recycled.Signals.Any(s => s.Code == "PHOTO_RECYCLED"), "PHOTO_RECYCLED signal raised");

        var exif = Rules.ComputeFraud(claim, [new Document { DocType = "photo" }], 0,
            [new("PHOTO_EXIF_DATE_MISMATCH", "high", "Photo taken 2025-01-01 vs loss 2026-08-05.")]);
        Check(exif.Signals.Any(s => s.Code == "PHOTO_EXIF_DATE_MISMATCH"), "EXIF mismatch propagates");
        Check(exif.Score >= 0.4, "high-severity photo signal contributes 0.40");

        var late = new Claim { LossDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-260) };
        var lateResult = Rules.ComputeFraud(late, [], 0);
        Check(lateResult.Signals.Any(s => s.Code == "REPORT_VERY_LATE"), "very late report flagged");
        Check(lateResult.Signals.Any(s => s.Code == "NO_EVIDENCE"), "missing evidence flagged");
        Check(lateResult.Score >= 0.3, "late + no evidence crosses the 0.30 fraud gate");

        Check(Rules.ComputeFraud(claim, [], 5).Score <= 1.0, "fraud score is capped at 1.0");
    }

    static void MediaChecks()
    {
        Console.WriteLine("\nmedia forensics");

        var a = TinyJpeg(30);
        var b = TinyJpeg(30);
        var c = TinyJpeg(200);

        var ha = Media.PerceptualHash(a);
        Check(ha is { Length: 16 }, "perceptual hash is 64-bit hex");
        Check(ha == Media.PerceptualHash(b), "identical images hash identically");
        Check(ha != Media.PerceptualHash(c), "different images hash differently");
        Check(Media.PerceptualHash([1, 2, 3]) is null, "corrupt image degrades to null, does not throw");

        Check(Media.ClassifyByExtension("x.JPG") == "photo", "extension classifier is case-insensitive");
        Check(Media.Sha256Hex(a) == Media.Sha256Hex(b), "content hash is stable");
        Check(!Media.IsAcceptedUpload("payload.html"), "intake rejects non-artifact extensions");
        Check(Media.IsAcceptedUpload("estimate.PDF"), "intake accepts a claim artifact");

        // Recycled-photo detection is Hamming-based, so the fixtures have to be
        // genuinely distinct in the low frequencies a pHash keeps. Two fixtures
        // four bits apart would false-positive every demo run.
        var hashes = Seed.PhotoFixtures()
            .Select(f => (f.Index, Hash: Media.PerceptualHash(Seed.RenderPhoto(f.Palette, f.Seed))!))
            .ToList();
        var closest = (Distance: 64, Pair: "");
        foreach (var (i, x) in hashes.Select((h, i) => (i, h)))
            foreach (var y in hashes.Skip(i + 1))
            {
                if (x.Index == 0 && y.Index == 5) continue;   // the deliberate recycled pair
                var d = System.Numerics.BitOperations.PopCount(
                    Convert.ToUInt64(x.Hash, 16) ^ Convert.ToUInt64(y.Hash, 16));
                if (d < closest.Distance) closest = (d, $"#{x.Index + 1} vs #{y.Index + 1}");
            }
        Check(closest.Distance > Db.PhashHammingThreshold,
            $"distinct fixture photos stay above the {Db.PhashHammingThreshold}-bit "
          + $"near-duplicate threshold (closest {closest.Pair} at {closest.Distance})");

        var shared = Media.PerceptualHash(Seed.RenderPhoto([70, 90, 120], 1));
        Check(shared == Media.PerceptualHash(Seed.RenderPhoto([70, 90, 120], 1)),
            "the recycled-photo fixture reproduces byte-identically");
    }

    static byte[] TinyJpeg(byte shade)
    {
        using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(64, 64);
        for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
                img[x, y] = new SixLabors.ImageSharp.PixelFormats.Rgb24(
                    (byte)((x * shade) % 256), (byte)((y * shade) % 256), shade);
        using var ms = new MemoryStream();
        img.Save(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
        return ms.ToArray();
    }

    static async Task LegalChecks()
    {
        Console.WriteLine("\nlegal corpus + citations (FR-11)");

        var corpus = Db.ActiveCorpusVersion();
        Check(corpus == "v1.0.0", "active corpus version resolves");

        var (chunks, _) = Db.CorpusStats();
        Check(chunks >= 30, $"corpus loaded ({chunks} chunks)");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Lexical arm only — no embeddings in the smoke schema, which is the
        // graceful-degradation path we want covered.
        var rearEnd = await Legal.RetrieveAsync("kop-staart achteropaanrijding stopafstand volgafstand", today);
        Check(rearEnd.Count > 0, "hybrid retrieval returns hits with no embeddings present");
        Check(rearEnd.Any(h => h.Citation == "RVV 19"), "rear-end query retrieves RVV 19 (stopping distance)");

        var cyclist = await Legal.RetrieveAsync("fietser aangereden kwetsbare verkeersdeelnemer kind", today);
        Check(cyclist.Any(h => h.Citation == "WVW 185"), "cyclist query retrieves art. 185 WVW");

        // Temporal versioning: 2019 incident must not be grounded in 2024 law.
        var old = await Legal.RetrieveAsync("hoog risico AI risicobeoordeling verzekering",
                                            new DateOnly(2019, 1, 1));
        Check(old.All(h => h.Citation != "AI Act Annex III 5(c)"),
            "law that post-dates the incident is filtered out");
        var now = await Legal.RetrieveAsync("hoog risico AI risicobeoordeling verzekering", today);
        Check(now.Any(h => h.Citation == "AI Act Annex III 5(c)"),
            "the same query does retrieve it for a current incident");

        // Citation verification.
        var retrieved = rearEnd.Take(3).ToList();
        var good = Legal.VerifyCitations(retrieved, $"De achterste bestuurder [[cite:{retrieved[0].ChunkId}]].");
        Check(good.Integrity == 1.0, "cited passage from the prompt verifies clean");
        Check(good.Unresolved == 0, "no unresolved citations on a grounded answer");

        var cased = Legal.VerifyCitations(retrieved,
            $"Grond [[cite:{retrieved[0].ChunkId.ToUpperInvariant()}]].");
        Check(cased.Integrity == 1.0, "an id in the wrong case still resolves, not a false alarm");

        var byLabel = Legal.VerifyCitations(retrieved, $"Grond [[cite:{retrieved[0].Citation}]].");
        Check(byLabel.Integrity == 1.0, "citing a retrieved passage by its article label resolves");

        var noSource = Legal.VerifyCitations(retrieved, "Hiervoor is [[cite:geen bronpassage beschikbaar]].");
        Check(noSource.Emitted == 0 && noSource.Integrity == 1.0,
            "the no-source phrase is an absent source, not a fabricated one");

        var bad = Legal.VerifyCitations(retrieved,
            $"Grond [[cite:{retrieved[0].ChunkId}]] en [[cite:bw-9-999#1]].");
        Check(bad.Unresolved == 1, "invented citation id is detected");
        Check(bad.Integrity == 0.5, "citation integrity drops to 0.50");
        Check(bad.Citations.Any(c => !c.Verified && c.ChunkId == "bw-9-999#1"),
            "unresolved citation is surfaced, not silently dropped");

        // A real corpus id that was never retrieved is still a failure: it was not
        // in the prompt, so the model recalled it rather than read it.
        var freeRecall = Legal.VerifyCitations(retrieved, "[[cite:bw-6-162#1]]");
        Check(freeRecall.Unresolved == 1, "free-recalled corpus id counts as unresolved");

        Check(Db.GetChunk("wvw-185#2") is { } gate && gate.Passage.Contains("veertien"),
            "art. 185 case-law chunk is retrievable by id for the UI");
    }

    static void MetricsChecks()
    {
        Console.WriteLine("\nmetrics");

        var usage = Db.GetUsageTotals();
        Check(usage.TotalCalls == 0, "no LLM spend recorded in stub mode");

        Db.RecordUsage(null, "test", "gpt-4.1", 1_000_000, 0, Llm.CostUsd("gpt-4.1", 1_000_000, 0));
        Check(Math.Abs(Db.GetUsageTotals().TotalUsd - 2.00) < 0.0001, "1M input tokens on gpt-4.1 costs $2.00");
        Check(Math.Abs(Llm.CostUsd("my-gpt-4.1-mini-deploy", 1_000_000, 0) - 0.40) < 0.0001,
            "longest-match pricing: -mini is not priced as gpt-4.1");

        var stp = Db.GetStpSummary();
        Check(stp.Total >= 4, "claims counted");
        Check(stp.StpRate == 0.0, "STP rate is 0 before any decision is recorded");

        Check(Db.GetCitationHealth().Integrity == 1.0, "citation integrity starts clean");
    }
}
