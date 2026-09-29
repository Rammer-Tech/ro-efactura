# Generating CIUS-RO XML

RoEFactura 2.1.0 adds a generator that turns a plain C# document model into a UBL 2.1 Invoice that
conforms to CIUS-RO (specification identifier `CIUS-RO:1.0.1`, official validation artifacts 1.0.9).
The output is ready for ANAF `validare`/`upload`: UTF-8 **without** a byte order mark, first line
`<?xml version="1.0" encoding="utf-8"?>`.

All public types live in the `RoEFactura.Generation` namespace:

| Type | Purpose |
|---|---|
| `IEInvoiceXmlGenerator` / `EInvoiceXmlGenerator` | `byte[] Generate(EInvoiceDocument document)` — validate, compute totals, build XML |
| `EInvoiceDocument` | The invoice: number, dates, seller, buyer, lines, notes, payment, billing reference, VAT exemption |
| `EInvoiceSeller`, `EInvoiceBuyer`, `EInvoiceAddress`, `EInvoiceLine` | Parties, addresses and lines |
| `EInvoiceVatCategory` | `Standard` (S), `Exempt` (E), `NotSubject` (O) |
| `EInvoiceVatExemption`, `EInvoicePayment`, `EInvoiceBillingReference` | Exemption reason, IBAN payment, preceding invoice |
| `EInvoiceTotalsCalculator`, `EInvoiceTotals`, `EInvoiceVatBreakdown` | The totals and VAT breakdown the generator emits |
| `RomanianAddressConverter`, `RomanianAddress` | County → `RO-XX`, Bucharest city → `SECTORn` |

## Setup

```csharp
using RoEFactura;
using RoEFactura.Generation;

services.AddRoEFactura(); // registers IEInvoiceXmlGenerator (singleton, TryAdd — you can override it)

// or without DI — the generator is stateless:
IEInvoiceXmlGenerator generator = new EInvoiceXmlGenerator();
byte[] xml = generator.Generate(document);

// Optional: ask ANAF's public, stateless validator before uploading.
AnafValidationResult check = await anafClient.ValidateWithAnafAsync(xml, AnafDocumentStandard.Ubl);
```

The examples below share this seller (a VAT payer from Cluj):

```csharp
var seller = new EInvoiceSeller
{
    Name = "SC Test Generator SRL",
    Cui = "RO1234567897",          // "1234567897" or "RO 1234567897" work too; BT-30 = digits, BT-31 = "RO" + digits
    IsVatPayer = true,
    TradeRegisterNumber = "J12/1234/2020", // optional, emitted as CompanyLegalForm (BT-33)
    Address = new EInvoiceAddress
    {
        Street = "Str. Exemplu 1",
        City = "Cluj-Napoca",
        County = "Cluj",             // -> RO-CJ
        PostalCode = "400001",
        CountryCode = "RO"
    }
};
```

## The seven cases

### (g) B2B — Romanian VAT-payer buyer in Bucharest, paid by bank transfer

The buyer's VAT id (BT-48) and legal id (BT-47) are both emitted; `County = "București"` becomes `RO-B` and
`City = "Sector 3"` becomes `SECTOR3` (BR-RO-101/111). The payment emits code `30` + the IBAN (BR-61).

```csharp
var invoice = new EInvoiceDocument
{
    Number = "TST-E8-0001",
    IssueDate = new DateOnly(2026, 9, 15),
    DueDate = new DateOnly(2026, 9, 30),
    Seller = seller,
    Buyer = new EInvoiceBuyer
    {
        Name = "Client Test SRL",
        VatId = "RO876543213",
        LegalRegistrationId = "J12/345/2020",
        Address = new EInvoiceAddress
        {
            Street = "Bd. Exemplu 10",
            City = "Sector 3",          // -> SECTOR3
            County = "București",       // -> RO-B
            PostalCode = "030167",
            CountryCode = "RO"
        }
    },
    Payment = new EInvoicePayment("RO49 AAAA 1B31 0075 9384 0000"),
    Lines =
    [
        new EInvoiceLine { Name = "Servicii dezvoltare software", Quantity = 10m, UnitCode = "HUR",
            UnitPrice = 200.00m, VatCategory = EInvoiceVatCategory.Standard, VatRate = 21m },
        new EInvoiceLine { Name = "Licență software anuală", Quantity = 1m,
            UnitPrice = 499.99m, VatCategory = EInvoiceVatCategory.Standard, VatRate = 21m }
    ]
};
byte[] xml = generator.Generate(invoice);
```

