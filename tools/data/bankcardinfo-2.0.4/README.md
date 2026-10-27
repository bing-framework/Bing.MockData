# bankcardinfo 2.0.4 source snapshot

This directory preserves the npm archive whose SHA-256 is pinned in `ImportLegacyBanks.py`, the versioned `legacy-banks-baseline.json` comparison input, and the three source files used for the conversion: `package/index.js`, `package/package.json`, and `package/README.md`. The archive is the publication artifact; the extracted files are checked byte-for-byte against it by the converter. The ignored artifact copy at `artifacts/bank-resource-closure/legacy-banks-baseline.json`, when present, is checked against the versioned baseline and is never overwritten.

The package metadata declares the ISC license and names `navyxie` in its author field. The published archive has no standalone license file and no copyright-holder/year notice. The package author is recorded for attribution only and is not treated as proof of copyright ownership. The standard ISC terms included in `src/Bing.MockData/Datas/Notices/bankcardinfo-ISC.txt` are supplemental text, not a file copied from upstream.

`ImportLegacyBanks.py` statically parses only the `bankcardList` literal. It does not execute JavaScript or call the package's runtime API or Alipay fallback. Conversion counts, full-key differences, source locations, coverage, and closure checks are recorded in `artifacts/bank-resource-closure/banks-conversion.json`.
