/**
 * Browser and platform detection using ua-parser-js
 */

import { UAParser } from 'ua-parser-js';

export interface BrowserInfo {
    /** Browser name (e.g., "Chrome", "Edge", "Firefox", "Safari") */
    browser: string;
    /** Browser version */
    browserVersion: string;
    /** Operating system (e.g., "Windows", "macOS", "Linux", "iOS", "Android") */
    platform: string;
    /** Platform version */
    platformVersion: string;
    /** Whether the device is mobile */
    isMobile: boolean;
    /** CPU architecture (e.g., "x86", "arm") */
    architecture: string;
}

/**
 * Gets browser and platform information using ua-parser-js
 */
export function getBrowserInfo(): BrowserInfo {
    const parser = new UAParser();
    const result = parser.getResult();

    return {
        browser: result.browser.name || 'Unknown',
        browserVersion: result.browser.version || '',
        platform: result.os.name || 'Unknown',
        platformVersion: result.os.version || '',
        isMobile: result.device.type === 'mobile' || result.device.type === 'tablet',
        architecture: result.cpu.architecture || ''
    };
}