### (a) B2B — Romanian company that is not a VAT payer

Only BT-47 (the CUI) is given, so no buyer `PartyTaxScheme` is emitted. `Ilfov` becomes `RO-IF`.

```csharp
var invoice = new EInvoiceDocument
{
    Number = "TST-E8-0002",
    IssueDate = new DateOnly(2026, 9, 15),
    DueDate = new DateOnly(2026, 9, 30),
    Seller = seller,
    Buyer = new EInvoiceBuyer
    {
        Name = "Client Neplătitor SRL",
        LegalRegistrationId = "876543213",   // BT-47 satisfies BR-RO-120
        Address = new EInvoiceAddress
        {
            Street = "Str. Exemplu 5", City = "Voluntari", County = "Ilfov", CountryCode = "RO"
        }
    },
    Lines =
    [
        new EInvoiceLine { Name = "Servicii de consultanță", Quantity = 1m, UnitPrice = 1000.00m,
            VatCategory = EInvoiceVatCategory.Standard, VatRate = 21m }
    ]
};
```

### (b) Natural person without CNP

`IsNaturalPerson = true` with no `LegalRegistrationId` emits BT-47 =
`EInvoiceBuyer.NaturalPersonWithoutCnpId` (`0000000000000`, thirteen zeros), the identifier ANAF accepts
for a person without a CNP. Pass the CNP in `LegalRegistrationId` when you have it. Payment terms (BT-20)
may replace or accompany the due date (BR-CO-25).

```csharp
var invoice = new EInvoiceDocument
{
    Number = "TST-E8-0003",
    IssueDate = new DateOnly(2026, 9, 15),
    DueDate = new DateOnly(2026, 9, 30),
    PaymentTerms = "Plata la livrare",
    Seller = seller,
    Buyer = new EInvoiceBuyer
    {
        Name = "Ion Popescu",
        IsNaturalPerson = true,              // BT-47 = EInvoiceBuyer.NaturalPersonWithoutCnpId
        Address = new EInvoiceAddress
        {
            Street = "Str. Florilor 5", City = "Iași", County = "Iași", CountryCode = "RO"
        }
    },
    Lines =
    [
        new EInvoiceLine { Name = "Reparație laptop", Quantity = 1m, UnitPrice = 250.00m,
            VatCategory = EInvoiceVatCategory.Standard, VatRate = 21m }
    ]
};
```

### (c) Foreign buyer

A non-RO address emits the country code only — no county (BR-RO-110/111 apply to `RO` addresses only),
while street and city stay mandatory (BR-RO-082/092). The VAT id keeps its country prefix (BR-CO-09).
A buyer with no Romanian CUI/NIF also needs `LegalRegistrationId = "0000000000000"` (BT-47), otherwise
ANAF's validator answers `ERRIdentif` — see "ERRIdentif — foreign buyer identification" below.

```csharp
var invoice = new EInvoiceDocument
{
    Number = "TST-E8-0004",
    IssueDate = new DateOnly(2026, 9, 15),
    DueDate = new DateOnly(2026, 9, 30),
    Seller = seller,
    Buyer = new EInvoiceBuyer
    {
        Name = "Beispiel GmbH",
        VatId = "DE123456789",
        LegalRegistrationId = "0000000000000",   // BT-47: no Romanian CUI/NIF (ANAF ERRIdentif otherwise)
        Address = new EInvoiceAddress
        {
            Street = "Musterstraße 1", City = "Berlin", PostalCode = "10115", CountryCode = "DE"
        }
    },
    Lines =
    [
        new EInvoiceLine { Name = "Servicii de traducere", Quantity = 1m, UnitPrice = 1200.00m,
            VatCategory = EInvoiceVatCategory.Standard, VatRate = 21m }
    ]
};
```

> Standard-rated 21% to a DE VAT-registered buyer is correct only when the supply is taxed in Romania.
> Intra-EU B2B (reverse charge AE, intra-community K) and export (G) are out of scope in v1.

### (d) Seller not registered for VAT

