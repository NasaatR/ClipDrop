import {createSync} from "./downloads.mjs";

const sync = createSync(chrome);
// Register synchronously: a resumed MV3 worker must handle its wake event.
chrome.downloads.onChanged.addListener(delta => {
  if (delta.state || delta.filename || delta.exists || delta.danger || delta.endTime) void sync();
});
chrome.downloads.onErased.addListener(() => void sync());
chrome.runtime.onInstalled.addListener(() => { void chrome.alarms.create("reconcile", {periodInMinutes: 5}); void sync(); });
chrome.runtime.onStartup.addListener(() => { void chrome.alarms.create("reconcile", {periodInMinutes: 5}); void sync(); });
chrome.alarms.onAlarm.addListener(alarm => { if (alarm.name === "reconcile") void sync(); });
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  if (sender.id !== chrome.runtime.id || sender.url !== chrome.runtime.getURL("popup.html") || message?.type !== "sync") return false;
  sync().then(reply, () => reply({ok: false})); return true;
});
