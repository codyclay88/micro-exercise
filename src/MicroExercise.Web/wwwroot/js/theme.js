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
