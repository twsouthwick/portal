export function openUrlInNewTab(url) {
    // Open HTTP/HTTPS URLs in a new tab
    window.open(url, '_blank');
}

export function launchAppUrl(url) {
    // Try to detect if app protocol launch failed
    return new Promise((resolve) => {
        let hasFocus = true;
        let hasBlurred = false;

        // Set up blur detection - if the window loses focus, the app likely launched
        const onBlur = () => {
            hasBlurred = true;
            hasFocus = false;
        };

        const onFocus = () => {
            hasFocus = true;
        };

        window.addEventListener('blur', onBlur);
        window.addEventListener('focus', onFocus);

        // Try to launch the app
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

        function cleanup() {
            window.removeEventListener('blur', onBlur);
            window.removeEventListener('focus', onFocus);
        }
    });
}
