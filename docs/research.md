Ok, fac extragerea strict “tech”, astfel încât să-ți poți modela domain-urile C# (EF Core) și să implementezi un converter din UBL 2.1 (cu RO\_CIUS). Țin formatul pe: entități (BG-*), câmpuri (BT-*), obligatoriu/opțional (cardinalitate), tip sugerat C#, constrângeri (inclusiv RO\_CIUS), plus “cheat-sheet” de mapare UBL 2.1 și note EF Core. Voi marca sursele direct în text, cu BT/BG/BR și citare.

---

# 1) Entitățile (BG-*) și câmpurile (BT-*) necesare în domain

> Notă: Nomenclatura BG = “Business Group (entitate)”, BT = “Business Term (câmp)”. Cardinalitate: `1..1` = required, `0..1` = optional, `0..n / 1..n` = colecții. Constrângerile “BR-RO-\*” sunt reguli CIUS RO. Standardul semantic și regulile de integritate obligatorii (BR-1..BR-…) impun ce e minim necesar într-o factură EN 16931.&#x20;

## BG-1: INVOICE (rădăcina)

* **BT-24 Specification Identifier** – `1..1`, string; în RO e fix:
  `urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:RO_CIUS:1.0.0.2021`. Mapare UBL: `<cbc:CustomizationID>…</cbc:CustomizationID>`. (hard-fail dacă lipsește).
* **BT-1 Invoice Number** – `1..1`, string (max 30 caractere în RO). Trebuie să conțină cel puțin o cifră (BR-RO-010).&#x20;
* **BT-2 Invoice Issue Date** – `1..1`, date (ISO 8601).&#x20;
* **BT-3 Invoice Type Code** – `1..1`, cod din UNTDID 1001; în RO permis doar: `380, 389, 384, 381, 751`.&#x20;
* **BT-5 Invoice Currency Code** – `1..1`, ISO 4217.&#x20;
* **BT-6 VAT Accounting Currency Code** – `0..1`, ISO 4217; în RO: dacă BT-5 ≠ `RON`, atunci BT-6 = `RON` (Fatal).&#x20;
* **BT-7 VAT Point Date** – `0..1`, date.
* **BT-8 VAT Point Date Code** – `0..1`, cod (UNTDID 2005 subset admis în RO).&#x20;
* **BT-9 Payment Due Date** – `0..1`, date.
* **BT-20 Payment Terms** – `0..1`, text (max 300 RO).&#x20;

Reguli minime (standard): BT-24, BT-1, BT-2, BT-3, BT-5 sunt obligatorii (BR-1..BR-5).&#x20;

## BG-2: PROCESS CONTROL

* **BT-23 Business Process Type** – `0..1`, string (ProfileID, UBL `<cbc:ProfileID>`).
* **BT-24** (deja la BG-1 mai sus).

## BG-3: PRECEDING INVOICE REFERENCE (0..500 în RO)

* Colecție de referințe la facturi anterioare (ex. corecții). RO limitează la **max 500** apariții (BR-RO-A500).&#x20;

## BG-4: SELLER

**Obligatoriu** numele vânzătorului (BT-27) și adresa poștală (BG-5) (BR-6, BR-8).&#x20;
Câmpuri tipice:

* **BT-27 Seller Name** – `1..1`, text (max 200 RO).&#x20;
* **BT-29 Seller Identifier** – `0..1`, identificator + scheme (ID + scheme optională).
* **BT-31 Seller VAT Identifier** – `0..1`, string (RO: codul de TVA dacă e cazul).
* **BT-33 Seller Additional Legal Information** – `0..1`, text (max 1000 RO).&#x20;
* **BG-5 Seller Postal Address** – `1..1` (vezi imediat).
* **BG-6 Seller Contact** – `0..1`.

### BG-5: SELLER POSTAL ADDRESS

* **BT-35 / BT-36 Address lines** – `0..1` / `0..1`, text (BT-36 max 100 RO).&#x20;
* **BT-37 City** – `0..1`, text (max 50 RO).
* **BT-38 Postal Zone** – `0..1`, text (max 20 RO).&#x20;
* **BT-39 Country Subdivision** – `0..1`, text; dacă țara=RO → cod județ **ISO 3166-2\:RO**, 2 litere, iar pentru București `B` (RO rule).&#x20;
* **BT-40 Country Code** – `1..1`, ISO 3166-1 alpha-2. (Obligatoriu prin BR-9)&#x20;

### BG-6: SELLER CONTACT

* **BT-41, BT-42, BT-43** – `0..1` nume, telefon, e-mail (max 100 RO).&#x20;

## BG-7: BUYER

**Obligatoriu** numele cumpărătorului (BT-44) și adresa poștală (BG-8) (BR-7, BR-10).&#x20;

* **BT-44 Buyer Name** – `1..1`, text (max 200 RO).&#x20;
* **BT-47a Buyer Legal Registration Identifier** – `0..1`; în RO: dacă cumpărătorul e român, se folosește **codul de identificare fiscală** drept legal registration ID. **BR-RO-120** cere BT-47a și/sau BT-48 (VAT ID) prezente.&#x20;
* **BT-48 Buyer VAT Identifier** – `0..1`.
* **BG-8 Buyer Postal Address** – `1..1` (vezi mai jos).
* **BG-9 Buyer Contact** – `0..1`.

### BG-8: BUYER POSTAL ADDRESS

* **BT-50/51/52/53/54**: similar cu vânzător; limite RO: BT-50 (max 150), BT-52 (max 50), BT-53 (max 20). **BT-55** Country Code `1..1` (BR-11).

### BG-9: BUYER CONTACT

* **BT-56/57/58** – `0..1` (max 100 fiecare în RO).&#x20;

## BG-10: PAYEE (0..1)

* **BT-59 Payee Name** – `0..1`, text (max 200 RO); devine obligatoriu dacă Payee ≠ Seller (BR-17). În **executare silită**, RO cere BT-59 = numele organului de executare și **BT-61a** = identificatorul legal al acestuia (BR-RO-130).
* **BT-61a Payee Legal Registration Identifier** – `0..1`.&#x20;

## BG-11: SELLER TAX REPRESENTATIVE PARTY (0..1)

* **BT-62 Name** `0..1` (devine obligatorie dacă există BG-11 – BR-18).
* **BT-64/65/66/67/68/69** – adresă; în RO: dacă există BG-11: BT-64 și BT-66 obligatorii; dacă BT-68=“B” (București), BT-66 ∈ {Sector 1..6}; dacă țara=RO, BT-68 conform ISO 3166-2\:RO.

## BG-12/13/14/15: DELIVERY info

* **BT-70 Actual Delivery Date** / **BT-71 Delivery Note Reference** – `0..1`, (max 200 RO pentru BT-70).&#x20;
* **BG-14 Invoicing Period** – **BT-73/BT-74** (start/end) – `0..1`; dacă ambele, end ≥ start (BR-29).&#x20;
* **BG-15 Deliver-to Address** – `0..1`; în RO: dacă BG-15 există → **BT-75 line1** obligatoriu; limite de lungime BT-75 (max 150 RO).

## BG-16: PAYMENT INSTRUCTIONS (1..1)

* **BT-81 Payment Means Code** – `1..1`, cod **UNTDID 4461** (distinge SEPA/non-SEPA, transfer/debit/card).&#x20;
* **BT-82 Payment Means Text** – `0..1`, text (max 100 RO).&#x20;
* **BT-83 Remittance Information** – `0..1`, text (recomandat ≤ 140 caractere pentru SEPA; în RO limită 140 pe BT-83).

### BG-17: CREDIT TRANSFER (0..n)

* **BT-84 Payment Account Identifier (IBAN)** – `1..1` dacă există BG-17; **BT-85** Account Name `0..1`; **BT-86** BIC `0..1` (BT-85, BT-88 max 200 RO).&#x20;

### BG-18: PAYMENT CARD (0..1) / BG-19: DIRECT DEBIT (0..1)

* **BT-87/BT-88/BT-89** info card; **BT-89** mandate ID pentru debit direct etc. (limite RO relevante la texte max 200 unde se aplică).&#x20;

## BG-20: DOCUMENT LEVEL ALLOWANCES (0..n) & BG-21: CHARGES (0..n)

* **Allowance (BG-20)**: **BT-92 Amount** `1..1` (2 zecimale în RO), **BT-93 Base** `0..1`, **BT-94 Percentage** `0..1`, **BT-95 VAT Category Code** `1..1`, **BT-96 VAT Rate** `0..1`, **BT-97 Reason** `0..1` (max 100 RO). Sumele/percentage au reguli de calcul în standard.&#x20;
* **Charge (BG-21)**: **BT-99 Amount** `1..1` (2 zecimale RO), **BT-100 Base** `0..1`, **BT-101 Percentage** `0..1`, **BT-102 VAT Category Code** `1..1`, **BT-103 VAT Rate** `0..1`, **BT-104 Reason** `0..1` (max 100 RO).&#x20;

