// Signed-in experience: sign-in, refresh status, the site grid, and site details.
import 'bootstrap-icons/font/bootstrap-icons.css';
import DOMPurify from 'dompurify';
import { marked } from 'marked';
import { appsFor, badges, groupSites, iconClass, isWebUrl, statusLine } from './sites.js';

const invoke = window.__TAURI__?.core?.invoke;
const $ = id => document.getElementById(id);

const onboardingView = $('onboarding-view');
const portalView = $('portal-view');
const banner = $('portal-banner');
const bannerText = $('portal-banner-text');
const bannerCancel = $('portal-banner-cancel');
const signinPanel = $('signin-panel');
const signinTitle = $('signin-title');
const signinOrigin = $('signin-origin');
const signinButton = $('signin-button');
const changeAddress = $('change-address');
const sitesPanel = $('sites-panel');
const manifestName = $('manifest-name');
const manifestDescription = $('manifest-description');
const refreshStatus = $('refresh-status');
const refreshButton = $('refresh-button');
const headerSignin = $('header-signin');
const signoutButton = $('signout-button');
const siteGroups = $('site-groups');
const detailsPanel = $('site-details');

let platform = 'unknown';
let view = null;
let selectedCategory = null;
let openSiteId = null;
let signingIn = false;
let lastCard = null;

/** Builds an element using text content only; markup never comes from manifest strings here. */
function h(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (value === undefined || value === null || value === false) continue;
    if (key === 'class') node.className = value;
    else if (key.startsWith('on')) node.addEventListener(key.slice(2), value);
    else node.setAttribute(key, value === true ? '' : value);
  }
  node.append(...children.flat().filter(child => child !== null && child !== undefined && child !== false));
  return node;
}

const icon = (site, extra = '') => h('i', { class: `${iconClass(site.icon)} ${extra}`.trim(), 'aria-hidden': 'true' });
const badgeList = site => {
  const labels = badges(site);
  return labels.length ? h('span', { class: 'badges' }, labels.map(label => h('span', { class: `badge badge-${label.toLowerCase()}` }, label))) : null;
};

function showBanner(message, { state = 'info', cancellable = false } = {}) {
  banner.hidden = !message;
  bannerText.textContent = message ?? '';
  banner.dataset.state = state;
  bannerCancel.hidden = !cancellable;
}

/** @spec CLIENT-046 */
async function openExternal(url, kind) {
  try {
    await invoke('open_external', { url, kind });
  } catch (error) {
    showBanner(String(error), { state: 'error' });
  }
}

/** @spec CLIENT-040 */
function renderHeader() {
  const manifest = view.manifest;
  manifestName.textContent = manifest?.name || 'Your Digital Fortress';
  manifestDescription.textContent = manifest?.description ?? '';
  manifestDescription.hidden = !manifest?.description;
  const line = statusLine(view);
  refreshStatus.textContent = line.text;
  refreshStatus.dataset.state = line.state;
  refreshButton.disabled = view.status === 'refreshing' || signingIn;
  headerSignin.hidden = view.status !== 'signInRequired';
  headerSignin.disabled = signingIn;
}

