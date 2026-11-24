// Small JS helpers called from Blazor through IJSRuntime (JS interop).
window.familiaQuiz = {
    // Global keyboard shortcuts: 1-9 and Enter, ignored while typing in a field
    // (and Enter on a focused button is left to the button itself).
    registerKeys(dotnetRef) {
        const handler = e => {
            if (e.ctrlKey || e.altKey || e.metaKey) return;
            const t = e.target;
            const typing = t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || t.isContentEditable);
            if (typing) return;
            // Enter on a focused button clicks it, except for buttons that opt into the shortcut
            // (e.g. answer options: click to select, then Enter confirms).
            if (e.key === 'Enter' && t && t.tagName === 'BUTTON' && !t.hasAttribute('data-enter-shortcut')) return;
            if (/^[1-9]$/.test(e.key) || e.key === 'Enter') {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('OnKey', e.key);
            }
        };
        document.addEventListener('keydown', handler);
        return { dispose: () => document.removeEventListener('keydown', handler) };
    },

    copyText(text) {
        return navigator.clipboard.writeText(text);
    },

    storageGet(key) {
        try { return localStorage.getItem(key); } catch { return null; }
    },
    storageSet(key, value) {
        try { localStorage.setItem(key, value); } catch { /* private mode: ignore */ }
    },
    storageRemove(key) {
        try { localStorage.removeItem(key); } catch { /* ignore */ }
    }
};