> Tipuri de date/rotunjiri: EN 16931 definește tipurile (Amount, Quantity, Percentage, Identifier). Amount/UnitPriceAmount sunt numerice cu monedă separată; Percentage e numeric (ex. 34,78). Menține două zecimale pentru multe sume (vezi BR-RO-Z2).

## BG-22: DOCUMENT TOTALS (1..1)

* **BT-106 Sum of Invoice Line Net Amount** – `1..1` (2 zecimale RO).
* **BT-107 Sum of Allowances on Doc Level** – `0..1` (2 zecimale RO).
* **BT-108 Sum of Charges on Doc Level** – `0..1` (2 zecimale RO).
* **BT-109 Invoice Total Amount without VAT** – `1..1` (2 zecimale RO).
* **BT-110 Invoice Total VAT Amount** – `0..1` (2 zecimale RO).
* **BT-112 Invoice Total Amount with VAT** – `1..1` (2 zecimale RO).
* **BT-115 Amount Due for Payment** – `1..1` (2 zecimale RO).
  (Obligațiile BR-12, BR-14, BR-15 etc.).

## BG-23: VAT BREAKDOWN (1..n)

* **BT-116 VAT Category Taxable Amount** – `1..1` (2 zecimale RO).
* **BT-117 VAT Category Tax Amount** – `1..1` (2 zecimale RO).
* **BT-118 VAT Category Code** – `1..1` (subset UNTDID 5305, vezi mai jos).
* **BT-119 VAT Category Rate** – `0..1` (procente).
* **BT-120 Exemption Reason Text** – `0..1` (max 100 RO).
  (Detalii și exemple de calcul în standard).

## BG-25: INVOICE LINE (1..n, minim o linie)

Obligatorii per linie: **BT-126**, **BT-129**, **BT-130**, **BT-131**, **BT-146**, **BT-153** (prin BR-21..BR-26).

* **BT-126 Line ID** – `1..1`, string (max 300 RO pentru BT-127, dar BT-126 nu e limitat explicit de RO; practică: ≤ 30-50).&#x20;
* **BT-127 Line Note** – `0..1`, text (max 300 RO).&#x20;
* **BT-128 Object Identifier** – `0..1` (+ optional scheme UNTDID 1153).&#x20;
* **BT-129 Invoiced Quantity** – `1..1`, decimal (cantitate).&#x20;
* **BT-130 Unit of Measure** – `1..1`, cod UNECE Rec.20.&#x20;
* **BG-26 Line Period** – `0..1` (BT-134/BT-135 date; end ≥ start dacă ambele – BR-30).&#x20;
* **BG-27 Line Allowances** – `0..n` (BT-136 amount, BT-137 base, BT-138 percent, BT-139 reason (cod 7161), BT-140 reason text). (Limite RO la texte/2 zecimale pe sume).&#x20;
* **BG-28 Line Charges** – `0..n` (BT-141 amount, BT-142 base, BT-143 percent, BT-144 reason text, **BT-145 reason code din UNTDID 7161**).&#x20;
* **BG-29 Price Details** – `1..1`

  * **BT-146 Net unit price** – `1..1` (non-negativ – BR-27).
  * **BT-147 Price discount** – `0..1`.
  * **BT-148 Gross unit price** – `0..1` (non-negativ – BR-28).
  * **BT-149 Price base quantity** – `0..1`.
  * **BT-150 Price base UoM** – `0..1` (trebuie să fie același UoM ca BT-130).&#x20;
* **BG-30 Line VAT Information** – `1..1`:

  * **BT-151 VAT Category Code** – `1..1` (subset UNTDID 5305).
  * **BT-152 VAT Rate** – `0..1`.&#x20;
* **BG-31 Item information** – `1..1`:

  * **BT-153 Item Name** – `1..1` (max 200 RO).
  * **BT-154 Item Description** – `0..1` (max 200 RO).
  * **BT-159 Country of Origin** – `0..1` (ISO 3166-1).
  * **BG-32 Item Attributes** – `0..50 în RO`? (RO limitează BG-32 la max 50 apariții via BR-RO-A050; BG-32 este un grup; pe linie obișnuit). Fiecare atribut are **BT-160 name** și **BT-161 value** (ambele text).

---

# 2) Liste de coduri & tipuri – ce să validezi în model/converter

* **BT-3 InvoiceTypeCode**: UNTDID 1001; în **RO doar {380, 389, 384, 381, 751}**. Pune enum și validare strictă.&#x20;
* **BT-81 PaymentMeansCode**: **UNTDID 4461** (distinge SEPA/non-SEPA; transfer/debit/card).&#x20;
* **BT-145 / BT-139 ReasonCode (line charge/allowance)**: **UNTDID 7161**.&#x20;
* **BT-151/BT-118 VAT Category Code**: subset **UNTDID 5305** – categorii tipice: “Cota normală”, “Cota zero”, “Scutit”, “Taxare inversă”, “Scutit pentru livrări intracomunitare”, “Export” etc. (în implementare, mapează la un enum intern).&#x20;
* **BT-130 / BT-150 unit codes**: **UNECE Recommendation 20** (coduri UoM).&#x20;
* **Țări / Subdiviziuni**: ISO 3166-1 (country), **ISO 3166-2\:RO** pentru județe; în RO: București = `B`; dacă `B`, orașul (BT-66 / BT-37 etc) trebuie “Sector 1..6”.&#x20;
* **Tipuri de date numeric/monetar**: Amount/UnitPriceAmount/Percentage/Quantity – vezi §6.5 în standard; stochează sumele cu scale adecvate; aplicațiile RO cer **2 zecimale** pe majoritatea sumelor (BR-RO-Z2).

---

# 3) Constrângeri RO\_CIUS (hard rules pentru validare)

* **CustomizationID** fix (BT-24).&#x20;
* **Cardinalități maxime adiționale** (pe lângă EN 16931):

  * BG-1 max 20 apariții (tehnic irelevant pentru root – dar e în listă) – BR-RO-A020.
  * **BG-24, BG-32 max 50 apariții** – BR-RO-A050 (ex. atribute articol).
  * **BG-3 max 500 apariții** – BR-RO-A500 (preceding invoices).
  * **BG-25 max 999 apariții** – BR-RO-A999 (linii factură).
    (Le poți reflecta prin validări în serviciul de import.)&#x20;
* **Lungimi maxime** (doar ce e relevant dev):

  * 20 char: BT-38, BT-53, BT-67, BT-78.
  * 30 char: **BT-1**, BT-12..BT-18a, BT-25, BT-122.
  * 50 char: BT-37, BT-52, BT-66, BT-77, **BT-160**.
  * 100 char: BT-19, BT-36, BT-41..BT-43, BT-51, BT-56..BT-58, BT-65, BT-76, BT-82, BT-97, BT-104, BT-120, BT-123, BT-133, BT-139, BT-144, BT-153, BT-161.
  * 140 char: **BT-83**.
  * 150 char: BT-35, BT-50, **BT-64**, **BT-75**.
  * 200 char: **BT-27, BT-28, BT-44, BT-45, BT-59, BT-62, BT-70, BT-85, BT-88, BT-124, BT-125c, BT-154**.
  * 300 char: **BT-20, BT-22, BT-127**.
    (Aplică în model/DTO/validator).
* **Zecimale**: **Max 2** pentru extrem de multe BT monetare (ex. BT-92, BT-93, BT-99, BT-100, BT-106..BT-117, BT-131, BT-136..BT-142 etc.) – rule BR-RO-Z2. Configurează `decimal(18,2)` la coloanele monetare și calcule/rotunjiri consistente.&#x20;
* **Buyer identifiers** (românești): BT-47a și/sau BT-48; pentru români, BT-47a = CUI/CIF.&#x20;
* **Adrese București/județe** (reprezentant fiscal sau adrese RO): BT-68=“B” ⇒ city = “Sector 1..6”; subdiviziunea (BT-39/BT-68) = cod ISO 3166-2\:RO.&#x20;

---

# 4) “Cheat-sheet” de mapare UBL 2.1 → domain (doar ce contează implementării)

> Pentru maparea completă la elemente UBL/XPath există CEN/TS 16931-3-2 (binding UBL). Mai jos sunt cele esențiale și “safe” pentru transformare în domain.

