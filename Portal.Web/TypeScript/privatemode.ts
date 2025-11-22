import { detectIncognito } from 'detectincognitojs';

/**
 * Detects if the browser is in private/incognito mode
 */
export async function isPrivateMode(): Promise<boolean> {
    try {
        const result = await detectIncognito();
        return result.isPrivate;
    } catch (error) {
        console.error('Failed to detect private mode:', error);
        return false;
    }
}
