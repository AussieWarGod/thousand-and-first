// Run against an owned local workbench. Uses an explicitly supplied Playwright install/browser.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");

async function main() {
  const [modulePath, browserPath, url, output] = process.argv.slice(2);
  if (!modulePath || !browserPath || !url || !output) {
    throw Error("Usage: node Tools/layout-studio-browser-check.cjs PLAYWRIGHT_CORE CHROMIUM URL FRESH_OUTPUT");
  }
  const parsed = new URL(url);
  assert.equal(parsed.hostname, "127.0.0.1");
  fs.mkdirSync(output, { recursive: false });
  const { chromium } = require(path.resolve(modulePath));
  const browser = await chromium.launch({ executablePath: browserPath, headless: true });
  const errors = [];
  let page;
  try {
    page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
    page.on("pageerror", (error) => errors.push(error.message));
    await page.goto(url);
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("sleep fixtures"));
    const options = await page.locator("#case option").evaluateAll((items) =>
      items.map((item) => ({ value: item.value, text: item.textContent })));
    const tent = options.find((item) => item.text.includes("tentrow / S /"));
    assert.ok(tent, "actual starter tentrow must be available");
    await page.selectOption("#case", tent.value);
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("0/3 sleep fixtures"));
    const source = await page.inputValue("#xml");
    const draft = fs.readFileSync(path.join(__dirname, "layout-examples/enclosed-canvas-room.xml"), "utf8");
    await page.fill("#xml", draft);
    await page.click("#apply");
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("3/3 sleep fixtures"));
    assert.match(await page.textContent("#afterStats"), /1 enclosed rooms/);
    assert.match(await page.textContent("#afterStats"), /needs M or larger lot/);
    assert.match(await page.textContent("#beforeStats"), /0\/3 sleep fixtures/);
    await page.screenshot({ path: path.join(output, "enclosed-draft.png"), fullPage: true });

    // Remove a real wall through the paint tool; Undo must restore enclosure and Redo break it again.
    await page.selectOption("#paint", "i");
    await page.click('#after g[data-x="0"][data-y="2"]');
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("0/3 sleep fixtures"));
    await page.click("#undo");
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("3/3 sleep fixtures"));
    await page.click("#redo");
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("0/3 sleep fixtures"));
    await page.click("#undo");
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("3/3 sleep fixtures"));

    await page.selectOption("#pose", "east");
    await page.waitForFunction(() => document.querySelector("#after svg").getAttribute("viewBox") === "0 0 216 288");
    assert.match(await page.textContent("#afterStats"), /3\/3 sleep fixtures/);
    const downloadEvent = page.waitForEvent("download");
    await page.click("#export");
    const download = await downloadEvent;
    const exported = path.join(output, "exported-draft.xml");
    await download.saveAs(exported);
    assert.match(fs.readFileSync(exported, "utf8"), /Structure="r_KingdomFixtureDoorTimber"/);

    await page.selectOption("#pose", "north");
    await page.waitForFunction(() => document.querySelector("#after svg").getAttribute("viewBox") === "0 0 288 216");
    await page.fill("#width", "10");
    await page.fill("#height", "8");
    await page.click("#resize");
    await page.waitForFunction(() => document.querySelector("#after svg").getAttribute("viewBox") === "0 0 360 288");
    await page.selectOption("#tool", "room");
    await page.click('#after g[data-x="0"][data-y="0"]');
    await page.click('#after g[data-x="9"][data-y="7"]');
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("0/0 sleep fixtures"));
    assert.match(await page.textContent("#afterStats"), /1 enclosed rooms/);
    await page.selectOption("#tool", "paint");
    await page.selectOption("#paint", "d");
    await page.click('#after g[data-x="4"][data-y="7"]');
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("1 doors"));
    await page.selectOption("#paint", "b");
    await page.click('#after g[data-x="1"][data-y="1"]');
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("1/1 sleep fixtures"));
    for (let i = 0; i < 4; i++) {
      await page.click("#undo");
      await page.waitForFunction(() => !document.querySelector("#xml").disabled);
    }
    assert.match(await page.textContent("#afterStats"), /3\/3 sleep fixtures/);

    // Unreviewed edits survive changing layout and returning; malformed XML is never exported.
    const retained = (await page.inputValue("#xml")).replace('Key="canvas-shared-room-draft"', 'Key="retained-draft"');
    await page.fill("#xml", retained);
    await page.selectOption("#case", options.find((item) => item.value !== tent.value).value);
    await page.waitForFunction(() => !document.querySelector("#xml").disabled);
    await page.selectOption("#case", tent.value);
    await page.waitForFunction(() => !document.querySelector("#xml").disabled);
    assert.match(await page.inputValue("#xml"), /Key="retained-draft"/);
    await page.fill("#xml", "<map>");
    await page.click("#apply");
    await page.waitForFunction(() => !document.querySelector("#xml").disabled);
    assert.equal(await page.isDisabled("#export"), true);
    await page.selectOption("#case", options.find((item) => item.value !== tent.value).value);
    await page.waitForFunction(() => !document.querySelector("#xml").disabled);
    await page.selectOption("#case", tent.value);
    await page.waitForFunction(() => !document.querySelector("#xml").disabled);
    assert.equal(await page.inputValue("#xml"), "<map>");
    await page.fill("#xml", source);
    await page.click("#apply");
    await page.waitForFunction(() => document.querySelector("#afterStats").textContent.includes("0/3 sleep fixtures"));
    assert.deepEqual(errors, []);
    fs.writeFileSync(path.join(output, "result.json"), JSON.stringify({
      status: "PASS", cases: ["source leak", "enclosed draft", "wall loss", "undo/redo", "rotation",
        "XML export", "resize", "draw room", "place door and bed", "malformed refusal", "draft retention"], browserErrors: errors,
      scope: "Authoring browser behavior only; not native Qud or gameplay quality acceptance."
    }, null, 2) + "\n");
    console.log("LAYOUT_STUDIO_BROWSER PASS");
  } catch (error) {
    fs.writeFileSync(path.join(output, "failure.json"), JSON.stringify({status:"FAIL", error:String(error),
      message:page ? await page.textContent("#message") : null, browserErrors:errors}, null, 2));
    if (page) await page.screenshot({path:path.join(output,"failure.png"),fullPage:true});
    throw error;
  } finally {
    await browser.close();
  }
}

main().catch((error) => { console.error(error); process.exitCode = 1; });
