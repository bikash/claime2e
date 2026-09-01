namespace JbAutoAi;

/// What a policyholder is allowed to see about their own claim.
///
/// Everything the workspace shows — fraud score, rule gates, citation integrity,
/// routing, handler notes, the model's reasoning — stops here. The portal gets a
/// status, a plain-language sentence, and the customer's own contributions.
public static class PortalView
{
    public record ClaimState(string Cls, string Label, string Message, int Step);

    public static ClaimState State(Claim c) => c.DecisionOutcome switch
    {
        "auto_approved" => new("ok", I18n.T("decision.auto"), I18n.T("portal.state.auto_approved"), 3),
        "assisted" => new("warn", I18n.T("portal.step.review"), I18n.T("portal.state.assisted"), 3),
        "manual" => new("warn", I18n.T("portal.step.review"), I18n.T("portal.state.manual"), 3),
        _ => new("", I18n.T("portal.step.submitted"), I18n.T("portal.state.pending"), 1),
    };

    /// Three visible steps. Deliberately coarse: "manual review" and "referred to
    /// the fraud team" look identical from outside, because telling a claimant they
    /// are under fraud investigation is neither our call nor PIFI-compliant.
    public static (string Label, bool Done)[] Steps(Claim c)
    {
        var step = State(c).Step;
        return
        [
            (I18n.T("portal.step.submitted"), step >= 1),
            (I18n.T("portal.step.review"), step >= 2 || c.Status != "submitted"),
            (I18n.T("portal.step.decided"), step >= 3),
        ];
    }

    /// Timeline text for a customer-visible activity row.
    public static string EventText(ActivityEntry e) => e.Kind switch
    {
        "status" => e.Body switch
        {
            "auto_approved" => I18n.T("portal.state.auto_approved"),
            "assisted" => I18n.T("portal.state.assisted"),
            "manual" => I18n.T("portal.state.manual"),
            _ => I18n.T("portal.state.pending"),
        },
        "created" => I18n.T("portal.event.created"),
        "customer_upload" => e.Body ?? I18n.T("portal.event.customer_upload"),
        "customer_comment" => e.Body ?? "",
        _ => e.Body ?? "",
    };

    public static string EventLabel(ActivityEntry e) => e.Kind switch
    {
        "status" => I18n.T("portal.status"),
        "created" => I18n.T("portal.step.submitted"),
        "customer_upload" => I18n.T("portal.event.customer_upload"),
        "customer_comment" => I18n.T("portal.event.customer_comment"),
        _ => e.KindLabel,
    };
}