`IsVatPayer = false` requires every line to be `EInvoiceVatCategory.NotSubject` (O). The generator emits a
single O breakdown with `VATEX-EU-O` + "Neplătitor de TVA" (BR-O-10/11/12), no rate (BR-O-05), and **no**
seller or buyer VAT identifier (BR-O-02). The seller is still identified by BT-30 (the CUI, BR-CO-26).

```csharp
var nonPayer = seller with { IsVatPayer = false };

var invoice = new EInvoiceDocument
{
    Number = "TST-E8-0005",
    IssueDate = new DateOnly(2026, 9, 15),
    DueDate = new DateOnly(2026, 9, 30),
    Seller = nonPayer,
    Buyer = new EInvoiceBuyer
    {
        Name = "Client Test Timiș SRL",
        LegalRegistrationId = "876543213",   // BT-47 is still emitted on O invoices
        Address = new EInvoiceAddress
        {
            Street = "Bd. Exemplu 7", City = "Timișoara", County = "Timiș", CountryCode = "RO"
        }
    },
    Lines =
    [
        new EInvoiceLine { Name = "Servicii de design grafic", Quantity = 1m, UnitPrice = 750.00m,
            VatCategory = EInvoiceVatCategory.NotSubject }
    ]
};
```

### (e) Mixed rates: 21%, 11% and exempt

One VAT breakdown per (category, rate): S 21, S 11, then E 0. VAT is rounded per group after summing the
group (BR-S-08/09, BR-CO-17). An `EInvoiceVatCategory.Exempt` line requires the document's single
`VatExemption` reason (BR-E-10). Prices keep up to 4 decimals and quantities up to 3 decimals, exactly.

```csharp
var invoice = new EInvoiceDocument
{
    Number = "TST-E8-0006",
    IssueDate = new DateOnly(2026, 9, 15),
    DueDate = new DateOnly(2026, 9, 30),
    Seller = seller,
    Buyer = buyer,                           // e.g. the Bucharest VAT payer from case (g)
    VatExemption = new EInvoiceVatExemption(
        ReasonCode: null,                    // or a VATEX code, e.g. "VATEX-EU-132-1I" (BR-CL-22)
        Reason: "Scutit de TVA fără drept de deducere conform art. 292 din Legea nr. 227/2015 privind Codul fiscal"),
    Lines =
    [
        new EInvoiceLine { Name = "Servicii de consultanță", Quantity = 2m, UnitPrice = 150.00m,
            VatCategory = EInvoiceVatCategory.Standard, VatRate = 21m },
        new EInvoiceLine { Name = "Mere Golden", Quantity = 1.235m, UnitCode = "KGM", UnitPrice = 12.3456m,
            VatCategory = EInvoiceVatCategory.Standard, VatRate = 11m },   // net = round(15.246816) = 15.25
        new EInvoiceLine { Name = "Curs de formare profesională", Quantity = 1m, UnitPrice = 500.00m,
            VatCategory = EInvoiceVatCategory.Exempt }
    ]
};

EInvoiceTotals totals = EInvoiceTotalsCalculator.Calculate(invoice); // the same numbers the XML carries
// BT-106 815.25, BT-110 64.68 (63.00 + 1.68 + 0.00), BT-112 = BT-115 879.93
```

### (f) Storno (reversal) of an issued invoice

A storno is an Invoice with TypeCode 380 (credit note 381 is not used), **negative quantities** with the
original, non-negative prices (BR-27), and a `BillingReference` to the original number and issue date
(BG-3, BR-55). Totals are negative, so no due date is required (BR-CO-25 only applies to a positive
amount due) and no payment means is needed.

```csharp
var storno = new EInvoiceDocument
{
    Number = "TST-E8-0007",
    IssueDate = new DateOnly(2026, 9, 15),
    Notes = ["Stornare factura TST-E8-0001 din 01.09.2026"],
    BillingReference = new EInvoiceBillingReference("TST-E8-0001", new DateOnly(2026, 9, 1)),
    Seller = seller,
    Buyer = buyer,
    Lines =
    [
        new EInvoiceLine { Name = "Servicii de consultanță", Quantity = -1m, UnitPrice = 150.00m,
            VatCategory = EInvoiceVatCategory.Standard, VatRate = 21m },
        new EInvoiceLine { Name = "Licență software", Quantity = -2m, UnitPrice = 49.99m,
            VatCategory = EInvoiceVatCategory.Standard, VatRate = 21m }
    ]
};
// PayableAmount = -302.48 RON
```

