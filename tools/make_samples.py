#!/usr/bin/env python3
"""Generate the invented eval set (eval/) and demo samples (samples/) for Clio.

All borrowers, amounts and dates are invented. Run: python3 tools/make_samples.py
Writes .txt files and simple text-layer PDFs (Helvetica, FlateDecode), plus eval/labels.json.
"""
import json, zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BREAK = "--- page break ---"

# (file, text, labels{loanAmount, margin, maturity}); None = the document does not state it.
DOCS = [
 ("01-kestrel-bay.txt", """INDICATIVE TERM SHEET
Borrower: Kestrel Bay Logistics (Pty) Ltd
Facility: Senior secured term loan
Facility Amount: ZAR 48,500,000
Margin: 2.85% per annum
Final Maturity Date: 30 June 2031""", ("ZAR 48500000.00", "2.85", "2031-06-30")),
 ("02-amberfield.pdf", f"""INDICATIVE TERM SHEET
Borrower: Amberfield Farms Ltd
Purpose: irrigation and cold storage
{BREAK}
Key terms
Loan Amount: USD 3,000,000.00
Margin: 3.10%
Maturity Date: 2030-03-31""", ("USD 3000000.00", "3.10", "2030-03-31")),
 ("03-quillon.txt", """TERM SHEET - REVOLVING CREDIT FACILITY
Borrower: Quillon Retail Group
Commitment: ZAR 120,000,000
Pricing: 3-month JIBAR plus 245 basis points
Termination Date: 15 September 2029""", ("ZAR 120000000.00", "2.45", "2029-09-15")),
 ("04-marula.txt", """SUMMARY OF TERMS
Borrower: Marula Ridge Estates
Facility Amount: R 8.4 million
Margin: 4.00 per cent
Maturity Date: 1 December 2028""", ("ZAR 8400000.00", "4.00", "2028-12-01")),
 ("05-tidewater.txt", """INDICATIVE TERMS
Borrower: Tidewater Marine Services
Loan Amount: EUR 2,250,000
Margin: 3.5%
Tenor: 5 years from first utilisation""", ("EUR 2250000.00", "3.50", None)),
 ("06-silverbank.txt", """TERM SHEET
Borrower: Silverbank Clinics
Facility Amount: ZAR 22,000,000
Margin: 2.40%
Final Maturity Date: 31/03/2032""", ("ZAR 22000000.00", "2.40", "2032-03-31")),
 ("07-oakhaven.txt", """LETTER OF INTENT
Harbourline will make available a term loan facility of ZAR 15,000,000 to Oakhaven Packaging.
The loan bears interest at a margin of 2.6% above prime.
The facility is repayable in full on 28 February 2030.""", ("ZAR 15000000.00", "2.60", "2030-02-28")),
 ("08-blue-crane.pdf", f"""INDICATIVE TERM SHEET
Borrower: Blue Crane Solar
Project: 40 MW rooftop portfolio
Sponsor equity: 30%
{BREAK}
Financial terms
Facility Amount: ZAR 310,000,000
Margin: 3.40%
Final Maturity Date: 30 November 2038""", ("ZAR 310000000.00", "3.40", "2038-11-30")),
 ("09-granite-peak.txt", """TERM SHEET
Borrower               Granite Peak Quarries
Facility amount        ZAR 64 000 000
Margin                 2.25%
Maturity date          2031-11-30""", ("ZAR 64000000.00", "2.25", "2031-11-30")),
 ("10-fynbos.txt", """INDICATIVE TERM SHEET
Borrower: Fynbos Hospitality
Facility Amount: ZAR 18,750,000
Margin: 2.50% stepping up to 3.00% after year 3
Final Maturity Date: 31 May 2030""", ("ZAR 18750000.00", "2.50", "2030-05-31")),
 ("11-ironwood.txt", """TERM SHEET (REVISED)
Borrower: Ironwood Timber
Facility Amount: ZAR 30,000,000
Margin: 3.00%
Final Maturity Date: 30 April 2031
Amendment: the parties agreed a larger facility.
Facility Amount: ZAR 35,000,000""", ("ZAR 35000000.00", "3.00", "2031-04-30")),
 ("12-northgate.pdf", """INDICATIVE TERM SHEET
Borrower: Northgate Pharma UK Ltd
Principal Amount: GBP 5,750,000
Interest Margin: 3.75%
Final Repayment Date: 30 September 2027""", ("GBP 5750000.00", "3.75", "2027-09-30")),
 ("13-karoo-wind.txt", """SUMMARY OF PRINCIPAL TERMS
Borrower: Karoo Wind Partners
Total Commitments: ZAR 250,000,000
Margin: 195 bps
Maturity: 31 January 2033""", ("ZAR 250000000.00", "1.95", "2033-01-31")),
 ("14-harbour-mill.txt", """TERM SHEET (ARCHIVED)
Borrower: Harbour Mill Bakeries
Facility Amount: ZAR 6,000,000
Margin: 3.20%
Final Maturity Date: 30 June 2019""", ("ZAR 6000000.00", "3.20", "2019-06-30")),
 ("15-redwood.txt", """INDICATIVE TERM SHEET
Borrower: Redwood Data Centres Inc
Facility Amount: $ 12,000,000
Margin: SOFR + 3.25%
Maturity Date: June 30, 2031""", ("USD 12000000.00", "3.25", "2031-06-30")),
 ("16-baobab.txt", """FIXED RATE LOAN - INDICATIVE TERMS
Borrower: Baobab Cold Chain
Loan Amount: ZAR 9,500,000
Interest: fixed at 11.25% per annum
Maturity Date: 2029-08-31""", ("ZAR 9500000.00", None, "2029-08-31")),
 ("17-saltpan.txt", """TERM SHEET
BORROWER - Saltpan Minerals
FACILITY AMOUNT - ZAR 9,999,500.50
MARGIN - 5.125%
FINAL MATURITY DATE - 15 March 2030""", ("ZAR 9999500.50", "5.125", "2030-03-15")),
]