/** @spec CLIENT-041 CLIENT-042 CLIENT-048 */
function renderGrid() {
  const manifest = view.manifest;
  const groups = groupSites(manifest?.sites ?? []);
  const selected = groups.find(group => group.category === selectedCategory);
  if (!selected) selectedCategory = null;
  if (openSiteId !== null && !selected?.sites.some(site => site.id === openSiteId)) openSiteId = null;
  if (!manifest) {
    siteGroups.replaceChildren(h('p', { class: 'empty-state' },
      view.status === 'refreshing' ? 'Loading your sites…' : "Your sites couldn't be loaded yet."));
    return;
  }
  if (!manifest.sites.length) {
    siteGroups.replaceChildren(h('p', { class: 'empty-state' }, 'No sites are available to you yet.'));
    return;
  }
  if (!selected) {
    siteGroups.replaceChildren(
      h('section', { class: 'site-group' },
        h('h2', { class: 'group-title' }, 'Categories'),
        h('ul', { class: 'site-grid', role: 'list' }, groups.map(({ category, sites }) => h('li', {},
          h('button', { class: 'category-card', type: 'button', 'data-category': category, onclick: () => showCategory(category) },
            h('span', { class: 'site-icon' }, h('i', { class: 'bi bi-folder', 'aria-hidden': 'true' })),
            h('span', { class: 'site-text' },
              h('span', { class: 'site-name' }, category),
              h('span', { class: 'site-description' }, `${sites.length} ${sites.length === 1 ? 'site' : 'sites'}`))))))));
    return;
  }
  siteGroups.replaceChildren(
    h('section', { class: 'site-group' },
      h('button', { id: 'categories-back', class: 'back-button', type: 'button', onclick: hideCategory },
        h('i', { class: 'bi bi-arrow-left', 'aria-hidden': 'true' }), 'Back to categories'),
      h('h2', { class: 'group-title' }, selected.category),
      h('ul', { class: 'site-grid', role: 'list' }, selected.sites.map(site => h('li', {},
        h('button', { class: 'site-card', type: 'button', 'data-site-id': site.id, onclick: event => showDetails(site.id, event.currentTarget) },
          h('span', { class: 'site-icon' }, icon(site)),
          h('span', { class: 'site-text' },
            h('span', { class: 'site-name' }, site.name),
            site.description ? h('span', { class: 'site-description' }, site.description) : null),
          badgeList(site)))))),
  );
}

/** @spec CLIENT-048 */
function showCategory(category) {
  selectedCategory = category;
  openSiteId = null;
  renderGrid();
  $('categories-back').focus();
  resetScroll();
}

function hideCategory() {
  const category = selectedCategory;
  selectedCategory = null;
  renderGrid();
  Array.from(siteGroups.querySelectorAll('.category-card')).find(card => card.dataset.category === category)?.focus();
}

function resetScroll() {
  document.querySelector?.('.app-content')?.scrollTo?.(0, 0);
}

/** @spec CLIENT-044 */
function instructions(markdown) {
  const container = h('div', { class: 'instructions' });
  container.innerHTML = DOMPurify.sanitize(marked.parse(markdown, { async: false }));
  container.addEventListener('click', event => {
    const link = event.target.closest?.('a');
    if (!link) return;
    event.preventDefault();
    const href = link.getAttribute('href');
    if (isWebUrl(href)) openExternal(href, 'web');
  });
  return container;
}

/** @spec CLIENT-043 CLIENT-044 CLIENT-045 */
function renderDetails() {
  const site = view.manifest?.sites.find(candidate => candidate.id === openSiteId);
  if (!site) {
    openSiteId = null;
    detailsPanel.hidden = true;
    return false;
  }
  const apps = appsFor(site.apps, platform);
  detailsPanel.replaceChildren(
    h('button', { id: 'details-back', class: 'back-button', type: 'button', onclick: hideDetails },
      h('i', { class: 'bi bi-arrow-left', 'aria-hidden': 'true' }), 'Back'),
    h('div', { class: 'details-card' },
      h('div', { class: 'details-header' },
        h('span', { class: 'site-icon site-icon-large' }, icon(site)),
        h('div', { class: 'details-title' },
          h('h2', { id: 'details-name' }, site.name),
          badgeList(site))),
      site.description ? h('p', { class: 'details-description' }, site.description) : null,
      isWebUrl(site.url)
        ? h('button', { class: 'primary-button details-open', type: 'button', onclick: () => openExternal(site.url, 'web') },
          'Open site', h('i', { class: 'bi bi-box-arrow-up-right', 'aria-hidden': 'true' }))
        : null,
      apps.length ? h('section', { class: 'details-section' },
        h('h3', {}, 'Apps'),
        h('ul', { class: 'app-list', role: 'list' }, apps.map(app => h('li', { class: 'app-row' },
          h('span', { class: 'app-name' }, app.name),
          h('span', { class: 'app-actions' },
            app.launch ? h('button', { class: 'chip-button', type: 'button', onclick: () => openExternal(app.launch, 'app') }, 'Open') : null,
            app.storeUrl ? h('button', { class: 'chip-button', type: 'button', onclick: () => openExternal(app.storeUrl, 'web') }, 'Get app') : null))))) : null,
      site.usageInstructions ? h('section', { class: 'details-section' },
        h('h3', {}, 'How to use'),
        instructions(site.usageInstructions)) : null),
  );
  detailsPanel.hidden = false;
  return true;
}

