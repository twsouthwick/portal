// @spec CLIENT-011 CLIENT-012 CLIENT-013 CLIENT-028
const invoke = window.__TAURI__?.core?.invoke;

const form = document.getElementById('portal-url-form');
const input = document.getElementById('portal-url');
const status = document.getElementById('portal-status');
const confirm = document.getElementById('portal-confirm');
const save = document.getElementById('portal-save');
const saved = document.getElementById('portal-saved');

let checkedOrigin = null;

const outcomeMessages = {
  signInRequired: origin => `Found a Digital Fortress sign-in endpoint at ${origin}. Sign-in is not available in this client yet.`,
  reachable: origin => `Reached the Digital Fortress manifest endpoint at ${origin}.`,
  tls: origin => `Could not establish a secure connection to ${origin}. Check that its certificate is valid and trusted by this device.`,
  network: origin => `Could not reach ${origin}. Check the address and your network connection.`,
  timeout: origin => `${origin} did not respond within 10 seconds.`,
  authentication: (origin, { status }) => `${origin} rejected the request before reaching Digital Fortress (HTTP ${status}). A proxy or gateway may require separate authentication.`,
  redirect: (origin, { status }) => `${origin} redirected the request (HTTP ${status}). Redirects are not followed; enter your Digital Fortress server's own address.`,
  unexpectedResponse: (origin, { status }) => `${origin} did not respond like a Digital Fortress server (HTTP ${status}).`,
};

function showSaved(origin) {
  saved.hidden = !origin;
  saved.textContent = origin ? `Saved Digital Fortress URL: ${origin} (not signed in or verified).` : '';
}

function showStatus(message, state) {
  status.textContent = message;
  if (state) {
    status.dataset.state = state;
  } else {
    delete status.dataset.state;
  }
}

function resetConfirmation() {
  checkedOrigin = null;
  confirm.hidden = true;
}

let checkGeneration = 0;

form.addEventListener('submit', async event => {
  event.preventDefault();
  const generation = ++checkGeneration;
  resetConfirmation();
  if (!invoke) {
    showStatus('Digital Fortress connection checks are available only in the native app.', 'error');
    return;
  }
  showStatus('Checking…', 'checking');
  try {
    const { origin, outcome } = await invoke('check_portal_url', { input: input.value });
    if (generation !== checkGeneration) return;
    const succeeded = outcome.kind === 'signInRequired' || outcome.kind === 'reachable';
    showStatus(outcomeMessages[outcome.kind](origin, outcome), succeeded ? 'success' : 'error');
    if (succeeded) {
      checkedOrigin = origin;
      confirm.hidden = false;
    }
  } catch (error) {
    if (generation !== checkGeneration) return;
    showStatus(String(error), 'error');
  }
});

input.addEventListener('input', () => {
  ++checkGeneration;
  resetConfirmation();
  showStatus('');
});

save.addEventListener('click', async () => {
  if (!checkedOrigin) return;
  try {
    showSaved(await invoke('save_portal_url', { origin: checkedOrigin }));
    showStatus('');
  } catch (error) {
    showStatus(String(error), 'error');
  }
  resetConfirmation();
});

if (invoke) {
  invoke('saved_portal_url').then(showSaved, error => {
    showStatus(String(error), 'error');
  });
}
