namespace RoEFactura.Generation;

/// <summary>
/// Payment instructions (BG-16) for a credit transfer: payment means code <c>30</c> (UNCL4461, BR-CL-16)
/// and the payee IBAN (BT-84, BR-50/BR-61). The generator strips whitespace, upper-cases the IBAN and
/// checks the ISO 13616 mod-97 checksum before emitting it.
/// </summary>
/// <param name="Iban">The seller's IBAN, with or without spaces.</param>
public sealed record EInvoicePayment(string Iban)
{
    /// <summary>UNCL4461 payment means code for "Credit transfer" (BT-81).</summary>
    public const string CreditTransferPaymentMeansCode = "30";
}
