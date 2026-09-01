using System.Text;
using System.Text.RegularExpressions;

namespace JbAutoAi;

/// FR-11 — the legal knowledge base in front of the model.
///
/// Retrieval is hybrid: a dense arm (pgvector cosine over 1024-d embeddings) and a
/// lexical arm (Postgres GIN + ts_rank_cd), fused with Reciprocal Rank Fusion. Both
/// arms are filtered by corpus version, document class, and — the part that matters
/// legally — by the law in force on the incident date.
///
/// Everything the model then says about law has to point back into that retrieved
/// set. VerifyCitations is the gate: a citation the model invented does not resolve,
/// gets flagged, and drags citation integrity below 1.0, which blocks STP.
public static partial class Legal
{
    // Permissive on purpose. A strict id shape would simply fail to match a
    // malformed marker, and an unmatched marker is worse than an unresolved one:
    // it is invisible, so Emitted stays 0 and integrity reads a clean 1.0 while the
    // handler sees a legal claim with no source. Match anything, validate after.
    [GeneratedRegex(@"\[\[cite:\s*([^\]\r\n]{1,200}?)\s*\]\]")]
    private static partial Regex CiteRe();

    const int RrfK = 60;          // standard RRF damping
    public const int DefaultTopK = 6;

    /// What a claim asks the corpus. Deliberately concatenative: the lexical arm
    /// wants the raw Dutch narrative, the dense arm wants the surrounding facts.
    public static string BuildQuery(Claim claim, IReadOnlyList<Document> documents)
    {
        var sb = new StringBuilder();
        sb.Append(claim.Description).Append(' ').Append(claim.LossLocation).Append(' ');
        if (claim.Injuries) sb.Append("persoonlijk letsel letselschade gewonde ");
        if (claim.ThirdPartyInvolved) sb.Append("tegenpartij derde aansprakelijkheid WA ");
        if (!string.IsNullOrWhiteSpace(claim.PoliceReportNumber)) sb.Append("proces-verbaal politie ");
        if ((claim.DamageCategories ?? "").Contains("total_loss")) sb.Append("total loss dagwaarde restwaarde ");
        if (claim.FraudScore >= 0.3) sb.Append("fraude onderzoek EVR incidentenregister ");
        if (claim.LossDate is { } d && DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - d.DayNumber > 60)
            sb.Append("late melding meldingsplicht verjaring ");
        foreach (var doc in documents.Select(x => x.DocType).Distinct())
            if (doc is not null) sb.Append(doc.Replace('_', ' ')).Append(' ');
        return sb.ToString().Trim();
    }

    /// Hybrid retrieval with RRF fusion. `asOf` is the incident date, so a 2019
    /// claim never gets grounded in a 2024 provision.
    public static async Task<List<LegalHit>> RetrieveAsync(string query, DateOnly asOf,
                                                           int k = DefaultTopK,
                                                           string[]? docClasses = null,
                                                           string? claimId = null)
    {
        var corpus = Db.ActiveCorpusVersion();
        if (corpus is null || string.IsNullOrWhiteSpace(query)) return [];

        var pool = k * 4;
        var lexical = Db.SearchLexical(query, asOf, pool, corpus, docClasses);

        List<LegalHit> dense = [];
        var vectors = await Llm.EmbedAsync([query], claimId);
        if (vectors is { Count: > 0 })
            dense = Db.SearchDense(Llm.ToVectorLiteral(vectors[0]), asOf, pool, corpus, docClasses);

        return Fuse(dense, lexical, k);
    }

    /// Reciprocal Rank Fusion — rank-based, so the dense arm's cosine scale and the
    /// lexical arm's ts_rank scale never have to be normalised against each other.
    static List<LegalHit> Fuse(List<LegalHit> dense, List<LegalHit> lexical, int k)
    {
        var scores = new Dictionary<string, double>();
        var byId = new Dictionary<string, LegalHit>();
        var modes = new Dictionary<string, HashSet<string>>();

        void Accumulate(List<LegalHit> arm)
        {
            for (var i = 0; i < arm.Count; i++)
            {
                var h = arm[i];
                scores[h.ChunkId] = scores.GetValueOrDefault(h.ChunkId) + 1.0 / (RrfK + i + 1);
                byId.TryAdd(h.ChunkId, h);
                (modes.TryGetValue(h.ChunkId, out var m) ? m : modes[h.ChunkId] = []).Add(h.RetrievalMode);
            }
        }

        Accumulate(dense);
        Accumulate(lexical);

        return scores.OrderByDescending(kv => kv.Value).Take(k).Select(kv =>
        {
            var hit = byId[kv.Key];
            hit.Score = Math.Round(kv.Value, 6);
            hit.RetrievalMode = modes[kv.Key].Count > 1 ? "hybrid" : modes[kv.Key].First();
            hit.UsedIn = "retrieved";
            return hit;
        }).ToList();
    }

