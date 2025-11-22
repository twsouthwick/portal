/**
 * Opens a URL in a new browser tab
 */
export function openUrlInNewTab(url: string): void {
    window.open(url, '_blank');
}

/**
 * Attempts to launch an app protocol URL and detects if it succeeded
 */
export function launchAppUrl(url: string): Promise<boolean> {
    return new Promise((resolve) => {
        let hasFocus = true;
        let hasBlurred = false;

        // Set up blur detection - if the window loses focus, the app likely launched
        const onBlur = (): void => {
            hasBlurred = true;
            hasFocus = false;
        };

        const onFocus = (): void => {
            hasFocus = true;
        };

        window.addEventListener('blur', onBlur);
        window.addEventListener('focus', onFocus);

        try {
            window.location.href = url;
        } catch (e) {
            console.error('Failed to launch app protocol:', e);
            cleanup();
            resolve(false);
            return;
        }

        // Check after a delay if the window lost focus (indicating app launched)
        setTimeout(() => {
            cleanup();
            // If the window blurred or lost focus, assume app launched successfully
            // If it stayed focused, the protocol handler likely doesn't exist
            resolve(hasBlurred || !hasFocus);
        }, 1000);

        function cleanup(): void {
            window.removeEventListener('blur', onBlur);
            window.removeEventListener('focus', onFocus);
        }
    });
}