* Root `<Invoice>`

  * **BT-24** → `<cbc:CustomizationID>` (RO\_CIUS string).&#x20;
  * **BT-23** → `<cbc:ProfileID>`
  * **BT-1** → `<cbc:ID>`
  * **BT-2** → `<cbc:IssueDate>`
  * **BT-3** → `<cbc:InvoiceTypeCode>`
  * **BT-5** → `<cbc:DocumentCurrencyCode>`
  * **BT-6** → `<cbc:TaxCurrencyCode>`
  * **BT-7/BT-8** → `<cbc:TaxPointDate>` (+ eventual cod/indicator conform binding-ului)
  * **BT-9** → `<cbc:DueDate>` sau în `<cac:PaymentTerms>` (depinde de instanță)

* Părți:

  * **BG-4 Seller** → `<cac:AccountingSupplierParty>/<cac:Party>`

    * Address (BG-5) → `<cac:PostalAddress>` (StreetName/AdditionalStreetName/CityName/PostalZone/CountrySubentity + `<cac:Country>/<cbc:IdentificationCode>`)
    * Contact (BG-6) → `<cac:Contact>` (Name/Telephone/ ElectronicMail)
    * VAT ID (BT-31) → `<cac:PartyTaxScheme>/<cbc:CompanyID>`
    * Legal reg. id (BT-29 etc.) → `<cac:PartyLegalEntity>/<cbc:CompanyID>`
  * **BG-7 Buyer** → `<cac:AccountingCustomerParty>/<cac:Party>` (simetric cu Seller)
  * **BG-10 Payee** → `<cac:PayeeParty>`
  * **BG-11 Tax Representative** → `<cac:TaxRepresentativeParty>`

* Livrare:

  * **BG-13/14/15** → `<cac:Delivery>` (ActualDeliveryDate / DeliveryLocation / DeliveryAddress etc.)

* Plată:

  * **BG-16 PaymentInstructions** → `<cac:PaymentMeans>` (PaymentMeansCode, Payee account etc.), `<cac:PaymentTerms>` (text/BT-20/BT-22)
  * **BG-17 CreditTransfer** → `<cac:PayeeFinancialAccount>` + `<cac:FinancialInstitutionBranch>/<cac:FinancialInstitution>/<cbc:ID>` (BIC)
  * **BG-19 DirectDebit** → `<cac:PaymentMeans>/<cbc:PaymentID>`/mandate

* Linii (BG-25):

  * `<cac:InvoiceLine>`

    * **BT-126** → `<cbc:ID>`
    * **BT-129/BT-130** → `<cbc:InvoicedQuantity unitCode=…>`
    * **BT-131** → `<cbc:LineExtensionAmount>`
    * **BG-29 Price** → `<cac:Price>` (PriceAmount, BaseQuantity + unitCode)
    * **BG-30 VAT** → `<cac:Item>/<cac:ClassifiedTaxCategory>` (ID=category code; Percent=rate)
    * **BG-31 Item** → `<cac:Item>` (Name/Description/OriginCountry)
    * **BG-27/BG-28** → `<cac:AllowanceCharge>` (within line) cu `ChargeIndicator=true/false` + Reason/ReasonCode/Amount/BaseAmount/MultiplierFactorNumeric (percent)

* Totaluri & VAT breakdown:

  * **BG-22** → `<cac:LegalMonetaryTotal>` (LineExtensionAmount, TaxExclusiveAmount, TaxInclusiveAmount, PayableAmount etc.)
  * **BG-23** → `<cac:TaxTotal>/<cac:TaxSubtotal>` (TaxableAmount, TaxAmount, TaxCategory/Percent/ID)

Acest subset îți permite transformarea UBL→domain fără să atingi zonele mai sensibile din binding (ex. ProfileID specifics). Pentru detalii avansate, consultă 16931-3-2 (menționat de standard ca parte a setului de documente).&#x20;

---

# 5) Propunere de modelare C# (EF Core) – “ce pui în cod”

## (a) Tipuri de bază & precision

* Sume monetare: `decimal(18,2)` (pentru câmpurile marcate de RO cu 2 zecimale). Pune `HasPrecision(18,2)` în `OnModelCreating`. (BR-RO-Z2).&#x20;
* Cantități: `decimal(18,6)` (uzual), cu unitCode separat (string 3-8).
* Percentage: `decimal(5,2)`.

## (b) Value Objects (owned) recomandate

* `Address` (BT-35..BT-40/BT-50..BT-55): ține `CountryCode`, `CountrySubdivision` (pentru RO: ISO 3166-2\:RO), `City`, `PostalCode`, `Line1/Line2`. Constrângeri de lungime din RO.&#x20;
* `Contact` (Name/Phone/Email) cu lungimi RO.&#x20;
* `PaymentAccount` (IBAN, BIC, Name) – pentru BG-17.&#x20;
* `Price` (Net, Gross?, Discount?, BaseQty, BaseUom) – BG-29.&#x20;

## (c) Entități principale

* `Invoice` (BG-1) – colecții: `InvoiceLines`, `Allowances`, `Charges`, `VatBreakdowns`, `CreditTransfers`, opțional `PrecedingInvoices`. Chei exterioare spre `Seller`, `Buyer`, `Payee`, `TaxRepresentative`.
* `Party` (Seller/Buyer/Payee/TaxRep) – cu `Address` (owned), `Contact` (owned), `VatId`, `LegalRegistrationId`.
* `PaymentInstructions` (BG-16) – `PaymentMeansCode` (enum 4461), `RemittanceInfo`, plus referințe la `CreditTransfers`, `PaymentCard`, `DirectDebit`.
* `InvoiceLine` – owned `Price` + colecții `LineAllowances` / `LineCharges`, plus `Item` (BG-31) și `LineVat` (BG-30).

## (d) Validări de business (serviciu/validator)

* Prezență minimă: BT-24, BT-1, BT-2, BT-3, BT-5; Seller name + address; Buyer name + address; cel puțin o linie; line ID/qty/UoM/net price/item name obligatorii (BR-1..BR-8, BR-16, BR-21..BR-26).&#x20;
* Lungimi maxime pe BT conform listei RO (vezi §3).&#x20;
* Verificări RO:

  * Dacă moneda ≠ RON → BT-6 = RON.&#x20;
  * Buyer român: BT-47a (CUI/CIF) și/sau BT-48 (TVA) obligatoriu.&#x20;
  * București: BT-68 = “B” ⇒ City = Sector 1..6.&#x20;
* Constrângeri perioade: EndDate ≥ StartDate (document și linie).&#x20;
* Non-negative prices (net/gross unit).&#x20;
* UoM din UNECE Rec.20; VAT category din UNTDID 5305; reason codes din 7161; payment means din 4461. (validează împotriva acestor liste).

---

# 6) Algoritm de transformare UBL 2.1 → domain

1. **Parsează & validează header**:

   * `CustomizationID` exact RO\_CIUS; altfel respinge.
   * `InvoiceTypeCode` ∈ {380,389,384,381,751}.
   * Currency + VAT currency (RO).

2. **Extrage părțile** (`AccountingSupplierParty`, `AccountingCustomerParty`, `PayeeParty`, `TaxRepresentativeParty`):

   * Populează `Party` + `Address` + `Contact`; validează country/subdivision (RO) și lungimile; pentru RO, mapează “B” și “Sector x”.&#x20;

3. **Payment**:

   * `PaymentMeans` → `PaymentInstructions` (BT-81/82/83).
   * `PayeeFinancialAccount` → BG-17 (IBAN/BIC).&#x20;

4. **Linii**: pentru fiecare `<cac:InvoiceLine>`

   * ID/Qty/UoM/LineExtensionAmount.
   * Price (`<cac:Price>`) → Net/Gross/BaseQty/BaseUom.
   * Item (name/desc/origin) + Attributes (BG-32, limită RO 50 pe atribut set).
   * ClassifiedTaxCategory → VAT category (5305) + rate.
   * AllowanceCharge (line): Reason/ReasonCode (7161) + Amount/Base/Percent (aplică scale/2 zecimale).&#x20;

5. **Allowances/Charges pe document**: `<cac:AllowanceCharge>` la nivel root (nu în linie) → BG-20/BG-21 (validează sumele/2 zecimale și VAT category & rate).&#x20;

6. **VAT breakdown**: `<cac:TaxTotal>/<cac:TaxSubtotal>` → BG-23. Verifică: TaxAmount = TaxableAmount × Rate/100 (rotunjit 2 zecimale).&#x20;

7. **Totaluri**: `<cac:LegalMonetaryTotal>` → BG-22. Recalculează și compară cu UBL; dacă deviază peste toleranță (ex. 0.01), semnalează eroare/adjust. (BR-12, BR-14, BR-15).&#x20;

8. **Reguli periodă**: dacă există perioade (document / linie) – verifică end ≥ start.&#x20;

9. **Reguli RO finale**: lungimi, multiplicity (BG-25 ≤ 999, BG-32 ≤ 50 etc.), “Buyer IDs”, București/Sector.

---

# 7) Sugerări EF Core (config rapid)

