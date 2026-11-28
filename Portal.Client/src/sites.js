// Pure helpers for the site list; rendering lives in portal.js.

const OTHER = 'Other';

/** @spec CLIENT-041 */
export function groupSites(sites) {
  const groups = new Map();
  const add = (category, site) => {
    if (!groups.has(category)) groups.set(category, []);
    groups.get(category).push(site);
  };
  const other = [];
  for (const site of sites) {
    const categories = site.categories?.length ? site.categories : null;
    if (categories) categories.forEach(category => add(category, site));
    else other.push(site);
  }
  other.forEach(site => add(OTHER, site));
  return [...groups].map(([category, sites]) => ({ category, sites }));
}

/** @spec CLIENT-042 */
export function iconClass(icon) {
  const token = String(icon ?? '').split(/\s+/).find(part => part.startsWith('bi-'));
  return /^bi-[a-z0-9-]+$/.test(token ?? '') ? `bi ${token}` : 'bi bi-globe';
}

/** @spec CLIENT-042 */
export function badges(site) {
  return [site.isPrivate && 'Private', site.isInternal && 'Internal'].filter(Boolean);
}

function scheme(url) {
  const match = /^([a-z][a-z0-9+.-]*):/i.exec(String(url ?? '').trim());
  return match ? match[1].toLowerCase() : null;
}

/** @spec CLIENT-043 CLIENT-044 */
export function isWebUrl(url) {
  const value = scheme(url);
  return (value === 'http' || value === 'https') && /^https?:\/\/[^/]/i.test(String(url).trim());
}

const PLATFORM_STORES = {
  linux: ['linux'],
  windows: ['windows', 'microsoft'],
  android: ['google'],
};
const BLOCKED_LAUNCH_SCHEMES = ['javascript', 'data', 'file', 'vbscript'];

/** @spec CLIENT-045 */
export function appsFor(apps, platform) {
  const stores = PLATFORM_STORES[platform] ?? [];
  return (apps ?? [])
    .filter(app => stores.includes(String(app.store ?? '').toLowerCase()))
    .map(app => {
      const launchScheme = scheme(app.scheme);
      return {
        name: app.name,
        launch: launchScheme && !BLOCKED_LAUNCH_SCHEMES.includes(launchScheme) ? app.scheme : null,
        storeUrl: scheme(app.storeId) === 'https' && isWebUrl(app.storeId) ? app.storeId : null,
      };
    });
}

function ago(refreshedAt, now) {
  const seconds = Math.max(0, now - refreshedAt);
  const units = [['day', 86400], ['hour', 3600], ['minute', 60]];
  for (const [unit, size] of units) {
    const count = Math.floor(seconds / size);
    if (count >= 1) return `updated ${count} ${unit}${count === 1 ? '' : 's'} ago`;
  }
  return 'updated just now';
}

/** @spec CLIENT-040 */
export function statusLine(view, now = Math.floor(Date.now() / 1000)) {
  const when = view.refreshedAt ? ` · ${ago(view.refreshedAt, now)}` : '';
  switch (view.status) {
    case 'refreshing': return { state: 'refreshing', text: 'Refreshing…' };
    case 'upToDate': return { state: 'ok', text: `Up to date${when}` };
    case 'offline': return { state: 'warn', text: `Offline${when}` };
    case 'failed': return { state: 'warn', text: `Couldn't refresh${when}` };
    case 'signInRequired': return { state: 'error', text: 'Sign-in required' };
    default: return { state: 'idle', text: '' };
  }
}
