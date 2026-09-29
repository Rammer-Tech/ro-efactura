using RoEFactura.Generation;

namespace RoEFactura.Tests.Generation.TestData;

/// <summary>
/// The seven synthetic MTX-131 cases shared by the unit tests and the opt-in ANAF live validation.
/// Synthetic data only: checksum-valid test CUIs (1234567897, 876543213; key 753217532), the ISO 13616
/// sample IBAN, invoice numbers TST-E8-000n — never real invoices or the production series.
/// </summary>
public static class EInvoiceTestCases
{
    /// <summary>
    /// BT-120 reason of the MixedRates Exempt group: 141 characters, i.e. above the 100-character limit of
    /// the (inactive) BR-RO-L1019 assert, so the live run proves that ANAF accepts the pass-through.
    /// </summary>
    public const string MixedRatesExemptionReason =
        "Scutit de TVA fără drept de deducere conform art. 292 din Legea nr. 227/2015 privind Codul fiscal, cu modificările și completările ulterioare";

    public const string SellerCui = "1234567897";
    public const string BuyerCui = "876543213";
    public const string BuyerVatId = "RO876543213";
    public const string BuyerTradeRegisterId = "J12/345/2020";
    public const string ForeignBuyerVatId = "DE123456789";

    /// <summary>
    /// BT-47 of the foreign company buyer: ANAF's 13-zero "no Romanian identifier" value. ANAF's public
    /// validator (<c>validare/FACT1</c>) must identify a buyer CUI from an <c>RO</c>-prefixed BT-48 or an
    /// all-digit, checksum-valid BT-47; a foreign VAT id alone, or a non-numeric registry id, is rejected with
    /// <c>ERRIdentif</c> "nu a fost identificat cui cumparator" (probe evidence in docs/XML_GENERATION.md).
    /// </summary>
    public const string ForeignBuyerLegalId = "0000000000000";
    public const string Iban = "RO49AAAA1B31007593840000";

    public static readonly DateOnly IssueDate = new(2026, 9, 15);
    public static readonly DateOnly DueDate = new(2026, 9, 30);

    /// <summary>The case names, in the order used by <see cref="All"/>.</summary>
    public static readonly IReadOnlyList<string> Names =
    [
        nameof(B2BRoVatPayerBuyer),
        nameof(B2BRoNonVatPayerBuyer),
        nameof(NaturalPersonWithoutCnp),
        nameof(ForeignBuyer),
        nameof(NonVatPayerSeller),
        nameof(MixedRates),
        nameof(Storno)
    ];

    /// <summary>xunit member data: one serializable row (the case name) per case.</summary>
    public static IEnumerable<object[]> All => Names.Select(name => new object[] { name });

