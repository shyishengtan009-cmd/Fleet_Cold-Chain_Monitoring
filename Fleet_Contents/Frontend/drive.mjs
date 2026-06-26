import { chromium } from "playwright";

const url = process.argv[2] || "http://localhost:5173/monitoring/tt19-fleet/dashboard";
const shotPath = process.argv[3] || "/tmp/fleet-shot.png";

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });

const consoleMsgs = [];
page.on("console", (msg) => consoleMsgs.push(`[${msg.type()}] ${msg.text()}`));
page.on("pageerror", (err) => consoleMsgs.push(`[pageerror] ${err.message}`));

await page.goto(url, { waitUntil: "networkidle", timeout: 30000 });
await page.waitForTimeout(3000); // let async data fetches resolve

await page.screenshot({ path: shotPath, fullPage: true });
console.log("SCREENSHOT:", shotPath);

const bodyText = await page.evaluate(() => document.body.innerText);
console.log("---BODY TEXT (first 2000 chars)---");
console.log(bodyText.slice(0, 2000));

console.log("---CONSOLE MESSAGES---");
console.log(consoleMsgs.join("\n") || "(none)");

await browser.close();
