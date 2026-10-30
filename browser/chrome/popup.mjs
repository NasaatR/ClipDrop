const enabled = document.querySelector("#enabled"), status = document.querySelector("#status"), sync = document.querySelector("#sync");
async function render() {
  const state = await chrome.storage.local.get(["enabled", "status", "lastSync"]);
  enabled.checked = state.enabled !== false;
  status.textContent = (state.status || "In ClipDrop Settings → Downloads, choose Connect Chrome") +
    (state.lastSync ? " · " + new Date(state.lastSync).toLocaleTimeString([], {hour: "2-digit", minute: "2-digit"}) : "");
}
async function synchronize() {
  sync.disabled = true; status.textContent = "Syncing…";
  try { await chrome.runtime.sendMessage({type: "sync"}); await render(); }
  catch { status.textContent = "Could not sync · reopen this popup and try again"; }
  finally { sync.disabled = false; }
}
enabled.addEventListener("change", async () => { await chrome.storage.local.set({enabled: enabled.checked}); await synchronize(); });
sync.addEventListener("click", synchronize);
chrome.storage.onChanged.addListener(() => void render());
void render();
