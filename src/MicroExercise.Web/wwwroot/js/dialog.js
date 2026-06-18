// Focus an input and select its contents so a pre-filled default is overwritten by typing.
// Used by the shared burst-log dialog (BurstLogDialog.razor) when it opens.
window.microburstDialog = {
    focusSelect: function (el) {
        if (!el) return;
        el.focus();
        if (typeof el.select === 'function') el.select();
    }
};
