using Dapper;
using Npgsql;

namespace JbAutoAi;

// --- row models ---------------------------------------------------------------
// JSONB columns are carried as raw JSON strings and parsed at the edges. Keeps
// one shape in the DB, the API and the audit export.

public class Claim
{
    public string Id { get; set; } = "";
    public string ClaimNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "";
    public string? PolicyholderName { get; set; }
    public string? PolicyNumber { get; set; }
    public string? LicensePlate { get; set; }
    public string? Vin { get; set; }
    public DateOnly? LossDate { get; set; }
    public string? LossLocation { get; set; }
    public string? Description { get; set; }
    public bool ThirdPartyInvolved { get; set; }
    public bool Injuries { get; set; }
    public string? PoliceReportNumber { get; set; }
    public double? EstimatedAmountEur { get; set; }
    public double? ExtractionConfidence { get; set; }
    public double FraudScore { get; set; }
    public string? DamageCategories { get; set; }
    public string? Summary { get; set; }
    public string? DecisionOutcome { get; set; }
    public string? DecisionReasons { get; set; }
    public string? RulesTrace { get; set; }
    public string? FraudSignals { get; set; }
    public string? Assessment { get; set; }
    public string? LegalCorpusVersion { get; set; }
    public string? LegalCitations { get; set; }
    public double? CitationIntegrity { get; set; }
    public string? AssignedHandlerId { get; set; }
}

/// A dashboard population. Built only by Metrics.ScopeFilter, so a card and the list
/// behind it cannot drift apart (CR-4).
public class ClaimFilter
{
    public string? HandlerId { get; set; }
    public bool OpenOnly { get; set; }
    public bool ClosedOnly { get; set; }
    public bool DecidedOnly { get; set; }
    public bool StpOnly { get; set; }
    public bool StpReady { get; set; }
    public bool SlaBreached { get; set; }
    public bool DueToday { get; set; }
    public bool ToSiu { get; set; }
    public string? Stage { get; set; }
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// Point-in-time cards (backlog, queue, aging) are a snapshot of now, not of a window.
    public bool IgnoreRange { get; set; }
}

/// A claim plus the derived columns the dashboards work in.
public class ClaimRow : Claim
{
    public DateTime? DecidedAt { get; set; }
    public int AgeWd { get; set; }
    public int DocCount { get; set; }
}

public class Document
{
    public string Id { get; set; } = "";
    public string ClaimId { get; set; } = "";
    public string Filename { get; set; } = "";
    public string Filepath { get; set; } = "";
    public string? DocType { get; set; }
    public string? ContentHash { get; set; }
    public string? PerceptualHash { get; set; }
    public string? Extracted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Handler
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public bool Active { get; set; }
    public string RoleLabel => Role.Replace('_', ' ');
}

public class AgentDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Template { get; set; } = "";
    public string Lang { get; set; } = "both";
    public string Tone { get; set; } = "concise";
    public string Tools { get; set; } = "[]";
    public string Prompt { get; set; } = "";
    public bool Active { get; set; } = true;
    public string TriggerKind { get; set; } = "manual";
    public string Autonomy { get; set; } = "suggest";
    public string Flow { get; set; } = """{"nodes":[],"edges":[]}""";
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public IReadOnlyList<string> ToolList =>
        System.Text.Json.JsonSerializer.Deserialize<List<string>>(Tools) ?? [];
}

public class AgentGrant
{
    public long Id { get; set; }
    public string AgentId { get; set; } = "";
    public string? HandlerId { get; set; }           // null = entire company
    public string? GrantedBy { get; set; }
    public DateTime GrantedAt { get; set; }
    public string? HandlerName { get; set; }         // joined for display
    public string? GrantedByName { get; set; }
}

public class PortalUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class EmailTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
}