    public static IReadOnlySet<string> ExtractCitationIds(params string?[] texts)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in texts)
        {
            if (string.IsNullOrEmpty(t)) continue;
            foreach (Match m in CiteRe().Matches(t)) ids.Add(m.Groups[1].Value);
        }
        return ids;
    }

    public record CitationAudit(
        List<LegalHit> Citations,   // retrieved set, plus any unresolved ids the model emitted
        double Integrity,           // resolved / emitted; 1.0 when the model cited nothing
        int Emitted,
        int Unresolved);

    /// The hallucination gate. Every id the model emitted must be in the set we
    /// actually put in its prompt — resolving against the wider corpus would let
    /// free-recall through, so we deliberately do not do that.
    /// The model is told to say this when the context does not cover a point. If it
    /// wraps the phrase in a marker anyway, that is an absence of a source, not a
    /// fabricated one, and must not be scored as a hallucination.
    static readonly string[] NoSourceMarkers =
        ["geen bronpassage beschikbaar", "no source passage available", "geen bron", "none"];

    public static CitationAudit VerifyCitations(IReadOnlyList<LegalHit> retrieved, params string?[] modelTexts)
    {
        var retrievedIds = retrieved.Select(h => h.ChunkId).ToHashSet(StringComparer.Ordinal);

        // A citation counts as grounded when it names a passage that was in the
        // prompt — by id (in any casing) or by that passage's article label. The
        // guarantee is "the model read this passage", not "the model reproduced our
        // id byte for byte", and a case mismatch scored as a hallucination is a
        // false alarm that blocks a perfectly grounded claim from STP.
        var canonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in retrieved)
        {
            canonical.TryAdd(h.ChunkId, h.ChunkId);
            canonical.TryAdd(h.Citation, h.ChunkId);
        }

        var emitted = ExtractCitationIds(modelTexts)
            .Select(id => id.Trim())
            .Where(id => id.Length > 0
                      && !NoSourceMarkers.Contains(id, StringComparer.OrdinalIgnoreCase))
            .Select(id => canonical.TryGetValue(id, out var mapped) ? mapped : id)
            .ToHashSet(StringComparer.Ordinal);

        var result = new List<LegalHit>();
        foreach (var h in retrieved)
        {
            h.UsedIn = emitted.Contains(h.ChunkId) ? "summary" : "retrieved";
            h.Verified = true;
            result.Add(h);
        }

        var unresolved = emitted.Where(id => !retrievedIds.Contains(id)).ToList();
        foreach (var id in unresolved)
        {
            // Was it at least a real corpus chunk? Either way it is a failure — it
            // was not in the prompt — but the distinction helps the corpus owner.
            var known = Db.GetChunk(id);
            result.Add(new LegalHit
            {
                ChunkId = id,
                Citation = known?.Citation ?? id,
                Title = known is null ? "Onbekende citatie — niet in corpus" : "Buiten de opgehaalde context",
                Passage = known?.Passage ?? "Het model noemde een bron-id dat niet in de prompt stond.",
                Url = known?.Url,
                Score = 0,
                RetrievalMode = "none",
                UsedIn = "summary",
                Verified = false,
            });
        }

        var integrity = emitted.Count == 0 ? 1.0
            : Math.Round((double)(emitted.Count - unresolved.Count) / emitted.Count, 4);

        return new CitationAudit(result, integrity, emitted.Count, unresolved.Count);
    }

    /// Renders [[cite:ID]] markers into readable inline references. Unresolved ids
    /// are marked, never silently dropped — a handler must see the gap.
    public static IEnumerable<(string Text, LegalHit? Cite, bool Broken)> Segments(
        string? text, IReadOnlyList<LegalHit> citations)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var byId = citations.GroupBy(c => c.ChunkId).ToDictionary(g => g.Key, g => g.First());

        var pos = 0;
        foreach (Match m in CiteRe().Matches(text))
        {
            if (m.Index > pos) yield return (text[pos..m.Index], null, false);
            var id = m.Groups[1].Value;
            byId.TryGetValue(id, out var hit);
            yield return ("", hit ?? new LegalHit { ChunkId = id, Citation = id, Verified = false }, hit is null || !hit.Verified);
            pos = m.Index + m.Length;
        }
        if (pos < text.Length) yield return (text[pos..], null, false);
    }

    /// Embedding pass. Only touches chunks whose text changed since last run —
    /// the R__ migration nulls the embedding when a passage is edited.
    public static async Task<int> EmbedCorpusAsync(int batchSize = 16)
    {
        var pending = Db.ChunksNeedingEmbedding();
        if (pending.Count == 0) return 0;
        if (Llm.Stubbed())
        {
            Console.WriteLine($"embed: {pending.Count} chunk(s) pending, but no AZURE_OPENAI_KEY — "
                            + "retrieval will run lexical-only.");
            return 0;
        }

        var done = 0;
        foreach (var batch in pending.Chunk(batchSize))
        {
            var vectors = await Llm.EmbedAsync(batch.Select(b => b.Text).ToList());
            if (vectors is null || vectors.Count != batch.Length)
            {
                Console.WriteLine($"embed: unexpected response for batch of {batch.Length}, stopping.");
                break;
            }
            for (var i = 0; i < batch.Length; i++)
            {
                Db.SetChunkEmbedding(batch[i].Id, Llm.ToVectorLiteral(vectors[i]));
                done++;
            }
            Console.WriteLine($"embed: {done}/{pending.Count}");
        }
        return done;
    }
}