## VAT category rules

| Seller | Allowed line categories | Rate (BT-152) | Emitted VAT ids | Breakdown reason |
|---|---|---|---|---|
| `IsVatPayer = true` | `Standard` (S), `Exempt` (E) | S: > 0, at most 2 decimals (BR-S-05); E: 0 (BR-E-05) | Seller BT-31 = `RO` + CUI; buyer BT-48 when `VatId` is set | S: none (BR-S-10); E: the document `VatExemption` (BR-E-10) |
| `IsVatPayer = false` | `NotSubject` (O) only (BR-O-11/12) | not emitted (BR-O-05) | none — BR-O-02 forbids seller and buyer VAT ids | `VATEX-EU-O` + "Neplătitor de TVA" (BR-O-10) |

Notes:

- **BR-O-02 — buyer VAT id on O invoices.** On an invoice with NotSubject lines the buyer `VatId` is
  omitted, because BR-O-02 forbids BT-48 there. BT-47 (`LegalRegistrationId`) is still emitted, so pass
  the buyer's CUI as `LegalRegistrationId` when you have it. (The local `RoCiusUblValidator` checks
  BR-RO-120 for every Romanian buyer regardless of the line categories, so an O invoice to a Romanian
  company with neither id passes ANAF's rule but is flagged locally.)
- **BR-E-01 — one exemption reason per document.** An invoice has exactly one Exempt breakdown (BR-E-01,
  and UBL-SR-32 allows one reason text), so `EInvoiceDocument.VatExemption` is a single value. A caller
  whose exempt lines carry different reasons (e.g. micro-taxe E4 lines) must pick one reason per document
  or refuse to issue; the model cannot express two.
- **BR-RO-120 — buyer identifier.** Whenever a line is S or E, every buyer that is not a natural person —
  Romanian or foreign — needs `VatId` (BT-48) or `LegalRegistrationId` (BT-47); the generator throws
  otherwise (RO16931-rules.sch:409-415). A natural person always gets BT-47. The local validator's
  BR-RO-120 check only looks at Romanian buyers, so it does not catch a foreign company without ids —
  a gap, which the generator's own guard closes; it is left unchanged. (Its only stricter case is the
  O-invoice one described under BR-O-02 above.)
- **ERRIdentif — foreign buyer identification.** This is not a CIUS-RO schematron rule. ANAF's validator
  (`validare/FACT1`) also identifies the buyer's Romanian CUI and fails with
  `codEroare=ERRIdentif; textEroare=nu a fost identificat cui cumparator` when it cannot find one. It
  identifies the buyer only from an `RO`-prefixed BT-48 or from an all-digit BT-47: a CUI with a valid
  check digit, or `0000000000000` (a CNP was not probed). A non-RO VAT id is never used, and neither is
  a non-numeric registry id. An all-digit BT-47 with a wrong CUI check digit fails with
  `ERRIdentif; textEroare=CUI cumparator incorect`. The public validator, probed on 29.09.2026 with
  synthetic data and a DE/CH buyer, S 21 %:

  | BT-48 (`VatId`) | BT-47 (`LegalRegistrationId`) | Country | ANAF result |
  |---|---|---|---|
  | `DE123456789` | — | DE | nok: ERRIdentif, nu a fost identificat cui cumparator |
  | `DE123456789` | `HRB 123456` | DE | nok: ERRIdentif, nu a fost identificat cui cumparator |
  | — | `HRB 123456` | DE | nok: ERRIdentif, nu a fost identificat cui cumparator |
  | `DE123456788` (valid DE check digit) | — | DE | nok: ERRIdentif, nu a fost identificat cui cumparator |
  | — | `CHE-123.456.789` | CH | nok: ERRIdentif, nu a fost identificat cui cumparator |
  | `DE123456789` | `123456788` (wrong CUI check digit) | DE | nok: ERRIdentif, CUI cumparator incorect |
  | `DE123456789` | `123456789` (valid CUI check digit) | DE | ok |
  | `DE123456789` | `0000000000000` | DE | ok |
  | — | `0000000000000` | CH | ok |

  So a foreign company buyer with no Romanian CUI/NIF passes the validator with
  `LegalRegistrationId = "0000000000000"`, keeping its own VAT id in `VatId` (EU) or no `VatId` (non-EU).
  Case (c) uses this shape. Do not put a foreign registry number or the digits of a foreign VAT id in
  BT-47. A digit string that happens to be a valid CUI is treated as a Romanian company, and on SPV upload
  without `extern=DA` it could route the invoice to that company. The generator has no guard for this,
  because it is a validator identification step and not a document rule. ANAF's upload contract
  accepts a buyer without a CUI/NIF through `extern=DA` (`AnafUploadOptions.ExternalBuyer`: "se completeaza
  doar in cazul in care cumparatorul este din exteriorul Romaniei (nu are CUI sau NIF)"). This epic did
  not check the upload behaviour; micro-taxe E10 checks it on the ANAF TEST environment. The public
  validator has no `extern` parameter, so it cannot pre-check a foreign buyer whose BT-47 is not numeric.
- **BT-120 — exemption reason text length.** The text is passed through up to 200 characters (micro-taxe
  E4 parity) and longer text is rejected. The 1.0.9 artifact's 100-character assert (`BR-RO-L1019`,
  RO16931-rules.sch:688-693) never fires: its `TaxSubtotal` rule is shadowed by the earlier
  `cac:TaxTotal/cac:TaxSubtotal` rule at sch:600 (the other copy is commented out). This is a known risk:
  if ANAF ever activates it, reasons over 100 characters will be rejected.