def pdf(pages):
    objs = {1: b"<< /Type /Catalog /Pages 2 0 R >>", 3: b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"}
    kids, nid = [], 4
    for lines in pages:
        body = "BT /F1 11 Tf 14 TL 72 760 Td\n" + "".join(
            "(" + l.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)") + ") Tj T*\n" for l in lines) + "ET\n"
        data = zlib.compress(body.encode("latin-1"))
        objs[nid] = b"<< /Length %d /Filter /FlateDecode >>\nstream\n" % len(data) + data + b"\nendstream"
        objs[nid + 1] = b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents %d 0 R >>" % nid
        kids.append(nid + 1); nid += 2
    objs[2] = ("<< /Type /Pages /Kids [%s] /Count %d >>" % (" ".join(f"{k} 0 R" for k in kids), len(kids))).encode()
    out = bytearray(b"%PDF-1.4\n")
    for i in sorted(objs):
        out += b"%d 0 obj\n" % i + objs[i] + b"\nendobj\n"
    out += b"trailer\n<< /Root 1 0 R /Size %d >>\n%%%%EOF\n" % (len(objs) + 1)
    return bytes(out)

def image_only_pdf():
    """A 'scanned' PDF: one page, no text layer (drawing operators only)."""
    data = b"q 600 0 0 800 0 0 cm /Im1 Do Q\n"
    objs = {1: b"<< /Type /Catalog /Pages 2 0 R >>", 2: b"<< /Type /Pages /Kids [4 0 R] /Count 1 >>",
            3: b"<< /Length %d >>\nstream\n" % len(data) + data + b"\nendstream",
            4: b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 3 0 R >>"}
    out = bytearray(b"%PDF-1.4\n")
    for i in sorted(objs):
        out += b"%d 0 obj\n" % i + objs[i] + b"\nendobj\n"
    return bytes(out + b"trailer\n<< /Root 1 0 R /Size 5 >>\n%%EOF\n")

def write(path, name, text):
    path.mkdir(parents=True, exist_ok=True)
    if name.endswith(".pdf"):
        pages = [p.strip("\n").split("\n") for p in text.split(BREAK)]
        (path / name).write_bytes(pdf(pages))
    else:
        (path / name).write_text(text + "\n", encoding="utf-8")

def main():
    docs = ROOT / "eval" / "documents"
    labels = []
    for name, text, (amount, margin, maturity) in DOCS:
        write(docs, name, text)
        labels.append({"file": name, "loanAmount": amount, "margin": margin, "maturity": maturity})
    (ROOT / "eval" / "labels.json").write_text(json.dumps(labels, indent=2) + "\n", encoding="utf-8")
    samples = ROOT / "samples"
    by_name = {n: t for n, t, _ in DOCS}
    write(samples, "kestrel-bay-term-sheet.txt", by_name["01-kestrel-bay.txt"])
    write(samples, "blue-crane-term-sheet.pdf", by_name["08-blue-crane.pdf"])
    write(samples, "oakhaven-letter-of-intent.txt", by_name["07-oakhaven.txt"])
    write(samples, "fynbos-term-sheet.txt", by_name["10-fynbos.txt"])
    (samples / "photo-scan-no-text.pdf").write_bytes(image_only_pdf())
    print(f"wrote {len(DOCS)} eval documents and 5 samples")

if __name__ == "__main__":
    main()
