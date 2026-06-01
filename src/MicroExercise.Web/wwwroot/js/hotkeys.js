// Global keyboard hotkeys for the One-Click Log dashboard.
// Digit keys 1-9 log the matching Quick-Log Card, so a burst can be recorded
// entirely mouse-free ("Greasing the Groove" without leaving the keyboard).
window.microburstHotkeys = {
    _handler: null,

    register(dotNetRef) {
        this.unregister();
        this._handler = (e) => {
            // Don't hijack browser/OS shortcuts or auto-repeat from a held key.
            if (e.ctrlKey || e.altKey || e.metaKey || e.repeat) return;
            // Don't steal keystrokes while the user is typing in a field.
            const t = e.target;
            if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' ||
                      t.tagName === 'SELECT' || t.isContentEditable)) return;
            if (e.key.length !== 1 || e.key < '1' || e.key > '9') return;

            e.preventDefault();
            dotNetRef.invokeMethodAsync('OnHotkey', Number(e.key));
        };
        document.addEventListener('keydown', this._handler);
    },

    unregister() {
        if (this._handler) {
            document.removeEventListener('keydown', this._handler);
            this._handler = null;
        }
    }
};
