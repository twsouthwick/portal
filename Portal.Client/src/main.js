// @spec CLIENT-011 CLIENT-012 CLIENT-013 CLIENT-016 CLIENT-017
const invoke = window.__TAURI__?.core?.invoke;

const form = document.getElementById('portal-url-form');
const input = document.getElementById('portal-url');
const status = document.getElementById('portal-status');
const confirm = document.getElementById('portal-confirm');
const save = document.getElementById('portal-save');
const saved = document.getElementById('portal-saved');

let checkedOrigin = null;

const outcomeMessages = {
  signInRequired: origin => `Found a Portal sign-in endpoint at ${origin}. Sign-in is not available in this client yet.`,
  reachable: origin => `Reached the Portal manifest endpoint at ${origin}.`,
  tls: origin => `Could not establish a secure connection to ${origin}. Check that its certificate is valid and trusted by this device.`,
  network: origin => `Could not reach ${origin}. Check the address and your network connection.`,
  timeout: origin => `${origin} did not respond within 10 seconds.`,
  authentication: (origin, { status }) => `${origin} rejected the request before reaching Portal (HTTP ${status}). A proxy or gateway may require separate authentication.`,
  redirect: (origin, { status }) => `${origin} redirected the request (HTTP ${status}). Redirects are not followed; enter the Portal's own address.`,
  unexpectedResponse: (origin, { status }) => `${origin} did not respond like a Portal (HTTP ${status}).`,
  discoveryInvalid: origin => `The discovery document at ${origin} is invalid or does not contain a valid Portal origin.`,
  discoveryTooLarge: origin => `The discovery document at ${origin} exceeds the 8 KiB limit.`,
  discoveryResponse: (origin, { status }) => `The discovery document at ${origin} returned HTTP ${status}. Redirects are not followed.`,
};

function showSaved(origin) {
  saved.hidden = !origin;
  saved.textContent = origin ? `Saved Portal URL: ${origin} (not signed in or verified).` : '';
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
    status.textContent = 'Portal connection checks are available only in the native app.';
    return;
  }
  status.textContent = 'Checking…';
  try {
    const { origin, outcome } = await invoke('check_portal_url', { input: input.value });
    if (generation !== checkGeneration) return;
    status.textContent = outcomeMessages[outcome.kind](origin, outcome);
    if (outcome.kind === 'signInRequired' || outcome.kind === 'reachable') {
      checkedOrigin = origin;
      confirm.hidden = false;
    }
  } catch (error) {
    if (generation !== checkGeneration) return;
    status.textContent = String(error);
  }
});

input.addEventListener('input', () => {
  ++checkGeneration;
  resetConfirmation();
});

save.addEventListener('click', async () => {
  if (!checkedOrigin) return;
  try {
    showSaved(await invoke('save_portal_url', { origin: checkedOrigin }));
    status.textContent = '';
  } catch (error) {
    status.textContent = String(error);
  }
  resetConfirmation();
});

if (invoke) {
  invoke('saved_portal_url').then(showSaved, error => {
    status.textContent = String(error);
  });
}
