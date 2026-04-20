namespace VictoriaLike.Core.Core.Military;

public sealed class WarState
{
    public required string Id { get; init; }
    public required string AttackerCountryId { get; set; }
    public required string DefenderCountryId { get; set; }
    public required DateOnly StartedOn { get; set; }
    public DateOnly? EndedOn { get; set; }
    public bool IsActive { get; set; } = true;

    public bool Involves(string countryId) =>
        string.Equals(AttackerCountryId, countryId, StringComparison.Ordinal) ||
        string.Equals(DefenderCountryId, countryId, StringComparison.Ordinal);

    public bool IsBetween(string firstCountryId, string secondCountryId) =>
        (string.Equals(AttackerCountryId, firstCountryId, StringComparison.Ordinal) &&
         string.Equals(DefenderCountryId, secondCountryId, StringComparison.Ordinal)) ||
        (string.Equals(AttackerCountryId, secondCountryId, StringComparison.Ordinal) &&
         string.Equals(DefenderCountryId, firstCountryId, StringComparison.Ordinal));
}
