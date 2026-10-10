// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

// Keeps a tethered scanner's keystrokes from being lost (E82.4): when focus leaves an element and lands on
// nothing (the page body, e.g. after a click on empty space or a closed dialog), it goes back to the scan field.
// Focus that moves to another input, button or link is left alone, so the screen's own fields still work.
export function attach(field) {
    let pending = 0;

    const onFocusOut = () => {
        // focusout fires before the new element is focused; look once focus has settled. Repeated focusouts
        // coalesce into one check.
        clearTimeout(pending);
        pending = setTimeout(() => { // DevSkim: ignore DS172411 - a closure, nothing is evaluated from a string
            pending = 0;
            const active = document.activeElement;
            if (field.isConnected && (active === null || active === document.body)) {
                field.focus();
            }
        }, 0);
    };

    document.addEventListener("focusout", onFocusOut);

    return {
        // A check already queued is cancelled too, so a field being torn down is never refocused.
        detach: () => {
            document.removeEventListener("focusout", onFocusOut);
            clearTimeout(pending);
        },
    };
}
