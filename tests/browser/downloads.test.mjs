import test from "node:test";
import assert from "node:assert/strict";
import {snapshot, createSync, QUERY} from "../../browser/chrome/downloads.mjs";
import {readFile} from "node:fs/promises";
import {createHash} from "node:crypto";

const now = new Date("2026-10-30T10:00:00Z"), id = "16d39af6-474e-4b87-8bd0-634626482f45";
const file = (filename, offset = 0) => ({filename, state: "complete", exists: true, danger: "safe", incognito: false, endTime: new Date(+now + offset).toISOString(), url: "https://private.example/path"});
test("any Windows save folder, completion order, deduplication and no URL leakage", () => {
  const data = snapshot([file("D:\\Art\\image.webp", -1000), file("C:\\Elsewhere\\layout.fig"), file("d:\\Art\\image.webp", -2000)], id, 1, now);
  assert.deepEqual(data.files.map(f => f.path), ["C:\\Elsewhere\\layout.fig", "D:\\Art\\image.webp"]);
  assert.equal(JSON.stringify(data).includes("private.example"), false);
});
test("unfinished, blocked, private, deleted and invalid paths stay out", () => {
  const normal = file("C:\\x.png");
  const data = snapshot([{...normal, state: "in_progress"}, {...normal, danger: "content"}, {...normal, incognito: true}, {...normal, exists: false}, file("../oops"), file("\\\\.\\pipe\\x"), {...normal, endTime: "invalid"}], id, 1, now);
  assert.equal(data.files.length, 0);
});
test("bounded payload, Unicode and a maximum of 200 metadata references", () => {
  const data = snapshot(Array.from({length: 500}, (_, i) => file(`D:\\${"👋".repeat(1000)}${i}.webp`, -i)), id, 1, now);
  assert.ok(data.files.length > 0 && data.files.length <= 200);
  assert.ok(new TextEncoder().encode(JSON.stringify(data)).length < 512 * 1024);
});
function mock() {
  const state = {}, sent = [], badges = []; let searchCalls = 0;
  const api = {storage: {local: {get: async () => ({...state}), set: async value => Object.assign(state, value)}},
    downloads: {search: async query => { searchCalls++; assert.deepEqual(query, QUERY); return [file("D:\\Custom\\тест.png")]; }},
    runtime: {sendNativeMessage: async (host, message) => { sent.push({host, message}); return {ok: true}; }},
    action: {setBadgeText: async value => badges.push(value), setBadgeBackgroundColor: async () => {}}};
  return {api, state, sent, badges, searchCalls: () => searchCalls};
}
test("completed-download snapshot is acknowledged and pause clears its own profile", async () => {
  const m = mock(), sync = createSync(m.api, () => id, () => now);
  await sync(); assert.equal(m.sent[0].message.files.length, 1); assert.match(m.state.status, /Connected/);
  m.state.enabled = false; await sync(); assert.equal(m.sent[1].message.files.length, 0);
  assert.equal(m.searchCalls(), 1); assert.equal(m.sent[1].message.revision, m.sent[0].message.revision + 1);
});
test("missing host reports disconnection and a later sync recovers without losing profile identity", async () => {
  const m = mock(), sync = createSync(m.api, () => id, () => now), send = m.api.runtime.sendNativeMessage;
  m.api.runtime.sendNativeMessage = async () => { throw new Error("missing host"); };
  assert.equal((await sync()).ok, false); assert.match(m.state.status, /Not connected/); assert.equal(m.state.lastSync, undefined);
  m.api.runtime.sendNativeMessage = send; assert.equal((await sync()).ok, true); assert.equal(m.state.profileId, id);
});
test("concurrent events serialize snapshots and keep the final revision", async () => {
  const m = mock(), sync = createSync(m.api, () => id, () => now);
  await Promise.all([sync(), sync(), sync()]);
  assert.ok(m.sent.length <= 2); assert.equal(new Set(m.sent.map(s => s.message.revision)).size, m.sent.length);
});
test("MV3 has no page/History/cookie permissions and a stable allowlisted extension identity", async () => {
  const manifest = JSON.parse(await readFile(new URL("../../browser/chrome/manifest.json", import.meta.url)));
  assert.equal(manifest.manifest_version, 3); assert.equal(manifest.incognito, "not_allowed");
  assert.deepEqual(manifest.permissions.sort(), ["alarms", "downloads", "nativeMessaging", "storage"]);
  assert.equal(manifest.host_permissions, undefined); assert.equal(manifest.content_scripts, undefined);
  const hash = createHash("sha256").update(Buffer.from(manifest.key, "base64")).digest("hex").slice(0, 32);
  const extensionId = [...hash].map(c => String.fromCharCode(97 + parseInt(c, 16))).join("");
  assert.equal(extensionId, "iedbmkchlbjjabmopniipnffmdjdpmmn");
});
