using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Application.Commands;

public class ChangeTaxRateCommand
{
    public CountryId CountryId { get; set; }
    public int NewTaxRate { get; set; }
}
