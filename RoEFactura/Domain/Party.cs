namespace RoEFactura.Domain;

public class Party
{
    public string Name { get; set; } = string.Empty;
    public string VatId { get; set; } = string.Empty;
    public Address Address { get; set; } = new();
}
