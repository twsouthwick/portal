/**
 * Custom Blazor reconnection display style override
 * Uses MutationObserver to detect when Blazor creates the reconnect modal
 * and applies custom styles to override the inline styles
 * 
 * @sideEffects This module has side effects and should not be tree-shaken
 */

// Constants
const RELOAD_DELAY_MS = 1000;
const RELOAD_MESSAGE_TEXT = 'Please reload the page';
const MODAL_ID = 'components-reconnect-modal';
const CUSTOM_STYLES_MARKER = 'blazor-reconnect-custom-styles-applied';

function applyCustomStyles(modal: HTMLElement): void {
    console.log('Applying custom styles to reconnect modal');
    
    // Access the shadow root
    const shadowRoot = modal.shadowRoot;
    if (!shadowRoot) {
        console.warn('Shadow root not found on modal element');
        return;
    }
    
    console.log('Shadow root found, accessing elements inside');
    
    // Override styles in the shadow DOM's style element
    const styleElement = shadowRoot.querySelector('style');
    if (styleElement) {
        // Check if styles have already been applied to avoid duplication
        if (styleElement.textContent?.includes(CUSTOM_STYLES_MARKER)) {
            console.log('Custom styles already applied, skipping');
            return;
        }
        
        console.log('Adding custom styles to shadow DOM style element');
        const customStyles = `
            /* ${CUSTOM_STYLES_MARKER} */
            .components-reconnect-overlay::before {
                background-color: rgba(0, 0, 0, 0.5) !important;
            }
            .components-reconnect-dialog {
                background-color: rgba(0, 0, 0, 0.8) !important;
                color: white !important;
                padding: 1.5rem 2rem !important;
                border-radius: 8px !important;
                font-weight: 500 !important;
                box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3) !important;
            }
        `;
        styleElement.textContent += customStyles;
        console.log('Dialog styles applied successfully');
    } else {
        console.warn('Style element not found within shadow root');
    }
    
    // Check for "Please reload the page." message and auto-reload
    checkForReloadMessage(shadowRoot);
    
    console.log('Custom styles application complete');
}

function checkForReloadMessage(shadowRoot: ShadowRoot): void {
    let textObserver: MutationObserver | null = null;
    let cleanupObserver: MutationObserver | null = null;
    
    const checkParagraphs = (): boolean => {
        const paragraphs = shadowRoot.querySelectorAll('.components-reconnect-dialog p');
        
        for (const p of Array.from(paragraphs)) {
            const text = p.textContent?.trim() || '';
            console.log('Checking paragraph text:', text);
            
            if (text.includes(RELOAD_MESSAGE_TEXT)) {
                console.log(`Reload message detected, reloading page in ${RELOAD_DELAY_MS}ms...`);
                
                // Clean up observers
                if (textObserver) {
                    textObserver.disconnect();
                    textObserver = null;
                }
                if (cleanupObserver) {
                    cleanupObserver.disconnect();
                    cleanupObserver = null;
                }
                
                setTimeout(() => {
                    console.log('Reloading page now');
                    window.location.reload();
                }, RELOAD_DELAY_MS);
                
                return true;
            }
        }
        return false;
    };
    
    // Check immediately
    if (checkParagraphs()) {
        return;
    }
    
    // If message not found yet, observe for text changes
    const dialog = shadowRoot.querySelector('.components-reconnect-dialog');
    if (dialog) {
        textObserver = new MutationObserver(() => {
            if (checkParagraphs()) {
                // Observers are cleaned up in checkParagraphs
            }
        });
        
        textObserver.observe(dialog, {
            childList: true,
            subtree: true,
            characterData: true
        });
        
        console.log('Observing dialog for reload message changes');
        
        // Clean up observer if dialog is removed from DOM
        cleanupObserver = new MutationObserver(() => {
            if (!document.body.contains(dialog)) {
                console.log('Dialog removed from DOM, cleaning up observers');
                if (textObserver) {
                    textObserver.disconnect();
                    textObserver = null;
                }
                if (cleanupObserver) {
                    cleanupObserver.disconnect();
                    cleanupObserver = null;
                }
            }
        });
        
        cleanupObserver.observe(document.body, {
            childList: true,
            subtree: true
        });
    }
}

function initializeCustomReconnectDisplay(): void {
    let modalStyled = false;
    
    // Create a MutationObserver to watch for the reconnect modal being added
    const observer = new MutationObserver((mutations) => {
        if (modalStyled) {
            return; // Already handled, skip processing
        }
        
        for (const mutation of mutations) {
            // Convert NodeList to Array to iterate
            Array.from(mutation.addedNodes).forEach((node) => {
                if (node instanceof HTMLElement && !modalStyled) {
                    // Check if this is the reconnect modal (by id since class might be in shadow DOM)
                    if (node.id === MODAL_ID) {
                        console.log('Blazor reconnect modal detected, applying custom styles');
                        applyCustomStyles(node);
                        modalStyled = true;
                        observer.disconnect();
                        return;
                    }
                    
                    // Also check children in case it's added in a wrapper
                    const modal = node.querySelector(`#${MODAL_ID}`);
                    if (modal instanceof HTMLElement) {
                        console.log('Blazor reconnect modal detected in child, applying custom styles');
                        applyCustomStyles(modal);
                        modalStyled = true;
                        observer.disconnect();
                        return;
                    }
                }
            });
            
            if (modalStyled) {
                break;
            }
        }
    });

    // Check if modal already exists in DOM
    const existingModal = document.querySelector(`#${MODAL_ID}`);
    if (existingModal instanceof HTMLElement) {
        console.log('Blazor reconnect modal already exists in DOM, applying custom styles');
        applyCustomStyles(existingModal);
        modalStyled = true;
    } else {
        // Start observing the document body for added elements
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });

        console.log('Blazor reconnect style override initialized');
    }
}

// Initialize when the page loads
if (document.readyState === 'loading') {
    console.log('Document loading, waiting for DOMContentLoaded to initialize Blazor reconnect style override');
    document.addEventListener('DOMContentLoaded', initializeCustomReconnectDisplay);
} else {
    console.log('Document already loaded, initializing Blazor reconnect style override');
    initializeCustomReconnectDisplay();
}
