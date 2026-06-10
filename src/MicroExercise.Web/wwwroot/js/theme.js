// Minimal dark/light theme handling via Bootstrap 5.3's data-bs-theme attribute.
window.microburstTheme = {
    current() {
        return document.documentElement.getAttribute('data-bs-theme') || 'light';
    },
    apply(theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
        try { localStorage.setItem('microburst-theme', theme); } catch { /* ignore */ }
    },
    toggle() {
        const next = this.current() === 'dark' ? 'light' : 'dark';
        this.apply(next);
        return next;
    }
};

// Small localStorage-backed store for lightweight client UI preferences (e.g. the
// log-screen view mode). Keys are namespaced; failures (private mode, quota) are ignored.
window.microburstPrefs = {
    get(key, fallback) {
        try { return localStorage.getItem('microburst-' + key) ?? fallback; }
        catch { return fallback; }
    },
    set(key, value) {
        try { localStorage.setItem('microburst-' + key, value); } catch { /* ignore */ }
    }
};
