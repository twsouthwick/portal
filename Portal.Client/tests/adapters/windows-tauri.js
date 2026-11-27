import { createAdapter as createTauriAdapter } from './linux-tauri.js';

export async function createAdapter() {
  if (process.platform !== 'win32') {
    throw new Error('The Windows Tauri adapter requires Windows.');
  }
  return createTauriAdapter();
}
