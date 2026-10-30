export const HOST = "com.nasaat.clipdrop.downloads";
export const QUERY = Object.freeze({state: "complete", exists: true, orderBy: ["-endTime"], limit: 200});
const readyDanger = new Set(["safe", "accepted", "allowlistedByPolicy", "deepScannedSafe"]);

export function snapshot(items, profileId, revision, now = new Date()) {
  const seen = new Set(), encoder = new TextEncoder();
  const message = {version: 1, type: "downloads", profileId, revision, capturedUtc: now.toISOString(), files: []};
  let size = encoder.encode(JSON.stringify(message)).length;
  const candidates = items.filter(item => item.state === "complete" && item.exists !== false && !item.incognito &&
    readyDanger.has(item.danger) && typeof item.filename === "string" && item.filename.length <= 4096 &&
    /^(?:[a-z]:[\\/]|\\\\[^?.\\/])/i.test(item.filename) && !/[\u0000-\u001f]/.test(item.filename) &&
    Number.isFinite(Date.parse(item.endTime)) && Date.parse(item.endTime) <= now.getTime() + 300000)
    .sort((a, b) => Date.parse(b.endTime) - Date.parse(a.endTime));
  for (const item of candidates) {
    const key = item.filename.toLowerCase();
    if (seen.has(key)) continue;
    const entry = {path: item.filename, completedUtc: new Date(item.endTime).toISOString()};
    const added = encoder.encode(JSON.stringify(entry)).length + (message.files.length ? 1 : 0);
    if (size + added > 400 * 1024) break;
    size += added; seen.add(key); message.files.push(entry);
    if (message.files.length === 200) break;
  }
  return message;
}

export function createSync(api, randomId = () => crypto.randomUUID(), clock = () => new Date()) {
  let active, pending = false;
  async function once() {
    const state = await api.storage.local.get(["enabled", "profileId", "revision"]);
    const profileId = state.profileId || randomId();
    const revision = Math.max(clock().getTime(), (state.revision || 0) + 1);
    await api.storage.local.set({profileId, revision});
    const items = state.enabled === false ? [] : await api.downloads.search(QUERY);
    const response = await api.runtime.sendNativeMessage(HOST, snapshot(items, profileId, revision, clock()));
    if (response?.ok !== true) throw new Error("Native host did not acknowledge the snapshot.");
    await api.storage.local.set({status: state.enabled === false ? "Paused · local list cleared" : "Connected · downloads synced", lastSync: clock().toISOString()});
    await api.action.setBadgeText({text: ""});
  }
  return function sync() {
    pending = true;
    if (!active) active = (async () => {
      let ok = true;
      while (pending) {
        pending = false;
        try { await once(); }
        catch {
          ok = false;
          await api.storage.local.set({status: "Not connected · in ClipDrop Settings → Downloads, choose Connect Chrome"});
          await api.action.setBadgeText({text: "!"});
          await api.action.setBadgeBackgroundColor({color: "#99714a"});
        }
      }
      return {ok};
    })().finally(() => { active = undefined; });
    return active;
  };
}