public class ActivityEntry
{
    public long Id { get; set; }
    public string? ClaimId { get; set; }             // null for platform events (agent studio)
    public string? ActorHandlerId { get; set; }
    public string Kind { get; set; } = "";
    public string? Body { get; set; }
    public string? Meta { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ActorName { get; set; }
    public string? ActorRole { get; set; }
    public bool VisibleToCustomer { get; set; }
    public string? PortalUserId { get; set; }
    public string? ClaimNumber { get; set; }         // populated by cross-claim queries only
    public string KindLabel => Kind.Replace('_', ' ');
}

/// One retrieved (or cited) passage from the legal corpus.
public class LegalHit
{
    public string ChunkId { get; set; } = "";
    public string Citation { get; set; } = "";
    public string Title { get; set; } = "";
    public string Source { get; set; } = "";
    public string DocClass { get; set; } = "";
    public string PassageKind { get; set; } = "";
    public string ReviewStatus { get; set; } = "";
    public string Passage { get; set; } = "";
    public string? Url { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public double Score { get; set; }
    public string RetrievalMode { get; set; } = "hybrid";
    public bool Verified { get; set; } = true;
    public string UsedIn { get; set; } = "retrieved";

    public string Snippet(int max = 240) =>
        Passage.Length <= max ? Passage : Passage[..max].TrimEnd() + "…";
}

public class Workflow
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TriggerKind { get; set; } = "manual";
    public bool Active { get; set; } = true;
    public string? Config { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class WorkflowStep
{
    public long Id { get; set; }
    public string WorkflowId { get; set; } = "";
    public int Ordinal { get; set; }
    public string Kind { get; set; } = "";
    public string? Config { get; set; }
}

public class WorkflowRun
{
    public long Id { get; set; }
    public string WorkflowId { get; set; } = "";
    public string? ClaimId { get; set; }
    public string? TriggerRef { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Error { get; set; }
    public string? Context { get; set; }
    public string? WorkflowName { get; set; }        // populated by joins
    public string? ClaimNumber { get; set; }
}

public class LegalDoc
{
    public string Id { get; set; } = "";
    public string CorpusVersion { get; set; } = "";
    public string Citation { get; set; } = "";
    public string Source { get; set; } = "";
    public string DocClass { get; set; } = "statute";
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public DateOnly ValidFrom { get; set; } = new(1970, 1, 1);
    public DateOnly? ValidTo { get; set; }
    public string PassageKind { get; set; } = "summary";
    public string ReviewStatus { get; set; } = "draft";
    public string Passage { get; set; } = "";
    public bool Embedded { get; set; }
}

public class DayStat
{
    public DateOnly Day { get; set; }
    public int Total { get; set; }
    public int Stp { get; set; }
    public int Assisted { get; set; }
    public int Manual { get; set; }
}

public class MonthStat
{
    public string Month { get; set; } = "";
    public int Total { get; set; }
    public int Stp { get; set; }
    public int Assisted { get; set; }
    public int Manual { get; set; }
}

public class CostDay
{
    public DateOnly Day { get; set; }
    public double CostUsd { get; set; }
    public long InTok { get; set; }
    public long OutTok { get; set; }
    public int Calls { get; set; }
}

public class CostMonth
{
    public string Month { get; set; } = "";
    public double CostUsd { get; set; }
    public int Calls { get; set; }
}

public class UsageTotals
{
    public double TotalUsd { get; set; }
    public long TotalTokens { get; set; }
    public int TotalCalls { get; set; }
    public double MonthUsd { get; set; }
    public int MonthCalls { get; set; }
    public double TodayUsd { get; set; }
    public int TodayCalls { get; set; }
}

public class StpSummary
{
    public int Total { get; set; }
    public int Stp { get; set; }
    public int Assisted { get; set; }
    public int Manual { get; set; }
    public int Pending { get; set; }
    public double StpRate { get; set; }
    public int Decided => Stp + Assisted + Manual;
}

/// NFR-2 / FR-11 metric: are the legal citations we ship actually resolvable?
public class CitationHealth
{
    public int ClaimsWithCitations { get; set; }
    public int TotalCitations { get; set; }
    public int Unresolved { get; set; }
    public double Integrity { get; set; }   // 1.0 = every emitted citation resolved
    public string? CorpusVersion { get; set; }
    public int CorpusChunks { get; set; }
    public int CorpusEmbedded { get; set; }
}

// --- data access ---------------------------------------------------------------

public static class Db
{
    static string _cs = "";

    public static void Configure(string connectionString)
    {
        _cs = connectionString;
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
    }

    /// Dapper has no built-in mapping for DateOnly; loss dates are DATE columns.
    sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(System.Data.IDbDataParameter parameter, DateOnly value)
        {
            if (parameter is NpgsqlParameter p) p.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Date;
            parameter.Value = value;
        }

        public override DateOnly Parse(object value) => value switch
        {
            DateOnly d => d,
            DateTime dt => DateOnly.FromDateTime(dt),
            string s => DateOnly.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidCastException($"cannot convert {value?.GetType()} to DateOnly"),
        };
    }

    internal static NpgsqlConnection Open()
    {
        var c = new NpgsqlConnection(_cs);
        c.Open();
        return c;
    }

    public static void Exec(string sql)
    {
        using var c = Open();
        c.Execute(sql);
    }

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    /// Schema is owned by Flyway (db/migration). This only tops up reference data
    /// the app needs to function, and is safe to run on every boot.
    public static void SeedReferenceData()
    {
        foreach (var h in DefaultHandlers) UpsertHandler(h.Id, h.Name, h.Email, h.Role);
        foreach (var t in DefaultTemplates) UpsertEmailTemplate(t.Id, t.Name, t.Audience, t.Subject, t.Body);

        // Demo credentials for the staff accounts. Set once — never overwrites a
        // password that has been changed. Obviously not a production practice; the
        // production path is SSO (NFR-3), which replaces this whole block.
        var demoHash = Auth.HashPassword(DemoPassword);
        foreach (var h in DefaultHandlers) SetHandlerPasswordIfMissing(h.Id, demoHash);
    }

    public const string DemoPassword = "demo1234";

    public static bool SchemaReady()
    {
        using var c = Open();
        return c.ExecuteScalar<bool>("SELECT to_regclass('public.claims') IS NOT NULL");
    }

    // --- claims ---------------------------------------------------------------

    public static string CreateClaim(Claim fnol, string? portalUserId = null)
    {
        var id = NewId();
        using var c = Open();
        c.Execute("""
            INSERT INTO claims (id, status, policyholder_name, policy_number, license_plate,
                                vin, loss_date, loss_location, description,
                                third_party_involved, injuries, police_report_number, portal_user_id)
            VALUES (@id, 'submitted', @name, @policy, @plate, @vin, @lossDate, @location,
                    @description, @thirdParty, @injuries, @policeNumber, @portalUserId)
            """,
            new
            {
                id,
                name = fnol.PolicyholderName,
                policy = fnol.PolicyNumber,
                plate = NormalisePlate(fnol.LicensePlate),
                vin = (fnol.Vin ?? "").ToUpperInvariant().Trim(),
                lossDate = fnol.LossDate,
                location = fnol.LossLocation,
                description = fnol.Description,
                thirdParty = fnol.ThirdPartyInvolved,
                injuries = fnol.Injuries,
                policeNumber = fnol.PoliceReportNumber,
                portalUserId,
            });
        return id;
    }

    public static string NormalisePlate(string? plate) =>
        (plate ?? "").ToUpperInvariant().Replace("-", "").Trim();

    public static Claim? GetClaim(string id)
    {
        using var c = Open();
        return c.QuerySingleOrDefault<Claim>("SELECT * FROM claims WHERE id = @id", new { id });
    }

    // --- dashboard queries (spec CR-1: the one place these populations are defined) ---

    /// Every claim, with the three derived columns the dashboards ask about:
    /// when it was decided, how many working days old it is, and how much evidence
    /// it carries. Kept as one CTE so a card and its drill-down read identical rows.
    const string ClaimBase = """
        WITH base AS (
          SELECT c.*,
                 (SELECT min(a.created_at) FROM activity a
                   WHERE a.claim_id = c.id AND a.kind = 'decision') AS decided_at,
                 (SELECT count(*) FROM generate_series(c.created_at::date, current_date, '1 day') g
                   WHERE extract(isodow FROM g) < 6)::int - 1              AS age_wd,
                 (SELECT count(*) FROM documents d WHERE d.claim_id = c.id)::int AS doc_count
          FROM claims c
        )
        """;

    static string Where(ClaimFilter f)
    {
        var w = new List<string>();
        if (f.HandlerId is { Length: > 0 }) w.Add("assigned_handler_id = @HandlerId");
        if (f.OpenOnly) w.Add("status <> 'settled'");
        if (f.ClosedOnly) w.Add("decided_at IS NOT NULL");
        if (f.DecidedOnly) w.Add("decision_outcome IS NOT NULL");
        if (f.StpOnly) w.Add("decision_outcome = 'auto_approved'");
        if (f.StpReady) w.Add("decision_outcome = 'auto_approved' AND status <> 'settled'");
        if (f.SlaBreached) w.Add($"age_wd > {Metrics.SlaWorkingDays}");
        if (f.DueToday) w.Add($"age_wd = {Metrics.SlaWorkingDays}");
        if (f.ToSiu) w.Add("""decision_reasons @> '[{"code":"FRAUD_SIGNALS_LOW","ok":false}]'::jsonb""");
        if (f.AgeMin is { } lo) w.Add($"age_wd >= {lo}");
        if (f.AgeMax is { } hi) w.Add($"age_wd < {hi}");
        w.Add(f.Stage switch
        {
            "intake"      => "decision_outcome IS NULL AND doc_count = 0",
            "extraction"  => "decision_outcome IS NULL AND doc_count > 0",
            "review"      => "decision_outcome IN ('assisted','manual') AND status <> 'settled'",
            "decision"    => "decision_outcome = 'auto_approved' AND status <> 'settled'",
            "settlement"  => "status = 'settled'",
            _             => "TRUE",
        });
        if (!f.IgnoreRange)
        {
            // A range on a decided population means "decided in the window"; on any other
            // population it means "reported in the window".
            var col = f.DecidedOnly || f.ClosedOnly ? "decided_at::date" : "created_at::date";
            w.Add($"{col} BETWEEN @From AND @To");
        }
        return string.Join(" AND ", w);
    }

    public static int CountClaims(ClaimFilter f)
    {
        using var c = Open();
        return c.ExecuteScalar<int>($"{ClaimBase} SELECT count(*)::int FROM base WHERE {Where(f)}", f);
    }

    public static List<ClaimRow> QueryClaims(ClaimFilter f, int limit = 200)
    {
        using var c = Open();
        return c.Query<ClaimRow>(
            $"{ClaimBase} SELECT * FROM base WHERE {Where(f)} ORDER BY age_wd DESC, created_at DESC LIMIT {limit}",
            f).AsList();
    }

    public static double SumAmount(ClaimFilter f)
    {
        using var c = Open();
        return c.ExecuteScalar<double?>(
            $"{ClaimBase} SELECT COALESCE(sum(estimated_amount_eur), 0) FROM base WHERE {Where(f)}", f) ?? 0;
    }

    /// Median and p95 of FNOL → decision, in hours. Appendix A: report both.
    public static (double? P50, double? P95, int N) CycleTime(Metrics.Range r)
    {
        using var c = Open();
        var row = c.QuerySingle($"""
            {ClaimBase}
            SELECT percentile_cont(0.5)  WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (decided_at - created_at)))/3600 AS p50,
                   percentile_cont(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (decided_at - created_at)))/3600 AS p95,
                   count(*)::int AS n
            FROM base
            WHERE decided_at IS NOT NULL AND decided_at::date BETWEEN @From AND @To
            """, new { r.From, r.To });
        return ((double?)row.p50, (double?)row.p95, (int)row.n);
    }

    public record WeekRate(DateTime WeekStart, int Decided, int Stp)
    {
        public double Rate => Decided == 0 ? 0 : (double)Stp / Decided;
    }

    /// MD-1: trailing weeks of STP rate. Same numerator and denominator as the KPI.
    public static List<WeekRate> StpByWeek(int weeks)
    {
        using var c = Open();
        return c.Query<WeekRate>($"""
            {ClaimBase}
            SELECT date_trunc('week', decided_at)::timestamp AS week_start,
                   count(*)::int AS decided,
                   count(*) FILTER (WHERE decision_outcome = 'auto_approved')::int AS stp
            FROM base
            WHERE decided_at IS NOT NULL
              AND decided_at >= date_trunc('week', current_date) - (@weeks || ' weeks')::interval
            GROUP BY 1 ORDER BY 1
            """, new { weeks }).AsList();
    }

    public record DayCount(DateTime Day, int N);

    /// HD-3: claims the handler brought to a decision, per day.
    public static List<DayCount> ClosedPerDay(Metrics.Range r, string? handlerId)
    {
        using var c = Open();
        return c.Query<DayCount>($"""
            {ClaimBase}
            SELECT decided_at::date::timestamp AS day, count(*)::int AS n
            FROM base
            WHERE decided_at IS NOT NULL
              AND decided_at::date BETWEEN @From AND @To
              AND (@handlerId IS NULL OR assigned_handler_id = @handlerId)
            GROUP BY 1 ORDER BY 1
            """, new { r.From, r.To, handlerId }).AsList();
    }

    public record Workload(string HandlerId, string Name, string Role, int Open);

    /// MD-2: open cases per handler — capacity for rebalancing, deliberately not a
    /// throughput ranking (spec §4.4). Distinct from the older WorkloadByHandler that
    /// feeds the legacy Analytics page: that one counts "open" as undecided, this one
    /// as not-settled. Analytics should move onto Metrics before both are trusted.
    public static List<Workload> WorkloadForRebalance()
    {
        using var c = Open();
        return c.Query<Workload>("""
            SELECT h.id AS handler_id, h.name, h.role,
                   count(c.id) FILTER (WHERE c.status <> 'settled')::int AS open
            FROM handlers h
            LEFT JOIN claims c ON c.assigned_handler_id = h.id
            WHERE h.role NOT IN ('super_admin', 'cfo')
            GROUP BY h.id, h.name, h.role
            ORDER BY open DESC, h.name
            """).AsList();
    }

    public record MonthCost(DateTime Month, double Settled, double Open);

    /// CD-1: claim cost by month. Settled vs still-reserved, from the claim store —
    /// the finance ledger is not connected, so this is claim-store derived.
    public static List<MonthCost> CostByMonth(Metrics.Range r)
    {
        using var c = Open();
        return c.Query<MonthCost>("""
            SELECT date_trunc('month', created_at)::timestamp AS month,
                   COALESCE(sum(estimated_amount_eur) FILTER (WHERE status = 'settled'), 0) AS settled,
                   COALESCE(sum(estimated_amount_eur) FILTER (WHERE status <> 'settled'), 0) AS open
            FROM claims
            WHERE created_at::date BETWEEN @From AND @To
            GROUP BY 1 ORDER BY 1
            """, new { r.From, r.To }).AsList();
    }

    public static List<Claim> ListClaims(int limit = 500)
    {
        using var c = Open();
        return c.Query<Claim>(
            "SELECT * FROM claims ORDER BY created_at DESC LIMIT @limit", new { limit }).AsList();
    }

    public static (List<Claim> Items, int Total) ListClaimsPaged(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        using var c = Open();
        var total = c.ExecuteScalar<int>("SELECT count(*)::int FROM claims");
        var items = c.Query<Claim>(
            "SELECT * FROM claims ORDER BY created_at DESC OFFSET @off LIMIT @lim",
            new { off = (page - 1) * pageSize, lim = pageSize }).AsList();
        return (items, total);
    }

    // ponytail: single ILIKE across the columns a handler actually types into a
    // search box. Full-text index / trigram upgrade path if the table outgrows this.
    /// The dashboard's work list, shaped by what a role is actually responsible for.
    /// ponytail: one query with a role-picked predicate, not seven dashboard pages.
    /// The predicate comes from this switch only — never from request input.
    public static List<Claim> ListClaimsForRole(string role, string? handlerId, int limit = 25)
    {
        var where = role switch
        {
            "fraud_specialist"     => "fraud_score >= 0.5",
            "injury_department"    => "injuries",
            "liability_department" => "third_party_involved",
            "senior_adjuster"      => "(assigned_handler_id = @handlerId OR assigned_handler_id IS NULL)",
            Auth.SuperAdminRole    => "TRUE",
            _                      => "assigned_handler_id = @handlerId",
        };
        using var c = Open();
        return c.Query<Claim>(
            $"SELECT * FROM claims WHERE {where} ORDER BY created_at DESC LIMIT @limit",
            new { handlerId, limit }).AsList();
    }

    public static List<Claim> SearchClaims(string? q, int limit = 500)
    {
        using var c = Open();
        if (string.IsNullOrWhiteSpace(q))
            return c.Query<Claim>(
                "SELECT * FROM claims ORDER BY created_at DESC LIMIT @limit", new { limit }).AsList();
        var pat = "%" + q.Trim() + "%";
        return c.Query<Claim>("""
            SELECT * FROM claims
             WHERE claim_number      ILIKE @pat
                OR policyholder_name ILIKE @pat
                OR license_plate     ILIKE @pat
                OR policy_number     ILIKE @pat
                OR COALESCE(vin,'')  ILIKE @pat
             ORDER BY created_at DESC LIMIT @limit
            """, new { pat, limit }).AsList();
    }

    public static void UpdateClaimAnalysis(string id, double? amount, double? confidence,
                                           double? fraudScore, string? damageCategoriesJson,
                                           string? summary, string status)
    {
        using var c = Open();
        c.Execute("""
            UPDATE claims SET
              estimated_amount_eur  = COALESCE(@amount, estimated_amount_eur),
              extraction_confidence = COALESCE(@confidence, extraction_confidence),
              fraud_score           = COALESCE(@fraudScore, fraud_score),
              damage_categories     = COALESCE(@damage::jsonb, damage_categories),
              summary               = COALESCE(@summary, summary),
              status                = @status
            WHERE id = @id
            """,
            new { id, amount, confidence, fraudScore, damage = damageCategoriesJson, summary, status });
    }

    public static void SetClaimStatus(string id, string status)
    {
        using var c = Open();
        c.Execute("UPDATE claims SET status = @status WHERE id = @id", new { id, status });
    }

    /// Extraction can discover an injury or a counterparty the FNOL form missed.
    /// Latched on only — a later run that fails to see the signal must not clear a
    /// hard gate that already fired.
    public static void SetDerivedFlags(string id, bool injuries, bool thirdParty)
    {
        using var c = Open();
        c.Execute("""
            UPDATE claims SET injuries = injuries OR @injuries,
                              third_party_involved = third_party_involved OR @thirdParty
            WHERE id = @id
            """, new { id, injuries, thirdParty });
    }

    public static void RecordDecision(string id, string outcome, string reasonsJson, string traceJson)
    {
        using var c = Open();
        c.Execute("""
            UPDATE claims SET decision_outcome = @outcome, decision_reasons = @reasons::jsonb,
                              rules_trace = @trace::jsonb, status = 'decided'
            WHERE id = @id
            """,
            new { id, outcome, reasons = reasonsJson, trace = traceJson });
    }

    public static void RecordFraudAndAssessment(string id, string signalsJson, string assessmentJson)
    {
        using var c = Open();
        c.Execute("""
            UPDATE claims SET fraud_signals = @signals::jsonb, assessment = @assessment::jsonb
            WHERE id = @id
            """, new { id, signals = signalsJson, assessment = assessmentJson });
    }

    // --- documents ------------------------------------------------------------

    public static string AddDocument(string claimId, string filename, string filepath, string? docType,
                                     string? contentHash, string? perceptualHash, string? extractedJson,
                                     string? uploadedByPortalUserId = null)
    {
        var id = NewId();
        using var c = Open();
        c.Execute("""
            INSERT INTO documents (id, claim_id, filename, filepath, doc_type,
                                   content_hash, perceptual_hash, extracted, uploaded_by_portal_user_id)
            VALUES (@id, @claimId, @filename, @filepath, @docType, @contentHash,
                    @perceptualHash, @extracted::jsonb, @uploadedByPortalUserId)
            """,
            new { id, claimId, filename, filepath, docType, contentHash, perceptualHash,
                  extracted = extractedJson, uploadedByPortalUserId });
        return id;
    }

    public static List<Document> GetDocuments(string claimId)
    {
        using var c = Open();
        return c.Query<Document>(
            "SELECT * FROM documents WHERE claim_id = @claimId ORDER BY created_at", new { claimId }).AsList();
    }

    /// Prior claims sharing a photo's perceptual hash — the recycled-photo signal.
    ///
    /// Hamming distance, not equality: a pHash exists precisely so that a photo
    /// re-saved by a phone, WhatsApp or an email client still matches. Exact
    /// equality would only catch byte-identical files, which content_hash already
    /// does. Threshold 6 of 64 bits is the conventional near-duplicate cut-off.
    ///
    /// ponytail: sequential scan with bit_count. Fine to five figures of documents;
    /// if it stops being fine, store the hash as bigint and pre-filter on 16-bit bands.
    public const int PhashHammingThreshold = 6;

    public static List<string> FindDuplicatePhotoClaims(string? perceptualHash, string currentClaimId)
    {
        if (string.IsNullOrEmpty(perceptualHash) || perceptualHash.Length != 16) return [];
        using var c = Open();
        return c.Query<string>("""
            SELECT DISTINCT claim_id FROM documents
            WHERE perceptual_hash IS NOT NULL
              AND length(perceptual_hash) = 16
              AND claim_id <> @currentClaimId
              AND bit_count(('x' || perceptual_hash)::bit(64) # ('x' || @perceptualHash)::bit(64)) <= @threshold
            """, new { perceptualHash, currentClaimId, threshold = PhashHammingThreshold }).AsList();
    }

    // --- agents ---------------------------------------------------------------

    public static List<AgentDef> ListAgents()
    {
        using var c = Open();
        return c.Query<AgentDef>("SELECT * FROM agent ORDER BY created_at, name").AsList();
    }

    /// Agents this handler may use: active, and covered by a company-wide grant
    /// or a personal one. Super admin sees everything via ListAgents instead.
    public static List<AgentDef> ListAgentsFor(string handlerId)
    {
        using var c = Open();
        return c.Query<AgentDef>("""
            SELECT a.* FROM agent a
            WHERE a.active AND EXISTS (
                SELECT 1 FROM agent_grant g
                WHERE g.agent_id = a.id AND (g.handler_id IS NULL OR g.handler_id = @handlerId))
            ORDER BY a.created_at, a.name
            """, new { handlerId }).AsList();
    }

    public static AgentDef? GetAgent(string id)
    {
        using var c = Open();
        return c.QuerySingleOrDefault<AgentDef>("SELECT * FROM agent WHERE id = @id", new { id });
    }

    public static void CreateAgent(AgentDef a)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO agent (id, name, template, lang, tone, tools, prompt, active, trigger_kind, autonomy, created_by)
            VALUES (@Id, @Name, @Template, @Lang, @Tone, @Tools::jsonb, @Prompt, @Active, @TriggerKind, @Autonomy, @CreatedBy)
            """, a);
    }

    public static void SetAgentFlow(string id, string flowJson)
    {
        using var c = Open();
        c.Execute("UPDATE agent SET flow = @flowJson::jsonb WHERE id = @id", new { id, flowJson });
    }

    public static void SetAgentActive(string id, bool active)
    {
        using var c = Open();
        c.Execute("UPDATE agent SET active = @active WHERE id = @id", new { id, active });
    }

    public static List<AgentGrant> ListAgentGrants()
    {
        using var c = Open();
        return c.Query<AgentGrant>("""
            SELECT g.*, h.name AS handler_name, gb.name AS granted_by_name
            FROM agent_grant g
            LEFT JOIN handlers h ON h.id = g.handler_id
            LEFT JOIN handlers gb ON gb.id = g.granted_by
            ORDER BY g.granted_at DESC
            """).AsList();
    }

    public static void AddAgentGrant(string agentId, string? handlerId, string? grantedBy)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO agent_grant (agent_id, handler_id, granted_by)
            VALUES (@agentId, @handlerId, @grantedBy)
            ON CONFLICT (agent_id, COALESCE(handler_id, '')) DO NOTHING
            """, new { agentId, handlerId, grantedBy });
    }