function showDetails(siteId, card) {
  openSiteId = siteId;
  lastCard = card;
  if (renderDetails()) {
    siteGroups.hidden = true;
    $('details-back').focus();
    resetScroll();
  }
}

function hideDetails() {
  openSiteId = null;
  detailsPanel.hidden = true;
  siteGroups.hidden = false;
  const card = lastCard && siteGroups.querySelector(`[data-site-id="${CSS.escape(lastCard.dataset.siteId)}"]`);
  card?.focus();
}

/** @spec CLIENT-036 CLIENT-048 */
function render() {
  const showSignin = view.status === 'signedOut' || (!view.manifest && view.status === 'signInRequired');
  signinPanel.hidden = !showSignin;
  sitesPanel.hidden = showSignin;
  signinTitle.textContent = view.status === 'signInRequired' ? 'Sign in again' : 'Sign in to continue';
  signinButton.disabled = signingIn;
  changeAddress.disabled = signingIn;
  if (showSignin) return;
  renderHeader();
  const previousCategory = selectedCategory;
  const previousSite = openSiteId;
  renderGrid();
  const detailsOpen = openSiteId !== null && renderDetails();
  siteGroups.hidden = detailsOpen;
  if (!detailsOpen) detailsPanel.hidden = true;
  if (previousSite !== openSiteId || previousCategory !== selectedCategory) {
    if (selectedCategory !== null) $('categories-back')?.focus();
    else siteGroups.querySelectorAll('.category-card')[0]?.focus();
  }
}

async function refresh() {
  view = { ...view, status: 'refreshing' };
  render();
  try {
    view = await invoke('refresh_manifest');
    showBanner(view.warning, { state: 'warn' });
  } catch (error) {
    view = { ...view, status: 'failed' };
    showBanner(String(error), { state: 'error' });
  }
  render();
}

/** @spec CLIENT-015 CLIENT-034 CLIENT-035 CLIENT-036 */
async function signIn() {
  signingIn = true;
  render();
  showBanner('Finish signing in with your browser…', { state: 'info', cancellable: true });
  try {
    view = await invoke('sign_in');
    signingIn = false;
    render();
    showBanner(view.warning || 'Signed in. Updating your sites…', { state: view.warning ? 'warn' : 'info' });
    await refresh();
  } catch (error) {
    showBanner(String(error), { state: 'error' });
  } finally {
    signingIn = false;
    render();
  }
}

/** @spec CLIENT-021 */
async function signOut() {
  try {
    view = await invoke('sign_out');
    selectedCategory = null;
    openSiteId = null;
    render();
    showBanner(view.warning, { state: 'warn' });
  } catch (error) {
    showBanner(String(error), { state: 'error' });
  }
}

/** @spec CLIENT-018 */
async function showPortal(origin) {
  if (!invoke) return;
  onboardingView.hidden = true;
  portalView.hidden = false;
  signinOrigin.textContent = origin;
  platform = await invoke('platform').catch(() => 'unknown');
  try {
    view = await invoke('session_startup');
  } catch (error) {
    view = { status: 'signedOut', manifest: null, refreshedAt: null, warning: String(error) };
  }
  showBanner(view.warning, { state: 'warn' });
  render();
  if (view.status !== 'signedOut') await refresh();
}

signinButton.addEventListener('click', signIn);
headerSignin.addEventListener('click', signIn);
bannerCancel.addEventListener('click', () => invoke('cancel_sign_in'));
refreshButton.addEventListener('click', refresh);
signoutButton.addEventListener('click', signOut);
changeAddress.addEventListener('click', () => {
  if (signingIn) return;
  portalView.hidden = true;
  onboardingView.hidden = false;
  $('portal-url')?.focus();
});
document.addEventListener('keydown', event => {
  if (event.key === 'Escape' && !detailsPanel.hidden) hideDetails();
});

window.portalApp = { showPortal };

if (invoke) {
  invoke('saved_portal_url').then(origin => origin && showPortal(origin), () => {});
}
