# binlist-data

An open dataset of card **BIN/IIN ranges** — the first six digits of a payment card number — with the issuing bank, card scheme, funding type, product level and issuing country for each range. It powers [binlist.io](https://binlist.io/), a free card-issuer verification tool for merchants, risk teams and payment developers.

**Licence: CC BY 4.0.** This dataset is a merge of two CC BY 4.0 sources, both credited below. Use it freely with attribution.

## Files

| File | What it is |
|---|---|
| `bins.csv` | The dataset. One row per BIN. |
| `conflicts.csv` | Every field where the two sources disagreed, and which value was chosen. |
| `summary.json` | Counts from the last merge. |
| `merge.py` | The merge script. Deterministic; re-run it to reproduce `bins.csv`. |

## Columns

| Column | Meaning |
|---|---|
| `bin` | The BIN/IIN, 6 digits (a small number of legacy 5-digit ranges are retained). |
| `brand` | Card scheme: VISA, MASTERCARD, AMERICAN EXPRESS, DISCOVER, JCB, DINERS CLUB, UNIONPAY, PRIVATE LABEL, … |
| `type` | Funding type: CREDIT, DEBIT, CHARGE CARD, or blank when not on file. |
| `category` | Issuer product tier (CLASSIC, PLATINUM, BUSINESS, PREPAID, …) or blank. |
| `issuer` | Issuing institution, or blank. |
| `issuer_phone`, `issuer_url` | The issuer's public contact details where recorded. |
| `alpha_2`, `alpha_3`, `country` | Issuing country, ISO 3166. |
| `source` | Provenance: `iannuttall`, `venelinkochev`, `merged` (present in both), or `correction`. |
| `seen` | The date the record was last confirmed by its source. |

## Sources and attribution

- **[iannuttall/binlist-data](https://github.com/iannuttall/binlist-data)** by Ian Nuttall, CC BY 4.0. Last updated 24 December 2020; the repository is archived.
- **[venelinkochev/bin-list-data](https://github.com/venelinkochev/bin-list-data)** by Venelin Kochev, CC BY 4.0. Last updated 11 February 2025.

Where the two sources disagree on a field, the merge takes the **newer** source and records the disagreement in `conflicts.csv`. Where one source is blank, the other fills it. Nothing is invented.

## Accuracy

This is aggregated public data. It is not licensed from any card network and it is not exhaustive: BIN ranges are reassigned, banks merge and rebrand, and eight-digit BINs are not covered by any open source. Do not rely on it as the sole basis for a payment, fraud or compliance decision. See the [disclaimers](https://binlist.io/pages/disclaimers/).

## Corrections

Open an issue using the **Correction** template with the BIN and what you believe is right. Corrections from issuing institutions are actioned first and become rows with `source = correction`.

## Updates

A scheduled workflow re-runs the merge against the upstream sources monthly and opens a pull request with the diff.

Maintained by Techbuddie Solutions · [binlist.io](https://binlist.io/) · info@techbuddie.org