    public static void RemoveAgentGrant(long id)
    {
        using var c = Open();
        c.Execute("DELETE FROM agent_grant WHERE id = @id", new { id });
    }

    public static bool HasAgentAccess(string agentId, string handlerId)
    {
        using var c = Open();
        return c.ExecuteScalar<bool>("""
            SELECT EXISTS (SELECT 1 FROM agent_grant
                           WHERE agent_id = @agentId AND (handler_id IS NULL OR handler_id = @handlerId))
            """, new { agentId, handlerId });
    }

    // --- handlers -------------------------------------------------------------

    public static void UpsertHandler(string id, string name, string email, string role)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO handlers (id, name, email, role, active) VALUES (@id, @name, @email, @role, true)
            ON CONFLICT (id) DO UPDATE SET name = excluded.name, email = excluded.email, role = excluded.role
            """, new { id, name, email, role });
    }

    public static List<Handler> ListHandlers(bool activeOnly = true)
    {
        using var c = Open();
        return c.Query<Handler>(
            $"SELECT * FROM handlers {(activeOnly ? "WHERE active" : "")} ORDER BY role, name").AsList();
    }

    public static Handler? GetHandler(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        using var c = Open();
        return c.QuerySingleOrDefault<Handler>("SELECT * FROM handlers WHERE id = @id", new { id });
    }

    public static List<Handler> HandlersByRole(string role)
    {
        using var c = Open();
        return c.Query<Handler>(
            "SELECT * FROM handlers WHERE role = @role AND active ORDER BY name", new { role }).AsList();
    }

    public static void AssignClaim(string claimId, string? handlerId)
    {
        using var c = Open();
        c.Execute("UPDATE claims SET assigned_handler_id = @handlerId WHERE id = @claimId",
                  new { claimId, handlerId });
    }

    public static int BulkAssignClaims(IEnumerable<string> claimIds, string handlerId)
    {
        var ids = claimIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        if (ids.Length == 0) return 0;
        using var c = Open();
        return c.Execute(
            "UPDATE claims SET assigned_handler_id = @handlerId WHERE id = ANY(@ids)",
            new { handlerId, ids });
    }

    // --- authentication ---------------------------------------------------------

    public static Handler? GetHandlerByEmail(string email)
    {
        using var c = Open();
        return c.QuerySingleOrDefault<Handler>(
            "SELECT * FROM handlers WHERE lower(email) = lower(@email) AND active", new { email });
    }

    public static string? GetHandlerPasswordHash(string id)
    {
        using var c = Open();
        return c.ExecuteScalar<string?>("SELECT password_hash FROM handlers WHERE id = @id", new { id });
    }

    /// Only sets a password where none exists, so re-seeding never resets a
    /// password someone has already changed.
    public static void SetHandlerPasswordIfMissing(string id, string hash)
    {
        using var c = Open();
        c.Execute("UPDATE handlers SET password_hash = @hash WHERE id = @id AND password_hash IS NULL",
                  new { id, hash });
    }

    public static void TouchHandlerLogin(string id)
    {
        using var c = Open();
        c.Execute("UPDATE handlers SET last_login_at = now() WHERE id = @id", new { id });
    }

    // --- portal users -------------------------------------------------------------

    public static PortalUser? GetPortalUser(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        using var c = Open();
        return c.QuerySingleOrDefault<PortalUser>("SELECT * FROM portal_users WHERE id = @id", new { id });
    }

    public static PortalUser? GetPortalUserByEmail(string email)
    {
        using var c = Open();
        return c.QuerySingleOrDefault<PortalUser>(
            "SELECT * FROM portal_users WHERE lower(email) = lower(@email)", new { email });
    }

    /// Returns null when the email is already taken.
    public static PortalUser? CreatePortalUser(string email, string name, string? phone, string passwordHash)
    {
        var id = NewId();
        using var c = Open();
        var rows = c.Execute("""
            INSERT INTO portal_users (id, email, name, phone, password_hash)
            VALUES (@id, @email, @name, @phone, @passwordHash)
            ON CONFLICT DO NOTHING
            """, new { id, email, name, phone, passwordHash });
        return rows == 0 ? null : GetPortalUser(id);
    }

    public static void TouchPortalLogin(string id)
    {
        using var c = Open();
        c.Execute("UPDATE portal_users SET last_login_at = now() WHERE id = @id", new { id });
    }

    public static void LinkClaimToPortalUser(string claimId, string portalUserId)
    {
        using var c = Open();
        c.Execute("UPDATE claims SET portal_user_id = @portalUserId WHERE id = @claimId",
                  new { claimId, portalUserId });
    }

    public static List<Claim> ListClaimsForPortalUser(string portalUserId)
    {
        using var c = Open();
        return c.Query<Claim>(
            "SELECT * FROM claims WHERE portal_user_id = @portalUserId ORDER BY created_at DESC",
            new { portalUserId }).AsList();
    }

    /// The only way the portal reads a claim: ownership is part of the query, so a
    /// guessed claim id returns nothing rather than someone else's file.
    public static Claim? GetClaimForPortalUser(string claimId, string portalUserId)
    {
        using var c = Open();
        return c.QuerySingleOrDefault<Claim>(
            "SELECT * FROM claims WHERE id = @claimId AND portal_user_id = @portalUserId",
            new { claimId, portalUserId });
    }

    /// Customer-safe slice of the trail. Internal by default (V3): a delegation
    /// note or a fraud escalation is never in this result set.
    public static List<ActivityEntry> ListCustomerActivity(string claimId)
    {
        using var c = Open();
        return c.Query<ActivityEntry>("""
            SELECT a.id, a.claim_id, a.kind, a.body, a.created_at, a.visible_to_customer,
                   a.portal_user_id, NULL::text AS meta, NULL::text AS actor_handler_id,
                   NULL::text AS actor_name, NULL::text AS actor_role
            FROM activity a
            WHERE a.claim_id = @claimId AND a.visible_to_customer
            ORDER BY a.created_at, a.id
            """, new { claimId }).AsList();
    }

    public static void AddCustomerActivity(string claimId, string kind, string? portalUserId, string? body)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO activity (claim_id, kind, body, portal_user_id, visible_to_customer)
            VALUES (@claimId, @kind, @body, @portalUserId, true)
            """, new { claimId, kind, body, portalUserId });
    }

    // --- email templates ------------------------------------------------------

    public static void UpsertEmailTemplate(string id, string name, string audience, string subject, string body)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO email_templates (id, name, audience, subject, body)
            VALUES (@id, @name, @audience, @subject, @body)
            ON CONFLICT (id) DO UPDATE SET name = excluded.name, audience = excluded.audience,
                                           subject = excluded.subject, body = excluded.body
            """, new { id, name, audience, subject, body });
    }

    public static List<EmailTemplate> ListEmailTemplates()
    {
        using var c = Open();
        return c.Query<EmailTemplate>("SELECT * FROM email_templates ORDER BY audience, name").AsList();
    }

    public static EmailTemplate? GetEmailTemplate(string id)
    {
        using var c = Open();
        return c.QuerySingleOrDefault<EmailTemplate>(
            "SELECT * FROM email_templates WHERE id = @id", new { id });
    }

    // --- activity -------------------------------------------------------------

    public static void AddActivity(string? claimId, string kind, string? actorHandlerId,
                                   string? body, string? metaJson = null,
                                   bool visibleToCustomer = false)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO activity (claim_id, actor_handler_id, kind, body, meta, visible_to_customer)
            VALUES (@claimId, @actorHandlerId, @kind, @body, @meta::jsonb, @visibleToCustomer)
            """, new { claimId, actorHandlerId, kind, body, meta = metaJson, visibleToCustomer });
    }

    public static List<ActivityEntry> ListActivity(string claimId)
    {
        using var c = Open();
        return c.Query<ActivityEntry>("""
            SELECT a.*, h.name AS actor_name, h.role AS actor_role
            FROM activity a LEFT JOIN handlers h ON h.id = a.actor_handler_id
            WHERE a.claim_id = @claimId
            ORDER BY a.created_at DESC, a.id DESC
            """, new { claimId }).AsList();
    }

    public static List<ActivityEntry> ListAllActivity(int limit = 200, string? kind = null)
    {
        using var c = Open();
        return c.Query<ActivityEntry>($"""
            SELECT a.*, h.name AS actor_name, h.role AS actor_role, cl.claim_number
            FROM activity a
            LEFT JOIN handlers h ON h.id = a.actor_handler_id
            LEFT JOIN claims   cl ON cl.id = a.claim_id
            {(string.IsNullOrEmpty(kind) ? "" : "WHERE a.kind = @kind")}
            ORDER BY a.created_at DESC, a.id DESC
            LIMIT @limit
            """, new { limit, kind }).AsList();
    }

    /// Cross-claim activity for the header bell. `handlerId` filters to items
    /// that touch the current user (they authored, or the claim is assigned to
    /// them). Null returns global recent activity — useful for super_admin.
    /// HD-4: what this handler did, most recent first. Actions they took — not
    /// everything that happened on their claims.
    public static List<ActivityEntry> RecentByActor(string? handlerId, int limit = 8)
    {
        if (handlerId is null or "") return [];
        using var c = Open();
        return c.Query<ActivityEntry>("""
            SELECT a.*, h.name AS actor_name, h.role AS actor_role, cl.claim_number
            FROM activity a
            LEFT JOIN handlers h  ON h.id  = a.actor_handler_id
            LEFT JOIN claims   cl ON cl.id = a.claim_id
            WHERE a.actor_handler_id = @handlerId
            ORDER BY a.created_at DESC, a.id DESC
            LIMIT @limit
            """, new { handlerId, limit }).AsList();
    }

    public static List<ActivityEntry> RecentForBell(string? handlerId, int limit = 8, int hours = 48)
    {
        using var c = Open();
        var since = DateTime.UtcNow.AddHours(-hours);
        var where = handlerId is null
            ? "a.created_at >= @since"
            : "a.created_at >= @since AND (a.actor_handler_id = @handlerId OR cl.assigned_handler_id = @handlerId)";
        return c.Query<ActivityEntry>($"""
            SELECT a.*, h.name AS actor_name, h.role AS actor_role, cl.claim_number
            FROM activity a
            LEFT JOIN handlers h ON h.id = a.actor_handler_id
            LEFT JOIN claims   cl ON cl.id = a.claim_id
            WHERE {where}
            ORDER BY a.created_at DESC, a.id DESC
            LIMIT @limit
            """, new { since, handlerId, limit }).AsList();
    }

    public static List<string> ActivityKinds()
    {
        using var c = Open();
        return c.Query<string>("SELECT DISTINCT kind FROM activity ORDER BY kind").AsList();
    }

    public static int CountForBell(string? handlerId, int hours = 24)
    {
        using var c = Open();
        var since = DateTime.UtcNow.AddHours(-hours);
        var where = handlerId is null
            ? "created_at >= @since"
            : "created_at >= @since AND (actor_handler_id IS DISTINCT FROM @handlerId) AND claim_id IN " +
              "(SELECT id FROM claims WHERE assigned_handler_id = @handlerId)";
        return c.ExecuteScalar<int>(
            $"SELECT count(*)::int FROM activity WHERE {where}", new { since, handlerId });
    }

    // --- legal corpus (FR-11) ---------------------------------------------------

    const string HitColumns = """
        c.id AS chunk_id, d.citation, d.title, d.source, d.doc_class, d.passage_kind,
        d.review_status, c.passage, d.url, d.valid_from, d.valid_to
        """;

    public static string? ActiveCorpusVersion()
    {
        using var c = Open();
        return c.ExecuteScalar<string?>("SELECT id FROM legal_corpus_version WHERE is_active LIMIT 1");
    }

    /// Dense arm of hybrid retrieval. pgvector cosine distance, HNSW-indexed.
    /// `asOf` applies the law in force on the incident date (temporal versioning).
    public static List<LegalHit> SearchDense(string embeddingLiteral, DateOnly asOf, int k,
                                             string corpusVersion, string[]? docClasses = null)
    {
        using var c = Open();
        var hits = c.Query<LegalHit>($"""
            SELECT {HitColumns}, 1 - (c.embedding <=> @q::vector) AS score
            FROM legal_chunk c JOIN legal_doc d ON d.id = c.doc_id
            WHERE c.embedding IS NOT NULL
              AND d.corpus_version = @corpusVersion
              AND d.valid_from <= @asOf AND (d.valid_to IS NULL OR d.valid_to > @asOf)
              AND (@docClasses::text[] IS NULL OR d.doc_class = ANY(@docClasses))
            ORDER BY c.embedding <=> @q::vector
            LIMIT @k
            """, new { q = embeddingLiteral, asOf, k, corpusVersion, docClasses }).AsList();
        foreach (var h in hits) h.RetrievalMode = "dense";
        return hits;
    }

    /// Lexical arm. OR-of-lexemes so a long claim narrative still matches, ranked
    /// by ts_rank_cd (Postgres cover-density ranking, BM25-adjacent, GIN-indexed).
    public static List<LegalHit> SearchLexical(string query, DateOnly asOf, int k,
                                               string corpusVersion, string[]? docClasses = null)
    {
        using var c = Open();
        var hits = c.Query<LegalHit>($"""
            WITH q AS (
                SELECT to_tsquery('dutch', string_agg(quote_literal(lexeme), ' | ')) AS tsq
                FROM (SELECT DISTINCT lexeme FROM unnest(to_tsvector('dutch', @query))) t
            )
            SELECT {HitColumns}, ts_rank_cd(c.tsv, q.tsq) AS score
            FROM legal_chunk c JOIN legal_doc d ON d.id = c.doc_id, q
            WHERE q.tsq IS NOT NULL AND c.tsv @@ q.tsq
              AND d.corpus_version = @corpusVersion
              AND d.valid_from <= @asOf AND (d.valid_to IS NULL OR d.valid_to > @asOf)
              AND (@docClasses::text[] IS NULL OR d.doc_class = ANY(@docClasses))
            ORDER BY score DESC
            LIMIT @k
            """, new { query, asOf, k, corpusVersion, docClasses }).AsList();
        foreach (var h in hits) h.RetrievalMode = "lexical";
        return hits;
    }

    public static LegalHit? GetChunk(string chunkId)
    {
        using var c = Open();
        return c.QuerySingleOrDefault<LegalHit>($"""
            SELECT {HitColumns}, 0 AS score
            FROM legal_chunk c JOIN legal_doc d ON d.id = c.doc_id
            WHERE c.id = @chunkId
            """, new { chunkId });
    }

    public static List<LegalHit> ListCorpus(string corpusVersion)
    {
        using var c = Open();
        return c.Query<LegalHit>($"""
            SELECT {HitColumns}, 0 AS score
            FROM legal_chunk c JOIN legal_doc d ON d.id = c.doc_id
            WHERE d.corpus_version = @corpusVersion
            ORDER BY d.doc_class, d.citation, c.ordinal
            """, new { corpusVersion }).AsList();
    }

    public static List<LegalDoc> ListLegalDocs(string corpusVersion)
    {
        using var c = Open();
        return c.Query<LegalDoc>("""
            SELECT d.id, d.corpus_version, d.citation, d.source, d.doc_class, d.title,
                   d.url, d.valid_from, d.valid_to, d.passage_kind, d.review_status,
                   COALESCE(cs.passage, '') AS passage,
                   (cs.embedding IS NOT NULL) AS embedded
            FROM legal_doc d
            LEFT JOIN LATERAL (
                SELECT passage, embedding FROM legal_chunk
                WHERE doc_id = d.id ORDER BY ordinal LIMIT 1
            ) cs ON true
            WHERE d.corpus_version = @corpusVersion
            ORDER BY d.doc_class, d.citation
            """, new { corpusVersion }).AsList();
    }

    public static LegalDoc? GetLegalDoc(string id)
    {
        using var c = Open();
        return c.QuerySingleOrDefault<LegalDoc>("""
            SELECT d.id, d.corpus_version, d.citation, d.source, d.doc_class, d.title,
                   d.url, d.valid_from, d.valid_to, d.passage_kind, d.review_status,
                   COALESCE(cs.passage, '') AS passage,
                   (cs.embedding IS NOT NULL) AS embedded
            FROM legal_doc d
            LEFT JOIN LATERAL (
                SELECT passage, embedding FROM legal_chunk
                WHERE doc_id = d.id ORDER BY ordinal LIMIT 1
            ) cs ON true
            WHERE d.id = @id
            """, new { id });
    }

    public static void UpsertLegalDoc(LegalDoc d)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO legal_doc (id, corpus_version, citation, source, doc_class, title,
                                   url, valid_from, valid_to, passage_kind, review_status)
            VALUES (@Id, @CorpusVersion, @Citation, @Source, @DocClass, @Title,
                    @Url, @ValidFrom, @ValidTo, @PassageKind, @ReviewStatus)
            ON CONFLICT (id) DO UPDATE SET
              corpus_version = excluded.corpus_version,
              citation       = excluded.citation,
              source         = excluded.source,
              doc_class      = excluded.doc_class,
              title          = excluded.title,
              url            = excluded.url,
              valid_from     = excluded.valid_from,
              valid_to       = excluded.valid_to,
              passage_kind   = excluded.passage_kind,
              review_status  = excluded.review_status
            """, d);
    }

    /// One passage per doc for the admin UI. Delete existing chunks and insert
    /// a fresh ordinal=1 chunk with a NULL embedding, which the next embed pass
    /// (or the "Embed now" button) picks up.
    public static void ReplaceLegalDocChunk(string docId, string passage)
    {
        using var c = Open();
        c.Execute("DELETE FROM legal_chunk WHERE doc_id = @docId", new { docId });
        c.Execute("""
            INSERT INTO legal_chunk (id, doc_id, ordinal, passage, tags)
            VALUES (@id, @docId, 1, @passage, '')
            """, new { id = docId + "-c1", docId, passage });
    }

    public static void DeleteLegalDoc(string id)
    {
        using var c = Open();
        c.Execute("DELETE FROM legal_doc WHERE id = @id", new { id });
    }

    /// Chunks whose text changed (or which were never embedded) since the last pass.
    public static List<(string Id, string Text)> ChunksNeedingEmbedding()
    {
        using var c = Open();
        return c.Query<(string, string)>("""
            SELECT c.id, d.citation || ' — ' || d.title || E'\n' || c.passage || E'\n' || c.tags
            FROM legal_chunk c JOIN legal_doc d ON d.id = c.doc_id
            WHERE c.embedding IS NULL
            ORDER BY c.id
            """).AsList();
    }

    public static void SetChunkEmbedding(string chunkId, string embeddingLiteral)
    {
        using var c = Open();
        c.Execute("UPDATE legal_chunk SET embedding = @e::vector WHERE id = @chunkId",
                  new { chunkId, e = embeddingLiteral });
    }

    public static (int Chunks, int Embedded) CorpusStats()
    {
        using var c = Open();
        return c.QuerySingle<(int, int)>(
            "SELECT count(*)::int, count(embedding)::int FROM legal_chunk");
    }

    public static void ReplaceClaimCitations(string claimId, IEnumerable<LegalHit> hits,
                                             string corpusVersion, double integrity)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        c.Execute("DELETE FROM claim_legal_citation WHERE claim_id = @claimId", new { claimId }, tx);
        foreach (var h in hits)
        {
            c.Execute("""
                INSERT INTO claim_legal_citation
                  (claim_id, chunk_id, citation, title, passage, url, score,
                   retrieval_mode, corpus_version, used_in, verified)
                VALUES (@claimId, @ChunkId, @Citation, @Title, @Passage, @Url, @Score,
                        @RetrievalMode, @corpusVersion, @UsedIn, @Verified)
                """,
                new
                {
                    claimId, h.ChunkId, h.Citation, h.Title, h.Passage, h.Url, h.Score,
                    h.RetrievalMode, corpusVersion, h.UsedIn, h.Verified,
                }, tx);
        }
        c.Execute("""
            UPDATE claims SET legal_corpus_version = @corpusVersion,
                              citation_integrity = @integrity
            WHERE id = @claimId
            """, new { claimId, corpusVersion, integrity }, tx);
        tx.Commit();
    }

    public static List<LegalHit> GetClaimCitations(string claimId)
    {
        using var c = Open();
        return c.Query<LegalHit>("""
            SELECT chunk_id, citation, title, passage, url, score, retrieval_mode,
                   used_in, verified, '' AS source, '' AS doc_class, '' AS passage_kind,
                   '' AS review_status, current_date AS valid_from, NULL::date AS valid_to
            FROM claim_legal_citation WHERE claim_id = @claimId
            ORDER BY verified, score DESC
            """, new { claimId }).AsList();
    }

    public static CitationHealth GetCitationHealth()
    {
        using var c = Open();
        // Only rows the model actually emitted count. claim_legal_citation also
        // stores the retrieved context (used_in = 'retrieved'), and leaving those in
        // the denominator would pad the ratio with rows that were never citations —
        // the KPI would read 6/7 while the claim itself recorded integrity 0.0.
        var h = c.QuerySingle<CitationHealth>("""
            SELECT count(DISTINCT claim_id)::int              AS claims_with_citations,
                   count(*)::int                              AS total_citations,
                   count(*) FILTER (WHERE NOT verified)::int  AS unresolved,
                   COALESCE(round(count(*) FILTER (WHERE verified)::numeric
                            / NULLIF(count(*), 0), 4), 1)::float8 AS integrity
            FROM claim_legal_citation
            WHERE used_in <> 'retrieved'
            """);
        h.CorpusVersion = ActiveCorpusVersion();
        (h.CorpusChunks, h.CorpusEmbedded) = CorpusStats();
        return h;
    }

    // --- LLM usage + metrics ----------------------------------------------------

    public static void RecordUsage(string? claimId, string operation, string model,
                                   int inputTokens, int outputTokens, double costUsd)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO llm_usage (claim_id, operation, model, input_tokens, output_tokens, cost_usd)
            VALUES (@claimId, @operation, @model, @inputTokens, @outputTokens, @costUsd)
            """, new { claimId, operation, model, inputTokens, outputTokens, costUsd });
    }

    public static UsageTotals GetUsageTotals()
    {
        using var c = Open();
        return c.QuerySingle<UsageTotals>("""
            SELECT
              round(COALESCE(SUM(cost_usd)::numeric, 0), 4)::float8 AS total_usd,
              COALESCE(SUM(input_tokens + output_tokens), 0)        AS total_tokens,
              count(*)::int                                         AS total_calls,
              round(COALESCE(SUM(cost_usd) FILTER (
                    WHERE created_at >= date_trunc('month', now()))::numeric, 0), 4)::float8 AS month_usd,
              count(*) FILTER (WHERE created_at >= date_trunc('month', now()))::int          AS month_calls,
              round(COALESCE(SUM(cost_usd) FILTER (
                    WHERE created_at::date = current_date)::numeric, 0), 4)::float8          AS today_usd,
              count(*) FILTER (WHERE created_at::date = current_date)::int                   AS today_calls
            FROM llm_usage
            """);
    }

    public static List<CostDay> UsageByDay(int days = 14)
    {
        using var c = Open();
        var rows = c.Query<CostDay>("""
            SELECT created_at::date AS day, round(SUM(cost_usd)::numeric, 4)::float8 AS cost_usd,
                   SUM(input_tokens) AS in_tok, SUM(output_tokens) AS out_tok, count(*)::int AS calls
            FROM llm_usage GROUP BY 1 ORDER BY 1 DESC LIMIT @days
            """, new { days }).AsList();
        rows.Reverse();
        return rows;
    }

    public static List<CostMonth> UsageByMonth(int months = 6)
    {
        using var c = Open();
        var rows = c.Query<CostMonth>("""
            SELECT to_char(created_at, 'YYYY-MM') AS month,
                   round(SUM(cost_usd)::numeric, 4)::float8 AS cost_usd, count(*)::int AS calls
            FROM llm_usage GROUP BY 1 ORDER BY 1 DESC LIMIT @months
            """, new { months }).AsList();
        rows.Reverse();
        return rows;
    }

    public static List<DayStat> ClaimStatsByDay(int days = 14)
    {
        using var c = Open();
        var rows = c.Query<DayStat>("""
            SELECT created_at::date AS day, count(*)::int AS total,
                   count(*) FILTER (WHERE decision_outcome = 'auto_approved')::int AS stp,
                   count(*) FILTER (WHERE decision_outcome = 'assisted')::int      AS assisted,
                   count(*) FILTER (WHERE decision_outcome = 'manual')::int        AS manual
            FROM claims GROUP BY 1 ORDER BY 1 DESC LIMIT @days
            """, new { days }).AsList();
        rows.Reverse();
        return rows;
    }

    public static List<MonthStat> ClaimStatsByMonth(int months = 6)
    {
        using var c = Open();
        var rows = c.Query<MonthStat>("""
            SELECT to_char(created_at, 'YYYY-MM') AS month, count(*)::int AS total,
                   count(*) FILTER (WHERE decision_outcome = 'auto_approved')::int AS stp,
                   count(*) FILTER (WHERE decision_outcome = 'assisted')::int      AS assisted,
                   count(*) FILTER (WHERE decision_outcome = 'manual')::int        AS manual
            FROM claims GROUP BY 1 ORDER BY 1 DESC LIMIT @months
            """, new { months }).AsList();
        rows.Reverse();
        return rows;
    }

    public static StpSummary GetStpSummary()
    {
        using var c = Open();
        return c.QuerySingle<StpSummary>("""
            SELECT count(*)::int AS total,
                   count(*) FILTER (WHERE decision_outcome = 'auto_approved')::int AS stp,
                   count(*) FILTER (WHERE decision_outcome = 'assisted')::int      AS assisted,
                   count(*) FILTER (WHERE decision_outcome = 'manual')::int        AS manual,
                   count(*) FILTER (WHERE decision_outcome IS NULL)::int           AS pending,
                   COALESCE(round(
                     count(*) FILTER (WHERE decision_outcome = 'auto_approved')::numeric
                     / NULLIF(count(*) FILTER (WHERE decision_outcome IS NOT NULL), 0), 3), 0)::float8 AS stp_rate
            FROM claims
            """);
    }

    // --- reference data ---------------------------------------------------------

    static readonly Handler[] DefaultHandlers =
    [
        new() { Id = "h_root", Name = "Root Admin",    Email = "admin@example.nl",         Role = "super_admin" },
        new() { Id = "h_alex", Name = "Alex Terlouw",  Email = "alex.terlouw@example.nl",  Role = "senior_adjuster" },
        new() { Id = "h_sam",  Name = "Sam de Jong",   Email = "sam.dejong@example.nl",    Role = "adjuster" },
        new() { Id = "h_lin",  Name = "Lin Voormans",  Email = "lin.voormans@example.nl",  Role = "fraud_specialist" },
        new() { Id = "h_mira", Name = "Mira Wortel",   Email = "mira.wortel@example.nl",   Role = "claim_handler" },
        new() { Id = "h_dara", Name = "Dara Aksoy",    Email = "dara.aksoy@example.nl",    Role = "injury_department" },
        new() { Id = "h_bas",  Name = "Bas Kortenhof", Email = "bas.kortenhof@example.nl", Role = "liability_department" },
        new() { Id = "h_marit", Name = "Marit Kuipers", Email = "marit.kuipers@example.nl", Role = "team_manager" },
        new() { Id = "h_evert", Name = "Evert Blok",    Email = "evert.blok@example.nl",    Role = "cfo" },
    ];

    static readonly EmailTemplate[] DefaultTemplates =
    [
        new()
        {
            Id = "t_ack", Name = "First-response acknowledgement", Audience = "customer",
            Subject = "We ontvingen uw schademelding {claim_number}",
            Body = """
                Beste {policyholder_name},

                Wij bevestigen ontvangst van uw schademelding met kenmerk {claim_number} voor voertuig {license_plate}.
                Ons team beoordeelt uw dossier en neemt binnen 3 werkdagen contact op.

                Kunt u — indien nog niet bijgevoegd — de volgende documenten aanleveren:
                - foto's van de schade,
                - reparatienota / offerte van de garage,
                - kopie proces-verbaal (indien aanwezig).

                Met vriendelijke groet,
                {handler_name}
                {handler_email}
                """,
        },
        new()
        {
            Id = "t_docs", Name = "Document request", Audience = "customer",
            Subject = "Aanvullende documenten nodig voor {claim_number}",
            Body = """
                Beste {policyholder_name},

                Voor de verdere behandeling van schadedossier {claim_number} hebben wij nog het volgende nodig:
                - ontbrekende foto's van de schade,
                - volledige reparatienota / offerte,
                - (indien van toepassing) aanrijdingsformulier.

                Reageert u binnen 14 dagen zodat wij het dossier vlot kunnen afronden.

                Met vriendelijke groet,
                {handler_name}
                """,
        },
        new()
        {
            Id = "t_approve", Name = "Approval notification", Audience = "customer",
            Subject = "Uw schadeclaim {claim_number} is goedgekeurd",
            Body = """
                Beste {policyholder_name},

                Uw schadeclaim {claim_number} is goedgekeurd. De uitkering wordt binnen 5 werkdagen overgemaakt op het bij ons bekende rekeningnummer.

                Bij vragen: bel of mail ons met dossierkenmerk {claim_number}.

                Met vriendelijke groet,
                {handler_name}
                {handler_email}
                """,
        },
        new()
        {
            Id = "t_escalate_fraud", Name = "Escalation to fraud team", Audience = "internal",
            Subject = "[FRAUD REVIEW] {claim_number} — {license_plate}",
            Body = """
                Team fraude,

                Dossier {claim_number} van {policyholder_name} (kenteken {license_plate}, verliesdatum {loss_date}) staat op manual review met een fraud-score van niet-triviale hoogte.

                Verzoek: beoordeel de fraud-signalen (recycled photo / EXIF / laat gemeld / lagen) en geef binnen 2 werkdagen advies. Registratie in het EVR blijft een menselijk besluit conform PIFI.

                Groet,
                {handler_name}
                """,
        },
        new()
        {
            Id = "t_delegate", Name = "Internal delegation note", Audience = "internal",
            Subject = "Overdracht {claim_number}",
            Body = """
                Hallo {handler_name},

                Ik draag dossier {claim_number} ({policyholder_name} — {license_plate}) aan je over.

                Reden: [vul kort in]

                Status: {status}. Zie de claim-detailpagina voor de volledige assessment, fraud-signalen, juridische citaties en rules-audit.

                Groet,
                [Overdragende behandelaar]
                """,
        },
    ];

    // --- BI dashboard -----------------------------------------------------------

    public class HandlerLoad { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string Role { get; set; } = ""; public int Open { get; set; } public int Decided { get; set; } }
    public class BiHandler { public int MyOpen { get; set; } public int Awaiting { get; set; } public int DecidedToday { get; set; } public double AvgAgeDays { get; set; } public int OldestOpenDays { get; set; } public double AvgTouches { get; set; } }
    public class BiManager { public int Throughput30 { get; set; } public double StpMonth { get; set; } public int Backlog { get; set; } public double AvgAmountEur { get; set; } public int FraudFlags { get; set; } public int ActiveHandlers { get; set; } }
    public class BiCfo { public double TotalExposureEur { get; set; } public double MonthExposureEur { get; set; } public double AvgCostEur { get; set; } public double LlmSpend30 { get; set; } public int AutoApproved { get; set; } public int Total { get; set; } }

    public static BiHandler HandlerKpis(string? handlerId)
    {
        using var c = Open();
        var open = handlerId is null ? 0 : c.ExecuteScalar<int>(
            "SELECT count(*)::int FROM claims WHERE assigned_handler_id = @handlerId AND decision_outcome IS NULL",
            new { handlerId });
        var awaiting = c.ExecuteScalar<int>("SELECT count(*)::int FROM claims WHERE decision_outcome IS NULL");
        var decidedToday = c.ExecuteScalar<int>(
            "SELECT count(*)::int FROM claims WHERE decision_outcome IS NOT NULL AND created_at::date = current_date");
        var avgAge = c.ExecuteScalar<double?>(
            "SELECT COALESCE(AVG(EXTRACT(EPOCH FROM (now()-created_at))/86400)::float, 0) FROM claims WHERE decision_outcome IS NULL") ?? 0;
        var oldest = c.ExecuteScalar<double?>(
            "SELECT COALESCE(MAX(EXTRACT(EPOCH FROM (now()-created_at))/86400)::float, 0) FROM claims WHERE decision_outcome IS NULL") ?? 0;
        var avgTouches = c.ExecuteScalar<double?>("""
            SELECT COALESCE(AVG(cnt)::float, 0) FROM (
              SELECT claim_id, count(*)::int AS cnt FROM activity
              WHERE claim_id IS NOT NULL GROUP BY claim_id
            ) t
            """) ?? 0;
        return new BiHandler
        {
            MyOpen = open, Awaiting = awaiting, DecidedToday = decidedToday,
            AvgAgeDays = Math.Round(avgAge, 1), OldestOpenDays = (int)Math.Round(oldest),
            AvgTouches = Math.Round(avgTouches, 1),
        };
    }

    public static BiManager ManagerKpis()
    {
        using var c = Open();
        var throughput = c.ExecuteScalar<int>(
            "SELECT count(*)::int FROM claims WHERE decision_outcome IS NOT NULL AND created_at >= now() - interval '30 days'");
        var stp = c.ExecuteScalar<double?>("""
            SELECT COALESCE(
              (count(*) FILTER (WHERE decision_outcome='auto_approved')::float
               / NULLIF(count(*) FILTER (WHERE decision_outcome IS NOT NULL), 0)), 0)
            FROM claims WHERE created_at >= date_trunc('month', now())
            """) ?? 0;
        var backlog = c.ExecuteScalar<int>(
            "SELECT count(*)::int FROM claims WHERE decision_outcome IS NULL");
        var avgAmt = c.ExecuteScalar<double?>(
            "SELECT COALESCE(AVG(estimated_amount_eur)::float, 0) FROM claims WHERE estimated_amount_eur IS NOT NULL") ?? 0;
        var fraud = c.ExecuteScalar<int>(
            "SELECT count(*)::int FROM claims WHERE fraud_score >= 0.3");
        var handlers = c.ExecuteScalar<int>(
            "SELECT count(*)::int FROM handlers WHERE active");
        return new BiManager
        {
            Throughput30 = throughput, StpMonth = stp, Backlog = backlog,
            AvgAmountEur = avgAmt, FraudFlags = fraud, ActiveHandlers = handlers,
        };
    }

    public static BiCfo CfoKpis()
    {
        using var c = Open();
        var total = c.ExecuteScalar<double?>(
            "SELECT COALESCE(SUM(estimated_amount_eur)::float, 0) FROM claims") ?? 0;
        var month = c.ExecuteScalar<double?>(
            "SELECT COALESCE(SUM(estimated_amount_eur)::float, 0) FROM claims WHERE created_at >= date_trunc('month', now())") ?? 0;
        var avg = c.ExecuteScalar<double?>(
            "SELECT COALESCE(AVG(estimated_amount_eur)::float, 0) FROM claims WHERE estimated_amount_eur IS NOT NULL") ?? 0;
        var llm = c.ExecuteScalar<double?>(
            "SELECT COALESCE(SUM(cost_usd)::float, 0) FROM llm_usage WHERE created_at >= now() - interval '30 days'") ?? 0;
        var auto = c.ExecuteScalar<int>(
            "SELECT count(*)::int FROM claims WHERE decision_outcome = 'auto_approved'");
        var totalCount = c.ExecuteScalar<int>("SELECT count(*)::int FROM claims");
        return new BiCfo
        {
            TotalExposureEur = total, MonthExposureEur = month, AvgCostEur = avg,
            LlmSpend30 = llm, AutoApproved = auto, Total = totalCount,
        };
    }

    public static List<HandlerLoad> WorkloadByHandler()
    {
        using var c = Open();
        return c.Query<HandlerLoad>("""
            SELECT h.id, h.name, h.role,
                   count(*) FILTER (WHERE cl.decision_outcome IS NULL)::int      AS open,
                   count(*) FILTER (WHERE cl.decision_outcome IS NOT NULL)::int  AS decided
            FROM handlers h
            LEFT JOIN claims cl ON cl.assigned_handler_id = h.id
            WHERE h.active
            GROUP BY h.id, h.name, h.role
            ORDER BY open DESC, decided DESC
            """).AsList();
    }

    public static List<Claim> LargestOpenClaims(int limit = 8)
    {
        using var c = Open();
        return c.Query<Claim>(
            "SELECT * FROM claims WHERE decision_outcome IS NULL " +
            "ORDER BY estimated_amount_eur DESC NULLS LAST LIMIT @limit", new { limit }).AsList();
    }

    // --- workflows --------------------------------------------------------------

    public static List<Workflow> ListWorkflows()
    {
        using var c = Open();
        return c.Query<Workflow>(
            "SELECT id, name, trigger_kind, active, config::text AS config, created_at, created_by " +
            "FROM workflow ORDER BY created_at DESC").AsList();
    }

    public static Workflow? GetWorkflow(string id)
    {
        using var c = Open();
        return c.QuerySingleOrDefault<Workflow>(
            "SELECT id, name, trigger_kind, active, config::text AS config, created_at, created_by " +
            "FROM workflow WHERE id = @id", new { id });
    }

    public static void UpsertWorkflow(Workflow w, string? createdBy = null)
    {
        using var c = Open();
        c.Execute("""
            INSERT INTO workflow (id, name, trigger_kind, active, config, created_by)
            VALUES (@Id, @Name, @TriggerKind, @Active, @Config::jsonb, @createdBy)
            ON CONFLICT (id) DO UPDATE SET
              name = excluded.name, trigger_kind = excluded.trigger_kind,
              active = excluded.active, config = excluded.config
            """, new { w.Id, w.Name, w.TriggerKind, w.Active, w.Config, createdBy });
    }

    public static void DeleteWorkflow(string id)
    {
        using var c = Open();
        c.Execute("DELETE FROM workflow WHERE id = @id", new { id });
    }

    public static List<WorkflowStep> ListSteps(string workflowId)
    {
        using var c = Open();
        return c.Query<WorkflowStep>(
            "SELECT id, workflow_id, ordinal, kind, config::text AS config " +
            "FROM workflow_step WHERE workflow_id = @workflowId ORDER BY ordinal",
            new { workflowId }).AsList();
    }

    /// Replace-all: simpler than diffing ordinals and safe because the whole
    /// step list ships from one form submit.
    public static void ReplaceSteps(string workflowId, IEnumerable<WorkflowStep> steps)
    {
        using var c = Open();
        c.Execute("DELETE FROM workflow_step WHERE workflow_id = @workflowId", new { workflowId });
        var i = 1;
        foreach (var s in steps)
        {
            c.Execute("""
                INSERT INTO workflow_step (workflow_id, ordinal, kind, config)
                VALUES (@workflowId, @ord, @kind, @cfg::jsonb)
                """, new { workflowId, ord = i++, kind = s.Kind, cfg = s.Config });
        }
    }

    public static long InsertRun(string workflowId, string? claimId, string? triggerRef)
    {
        using var c = Open();
        return c.ExecuteScalar<long>("""
            INSERT INTO workflow_run (workflow_id, claim_id, trigger_ref, status)
            VALUES (@workflowId, @claimId, @triggerRef, 'running')
            RETURNING id
            """, new { workflowId, claimId, triggerRef });
    }

    public static void FinishRun(long runId, string status, string? contextJson, string? error)
    {
        using var c = Open();
        c.Execute("""
            UPDATE workflow_run
               SET status = @status, finished_at = now(),
                   context = @contextJson::jsonb, error = @error
             WHERE id = @runId
            """, new { runId, status, contextJson, error });
    }

    public static List<WorkflowRun> ListRuns(string? workflowId, int limit = 30)
    {
        using var c = Open();
        var where = workflowId is null ? "" : "WHERE r.workflow_id = @workflowId";
        return c.Query<WorkflowRun>($"""
            SELECT r.id, r.workflow_id, r.claim_id, r.trigger_ref, r.status,
                   r.started_at, r.finished_at, r.error, r.context::text AS context,
                   w.name AS workflow_name, cl.claim_number
            FROM workflow_run r
            LEFT JOIN workflow w  ON w.id  = r.workflow_id
            LEFT JOIN claims   cl ON cl.id = r.claim_id
            {where}
            ORDER BY r.started_at DESC
            LIMIT @limit
            """, new { workflowId, limit }).AsList();
    }
}