    /// <summary>Builds the case with the given name.</summary>
    public static EInvoiceDocument Get(string name)
    {
        return name switch
        {
            nameof(B2BRoVatPayerBuyer) => B2BRoVatPayerBuyer(),
            nameof(B2BRoNonVatPayerBuyer) => B2BRoNonVatPayerBuyer(),
            nameof(NaturalPersonWithoutCnp) => NaturalPersonWithoutCnp(),
            nameof(ForeignBuyer) => ForeignBuyer(),
            nameof(NonVatPayerSeller) => NonVatPayerSeller(),
            nameof(MixedRates) => MixedRates(),
            nameof(Storno) => Storno(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown E8 test case.")
        };
    }

    /// <summary>(g) B2B to a Romanian VAT payer in Bucharest: BT-48 + BT-47, SECTOR conversion, IBAN.</summary>
    public static EInvoiceDocument B2BRoVatPayerBuyer()
    {
        return new EInvoiceDocument
        {
            Number = "TST-E8-0001",
            IssueDate = IssueDate,
            DueDate = DueDate,
            Seller = Seller(isVatPayer: true),
            Buyer = BucharestCompanyBuyer(),
            Payment = new EInvoicePayment(Iban),
            Lines =
            [
                new EInvoiceLine
                {
                    Name = "Servicii dezvoltare software",
                    Quantity = 10m,
                    UnitCode = "HUR",
                    UnitPrice = 200.00m,
                    VatCategory = EInvoiceVatCategory.Standard,
                    VatRate = 21m
                },
                new EInvoiceLine
                {
                    Name = "Licență software anuală",
                    Quantity = 1m,
                    UnitPrice = 499.99m,
                    VatCategory = EInvoiceVatCategory.Standard,
                    VatRate = 21m
                }
            ]
        };
    }

    /// <summary>(a) B2B to a Romanian company that is not a VAT payer: BT-47 only, Ilfov county.</summary>
    public static EInvoiceDocument B2BRoNonVatPayerBuyer()
    {
        return new EInvoiceDocument
        {
            Number = "TST-E8-0002",
            IssueDate = IssueDate,
            DueDate = DueDate,
            Seller = Seller(isVatPayer: true),
            Buyer = new EInvoiceBuyer
            {
                Name = "Client Neplătitor SRL",
                LegalRegistrationId = BuyerCui,
                Address = new EInvoiceAddress
                {
                    Street = "Str. Exemplu 5",
                    City = "Voluntari",
                    County = "Ilfov",
                    PostalCode = "077190",
                    CountryCode = "RO"
                }
            },
            Lines =
            [
                new EInvoiceLine
                {
                    Name = "Servicii de consultanță",
                    Quantity = 1m,
                    UnitPrice = 1000.00m,
                    VatCategory = EInvoiceVatCategory.Standard,
                    VatRate = 21m
                }
            ]
        };
    }

    /// <summary>(b) Natural person without CNP: BT-47 = 13 zeros; payment terms instead of a bank account.</summary>
    public static EInvoiceDocument NaturalPersonWithoutCnp()
    {
        return new EInvoiceDocument
        {
            Number = "TST-E8-0003",
            IssueDate = IssueDate,
            DueDate = DueDate,
            PaymentTerms = "Plata la livrare",
            Seller = Seller(isVatPayer: true),
            Buyer = new EInvoiceBuyer
            {
                Name = "Ion Popescu",
                IsNaturalPerson = true,
                Address = new EInvoiceAddress
                {
                    Street = "Str. Florilor 5",
                    City = "Iași",
                    County = "Iași",
                    CountryCode = "RO"
                }
            },
            Lines =
            [
                new EInvoiceLine
                {
                    Name = "Reparație laptop",
                    Quantity = 1m,
                    UnitPrice = 250.00m,
                    VatCategory = EInvoiceVatCategory.Standard,
                    VatRate = 21m
                }
            ]
        };
    }

    /// <summary>
    /// (c) Foreign (DE) VAT-registered buyer: country DE, prefixed VAT id, BT-47 = 13 zeros (the buyer has no
    /// Romanian CUI/NIF), no county.
    /// </summary>
    public static EInvoiceDocument ForeignBuyer()
    {
        return new EInvoiceDocument
        {
            Number = "TST-E8-0004",
            IssueDate = IssueDate,
            DueDate = DueDate,
            Seller = Seller(isVatPayer: true),
            Buyer = new EInvoiceBuyer
            {
                Name = "Beispiel GmbH",
                VatId = ForeignBuyerVatId,
                LegalRegistrationId = ForeignBuyerLegalId,
                Address = new EInvoiceAddress
                {
                    Street = "Musterstraße 1",
                    City = "Berlin",
                    PostalCode = "10115",
                    CountryCode = "DE"
                }
            },
            Lines =
            [
                new EInvoiceLine
                {
                    Name = "Servicii de traducere",
                    Quantity = 1m,
                    UnitPrice = 1200.00m,
                    VatCategory = EInvoiceVatCategory.Standard,
                    VatRate = 21m
                }
            ]
        };
    }

    /// <summary>(d) Seller not VAT-registered: NotSubject lines, one O breakdown, no VAT identifiers.</summary>
    public static EInvoiceDocument NonVatPayerSeller()
    {
        return new EInvoiceDocument
        {
            Number = "TST-E8-0005",
            IssueDate = IssueDate,
            DueDate = DueDate,
            Seller = Seller(isVatPayer: false),
            Buyer = new EInvoiceBuyer
            {
                Name = "Client Test Timiș SRL",
                LegalRegistrationId = BuyerCui,
                Address = new EInvoiceAddress
                {
                    Street = "Bd. Exemplu 7",
                    City = "Timișoara",
                    County = "Timiș",
                    CountryCode = "RO"
                }
            },
            Lines =
            [
                new EInvoiceLine
                {
                    Name = "Servicii de design grafic",
                    Quantity = 1m,
                    UnitPrice = 750.00m,
                    VatCategory = EInvoiceVatCategory.NotSubject
                }
            ]
        };
    }

    /// <summary>
    /// (e) Mixed rates: S 21%, S 11% (4-decimal price, 3-decimal quantity) and Exempt with a 141-character
    /// reason — three VAT breakdown groups.
    /// </summary>
    public static EInvoiceDocument MixedRates()
    {
        return new EInvoiceDocument
        {
            Number = "TST-E8-0006",
            IssueDate = IssueDate,
            DueDate = DueDate,
            Seller = Seller(isVatPayer: true),
            Buyer = BucharestCompanyBuyer(),
            VatExemption = new EInvoiceVatExemption(null, MixedRatesExemptionReason),
            Lines =
            [
                new EInvoiceLine
                {
                    Name = "Servicii de consultanță",
                    Description = "Consultanță fiscală, septembrie 2026",
                    Quantity = 2m,
                    UnitPrice = 150.00m,
                    VatCategory = EInvoiceVatCategory.Standard,
                    VatRate = 21m
                },
                new EInvoiceLine
                {
                    Name = "Mere Golden",
                    Note = "Livrare parțială",
                    Quantity = 1.235m,
                    UnitCode = "KGM",
                    UnitPrice = 12.3456m,
                    VatCategory = EInvoiceVatCategory.Standard,
                    VatRate = 11m
                },
                new EInvoiceLine
                {
                    Name = "Curs de formare profesională",
                    Quantity = 1m,
                    UnitPrice = 500.00m,
                    VatCategory = EInvoiceVatCategory.Exempt
                }
            ]
        };
    }

    /// <summary>(f) Storno of TST-E8-0001: negative quantities, BillingReference, negative total, no due date.</summary>
    public static EInvoiceDocument Storno()
    {
        return new EInvoiceDocument
        {
            Number = "TST-E8-0007",
            IssueDate = IssueDate,
            Notes = ["Stornare factura TST-E8-0001 din 01.09.2026"],
            BillingReference = new EInvoiceBillingReference("TST-E8-0001", new DateOnly(2026, 9, 1)),
            Seller = Seller(isVatPayer: true),
            Buyer = BucharestCompanyBuyer(),
            Lines =
            [
                new EInvoiceLine
                {
                    Name = "Servicii de consultanță",
                    Quantity = -1m,
                    UnitPrice = 150.00m,
                    VatCategory = EInvoiceVatCategory.Standard,
                    VatRate = 21m
                },
                new EInvoiceLine
                {
                    Name = "Licență software",
                    Quantity = -2m,
                    UnitPrice = 49.99m,
                    VatCategory = EInvoiceVatCategory.Standard,
                    VatRate = 21m
                }
            ]
        };
    }

    private static EInvoiceSeller Seller(bool isVatPayer)
    {
        return new EInvoiceSeller
        {
            Name = "SC Test Generator SRL",
            Cui = SellerCui,
            IsVatPayer = isVatPayer,
            TradeRegisterNumber = "J12/1234/2020",
            Address = new EInvoiceAddress
            {
                Street = "Str. Exemplu 1",
                City = "Cluj-Napoca",
                County = "Cluj",
                PostalCode = "400001",
                CountryCode = "RO"
            }
        };
    }

    private static EInvoiceBuyer BucharestCompanyBuyer()
    {
        return new EInvoiceBuyer
        {
            Name = "Client Test SRL",
            VatId = BuyerVatId,
            LegalRegistrationId = BuyerTradeRegisterId,
            Address = new EInvoiceAddress
            {
                Street = "Bd. Exemplu 10",
                City = "Sector 3",
                County = "București",
                PostalCode = "030167",
                CountryCode = "RO"
            }
        };
    }
}
