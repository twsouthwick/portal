// @spec CLIENT-001 CLIENT-011 CLIENT-012 CLIENT-013 CLIENT-025 CLIENT-026 CLIENT-028 CLIENT-030 CLIENT-032 CLIENT-033
const invoke = window.__TAURI__?.core?.invoke;

const form = document.getElementById('portal-url-form');
const input = document.getElementById('portal-url');
const status = document.getElementById('portal-status');
const confirm = document.getElementById('portal-confirm');
const save = document.getElementById('portal-save');
const saved = document.getElementById('portal-saved');
const appVersion = document.getElementById('app-version');
const websiteVersion = document.getElementById('website-version');

let checkedOrigin = null;

const outcomeMessages = {
  signInRequired: origin => `Found a Digital Fortress at ${origin}. Save it to sign in.`,
  reachable: origin => `Reached the Digital Fortress manifest endpoint at ${origin}.`,
  tls: origin => `Could not establish a secure connection to ${origin}. Check that its certificate is valid and trusted by this device.`,
  network: origin => `Could not reach ${origin}. Check the address and your network connection.`,
  timeout: origin => `${origin} did not respond within 10 seconds.`,
  authentication: (origin, { status }) => `${origin} rejected the request before reaching Digital Fortress (HTTP ${status}). A proxy or gateway may require separate authentication.`,
  redirect: (origin, { status }) => `${origin} redirected the request (HTTP ${status}). Redirects are not followed; enter your Digital Fortress server's own address.`,
  unexpectedResponse: (origin, { status }) => `${origin} did not respond like a Digital Fortress server (HTTP ${status}).`,
  discoveryInvalid: origin => `The discovery document at ${origin} is invalid or does not contain a valid Digital Fortress address.`,
  discoveryTooLarge: origin => `The discovery document at ${origin} exceeds the 8 KiB limit.`,
  discoveryResponse: (origin, { status }) => `The discovery document at ${origin} returned HTTP ${status}. Redirects are not followed.`,
};

function showSaved(origin) {
  saved.hidden = !origin;
  saved.textContent = origin ? `Saved Digital Fortress URL: ${origin} (not signed in or verified).` : '';
}

function showWebsiteVersion(version) {
  websiteVersion.hidden = !version;
  websiteVersion.textContent = version ? `Website ${version}` : '';
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
  showWebsiteVersion(null);
  if (!invoke) {
    showStatus('Digital Fortress connection checks are available only in the native app.', 'error');
    return;
  }
  showStatus('Checking…', 'checking');
  try {
    const { origin, outcome, portalVersion } = await invoke('check_portal_url', { input: input.value });
    if (generation !== checkGeneration) return;
    const succeeded = outcome.kind === 'signInRequired' || outcome.kind === 'reachable';
    showWebsiteVersion(succeeded ? portalVersion : null);
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
  showWebsiteVersion(null);
  showStatus('');
});

save.addEventListener('click', async () => {
  if (!checkedOrigin) return;
  try {
    const origin = await invoke('save_portal_url', { origin: checkedOrigin });
    showSaved(origin);
    showStatus('');
    window.portalApp?.showPortal(origin);
  } catch (error) {
    showStatus(String(error), 'error');
  }
  resetConfirmation();
});

if (invoke) {
  invoke('saved_portal_url').then(showSaved, error => {
    showStatus(String(error), 'error');
  });
  invoke('app_version').then(version => {
    appVersion.textContent = `App ${version}`;
  }, () => {});
  const startupGeneration = checkGeneration;
  invoke('saved_portal_version').then(version => {
    if (startupGeneration === checkGeneration) showWebsiteVersion(version);
  }, () => {});
}