* **Precision**:

  * `builder.Property(p => p.Amount).HasPrecision(18, 2);`
  * `builder.Property(p => p.UnitPrice).HasPrecision(18, 6);`
  * `builder.Property(p => p.Percentage).HasPrecision(5, 2);`
* **Owned types**: `OwnsOne(e => e.Address, a => { … length constraints … });`
* **Indexes**:

  * `Invoice.Number` (unique), `Seller.VatId`, `Buyer.LegalRegistrationId`, `Invoice.IssueDate`.
* **Check constraints**:

  * `InvoiceTypeCode` in set (RO).
  * `DocumentCurrencyCode` + (if ≠ RON) enforce `TaxCurrencyCode = RON` (la import/validator).&#x20;
* **Enums**: `PaymentMeansCode4461`, `VatCategory5305`, `Reason7161`, `UnitCodeRec20`.
  (Stochează ca string scurt + conversie; îți simplifică validarea listelor.)

---

# 8) Ce rămâne de ales (opțiuni implementare)

* **Rounding policy**: adoptă “Banker’s” sau “AwayFromZero” – important pentru BT-117/BT-110/BT-112/BT-115; standardul dă 2 zecimale la exemplificări; unifică politica la import vs. afișare.&#x20;
* **Validarea listelor de coduri**: pune surse locale (enum + whitelist) pentru 1001/4461/7161/5305/Rec20/ISO 3166.
* **Fallback pentru câmpuri lipsă**: ex. `DueDate` în `PaymentTerms` vs. `cbc:DueDate` – normalizează.


// ============================================================================
// EF eFactura RO (EN16931+RO_CIUS) – Domain, DTO, Mapper, Validators
// Target: .NET 8, EF Core 8, FluentValidation 11+
// Design choices (agreed):
//  1) Amounts decimal(18,2); Unit prices decimal(18,6); Percentages decimal(5,2).
//  2) Code lists via extensible "SmartEnum"-like classes (enum+conversion style).
//  3) Validation with FluentValidation on DTOs (semantic + CIUS-RO highlights).
// ----------------------------------------------------------------------------
// Folder-style layout inside one file (split into real files in your repo):
//   - Domain/* (entities + owned types + EF configuration + value converters)
//   - Codes/*  (code-list wrappers: 1001, 4461, 5305, 7161)
//   - Dto/*    (import DTOs aligned to EN16931 BTs/BGs that we actually use)
//   - Ubl/*    (XDocument→DTO parser helpers for UBL 2.1 binding)
//   - Mapping/*(DTO→Domain mapper)
//   - Validation/* (FluentValidation validators for DTOs)
//   - Infrastructure/* (DbContext)
// ============================================================================

// ============================== Domain/Primitives.cs =========================
using System; 
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfFactura.Domain;

public interface IEntity
{
    Guid Id { get; }
}

public abstract class Entity : IEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

public sealed class Money
{
    public decimal Amount { get; set; } // decimal(18,2)
    public string Currency { get; set; } = default!; // ISO 4217
}

public sealed class Address // Owned
{
    [MaxLength(150)] public string? Line1 { get; set; } // BT-35/50
    [MaxLength(100)] public string? Line2 { get; set; } // BT-36/51
    [MaxLength(50)]  public string? City { get; set; }  // BT-37/52
    [MaxLength(20)]  public string? PostalCode { get; set; } // BT-38/53
    [MaxLength(50)]  public string? CountrySubdivision { get; set; } // BT-39/54 – ISO 3166-2:RO for RO
    [MaxLength(2)]   public string CountryCode { get; set; } = default!; // BT-40/55 – ISO 3166-1 alpha-2
}

public sealed class Contact // Owned
{
    [MaxLength(100)] public string? Name { get; set; }    // BT-41/56
    [MaxLength(100)] public string? Telephone { get; set; } // BT-42/57
    [MaxLength(100)] public string? Email { get; set; }     // BT-43/58
}

// ============================== Domain/Party.cs ==============================
namespace EfFactura.Domain;

public sealed class Party : Entity
{
    [MaxLength(200)] public string Name { get; set; } = default!; // BT-27/44/62/59

    [MaxLength(100)] public string? AdditionalLegalInfo { get; set; } // BT-33

    [MaxLength(50)]  public string? LegalRegistrationId { get; set; } // BT-29/47a/61a
    [MaxLength(20)]  public string? VatIdentifier { get; set; }       // BT-31/48

    public Address Address { get; set; } = new(); // BG-5/BG-8/BG-11 addr
    public Contact? Contact { get; set; } // BG-6/BG-9
}

// ============================== Domain/Payments.cs ===========================
using EfFactura.Codes;

namespace EfFactura.Domain;

public sealed class PaymentInstructions : Entity
{
    public PaymentMeansCode4461 PaymentMeansCode { get; set; } = PaymentMeansCode4461.Unspecified; // BT-81
    [MaxLength(100)] public string? PaymentMeansText { get; set; } // BT-82
    [MaxLength(140)] public string? RemittanceInformation { get; set; } // BT-83

    public List<CreditTransfer> CreditTransfers { get; set; } = new(); // BG-17
    public PaymentCard? PaymentCard { get; set; } // BG-18 (optional)
    public DirectDebit? DirectDebit { get; set; } // BG-19 (optional)
}

public sealed class CreditTransfer : Entity
{
    [MaxLength(34)] public string Iban { get; set; } = default!; // BT-84
    [MaxLength(200)] public string? AccountName { get; set; } // BT-85
    [MaxLength(11)] public string? Bic { get; set; } // BT-86
}

public sealed class PaymentCard : Entity
{
    [MaxLength(200)] public string? HolderName { get; set; } // BT-87
    [MaxLength(200)] public string? CardId { get; set; } // BT-88 (masked/identifier)
}

public sealed class DirectDebit : Entity
{
    [MaxLength(200)] public string? MandateId { get; set; } // BT-89
}

// ============================== Domain/Lines.cs ==============================
using EfFactura.Codes;

namespace EfFactura.Domain;

public sealed class InvoiceLine : Entity
{
    [MaxLength(60)] public string LineId { get; set; } = default!; // BT-126
    [MaxLength(300)] public string? Note { get; set; } // BT-127

    public decimal Quantity { get; set; } // BT-129, decimal(18,6)
    [MaxLength(8)] public string UnitCode { get; set; } = default!; // BT-130 (UNECE Rec.20) – stored as string

    // Monetary values
    public decimal LineExtensionAmount { get; set; } // BT-131, dec(18,2)

    // Period (optional)
    public DateOnly? PeriodStart { get; set; } // BT-134
    public DateOnly? PeriodEnd { get; set; }   // BT-135

    // Price details (BG-29)
    public Price Price { get; set; } = new();

    // VAT info (BG-30)
    public VatCategory5305 Vat { get; set; } = VatCategory5305.StandardRated; // BT-151
    public decimal? VatRate { get; set; } // BT-152, percentage (5,2)

    // Item (BG-31)
    [MaxLength(200)] public string ItemName { get; set; } = default!; // BT-153
    [MaxLength(200)] public string? ItemDescription { get; set; } // BT-154
    [MaxLength(2)] public string? OriginCountryCode { get; set; } // BT-159

    public List<ItemAttribute> Attributes { get; set; } = new(); // BG-32 (limit 50 by validation)

    public List<LineAllowanceCharge> Allowances { get; set; } = new(); // BG-27
    public List<LineAllowanceCharge> Charges { get; set; } = new();    // BG-28
}

public sealed class Price // Owned
{
    public decimal NetUnitPrice { get; set; } // BT-146 dec(18,6)
    public decimal? PriceDiscount { get; set; } // BT-147 dec(18,2)
    public decimal? GrossUnitPrice { get; set; } // BT-148 dec(18,6)
    public decimal? BaseQuantity { get; set; } // BT-149 dec(18,6)
    [MaxLength(8)] public string? BaseUom { get; set; } // BT-150
}

public sealed class ItemAttribute : Entity
{
    [MaxLength(50)] public string Name { get; set; } = default!;  // BT-160
    [MaxLength(100)] public string? Description { get; set; }     // BT-161
}

public sealed class LineAllowanceCharge : Entity
{
    public bool IsCharge { get; set; } // true⇒Charge (BG-28); false⇒Allowance (BG-27)
    public decimal Amount { get; set; } // BT-136/141 dec(18,2)
    public decimal? BaseAmount { get; set; } // BT-137/142 dec(18,2)
    public decimal? Percentage { get; set; } // BT-138/143 dec(5,2)

    public ReasonCode7161? ReasonCode { get; set; } // BT-139/145
    [MaxLength(100)] public string? ReasonText { get; set; } // BT-140/144
}

// ============================== Domain/Totals.cs ============================
namespace EfFactura.Domain;

