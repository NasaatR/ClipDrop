import {spawn} from "node:child_process";
import {readFile, mkdir, rm} from "node:fs/promises";
import {join, dirname} from "node:path";
import {createServer} from "node:http";
import assert from "node:assert/strict";

const [executable, extension, profile] = process.argv.slice(2), extensionId = "iedbmkchlbjjabmopniipnffmdjdpmmn";
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(get, label, timeout = 20000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) { const value = await get(); if (value) return value; await delay(100); }
  throw new Error("Timed out: " + label);
}
class Cdp {
  next = 0; pending = new Map();
  constructor(socket) {
    this.socket = socket;
    socket.addEventListener("message", e => {
      const message = JSON.parse(e.data), pending = this.pending.get(message.id);
      if (!pending) return;
      this.pending.delete(message.id); clearTimeout(pending.timer);
      if (message.error) pending.reject(new Error(JSON.stringify(message.error))); else pending.resolve(message.result);
    });
  }
  call(method, params = {}, sessionId) {
    const id = ++this.next;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { this.pending.delete(id); reject(new Error("CDP timeout: " + method)); }, 12000);
      this.pending.set(id, {resolve, reject, timer}); this.socket.send(JSON.stringify({id, method, params, sessionId}));
    });
  }
}
await mkdir(profile, {recursive: true});
const webp = Buffer.from("UklGRjYAAABXRUJQVlA4TCkAAAAvAkAAEBcwMgKBJLb9EYYRCCTRDTa5gKDouuUC3jsoBBAARRRNRP9jAAA=", "base64");
const server = createServer((_, response) => { response.writeHead(200, {"Content-Type": "image/webp", "Content-Disposition": "attachment; filename=reference.webp"}); response.end(webp); });
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const browser = spawn(executable, ["--headless=new", "--no-first-run", "--disable-background-networking", "--disable-sync",
  "--remote-debugging-port=0", "--user-data-dir=" + profile, "--disable-extensions-except=" + extension, "--load-extension=" + extension, "about:blank"], {stdio: "ignore"});
let cdp, socket, catalog;
try {
  const portFile = await until(async () => { try { return await readFile(join(profile, "DevToolsActivePort"), "utf8"); } catch { return null; } }, "Chrome DevTools port");
  const [port, browserPath] = portFile.trim().split(/\r?\n/);
  socket = new WebSocket(`ws://127.0.0.1:${port}${browserPath}`);
  await new Promise((resolve, reject) => { socket.addEventListener("open", resolve, {once: true}); socket.addEventListener("error", reject, {once: true}); });
  cdp = new Cdp(socket);
  const target = await until(async () => (await cdp.call("Target.getTargets")).targetInfos.find(t => t.type === "service_worker" && t.url === `chrome-extension://${extensionId}/worker.mjs`), "MV3 service worker");
  const {sessionId} = await cdp.call("Target.attachToTarget", {targetId: target.targetId, flatten: true});
  async function evaluate(expression) {
    const result = await cdp.call("Runtime.evaluate", {expression, awaitPromise: true, returnByValue: true}, sessionId);
    if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails)); return result.result.value;
  }
  // A worker target is discoverable before its extension execution context has
  // finished initializing. Do not race the first native-download API call.
  await cdp.call("Runtime.enable", {}, sessionId);
  await until(() => evaluate("typeof globalThis.chrome === 'object' && typeof chrome.downloads?.download === 'function' && typeof chrome.storage?.local?.get === 'function'"), "MV3 extension APIs ready");
  const destination = join(dirname(profile), "Project Save As — outside Downloads"); await mkdir(destination, {recursive: true});
  await cdp.call("Browser.setDownloadBehavior", {behavior: "allow", downloadPath: destination});
  const url = `http://127.0.0.1:${server.address().port}/reference.webp`;
  await evaluate(`chrome.downloads.download({url:${JSON.stringify(url)},saveAs:false})`);
  const download = await until(async () => (await evaluate("chrome.downloads.search({state:'complete',limit:10})")).find(d => d.filename.toLowerCase().includes("reference.webp")), "actual completed Chrome download");
  assert.equal(download.filename, join(destination, "reference.webp"));
  const state = await until(async () => { const s = await evaluate("chrome.storage.local.get(['profileId','status'])"); return s.status?.startsWith("Connected") ? s : null; }, "native host acknowledgement");
  catalog = join(process.env.LOCALAPPDATA, "NasaatR", "ClipDrop", "browser-downloads", state.profileId + ".json");
  const saved = await until(async () => { try { const s = JSON.parse(await readFile(catalog, "utf8")); return s.files.some(f => f.path === download.filename) ? s : null; } catch { return null; } }, "download event persisted through native messaging");
  assert.equal(saved.version, 1); assert.ok(saved.files[0].completedUtc); assert.equal(JSON.stringify(saved).includes("127.0.0.1"), false);
  assert.deepEqual(await readFile(download.filename), webp);
  console.log("PASS Chrome for Testing loads the MV3 companion and a real WebP download to a custom Save As folder reaches the published Windows host with completion time and intact original bytes.");
  await evaluate("chrome.storage.local.set({enabled:false})");
  const {targetId: popupId} = await cdp.call("Target.createTarget", {url: `chrome-extension://${extensionId}/popup.html`});
  const {sessionId: popupSession} = await cdp.call("Target.attachToTarget", {targetId: popupId, flatten: true});
  await until(async () => (await cdp.call("Runtime.evaluate", {expression: "Boolean(document.querySelector('#sync'))", returnByValue: true}, popupSession)).result.value, "companion popup");
  await cdp.call("Runtime.evaluate", {expression: "chrome.runtime.sendMessage({type:'sync'})", awaitPromise: true, returnByValue: true}, popupSession);
  await until(async () => JSON.parse(await readFile(catalog, "utf8")).files.length === 0, "paused profile clears local list");
  await cdp.call("Page.captureScreenshot", {format: "png"}, popupSession).then(async ({data}) => {
    const {writeFile} = await import("node:fs/promises"); await mkdir("artifacts/qa", {recursive: true}); await writeFile("artifacts/qa/chrome-companion-popup.png", Buffer.from(data, "base64"));
  });
  console.log("PASS The real Chrome popup can pause sharing and its acknowledged snapshot clears only that profile's local list.");
} finally {
  if (cdp) { try { await cdp.call("Browser.close"); } catch { } }
  socket?.close(); if (browser.exitCode === null) browser.kill();
  await new Promise(resolve => server.close(resolve)); if (catalog) await rm(catalog, {force: true});
}
