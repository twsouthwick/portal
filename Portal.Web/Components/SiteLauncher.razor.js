export function openUrl(url) {
    // Try to open the URL (works for both http/https and custom protocols)
    const opened = window.open(url, '_blank');
    
    // For app protocols, window.open might return null but the app could still launch
    // This is expected behavior for custom protocol handlers
    return { success: opened !== null || !url.startsWith('http') };
}