public sealed class VatBreakdown : Entity
{
    public VatCategory5305 VatCategory { get; set; } // BT-118
    public decimal? VatRate { get; set; } // BT-119 (nullable for exemptions)
    public decimal TaxableAmount { get; set; } // BT-116 dec(18,2)
    public decimal TaxAmount { get; set; } // BT-117 dec(18,2)
    [MaxLength(100)] public string? ExemptionReason { get; set; } // BT-120
}

public sealed class DocumentAllowanceCharge : Entity
{
    public bool IsCharge { get; set; } // BG-21 vs BG-20
    public decimal Amount { get; set; } // BT-92/99 dec(18,2)
    public decimal? BaseAmount { get; set; } // BT-93/100 dec(18,2)
    public decimal? Percentage { get; set; } // BT-94/101 dec(5,2)

    public VatCategory5305 VatCategory { get; set; } // BT-95/102
    public decimal? VatRate { get; set; } // BT-96/103
    [MaxLength(100)] public string? ReasonText { get; set; } // BT-97/104
}

// ============================== Domain/Invoice.cs ===========================
using EfFactura.Codes;

namespace EfFactura.Domain;

public sealed class Invoice : Entity
{
    // BG-1 core
    [MaxLength(200)] public string CustomizationId { get; set; } = default!; // BT-24 – fixed RO value
    [MaxLength(30)] public string Number { get; set; } = default!; // BT-1
    public DateOnly IssueDate { get; set; } // BT-2

    public InvoiceTypeCode1001 TypeCode { get; set; } = InvoiceTypeCode1001.Invoice_380; // BT-3

    [MaxLength(3)] public string CurrencyCode { get; set; } = default!; // BT-5
    [MaxLength(3)] public string? VatCurrencyCode { get; set; } // BT-6 (RON when BT-5≠RON in RO)

    public DateOnly? VatPointDate { get; set; } // BT-7
    [MaxLength(3)] public string? VatPointDateCode { get; set; } // BT-8

    public DateOnly? DueDate { get; set; } // BT-9
    [MaxLength(300)] public string? PaymentTermsText { get; set; } // BT-20

    // Parties
    public Party Seller { get; set; } = new(); // BG-4
    public Party Buyer { get; set; } = new();  // BG-7
    public Party? Payee { get; set; } // BG-10
    public Party? TaxRepresentative { get; set; } // BG-11

    // Delivery / Period
    public DateOnly? ActualDeliveryDate { get; set; } // BT-70
    [MaxLength(200)] public string? DeliveryNoteReference { get; set; } // BT-71
    public DateOnly? InvoicingPeriodStart { get; set; } // BT-73
    public DateOnly? InvoicingPeriodEnd { get; set; }   // BT-74

    // Payment Instructions
    public PaymentInstructions Payment { get; set; } = new(); // BG-16

    // Collections
    public List<DocumentAllowanceCharge> Allowances { get; set; } = new(); // BG-20
    public List<DocumentAllowanceCharge> Charges { get; set; } = new();    // BG-21
    public List<VatBreakdown> VatBreakdowns { get; set; } = new(); // BG-23
    public List<InvoiceLine> Lines { get; set; } = new(); // BG-25 (≤999 by validation)

    // Preceding invoices (BG-3) – store minimal refs
    public List<PrecedingInvoiceRef> PrecedingInvoices { get; set; } = new();

    // Totals (BG-22)
    public decimal SumOfLineNet { get; set; } // BT-106
    public decimal? SumOfAllowances { get; set; } // BT-107
    public decimal? SumOfCharges { get; set; } // BT-108
    public decimal TotalWithoutVat { get; set; } // BT-109
    public decimal? TotalVatAmount { get; set; } // BT-110 (per doc currency)
    public decimal TotalWithVat { get; set; } // BT-112
    public decimal AmountDue { get; set; } // BT-115
}

public sealed class PrecedingInvoiceRef : Entity
{
    [MaxLength(30)] public string ReferenceNumber { get; set; } = default!;
    public DateOnly? IssueDate { get; set; }
}

// ============================== Domain/Configurations.cs ====================
namespace EfFactura.Domain;

public static class ModelBuilderExtensions
{
    public static void ConfigureEfFactura(this ModelBuilder modelBuilder)
    {
        // Invoice
        var inv = modelBuilder.Entity<Invoice>();
        inv.HasIndex(i => i.Number).IsUnique();
        inv.Property(p => p.SumOfLineNet).HasPrecision(18,2);
        inv.Property(p => p.SumOfAllowances).HasPrecision(18,2);
        inv.Property(p => p.SumOfCharges).HasPrecision(18,2);
        inv.Property(p => p.TotalWithoutVat).HasPrecision(18,2);
        inv.Property(p => p.TotalVatAmount).HasPrecision(18,2);
        inv.Property(p => p.TotalWithVat).HasPrecision(18,2);
        inv.Property(p => p.AmountDue).HasPrecision(18,2);

        // Enums (stored as string codes)
        inv.Property(p => p.TypeCode).HasConversion(InvoiceTypeCode1001.Converter).HasMaxLength(3);

        inv.OwnsOne(p => p.Seller, party => party.ConfigureParty());
        inv.OwnsOne(p => p.Buyer, party => party.ConfigureParty());
        inv.OwnsOne(p => p.Payee, party => party.ConfigureParty());
        inv.OwnsOne(p => p.TaxRepresentative, party => party.ConfigureParty());

        inv.OwnsOne(p => p.Payment, pay =>
        {
            pay.Property(x => x.PaymentMeansCode).HasConversion(PaymentMeansCode4461.Converter).HasMaxLength(3);
            pay.Property(x => x.PaymentMeansText).HasMaxLength(100);
            pay.Property(x => x.RemittanceInformation).HasMaxLength(140);

            pay.OwnsMany(x => x.CreditTransfers, ct =>
            {
                ct.WithOwner().HasForeignKey("PaymentInstructionsId");
                ct.Property(p => p.Iban).HasMaxLength(34);
                ct.Property(p => p.AccountName).HasMaxLength(200);
                ct.Property(p => p.Bic).HasMaxLength(11);
                ct.ToTable("CreditTransfers");
            });

            pay.OwnsOne(x => x.PaymentCard, pc =>
            {
                pc.Property(p => p.HolderName).HasMaxLength(200);
                pc.Property(p => p.CardId).HasMaxLength(200);
            });

            pay.OwnsOne(x => x.DirectDebit, dd =>
            {
                dd.Property(p => p.MandateId).HasMaxLength(200);
            });
        });

        inv.OwnsMany(p => p.Allowances, ac =>
        {
            ac.WithOwner().HasForeignKey("InvoiceId");
            ac.Property(p => p.Amount).HasPrecision(18,2);
            ac.Property(p => p.BaseAmount).HasPrecision(18,2);
            ac.Property(p => p.Percentage).HasPrecision(5,2);
            ac.Property(p => p.ReasonText).HasMaxLength(100);
            ac.Property(p => p.VatCategory).HasConversion(VatCategory5305.Converter).HasMaxLength(3);
            ac.ToTable("DocumentAllowanceCharges");
        });

        inv.OwnsMany(p => p.Charges, ac =>
        {
            ac.WithOwner().HasForeignKey("InvoiceId");
            ac.Property(p => p.Amount).HasPrecision(18,2);
            ac.Property(p => p.BaseAmount).HasPrecision(18,2);
            ac.Property(p => p.Percentage).HasPrecision(5,2);
            ac.Property(p => p.ReasonText).HasMaxLength(100);
            ac.Property(p => p.VatCategory).HasConversion(VatCategory5305.Converter).HasMaxLength(3);
            ac.ToTable("DocumentCharges");
        });

        inv.OwnsMany(p => p.VatBreakdowns, vb =>
        {
            vb.WithOwner().HasForeignKey("InvoiceId");
            vb.Property(p => p.VatCategory).HasConversion(VatCategory5305.Converter).HasMaxLength(3);
            vb.Property(p => p.VatRate).HasPrecision(5,2);
            vb.Property(p => p.TaxableAmount).HasPrecision(18,2);
            vb.Property(p => p.TaxAmount).HasPrecision(18,2);
            vb.Property(p => p.ExemptionReason).HasMaxLength(100);
            vb.ToTable("VatBreakdowns");
        });

        inv.OwnsMany(p => p.Lines, line =>
        {
            line.WithOwner().HasForeignKey("InvoiceId");
            line.Property(p => p.LineId).HasMaxLength(60);
            line.Property(p => p.Note).HasMaxLength(300);
            line.Property(p => p.Quantity).HasPrecision(18,6);
            line.Property(p => p.UnitCode).HasMaxLength(8);
            line.Property(p => p.LineExtensionAmount).HasPrecision(18,2);
            line.Property(p => p.Vat).HasConversion(VatCategory5305.Converter).HasMaxLength(3);
            line.Property(p => p.VatRate).HasPrecision(5,2);
            line.Property(p => p.ItemName).HasMaxLength(200);
            line.Property(p => p.ItemDescription).HasMaxLength(200);
            line.Property(p => p.OriginCountryCode).HasMaxLength(2);

            line.OwnsOne(p => p.Price, price =>
            {
                price.Property(p => p.NetUnitPrice).HasPrecision(18,6);
                price.Property(p => p.PriceDiscount).HasPrecision(18,2);
                price.Property(p => p.GrossUnitPrice).HasPrecision(18,6);
                price.Property(p => p.BaseQuantity).HasPrecision(18,6);
                price.Property(p => p.BaseUom).HasMaxLength(8);
            });

            line.OwnsMany(p => p.Attributes, attr =>
            {
                attr.WithOwner().HasForeignKey("InvoiceLineId");
                attr.Property(p => p.Name).HasMaxLength(50);
                attr.Property(p => p.Description).HasMaxLength(100);
                attr.ToTable("LineAttributes");
            });

            line.OwnsMany(p => p.Allowances, ac =>
            {
                ac.WithOwner().HasForeignKey("InvoiceLineId");
                ac.Property(p => p.Amount).HasPrecision(18,2);
                ac.Property(p => p.BaseAmount).HasPrecision(18,2);
                ac.Property(p => p.Percentage).HasPrecision(5,2);
                ac.Property(p => p.ReasonText).HasMaxLength(100);
                ac.Property(p => p.ReasonCode).HasConversion(ReasonCode7161.Converter).HasMaxLength(4);
                ac.ToTable("LineAllowances");
            });

            line.OwnsMany(p => p.Charges, ac =>
            {
                ac.WithOwner().HasForeignKey("InvoiceLineId");
                ac.Property(p => p.Amount).HasPrecision(18,2);
                ac.Property(p => p.BaseAmount).HasPrecision(18,2);
                ac.Property(p => p.Percentage).HasPrecision(5,2);
                ac.Property(p => p.ReasonText).HasMaxLength(100);
                ac.Property(p => p.ReasonCode).HasConversion(ReasonCode7161.Converter).HasMaxLength(4);
                ac.ToTable("LineCharges");
            });

            line.ToTable("InvoiceLines");
        });

        inv.OwnsMany(p => p.PrecedingInvoices, pre =>
        {
            pre.WithOwner().HasForeignKey("InvoiceId");
            pre.Property(p => p.ReferenceNumber).HasMaxLength(30);
            pre.ToTable("PrecedingInvoices");
        });
    }

