using System.Text.Json.Nodes;

namespace JbAutoAi;

/// Vehicle lookup against the RDW open-data registry (opendata.rdw.nl) — the
/// same public Socrata API the Python `rdw` package wraps. No key, no SLA:
/// treat every lookup as best-effort garnish, never as a hard dependency.
public static class Rdw
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    public record Vehicle(string Kenteken, string? Merk, string? Model, string? Kleur,
                          string? Soort, int? Bouwjaar, string? Brandstof,
                          string? ApkVervaldatum, bool? WamVerzekerd);

    public static string Normalize(string plate) =>
        new string(plate.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static async Task<Vehicle?> LookupAsync(string plate)
    {
        var k = Normalize(plate);
        if (k.Length is < 4 or > 8) return null;
        try
        {
            var arr = JsonNode.Parse(await Http.GetStringAsync(
                $"https://opendata.rdw.nl/resource/m9d7-ebf2.json?kenteken={Uri.EscapeDataString(k)}")) as JsonArray;
            if (arr is not { Count: > 0 }) return null;
            var v = arr[0]!;
            string? S(string f) => v[f]?.GetValue<string>();

            string? fuel = null;
            try
            {
                var fArr = JsonNode.Parse(await Http.GetStringAsync(
                    $"https://opendata.rdw.nl/resource/8ys7-d773.json?kenteken={Uri.EscapeDataString(k)}")) as JsonArray;
                var fuels = (fArr ?? []).Select(x => x?["brandstof_omschrijving"]?.GetValue<string>())
                                        .Where(x => x is { Length: > 0 }).ToList();
                if (fuels.Count > 0) fuel = string.Join(" / ", fuels);
            }
            catch { /* fuel dataset is optional detail */ }

            var first = S("datum_eerste_toelating");
            var apk = S("vervaldatum_apk");
            return new Vehicle(k, S("merk"), S("handelsbenaming"), S("eerste_kleur"), S("voertuigsoort"),
                first is { Length: >= 4 } && int.TryParse(first[..4], out var y) ? y : null,
                fuel,
                apk is { Length: 8 } ? $"{apk[..4]}-{apk[4..6]}-{apk[6..]}" : apk,
                S("wam_verzekerd") switch { "Ja" => true, "Nee" => false, _ => null });
        }
        catch { return null; }   // offline or API down — the form still works
    }
}