- **Non-payer seller identification (BT-32).** An O seller carries no BT-31/BT-32; it is identified by
  BT-30 only (BR-CO-26, and BR-RO-065 does not apply without S/E lines). Whether SPV upload matches the
  `cif` parameter against BT-30 is verified in micro-taxe E10.

## Address conversion

`RomanianAddressConverter` ignores case, diacritics (ș/ş, ț/ţ, ă, â, î), hyphens versus spaces and extra
whitespace, and strips the prefixes `județul`, `judet`, `jud.`, `jud`, `municipiul`, `mun.`.

| Input (county) | Output (BT-39/BT-54) |
|---|---|
| `Cluj`, `CJ`, `cj`, `RO-CJ`, `jud. Cluj` | `RO-CJ` |
| `Brașov`, `Braşov`, `BRASOV`, `județul Brașov` | `RO-BV` |
| `Caraș-Severin`, `Caras Severin`, `CARAS-SEVERIN` | `RO-CS` |
| `Bistrița-Năsăud`, `bistrita nasaud` | `RO-BN` |
| `Satu Mare`, `Satu-Mare` | `RO-SM` |
| `Ilfov` | `RO-IF` |
| `București`, `Municipiul Bucuresti`, `B`, `RO-B` | `RO-B` |
| `Atlantida` | `ArgumentException` `[BR-RO-110] Unknown Romanian county 'Atlantida'.` |

| Input (Bucharest city) | Output (BT-37/BT-52) |
|---|---|
| `Sector 3`, `sectorul 3`, `SECTOR3`, `S3`, `3`, `București, Sector 3` | `SECTOR3` |
| `Sector 7`, `București` | `ArgumentException` citing `BR-RO-100` |

```csharp
RomanianAddress ro = RomanianAddressConverter.Convert("București", "Sector 3"); // ("RO-B", "SECTOR3")
string code = RomanianAddressConverter.ToCountyCode("Bistrița-Năsăud");         // "RO-BN"
bool known = RomanianAddressConverter.TryToCountyCode(userInput, out string county);
```

The generator applies the conversion itself: put the county name (or code) in `EInvoiceAddress.County` and
the sector in `EInvoiceAddress.City`. For a foreign address `County` is ignored.

## Errors

`Generate` validates the whole document before building any XML. A null document throws
`ArgumentNullException`; every other problem throws `ArgumentException` whose message starts with the
official rule id in brackets (the schematron assert id — for length rules ANAF prints a shorter tag, e.g.
assert `BR-RO-L0502` is shown as `[BR-RO-L050]`), and whose `ParamName` is the member path
(e.g. `Lines[1].UnitPrice`).