    private static void ConfigureParty(this OwnedNavigationBuilder<Invoice, Party> b)
    {
        b.Property(p => p.Name).HasMaxLength(200);
        b.Property(p => p.AdditionalLegalInfo).HasMaxLength(1000);
        b.Property(p => p.LegalRegistrationId).HasMaxLength(50);
        b.Property(p => p.VatIdentifier).HasMaxLength(20);

        b.OwnsOne(p => p.Address, a =>
        {
            a.Property(p => p.Line1).HasMaxLength(150);
            a.Property(p => p.Line2).HasMaxLength(100);
            a.Property(p => p.City).HasMaxLength(50);
            a.Property(p => p.PostalCode).HasMaxLength(20);
            a.Property(p => p.CountrySubdivision).HasMaxLength(50);
            a.Property(p => p.CountryCode).HasMaxLength(2);
        });

        b.OwnsOne(p => p.Contact, c =>
        {
            c.Property(p => p.Name).HasMaxLength(100);
            c.Property(p => p.Telephone).HasMaxLength(100);
            c.Property(p => p.Email).HasMaxLength(100);
        });
    }
}

// ============================== Codes/Base.cs ===============================
using System;using System.Collections.Generic;using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EfFactura.Codes;

/// <summary>
/// Base class for extensible code lists (SmartEnum-like). Stores canonical string Code.
/// </summary>
public abstract class CodeSet<T> where T : CodeSet<T>
{
    public string Code { get; }
    public string Name { get; }

    protected CodeSet(string code, string name)
    {
        Code = code; Name = name;
    }

    protected static readonly Dictionary<string, T> _byCode = new(StringComparer.OrdinalIgnoreCase);

    protected static T Register(T item)
    {
        _byCode[item.Code] = item; return item;
    }

    public static bool TryFrom(string? code, out T? value)
    {
        if (code is null) { value = null; return false; }
        if (_byCode.TryGetValue(code, out var v)) { value = v; return true; }
        value = null; return false;
    }

    public static T FromOrUnknown(string? code, Func<string, T> unknownFactory)
        => TryFrom(code, out var v) && v is not null ? v : unknownFactory(code ?? string.Empty);

    public override string ToString() => Code;
}

// ============================== Codes/InvoiceTypeCode1001.cs ================
namespace EfFactura.Codes;

public sealed class InvoiceTypeCode1001 : CodeSet<InvoiceTypeCode1001>
{
    private InvoiceTypeCode1001(string code, string name) : base(code, name) {}

    // RO-admitted set (untdid 1001 subset)
    public static readonly InvoiceTypeCode1001 Invoice_380 = Register(new("380", "Invoice"));
    public static readonly InvoiceTypeCode1001 CreditNote_381 = Register(new("381", "Credit note"));
    public static readonly InvoiceTypeCode1001 CorrectedInvoice_384 = Register(new("384", "Corrected invoice"));
    public static readonly InvoiceTypeCode1001 DebitNote_389 = Register(new("389", "Debit note"));
    public static readonly InvoiceTypeCode1001 SelfBilledInvoice_751 = Register(new("751", "Self-billed invoice"));

    // Converter for EF
    public static readonly ValueConverter<InvoiceTypeCode1001, string> Converter =
        new(v => v.Code, v => FromOrUnknown(v, code => new(code, $"UNKNOWN({code})")));
}

// ============================== Codes/PaymentMeansCode4461.cs ===============
namespace EfFactura.Codes;

public sealed class PaymentMeansCode4461 : CodeSet<PaymentMeansCode4461>
{
    private PaymentMeansCode4461(string code, string name) : base(code, name) {}

    // Common ones (not exhaustive); extend as needed
    public static readonly PaymentMeansCode4461 Unspecified = Register(new("", "Unspecified"));
    public static readonly PaymentMeansCode4461 CreditTransfer_30 = Register(new("30", "Credit transfer"));
    public static readonly PaymentMeansCode4461 DirectDebit_49 = Register(new("49", "Direct debit"));
    public static readonly PaymentMeansCode4461 PaymentCard_48 = Register(new("48", "Payment card"));
    public static readonly PaymentMeansCode4461 SEPACreditTransfer_31 = Register(new("31", "SEPA credit transfer"));

    public static readonly ValueConverter<PaymentMeansCode4461, string> Converter =
        new(v => v.Code, v => FromOrUnknown(v, code => new(code, $"UNKNOWN({code})")));
}

// ============================== Codes/VatCategory5305.cs ====================
namespace EfFactura.Codes;

public sealed class VatCategory5305 : CodeSet<VatCategory5305>
{
    private VatCategory5305(string code, string name) : base(code, name) {}

    // Typical EN 16931 categories (subset; extend if needed)
    public static readonly VatCategory5305 StandardRated = Register(new("S", "Standard rated"));
    public static readonly VatCategory5305 ZeroRated = Register(new("Z", "Zero rated"));
    public static readonly VatCategory5305 Exempt = Register(new("E", "VAT exempt"));
    public static readonly VatCategory5305 ReverseCharge = Register(new("AE", "Reverse charge"));
    public static readonly VatCategory5305 IntraCommunitySupply = Register(new("K", "Intra-Community supply"));
    public static readonly VatCategory5305 ExportOutsideEU = Register(new("G", "Export outside EU"));
    public static readonly VatCategory5305 OutOfScope = Register(new("O", "Out of scope of VAT"));

    public static readonly ValueConverter<VatCategory5305, string> Converter =
        new(v => v.Code, v => FromOrUnknown(v, code => new(code, $"UNKNOWN({code})")));
}

// ============================== Codes/ReasonCode7161.cs =====================
namespace EfFactura.Codes;

public sealed class ReasonCode7161 : CodeSet<ReasonCode7161>
{
    private ReasonCode7161(string code, string name) : base(code, name) {}

    // Common allowance/charge reasons (7161) – extend per need
    public static readonly ReasonCode7161 Discount = Register(new("95", "Discount"));
    public static readonly ReasonCode7161 Surcharge = Register(new("AA", "Surcharge"));
    public static readonly ReasonCode7161 Freight = Register(new("FC", "Freight"));
    public static readonly ReasonCode7161 Packaging = Register(new("PK", "Packaging"));

