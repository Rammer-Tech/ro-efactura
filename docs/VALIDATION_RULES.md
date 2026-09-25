# RoEFactura Validation Rules

This document summarizes the local UBL validation rules enforced by `RoCiusUblValidator` and its component
validators. It targets **CIUS-RO 1.0.1**, validated against the schematron shipped in the official
**ro16931-ubl-1.0.9** artefact (see Sources below).

Local validation is a fast pre-check, not a replacement for ANAF. It is used by:
- `IUblProcessingService.ValidateInvoiceAsync` / `ProcessInvoiceAsync` (unless `skipValidation: true`)
- `IAnafEInvoiceClient.ProcessDownloadedInvoiceAsync` / `ProcessMultipleInvoicesAsync` (always `skipValidation: true` --
  documents already accepted by ANAF SPV are not re-validated locally)

**`IAnafEInvoiceClient.ValidateWithAnafAsync` (the ANAF public `validare` service) remains the authoritative
validator.** A document that fails a local rule may still need to go to ANAF to get the definitive answer; a
document that passes every local rule is not guaranteed to pass ANAF's full schematron, because this library
deliberately implements a curated subset (see "Out of scope" below).

## Sources

- Official artefact: `https://mfinante.gov.ro/static/10/eFactura/ro16931-ubl-1.0.9.zip` (RO_CIUS schematron
  version 1.0.9; RO_CIUS business-rule version 1.0.1, last update 2022-10-18). The schematron's own header
  comment (`cius-ro/RO16931-rules.sch`) gives version 1.0.9's own last-update date as `2024-05-78`, which is
  not a valid calendar date -- that is a typo in ANAF's source artefact itself, not a transcription error
  here; it is not repeated as if it were a verified date.
- Files consulted: `cius-ro/RO16931-rules.sch` (Romanian BR-RO-* rules), `UBL/EN16931-UBL-model.sch` (EN 16931
  core BR-*/BR-CO-*/BR-CO-1x/BR-{S,Z,E,AE,IC,G,O}-0x formulas bound to UBL XPaths).
- Step 0 verification (2026-09-25, orchestrator decision): the zip was not downloaded fresh; the WP-C worktree
  used the pre-fetched local copy at `.cave/official/ro16931-ubl-1.0.9/` checked into the epic's `.cave`
  directory (not committed to the library repo). Every BR-RO-* id and length limit in this document was
  cross-checked against that copy on 2026-09-25.
