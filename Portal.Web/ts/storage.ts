/**
 * Generic LocalStorage helpers for persisting arbitrary JSON data
 */

/**
 * Gets a value from localStorage and deserializes it from JSON
 */
export function getItem<T>(key: string): T | null {
    try {
        const data = localStorage.getItem(key);
        if (!data) {
            return null;
        }
        return JSON.parse(data) as T;
    } catch (error) {
        console.error(`Failed to read '${key}' from localStorage:`, error);
        return null;
    }
}

/**
 * Serializes a value to JSON and saves it to localStorage
 */
export function setItem(key: string, value: any): void {
    try {
        const json = JSON.stringify(value);
        localStorage.setItem(key, json);
    } catch (error) {
        console.error(`Failed to save '${key}' to localStorage:`, error);
    }
}

/**
 * Removes a value from localStorage
 */
export function removeItem(key: string): void {
    try {
        localStorage.removeItem(key);
    } catch (error) {
        console.error(`Failed to remove '${key}' from localStorage:`, error);
    }
}

/**
 * Clears all values from localStorage
 */
export function clear(): void {
    try {
        localStorage.clear();
    } catch (error) {
        console.error('Failed to clear localStorage:', error);
    }
}

/**
 * Gets all keys from localStorage
 */
export function getKeys(): string[] {
    try {
        const keys: string[] = [];
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key) {
                keys.push(key);
            }
        }
        return keys;
    } catch (error) {
        console.error('Failed to get keys from localStorage:', error);
        return [];
    }
}

/**
 * Checks if a key exists in localStorage
 */
export function hasKey(key: string): boolean {
    try {
        return localStorage.getItem(key) !== null;
    } catch (error) {
        console.error(`Failed to check key '${key}' in localStorage:`, error);
        return false;
    }
}
