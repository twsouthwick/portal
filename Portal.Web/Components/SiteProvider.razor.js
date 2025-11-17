// Collocated JavaScript module for SiteProvider component

/**
 * Dynamically loads a script and returns a promise that resolves when loaded
 * @param {string} src - Script source URL
 * @returns {Promise<void>}
 */
function loadScript(src) {
    return new Promise((resolve, reject) => {
        if (document.querySelector(`script[src="${src}"]`)) {
            resolve(); // Script already loaded
            return;
        }
        
        const script = document.createElement('script');
        script.src = src;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(`Failed to load script: ${src}`));
        document.head.appendChild(script);
    });
}

/**
 * Detects if the browser is in private/incognito mode using detectIncognito.js library
 * @returns {Promise<boolean>} True if in private mode, false otherwise
 */
export async function isPrivateMode() {
    try {
        // Dynamically load the detectIncognito library
        await loadScript('/lib/detectincognito/dist/es5/detectIncognito.min.js');
        
        // Check if detectIncognito is available as a global function
        if (typeof window.detectIncognito !== 'function') {
            throw new Error('detectIncognito not available as global function');
        }
        
        // Call detectIncognito and return the isPrivate result
        const result = await window.detectIncognito();
        return result.isPrivate;
    } catch (error) {
        // If detectIncognito fails or is not available, assume not private mode
        return false;
    }
}