- Outcome of the cross-check:
  - **BR-RO-A999** (max 999 invoice lines) does **not** exist in the official schematron -- it was removed in
    schematron version 1.0.8 (see the schematron's own changelog comment: "eliminate rules BR-RO-L030 and
    BR-RO-A999"). It is not implemented locally (previously a fabricated rule in this library).
  - All other BR-RO-* ids and length limits used by this library (BR-RO-001, 010, 020, 030, 040, 100, 101, 110,
    111, 120, A020, and the 100/200/300-character length limits on BT-1/BT-22/BT-127/BT-153/BT-154) matched the
    schematron exactly, including the previously-uncertain BT-1 (Invoice number) limit of 200 characters
    (`docs/research.md`, which reflects the 2021 spec's now-superseded limit of 30, is historical only).
  - BR-CO-17's local tolerance was widened relative to the E1 input plan's original "±0.01" design -- see
    "Tolerances" below.

## How validation is composed

`RoCiusUblValidator` wires together:
- `SellerPartyValidator` / `BuyerPartyValidator` -- each also runs a `RomanianAddressValidator` (Seller/Buyer
  role) against the party's postal address when the party is Romanian.
- `PayeePartyValidator`
- `InvoiceLineValidator` (per invoice line)
- `TotalsValidator` (document totals, including a `RuleForEach` over the document-currency `TaxTotal`'s
  subtotals for BR-CO-17)
- `VatBreakdownValidator` (per document-currency `TaxTotal` subtotal, for the VAT-exemption-reason rules)

"Document-currency TaxTotal" means `TotalsValidator.GetDocumentCurrencyTaxTotal`: the first `TaxTotal` whose
`TaxAmount/@currencyID` equals BT-5 (`DocumentCurrencyCode`). An invoice may carry a second `TaxTotal` in the
VAT accounting currency (BT-6/BT-111, present when BT-5 is not RON); BR-CO-14..17 and the VAT-exemption-reason
rules do not evaluate that second one.

## Core EN 16931 rules (BR-*, BR-CO-*)

| ID | BT/BG | Constraint | Code location |
| --- | --- | --- | --- |
| BR-1 | BT-1 | Invoice number is required | `RoCiusUblValidator` |
| BR-2 | BT-2 | Invoice issue date is required | `RoCiusUblValidator` |
| BR-3 | BT-3 | Invoice type code is required | `RoCiusUblValidator` |
| BR-5 | BT-5 | Invoice currency code is required | `RoCiusUblValidator` |
| BR-6 | BT-27/BT-28 | Seller name is required | `SellerPartyValidator` |
| BR-7 | BT-44/BT-45 | Buyer name is required | `BuyerPartyValidator` |
| BR-8 | BG-5 | Seller postal address is required | `SellerPartyValidator` |
| BR-10 | BG-8 | Buyer postal address is required | `BuyerPartyValidator` |
| BR-12 | BT-106 | Sum of Invoice line net amount is required | `TotalsValidator` |
| BR-13 | BT-109 | Invoice total amount without VAT is required | `TotalsValidator` |
| BR-14 | BT-112 | Invoice total amount with VAT is required | `TotalsValidator` |
| BR-15 | BT-115 | Amount due for payment is required | `TotalsValidator` |
| BR-16 | BG-25 | Invoice must have at least one line | `RoCiusUblValidator` |
| BR-17 | BT-59 | Payee name required when payee is specified | `PayeePartyValidator` |
| BR-21 | BT-126 | Invoice line identifier is required | `InvoiceLineValidator` |
| BR-22 | BT-129 | Invoice line quantity is required | `InvoiceLineValidator` |
| BR-23 | BT-130 | Invoice line unit of measure is required | `InvoiceLineValidator` |
| BR-24 | BT-131 | Invoice line net amount is required | `InvoiceLineValidator` |
| BR-25 | BT-146 | Invoice line net unit price is required | `InvoiceLineValidator` |
| BR-26 | BT-153 | Invoice line item name is required | `InvoiceLineValidator` |
| BR-27 | BT-146 | Item net price must not be negative | `InvoiceLineValidator` |
| BR-28 | BT-146 | Item gross price must not be negative (same field in UblSharp) | `InvoiceLineValidator` |
| BR-29 | BG-14 | Invoice period end date >= start date | `TotalsValidator` |
| BR-30 | BG-26 | Invoice line period end date >= start date | `InvoiceLineValidator` |
| BR-CO-4 | BT-151 | VAT category code is required on each line | `InvoiceLineValidator` |
| BR-CO-10 | BT-106 | = Σ line net amounts (BT-131) | `TotalsValidator` |
| BR-CO-11 | BT-107 | = Σ document-level allowance amounts (BT-92), or both absent | `TotalsValidator` |
| BR-CO-12 | BT-108 | = Σ document-level charge amounts (BT-99), or both absent | `TotalsValidator` |
| BR-CO-13 | BT-109 | = BT-106 − BT-107 + BT-108 (absent BT-107/BT-108 count as 0) | `TotalsValidator` |
| BR-CO-14 | BT-110 | = Σ VAT category tax amount (BT-117), skipped when no doc TaxTotal | `TotalsValidator` |
| BR-CO-15 | BT-112 | = BT-109 + BT-110 (absent BT-110 counts as 0) | `TotalsValidator` |
| BR-CO-16 | BT-115 | = BT-112 − BT-113 (Prepaid) + BT-114 (Rounding) | `TotalsValidator` |
| BR-CO-17 | BT-117 | = BT-116 × (BT-119 / 100), rounded -- **widened tolerance, see below** | `TotalsValidator` |
| BR-S-05 | BT-152 | Standard rated ⇒ rate > 0 | `InvoiceLineValidator` |
| BR-Z-05 | BT-152 | Zero rated ⇒ rate = 0 (lenient on absence, see below) | `InvoiceLineValidator` |
| BR-E-05 | BT-152 | Exempt from VAT ⇒ rate = 0 (lenient on absence) | `InvoiceLineValidator` |
| BR-AE-05 | BT-152 | Reverse charge ⇒ rate = 0 (lenient on absence) | `InvoiceLineValidator` |
| BR-IC-05 | BT-152 | Intra-community supply (category K) ⇒ rate = 0 (lenient on absence) | `InvoiceLineValidator` |
| BR-G-05 | BT-152 | Export outside the EU ⇒ rate = 0 (lenient on absence) | `InvoiceLineValidator` |
| BR-O-05 | BT-152 | Not subject to VAT ⇒ no rate (lenient on absence) | `InvoiceLineValidator` |
| BR-E-10 | BT-120/BT-121 | Exempt from VAT ⇒ exemption reason code and/or text required | `VatBreakdownValidator` |
| BR-AE-10 | BT-120/BT-121 | Reverse charge ⇒ exemption reason code and/or text required | `VatBreakdownValidator` |
| BR-IC-10 | BT-120/BT-121 | Intra-community supply ⇒ exemption reason code and/or text required | `VatBreakdownValidator` |
| BR-G-10 | BT-120/BT-121 | Export outside the EU ⇒ exemption reason code and/or text required | `VatBreakdownValidator` |
| BR-O-10 | BT-120/BT-121 | Not subject to VAT ⇒ exemption reason code and/or text required | `VatBreakdownValidator` |

Additional library-local (not CIUS-RO/EN16931-numbered) rules: `BR-6`/`BR-7`/`BR-8`/`BR-10` are paired with
`BR-8-ADDRESS`/`BR-10-ADDRESS` (Romanian-party postal-address presence) and `BR-RO-SELLER-ID` (Romanian seller
legal identifier presence); see the Romanian rules table.

## Romanian CIUS-RO rules (BR-RO-*)

| ID | BT/BG | Constraint | Code location |
| --- | --- | --- | --- |
| BR-RO-001 | BT-24 | CustomizationID must equal `RomanianConstants.CustomizationId` exactly | `RoCiusUblValidator` |
| BR-RO-010 | BT-1 | Invoice number must contain at least one digit | `RoCiusUblValidator` |
| BR-RO-020 | BT-3 | Invoice type code ∈ {380, 389, 384, 381, 751} | `RoCiusUblValidator` |
| BR-RO-030 | BT-5/BT-6 | Document currency ≠ RON ⇒ VAT accounting currency = RON | `RoCiusUblValidator` |
| BR-RO-040 | BT-8 | Every non-blank `InvoicePeriod[*]/DescriptionCode[*]` ∈ {3, 35, 432}; none present ⇒ skipped | `RoCiusUblValidator` |
| BR-RO-100 | BT-37/BT-39/BT-40 | Seller country RO and county RO-B ⇒ city ∈ SECTOR1..SECTOR6 | `RomanianAddressValidator` (Seller role) |
| BR-RO-101 | BT-52/BT-54/BT-55 | Buyer country RO and county RO-B ⇒ city ∈ SECTOR1..SECTOR6 | `RomanianAddressValidator` (Buyer role) |
| BR-RO-110 | BT-39/BT-40 | Seller country RO ⇒ county ∈ ISO 3166-2:RO (42 codes, `RO-` prefix) | `RomanianAddressValidator` (Seller role) |
| BR-RO-111 | BT-54/BT-55 | Buyer country RO ⇒ county ∈ ISO 3166-2:RO (42 codes, `RO-` prefix) | `RomanianAddressValidator` (Buyer role) |
| BR-RO-120 | BT-47/BT-48 | Romanian buyer must have legal registration ID and/or VAT ID | `BuyerPartyValidator` |
| BR-RO-A020 | BG-1 | At most 20 Invoice note occurrences | `RoCiusUblValidator` |
| BR-RO-L100 | BT-153 (100), BT-19/BT-36/... | Fields limited to 100 characters | `InvoiceLineValidator` (BT-153) |
| BR-RO-L200 | BT-1 (200), BT-154 (200), ... | Fields limited to 200 characters | `RoCiusUblValidator` (BT-1), `InvoiceLineValidator` (BT-154) |
| BR-RO-L300 | BT-22 (300), BT-127 (300) | Fields limited to 300 characters | `RoCiusUblValidator` (BT-22), `InvoiceLineValidator` (BT-127) |
| BR-RO-Z2 | monetary amounts | Maximum 2 decimal places | `RoCiusUblValidator` |

**Length rule codes vs. the schematron's own assert ids.** `RO16931-rules.sch` defines a separate,
field-specific schematron `id` for every length rule (for example the invoice number length is asserted with
`id="BR-RO-L155"`, the item name with `id="BR-RO-L1024"`, the invoice line note with `id="BR-RO-L303"`) --
dozens of them across the fields this library does *not* re-implement (see "Out of scope"). However, **every
one of those asserts' human-readable message text uses the same bracketed family label** (`[BR-RO-L100]`,
`[BR-RO-L200]`, `[BR-RO-L300]`), which is what ANAF's `validare` service actually echoes back in its response
text. This library reports the family label (`BR-RO-L100`/`L200`/`L300`), not the internal per-field
schematron `id`, so a local failure's error code lines up with what a caller would see from ANAF.

**BR-RO-130 is not validated locally.** It requires knowing whether the invoice was uploaded with the
`executare=DA` (forced execution) flag, which is an ANAF *upload parameter*, not something present in the UBL
document itself. ANAF enforces it at upload time based on that flag; this library cannot decide it from the
XML alone, so `PayeePartyValidator` does not implement it (see the XML doc comment on that class).

**Library-local codes (not CIUS-RO/EN16931 numbered):**

| Code | Constraint | Code location |
| --- | --- | --- |
| `BR-RO-CITY-REQUIRED` | City name required for a Romanian address | `RomanianAddressValidator` |
| `BR-RO-COUNTRY-CODE` | Country code must be `RO` for a Romanian address | `RomanianAddressValidator` |
| `BR-RO-SELLER-ID` | Romanian seller should have a legal registration identifier | `SellerPartyValidator` |
| `BR-8-ADDRESS` / `BR-10-ADDRESS` | Romanian party must have a (materially non-empty) postal address | `SellerPartyValidator` / `BuyerPartyValidator` |
| `BR-29` | Document-level period end >= start (library convenience; not a numbered EN16931 code in this context) | `TotalsValidator` |

## Tolerances

- **BR-CO-10..16**: compared with a **≤ 0.01** absolute tolerance. The official schematron computes these with
  exact equality after rounding to 2 decimals (`round(x * 100) div 100`); a 0.01 tolerance absorbs `decimal`
  rounding artefacts without ever being looser than what ANAF accepts.
- **BR-CO-17 is deliberately wider than 0.01** (orchestrator decision, E1, 2026-09-25), translated from the
  official schematron's own tolerance window in `EN16931-UBL-model.sch`'s `BR-CO-17` parameter, not from the
  ≤0.01 policy used elsewhere:
  - If the VAT rate (BT-119) rounds to zero (to the nearest whole percent) or is absent, the tax amount
    (BT-117) must itself round to zero (to the nearest whole currency unit).
  - Otherwise, `abs(BT-117)` must fall strictly within **one whole currency unit** of
    `abs(BT-116) × BT-119 / 100` rounded to 2 decimals -- not ±0.01.
  - Both sides use `abs()`, so negative (storno/credit) amounts are handled without special-casing.
  - This window is intentionally wide: the goal is that the local pre-check never rejects a document ANAF's
    real schematron would accept. It will not catch small VAT miscalculations the way a ±0.01 check would;
    `ValidateWithAnafAsync` remains the authoritative check for that.
- Length rules use `string-length(normalize-space(.))` semantics (trim + collapse internal whitespace runs
  before measuring), matching the schematron exactly.

## UblSharp-specific leniency

- **Absent `Percent` cannot be reliably distinguished from a genuine `0`.** UblSharp initializes
  `TaxCategoryType.Percent` (and most other aggregate/value-type properties) as a non-null placeholder when
  deserializing real ANAF/SPV XML, even when the source element is absent -- there is no analogue of the
  `currencyID`-presence trick used for `AmountType`. Because of this, `BR-{Z,E,AE,IC,G,O}-05` are lenient: they
  only fail when a rate is present **and non-zero**; they never fail purely because the rate is missing.
- **Amount presence = non-empty `currencyID`.** `AmountType.Value` is a non-nullable `decimal` that defaults to
  `0`; whether an amount was actually present in the source XML is inferred from a non-empty `currencyID`
  instead. All of `TotalsValidator`'s presence/tolerance checks use this convention.
- A missing date deserializes as `default(DateTimeOffset)`, not `null` (see `RoCiusUblValidator.HasValidIssueDate`).

## Out of scope (E1 WP-C)

The following are explicitly not implemented by this library (see the E1 plan, §6):
- The dozens of field-specific BR-RO-L* length rules beyond BT-1/BT-22/BT-127/BT-153/BT-154 (for example the
  seller/buyer/tax-representative/delivery address-line and contact-field length limits).
- BR-RO-080..092 (address-line presence), the tax-representative and deliver-to county rules
  (BR-RO-140..170/180..212).
- BR-CO-18/19 and the full UNTDID 4461/5305 and UNECE Rec 20 code-list checks.
- The `BR-DEC-RO-*` two-decimal-place rules per individual field (this library instead enforces `BR-RO-Z2` on
  the headline monetary totals only).

For all of the above, and as the final authority in general, use `IAnafEInvoiceClient.ValidateWithAnafAsync`.