    public static readonly ValueConverter<ReasonCode7161, string> Converter =
        new(v => v.Code, v => FromOrUnknown(v, code => new(code, $"UNKNOWN({code})")));
}

// ============================== Dto/InvoiceDto.cs ===========================
using EfFactura.Codes;

namespace EfFactura.Dto;

public sealed class InvoiceDto
{
    // BG-1
    public string CustomizationId { get; set; } = default!;         // BT-24
    public string Number { get; set; } = default!;                  // BT-1
    public DateOnly IssueDate { get; set; }                         // BT-2
    public InvoiceTypeCode1001 TypeCode { get; set; } = InvoiceTypeCode1001.Invoice_380; // BT-3
    public string CurrencyCode { get; set; } = default!;            // BT-5
    public string? VatCurrencyCode { get; set; }                    // BT-6
    public DateOnly? VatPointDate { get; set; }                     // BT-7
    public string? VatPointDateCode { get; set; }                   // BT-8
    public DateOnly? DueDate { get; set; }                          // BT-9
    public string? PaymentTermsText { get; set; }                   // BT-20

    // Parties
    public PartyDto Seller { get; set; } = new();
    public PartyDto Buyer { get; set; } = new();
    public PartyDto? Payee { get; set; }
    public PartyDto? TaxRepresentative { get; set; }

    // Delivery/Period
    public DateOnly? ActualDeliveryDate { get; set; }
    public string? DeliveryNoteReference { get; set; }
    public DateOnly? InvoicingPeriodStart { get; set; }
    public DateOnly? InvoicingPeriodEnd { get; set; }

    // Payment
    public PaymentDto Payment { get; set; } = new();

    // Collections
    public List<DocAllowanceChargeDto> Allowances { get; set; } = new();
    public List<DocAllowanceChargeDto> Charges { get; set; } = new();
    public List<VatBreakdownDto> VatBreakdowns { get; set; } = new();
    public List<InvoiceLineDto> Lines { get; set; } = new();
    public List<PrecedingInvoiceRefDto> PrecedingInvoices { get; set; } = new();

    // Totals
    public decimal SumOfLineNet { get; set; }
    public decimal? SumOfAllowances { get; set; }
    public decimal? SumOfCharges { get; set; }
    public decimal TotalWithoutVat { get; set; }
    public decimal? TotalVatAmount { get; set; }
    public decimal TotalWithVat { get; set; }
    public decimal AmountDue { get; set; }
}

public sealed class PartyDto
{
    public string Name { get; set; } = default!;
    public string? AdditionalLegalInfo { get; set; }
    public string? LegalRegistrationId { get; set; }
    public string? VatIdentifier { get; set; }

    public AddressDto Address { get; set; } = new();
    public ContactDto? Contact { get; set; }
}

public sealed class AddressDto
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = default!;
    public string? CountrySubdivision { get; set; }
}

public sealed class ContactDto
{
    public string? Name { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
}

public sealed class PaymentDto
{
    public PaymentMeansCode4461 PaymentMeansCode { get; set; } = PaymentMeansCode4461.Unspecified;
    public string? PaymentMeansText { get; set; }
    public string? RemittanceInformation { get; set; }

    public List<CreditTransferDto> CreditTransfers { get; set; } = new();
    public PaymentCardDto? PaymentCard { get; set; }
    public DirectDebitDto? DirectDebit { get; set; }
}

public sealed class CreditTransferDto
{
    public string Iban { get; set; } = default!;
    public string? AccountName { get; set; }
    public string? Bic { get; set; }
}

public sealed class PaymentCardDto { public string? HolderName { get; set; } public string? CardId { get; set; } }
public sealed class DirectDebitDto { public string? MandateId { get; set; } }

public sealed class DocAllowanceChargeDto
{
    public bool IsCharge { get; set; }
    public decimal Amount { get; set; }
    public decimal? BaseAmount { get; set; }
    public decimal? Percentage { get; set; }
    public VatCategory5305 VatCategory { get; set; } = VatCategory5305.StandardRated;
    public decimal? VatRate { get; set; }
    public string? ReasonText { get; set; }
}

public sealed class VatBreakdownDto
{
    public VatCategory5305 VatCategory { get; set; } = VatCategory5305.StandardRated;
    public decimal? VatRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string? ExemptionReason { get; set; }
}

public sealed class InvoiceLineDto
{
    public string LineId { get; set; } = default!;
    public string? Note { get; set; }
    public decimal Quantity { get; set; }
    public string UnitCode { get; set; } = default!;
    public decimal LineExtensionAmount { get; set; }

    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }

    public decimal NetUnitPrice { get; set; }
    public decimal? PriceDiscount { get; set; }
    public decimal? GrossUnitPrice { get; set; }
    public decimal? PriceBaseQuantity { get; set; }
    public string? PriceBaseUom { get; set; }

    public VatCategory5305 Vat { get; set; } = VatCategory5305.StandardRated;
    public decimal? VatRate { get; set; }

    public string ItemName { get; set; } = default!;
    public string? ItemDescription { get; set; }
    public string? OriginCountryCode { get; set; }

    public List<ItemAttributeDto> Attributes { get; set; } = new();
    public List<LineAllowanceChargeDto> Allowances { get; set; } = new();
    public List<LineAllowanceChargeDto> Charges { get; set; } = new();
}

public sealed class ItemAttributeDto { public string Name { get; set; } = default!; public string? Description { get; set; } }
public sealed class LineAllowanceChargeDto
{
    public bool IsCharge { get; set; }
    public decimal Amount { get; set; }
    public decimal? BaseAmount { get; set; }
    public decimal? Percentage { get; set; }
    public ReasonCode7161? ReasonCode { get; set; }
    public string? ReasonText { get; set; }
}

public sealed class PrecedingInvoiceRefDto { public string ReferenceNumber { get; set; } = default!; public DateOnly? IssueDate { get; set; } }

// ============================== Ubl/XmlHelpers.cs ===========================
using System.Xml.Linq;using System.Globalization;

namespace EfFactura.Ubl;

public static class XmlNs
{
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
}

internal static class XmlExt
{
    public static string? Val(this XElement? e, XName name) => e?.Element(name)?.Value;
    public static IEnumerable<XElement> Els(this XElement? e, XName name) => e?.Elements(name) ?? Enumerable.Empty<XElement>();
    public static XElement? El(this XElement? e, XName name) => e?.Element(name);

    public static DateOnly? DateOnlyVal(this XElement? e, XName name)
    {
        var v = e?.Element(name)?.Value; if (string.IsNullOrWhiteSpace(v)) return null;
        if (DateOnly.TryParse(v, out var d)) return d;
        if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)) return DateOnly.FromDateTime(dt);
        return null;
    }

    public static decimal? Dec(this XElement? e, XName name)
    {
        var v = e?.Element(name)?.Value; if (string.IsNullOrWhiteSpace(v)) return null;
        if (decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
        return null;
    }
}

// ============================== Ubl/UblParser.cs ============================
using EfFactura.Dto;using EfFactura.Codes;using static EfFactura.Ubl.XmlNs;using static EfFactura.Ubl.XmlExt;

namespace EfFactura.Ubl;

