# RoEFactura Validation Rules

This document summarizes the validation rules enforced by the library during local UBL validation. Validation is performed by the FluentValidation-based validators wired into `IUblProcessingService` and `IAnafEInvoiceClient.ValidateInvoiceXmlAsync`.

## How Validation Works

- Local validation uses `RoCiusUblValidator` and its related validators:
  - `SellerPartyValidator`
  - `BuyerPartyValidator`
  - `PayeePartyValidator`
  - `InvoiceLineValidator`
  - `TotalsValidator`
- Failures are returned as `ProcessingResult<T>.Errors` with `ValidationFailure.ErrorCode` and `ErrorMessage`.
- ANAF API validation (`ValidateXmlAsync`, `ValidateXmlContentAsync`) is separate and returns raw API response.

## Core EN 16931 Rules (BR-* and BR-CO-*)

| Code | Rule | Validator |
| --- | --- | --- |
| BR-1 | Invoice number is required | `RoCiusUblValidator` |
| BR-2 | Invoice issue date is required | `RoCiusUblValidator` |
| BR-3 | Invoice type code is required | `RoCiusUblValidator` |
| BR-5 | Invoice currency code is required | `RoCiusUblValidator` |
| BR-6 | Seller name is required | `SellerPartyValidator` |
| BR-7 | Buyer name is required | `BuyerPartyValidator` |
| BR-8 | Seller postal address is required | `SellerPartyValidator` |
| BR-10 | Buyer postal address is required | `BuyerPartyValidator` |
| BR-12 | Invoice total amount without VAT is required | `TotalsValidator` |
| BR-14 | Invoice total amount with VAT is required | `TotalsValidator` |
| BR-15 | Amount due for payment is required | `TotalsValidator` |
| BR-16 | Invoice must have at least one line | `RoCiusUblValidator` |
| BR-17 | Payee name required when payee is specified | `PayeePartyValidator` |
| BR-21 | Invoice line identifier is required | `InvoiceLineValidator` |
| BR-22 | Invoice line quantity is required | `InvoiceLineValidator` |
| BR-23 | Invoice line unit of measure is required | `InvoiceLineValidator` |
| BR-24 | Invoice line net amount is required | `InvoiceLineValidator` |
| BR-25 | Invoice line net unit price is required | `InvoiceLineValidator` |
| BR-26 | Invoice line item name is required | `InvoiceLineValidator` |
| BR-27 | Net unit price must not be negative | `InvoiceLineValidator` |
| BR-28 | Gross unit price must not be negative | `InvoiceLineValidator` |
| BR-29 | Invoice period end date must be >= start date | `TotalsValidator` |
| BR-30 | Invoice line period end date must be >= start date | `InvoiceLineValidator` |
| BR-CO-4 | VAT category code is required on line | `InvoiceLineValidator` |
| BR-CO-10 | Sum of line net amounts = total without VAT (0.01 tolerance) | `TotalsValidator` |
| BR-CO-11 | Total with VAT = total without VAT + VAT total (0.01 tolerance) | `TotalsValidator` |
| BR-CO-12 | VAT breakdown tax amount = taxable * rate (0.01 tolerance) | `TotalsValidator` |
| BR-CO-13 | VAT total equals sum of VAT breakdowns (0.01 tolerance) | `TotalsValidator` |

Notes:
- `BR-8-ADDRESS` and `BR-10-ADDRESS` are additional address presence checks for Romanian parties (see RO rules).

## Romanian RO_CIUS Rules (BR-RO-*)

| Code | Rule | Validator |
| --- | --- | --- |
| BR-RO-CIUS | CustomizationID must be RO_CIUS | `RoCiusUblValidator` |
| BR-RO-010 | Invoice number must contain at least one digit | `RoCiusUblValidator` |
| BR-RO-020 | Invoice type must be 380, 389, 384, 381, or 751 | `RoCiusUblValidator` |
| BR-RO-030 | If currency != RON, VAT currency must be RON | `RoCiusUblValidator` |
| BR-RO-040 | VAT point date code must be in allowed set | `RoCiusUblValidator` |
| BR-RO-120 | Romanian buyer must have CUI/CIF or VAT ID | `BuyerPartyValidator` |
| BR-RO-130 | Forced execution requires payee name and legal ID | `PayeePartyValidator` |
| BR-RO-A999 | Max 999 invoice lines | `RoCiusUblValidator` |
| BR-RO-Z2 | Monetary amounts max 2 decimals | `RoCiusUblValidator` |
| BR-RO-COUNTY | Romanian county code must be ISO 3166-2:RO | `RomanianAddressValidator` |
| BR-RO-BUCHAREST | Bucuresti sector must be Sector 1-6 | `RomanianAddressValidator` |
| BR-RO-CITY-REQUIRED | City is required for Romanian address | `RomanianAddressValidator` |
| BR-RO-COUNTRY-CODE | Country code must be RO for Romanian address | `RomanianAddressValidator` |
| BR-RO-SELLER-ID | Romanian seller must have legal ID | `SellerPartyValidator` |

Notes:
- `RomanianAddressValidator` exists but is not wired into `RoCiusUblValidator` by default. If you want these address rules enforced, validate `AddressType` explicitly or extend the validator chain.
- `BR-RO-130` is currently gated by a placeholder `IsForcedExecution` check and is not triggered unless you customize the logic.

## Romanian Length Constraints (Custom Codes)

These are custom rules for Romanian maximum lengths:

| Code | Rule | Validator |
| --- | --- | --- |
| RO-LINE-NOTE-LENGTH | Invoice line note length <= 300 | `InvoiceLineValidator` |
| RO-ITEM-NAME-LENGTH | Item name length <= 200 | `InvoiceLineValidator` |
| RO-ITEM-DESC-LENGTH | Item description length <= 200 | `InvoiceLineValidator` |

## Where These Rules Apply

Local validation is used in:
- `IAnafEInvoiceClient.ValidateInvoiceXmlAsync`
- `IUblProcessingService.ValidateInvoiceAsync`
- `IAnafEInvoiceClient.ProcessDownloadedInvoiceAsync`
- `IAnafEInvoiceClient.ProcessMultipleInvoicesAsync`

For ANAF API validation, use:
- `ValidateXmlAsync`
- `ValidateXmlContentAsync`