| Rule id | When |
|---|---|
| `BR-02`, `BR-RO-010`, `BR-RO-L155` | Invoice number blank, without a digit, or over 200 characters |
| `decision-4` | `CurrencyCode` is not `RON` |
| `BR-RO-A020`, `BT-22`, `BR-RO-L302` | More than 20 notes, a blank note, a note over 300 characters |
| `BR-RO-L301` | Payment terms over 300 characters |
| `BR-55`, `BR-RO-L156` | Billing reference number blank or over 200 characters |
| `BR-06`, `BR-RO-L201`, `BR-CO-26`, `BR-RO-L1000`, `BR-08` | Seller missing / name blank or over 200 / CUI not 2-10 digits / BT-33 over 1000 / address missing |
| `scope-v1` | Seller address not in Romania |
| `BR-07`, `BR-RO-L203`, `BR-10` | Buyer missing / name blank or over 200 / address missing |
| `BR-09`, `BR-11`, `BR-CL-14` | Country code blank or not ISO 3166-1 alpha-2 |
| `BR-RO-081`/`082`, `BR-RO-L151`/`L152` | Street blank or over 150 characters (seller/buyer) |
| `BR-RO-091`/`092`, `BR-RO-L0501`/`L0502` | City blank or over 50 characters (seller/buyer) |
| `BR-RO-L0201`/`L0202` | Post code over 20 characters |
| `BR-RO-110`/`111`, `BR-RO-100`/`101` | Romanian county missing/unknown, Bucharest city without sector 1-6 |
| `BR-CO-09` | Buyer VAT id prefix not in the official list (e.g. `RO`, `DE`, `EL`, `XI`) |
| `BR-RO-120` | Non-natural-person buyer without BT-47/BT-48 on an S/E invoice |
| `BR-16` | No lines, or a null line |
| `BR-25`, `BR-RO-L1024`, `BR-RO-L212`, `BR-RO-L303` | Item name blank or over 100; description over 200; line note over 300 |
| `BR-22`, `BT-129` | Quantity zero, or more than 3 decimals |
| `BR-27`, `BT-146` | Unit price negative, or more than 4 decimals |
| `BR-23`, `BR-CL-23` | Unit code blank or not UN/ECE Rec 20/21 |
| `BR-CL-18`, `BT-152` | Undefined VAT category; rate with more than 2 decimals |
| `BR-S-05`, `BR-E-05`, `BR-O-05` | S rate not > 0; E or O rate not 0 |
| `BR-S-02`, `BR-E-02` | S or E line from a seller with `IsVatPayer = false` |
| `BR-O-02` | O line from a seller with `IsVatPayer = true` |
| `BR-E-10`, `BR-CL-22`, `BT-120` | E line without `VatExemption`; reason code not `VATEX-…`; reason text over 200 |
| `BR-61`, `BT-84` | Payment without IBAN, or IBAN failing the ISO 13616 mod-97 check |
| `BR-CO-25` | Positive amount due without `DueDate` or `PaymentTerms` |

The BR-CO-09, BR-CL-14 and BR-CL-23 lists are copied verbatim from the 1.0.9 artifacts
(`EN16931-UBL-model.sch:74`, `EN16931-UBL-codes.sch:81`, `EN16931-UBL-codes.sch:156`).

## v1 limits

- **RON only**: document currency = VAT currency = RON; any other `CurrencyCode` is rejected.
- **VAT categories S, E, O only**: no reverse charge (AE), intra-community supply (K), export (G), zero
  rate (Z), or L/M.
- **Invoice 380 only**: storno = 380 with negative quantities + `BillingReference`; no credit note 381.
- **No document-level allowances or charges, no prepaid amount, no rounding amount.**
- **Precision**: unit price at most 4 decimals, quantity at most 3 decimals, VAT rate at most 2 decimals —
  larger precision is rejected, never silently rounded. Amounts are rounded to 2 decimals away from zero.
- **Seller in Romania**; foreign buyers are supported (no county emitted). A foreign buyer with no
  Romanian CUI/NIF passes ANAF's validator only with BT-47 `0000000000000` (see the ERRIdentif note).
- The live proof: `RoEFactura.Tests` runs all seven cases against ANAF's public validator when
  `ANAF_LIVE_VALIDATION=1` (opt-in, never in CI).
