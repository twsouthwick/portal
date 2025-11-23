/**
 * Browser and platform detection using ua-parser-js
 */

import { UAParser } from 'ua-parser-js';
import { detectIncognito } from 'detectincognitojs';

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
    /** Whether the browser is in private/incognito mode */
    isPrivate: boolean;
}

/**
 * Detects if the browser is in private/incognito mode
 */
async function getPrivateMode(): Promise<boolean> {
    try {
        const incognitoResult = await detectIncognito();
        return incognitoResult.isPrivate;
    } catch (error) {
        console.error('Failed to detect private mode:', error);
        return false;
    }
}

/**
 * Gets browser and platform information using ua-parser-js
 */
export async function getBrowserInfo(): Promise<BrowserInfo> {
    const parser = new UAParser();
    const result = parser.getResult();
    const isPrivate = await getPrivateMode();

    const browserInfo: BrowserInfo = {
        browser: result.browser.name || 'Unknown',
        browserVersion: result.browser.version || '',
        platform: result.os.name || 'Unknown',
        platformVersion: result.os.version || '',
        isMobile: result.device.type === 'mobile' || result.device.type === 'tablet',
        architecture: result.cpu.architecture || '',
        isPrivate: isPrivate
    };

    console.log('Detected browser info:', {
        browser: browserInfo.browser,
        browserVersion: browserInfo.browserVersion,
        platform: browserInfo.platform,
        platformVersion: browserInfo.platformVersion,
        isMobile: browserInfo.isMobile,
        architecture: browserInfo.architecture,
        isPrivate: browserInfo.isPrivate
    });

    return browserInfo;
}