public static class UblParser
{
    public static InvoiceDto ParseInvoice(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("UBL Invoice root missing");

        var dto = new InvoiceDto
        {
            CustomizationId = root.Val(Cbc + "CustomizationID") ?? string.Empty,
            Number = root.Val(Cbc + "ID") ?? string.Empty,
            IssueDate = root.DateOnlyVal(Cbc + "IssueDate") ?? default,
            TypeCode = InvoiceTypeCode1001.FromOrUnknown(root.Val(Cbc + "InvoiceTypeCode"), c => new(c, $"UNKNOWN({c})")),
            CurrencyCode = root.Val(Cbc + "DocumentCurrencyCode") ?? string.Empty,
            VatCurrencyCode = root.Val(Cbc + "TaxCurrencyCode"),
            VatPointDate = root.DateOnlyVal(Cbc + "TaxPointDate"),
            DueDate = root.DateOnlyVal(Cbc + "DueDate"),
            PaymentTermsText = root.El(Cac + "PaymentTerms")?.Val(Cbc + "Note"),
        };

        // Parties
        var seller = root.El(Cac + "AccountingSupplierParty")?.El(Cac + "Party");
        dto.Seller = ParseParty(seller);

        var buyer = root.El(Cac + "AccountingCustomerParty")?.El(Cac + "Party");
        dto.Buyer = ParseParty(buyer);

        var payee = root.El(Cac + "PayeeParty");
        if (payee is not null) dto.Payee = ParseParty(payee);

        var taxRep = root.El(Cac + "TaxRepresentativeParty");
        if (taxRep is not null) dto.TaxRepresentative = ParseParty(taxRep);

        // Delivery
        var delivery = root.El(Cac + "Delivery");
        if (delivery is not null)
        {
            dto.ActualDeliveryDate = delivery.DateOnlyVal(Cbc + "ActualDeliveryDate");
            dto.DeliveryNoteReference = delivery.Val(Cbc + "ID");
        }

        // Period
        var period = root.El(Cac + "InvoicePeriod");
        if (period is not null)
        {
            dto.InvoicingPeriodStart = period.DateOnlyVal(Cbc + "StartDate");
            dto.InvoicingPeriodEnd = period.DateOnlyVal(Cbc + "EndDate");
        }

        // Payment Means / Terms
        dto.Payment = ParsePayment(root);

        // Allowance/Charge at document level
        foreach (var ac in root.Els(Cac + "AllowanceCharge"))
        {
            var isCharge = string.Equals(ac.Val(Cbc + "ChargeIndicator"), "true", StringComparison.OrdinalIgnoreCase);
            var item = new DocAllowanceChargeDto
            {
                IsCharge = isCharge,
                Amount = ac.Dec(Cbc + "Amount") ?? 0m,
                BaseAmount = ac.Dec(Cbc + "BaseAmount"),
                Percentage = ac.Dec(Cbc + "MultiplierFactorNumeric"),
                ReasonText = ac.Val(Cbc + "AllowanceChargeReason")
            };
            var taxCategory = ac.El(Cac + "TaxCategory");
            if (taxCategory is not null)
            {
                item.VatCategory = VatCategory5305.FromOrUnknown(taxCategory.Val(Cbc + "ID"), c => new(c, $"UNKNOWN({c})"));
                item.VatRate = taxCategory.Dec(Cbc + "Percent");
            }
            (isCharge ? dto.Charges : dto.Allowances).Add(item);
        }

        // Tax Total / Breakdown
        foreach (var sub in root.El(Cac + "TaxTotal")?.Els(Cac + "TaxSubtotal") ?? Enumerable.Empty<XElement>())
        {
            var cat = sub.El(Cac + "TaxCategory");
            dto.VatBreakdowns.Add(new VatBreakdownDto
            {
                VatCategory = VatCategory5305.FromOrUnknown(cat.Val(Cbc + "ID"), c => new(c, $"UNKNOWN({c})")),
                VatRate = cat.Dec(Cbc + "Percent"),
                TaxableAmount = sub.Dec(Cbc + "TaxableAmount") ?? 0m,
                TaxAmount = sub.Dec(Cbc + "TaxAmount") ?? 0m,
                ExemptionReason = cat.Val(Cbc + "TaxExemptionReason")
            });
        }

        // Lines
        foreach (var il in root.Els(Cac + "InvoiceLine"))
        {
            var qtyEl = il.El(Cbc + "InvoicedQuantity");
            var price = il.El(Cac + "Price");
            var item = il.El(Cac + "Item");
            var taxCat = item?.El(Cac + "ClassifiedTaxCategory");

            var line = new InvoiceLineDto
            {
                LineId = il.Val(Cbc + "ID") ?? string.Empty,
                Note = il.Val(Cbc + "Note"),
                Quantity = qtyEl?.Value is { } qv && decimal.TryParse(qv, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : 0m,
                UnitCode = qtyEl?.Attribute("unitCode")?.Value ?? string.Empty,
                LineExtensionAmount = il.Dec(Cbc + "LineExtensionAmount") ?? 0m,
                PeriodStart = il.El(Cac + "InvoicePeriod").DateOnlyVal(Cbc + "StartDate"),
                PeriodEnd = il.El(Cac + "InvoicePeriod").DateOnlyVal(Cbc + "EndDate"),
                NetUnitPrice = price.Dec(Cbc + "PriceAmount") ?? 0m,
                PriceDiscount = price.Dec(Cbc + "AllowanceChargeReasonCode"), // Often discount is via AllowanceCharge at line; keep null here
                GrossUnitPrice = null,
                PriceBaseQuantity = price.Dec(Cbc + "BaseQuantity"),
                PriceBaseUom = price.El(Cbc + "BaseQuantity")?.Attribute("unitCode")?.Value,
                Vat = VatCategory5305.FromOrUnknown(taxCat.Val(Cbc + "ID"), c => new(c, $"UNKNOWN({c})")),
                VatRate = taxCat.Dec(Cbc + "Percent"),
                ItemName = item?.Val(Cbc + "Name") ?? string.Empty,
                ItemDescription = item?.Val(Cbc + "Description"),
                OriginCountryCode = item?.El(Cac + "OriginCountry")?.Val(Cbc + "IdentificationCode")
            };

            // Item attributes (BG-32)
            foreach (var att in item?.Els(Cac + "AdditionalItemProperty") ?? Enumerable.Empty<XElement>())
            {
                line.Attributes.Add(new ItemAttributeDto
                {
                    Name = att.Val(Cbc + "Name") ?? string.Empty,
                    Description = att.Val(Cbc + "Value")
                });
            }

            // Line Allowance/Charge
            foreach (var lac in il.Els(Cac + "AllowanceCharge"))
            {
                var isCharge = string.Equals(lac.Val(Cbc + "ChargeIndicator"), "true", StringComparison.OrdinalIgnoreCase);
                var lacDto = new LineAllowanceChargeDto
                {
                    IsCharge = isCharge,
                    Amount = lac.Dec(Cbc + "Amount") ?? 0m,
                    BaseAmount = lac.Dec(Cbc + "BaseAmount"),
                    Percentage = lac.Dec(Cbc + "MultiplierFactorNumeric"),
                    ReasonText = lac.Val(Cbc + "AllowanceChargeReason"),
                    ReasonCode = ReasonCode7161.FromOrUnknown(lac.Val(Cbc + "AllowanceChargeReasonCode"), c => new(c, $"UNKNOWN({c})"))
                };
                (isCharge ? line.Charges : line.Allowances).Add(lacDto);
            }

            dto.Lines.Add(line);
        }

        // Totals
        var lmt = root.El(Cac + "LegalMonetaryTotal");
        if (lmt is not null)
        {
            dto.SumOfLineNet = lmt.Dec(Cbc + "LineExtensionAmount") ?? 0m;
            dto.TotalWithoutVat = lmt.Dec(Cbc + "TaxExclusiveAmount") ?? 0m;
            dto.TotalWithVat = lmt.Dec(Cbc + "TaxInclusiveAmount") ?? 0m;
            dto.AmountDue = lmt.Dec(Cbc + "PayableAmount") ?? 0m;
        }

        // Preceding invoices
        foreach (var docRef in root.Els(Cac + "BillingReference").Select(e => e.El(Cac + "InvoiceDocumentReference")).Where(e => e != null)!)
        {
            dto.PrecedingInvoices.Add(new PrecedingInvoiceRefDto
            {
                ReferenceNumber = docRef!.Val(Cbc + "ID") ?? string.Empty,
                IssueDate = docRef.DateOnlyVal(Cbc + "IssueDate")
            });
        }

        return dto;
    }

    private static PartyDto ParseParty(XElement? party)
    {
        if (party == null) return new PartyDto { Name = string.Empty, Address = new AddressDto { CountryCode = "" } };

        var name = party.Val(Cac + "PartyName") ?? party.Val(Cac + "Name") ?? string.Empty;
        var addr = party.El(Cac + "PostalAddress");
        var contact = party.El(Cac + "Contact");

        return new PartyDto
        {
            Name = name,
            AdditionalLegalInfo = party.El(Cac + "PartyLegalEntity")?.Val(Cbc + "CompanyLegalForm"),
            LegalRegistrationId = party.El(Cac + "PartyLegalEntity")?.Val(Cbc + "CompanyID"),
            VatIdentifier = party.El(Cac + "PartyTaxScheme")?.Val(Cbc + "CompanyID"),
            Address = new AddressDto
            {
                Line1 = addr.Val(Cbc + "StreetName") ?? addr.Val(Cbc + "AddressLine") ?? addr.El(Cac + "AddressLine")?.Val(Cbc + "Line"),
                Line2 = addr.Val(Cbc + "AdditionalStreetName"),
                City = addr.Val(Cbc + "CityName"),
                PostalCode = addr.Val(Cbc + "PostalZone"),
                CountrySubdivision = addr.Val(Cbc + "CountrySubentity"),
                CountryCode = addr.El(Cac + "Country")?.Val(Cbc + "IdentificationCode") ?? string.Empty
            },
            Contact = contact == null ? null : new ContactDto
            {
                Name = contact.Val(Cbc + "Name"),
                Telephone = contact.Val(Cbc + "Telephone"),
                Email = contact.Val(Cbc + "ElectronicMail")
            }
        };
    }

    private static PaymentDto ParsePayment(XElement root)
    {
        var pm = root.El(Cac + "Payment....