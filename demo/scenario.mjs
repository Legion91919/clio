// Scripted walkthrough for the README demo. Run: node demo/record.mjs .
// Story: upload an invented term sheet, see fields with confidence and source line, approve the low-confidence one from the review queue.
import { fileURLToPath } from "node:url";
const sample = fileURLToPath(new URL("./fixtures/sable-ridge-term-sheet.txt", import.meta.url));
const FILE = "sable-ridge-term-sheet.txt";

export default async ({ page, step, shot, pause }) => {
  await step("Clio: a lender's analyst signs in and sees every term sheet with its status", async () => {
    await page.getByLabel("Password").fill("local-demo-only-pw"); // Development-only demo password (demo.config.json)
    await page.getByRole("button", { name: "Sign in" }).click();
    await page.getByRole("region", { name: "Review queue" }).waitFor();
    await page.getByRole("button", { name: "kestrel-bay-term-sheet.txt" }).click();
  });

  await step("Upload a new term sheet: a background worker reads it, no waiting on the request", async () => {
    await page.getByLabel(/Upload a term sheet/).setInputFiles(sample);
    await page.getByText(FILE, { exact: true }).first().waitFor();
    await page.getByRole("row", { name: /Loan amount|Facility amount|Amount/i }).getByRole("button", { name: /line/ }).waitFor({ timeout: 20000 });
  });

  await step("Every field has a confidence score and links to the exact page and line it came from", async () => {
    await page.getByRole("row", { name: /Maturity/ }).getByRole("button", { name: /line/ }).click();
    await page.getByRole("heading", { name: /^Page 1 · line/ }).waitFor();
    await page.evaluate(() => window.scrollTo(0, 0));
  });
  await shot("extracted");

  await step("Proof: the step-up margin scores 0.60, under the 0.90 bar, so it waits in the review queue", async () => {
    const queue = page.getByRole("region", { name: "Review queue" });
    await queue.scrollIntoViewIfNeeded();
    await queue.getByRole("button", { name: FILE }).click();
  });
  await shot("review-queue");

  await step("The analyst checks the source line, then approves; the queue shrinks by one", async () => {
    await page.getByRole("row", { name: /Margin/ }).getByRole("button", { name: /line/ }).click();
    await page.getByRole("heading", { name: /^Page 1 · line/ }).waitFor();
    await page.evaluate(() => window.scrollTo(0, 0));
    await pause(1200);
    await page.getByRole("row", { name: /Margin/ }).getByRole("button", { name: "Approve" }).click();
    await page.getByRole("row", { name: /Margin/ }).getByText("Approved by you").waitFor();
  });
  await shot("approved");
};

export async function mobile({ page, shot, pause }) {
  await page.getByLabel("Password").fill("local-demo-only-pw");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.getByRole("region", { name: "Review queue" }).waitFor();
  await pause(800);
  await shot("mobile");
}
