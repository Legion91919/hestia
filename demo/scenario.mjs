// Scripted walkthrough for the README demo. Run: cd demo && npm run record
const PASSWORD = "demo-only-passphrase"; // invented, set in demo.config.json
const INVESTOR = "ayanda@kestrel.example.com";
const IR = "ir@harbourline.example.com";

async function signIn(page, email, typed = false) {
  if (!page.url().includes("/login")) await page.goto(new URL("/login", page.url()).href);
  await page.getByLabel("Email").fill(email);
  if (typed) await page.getByLabel("Password").pressSequentially(PASSWORD, { delay: 30 });
  else await page.getByLabel("Password").fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.waitForLoadState("networkidle");
}

export default async ({ page, step, shot }) => {
  const context = page.context();
  let investorCookies = [];

  await step("Hestia: an investor in the Kestrel Family Trust signs in to the portal", async () => {
    await signIn(page, INVESTOR, true);
    await page.getByRole("heading", { name: "Your documents" }).waitFor();
  });
  await shot("documents");
  await step("This quarter's statement is one tap away; the download is logged before the file is sent", async () => {
    const download = page.waitForEvent("download");
    await page.getByRole("link", { name: "Open statement" }).click();
    await download;
    investorCookies = await context.cookies();
  });
  await step("Investor relations opens the IR console", async () => {
    await context.clearCookies(); // keep the investor's session alive on the server for the proof below
    await signIn(page, IR);
    await page.getByRole("heading", { name: "Investor relations" }).waitFor();
  });
  await step("The access log shows who opened which statement, seconds ago: append-only and hash-chained", async () => {
    await page.getByRole("heading", { name: "Access log" }).scrollIntoViewIfNeeded();
  });
  await shot("access-log");
  await step("IR revokes the investor's access: the row now reads Revoked", async () => {
    await page.getByRole("heading", { name: "Investor users" }).scrollIntoViewIfNeeded();
    await page.getByRole("row", { name: new RegExp(INVESTOR.replace(/\./g, "\\.")) }).getByRole("button", { name: "Revoke access" }).click();
    await page.getByRole("status").waitFor();
    await page.getByRole("heading", { name: "Investor users" }).evaluate(e => e.scrollIntoView({ block: "center", behavior: "smooth" }));
  });
  await step("The investor's still-open session stops working on the very next request", async () => {
    await context.clearCookies();
    await context.addCookies(investorCookies);
    await page.goto(new URL("/documents", page.url()).href);
    await page.waitForLoadState("networkidle");
  });
  await step("Signing in again is refused too: access is withdrawn until IR restores it", async () => {
    await signIn(page, INVESTOR);
    await page.getByRole("alert").waitFor();
  });
  await shot("revoked");
};

export async function mobile({ page, shot }) {
  // Pieter (Marula Pension Fund) has access; the recording above revoked Ayanda.
  await signIn(page, "pieter@marula.example.com");
  await page.getByRole("heading", { name: "Your documents" }).waitFor();
  await shot("mobile");
}
