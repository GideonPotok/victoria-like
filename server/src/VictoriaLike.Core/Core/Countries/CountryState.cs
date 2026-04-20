namespace VictoriaLike.Core.Core.Countries;

public sealed class CountryState
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required List<string> ProvinceIds { get; init; }
    public decimal Treasury { get; set; }
    public decimal TaxRate { get; set; }
    public decimal PoorTaxRate { get; set; } = -1m;
    public decimal MiddleTaxRate { get; set; } = -1m;
    public decimal RichTaxRate { get; set; } = -1m;
    public decimal TariffRate { get; set; }
    public decimal EducationSpending { get; set; }
    public decimal MilitarySpending { get; set; }
    public decimal AdministrationSpending { get; set; }
    public bool IsPlayable { get; init; }
    public Dictionary<string, decimal> Stockpile { get; init; } = [];
}
