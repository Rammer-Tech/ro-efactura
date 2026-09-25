namespace RoEFactura.Models;

/// <summary>
/// The ANAF e-Factura environment to call. Test is the default; Production is opt-in.
/// </summary>
public enum AnafEnvironment
{
    Test = 0,
    Production = 1
}
