// .NET 9 reports reconnection state through CSS classes, not DOM events.
const reconnectModal = document.getElementById("components-reconnect-modal");
const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);
document.getElementById("components-reload-button").addEventListener("click", () => location.reload());
reconnectModal.addEventListener("cancel", event => event.preventDefault());

function updateState() {
    const classes = reconnectModal.classList;
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    if (classes.contains("components-reconnect-rejected")) {
        location.reload();
    } else if (classes.contains("components-reconnect-hide")) {
        reconnectModal.close();
    } else if (classes.contains("components-reconnect-show") || classes.contains("components-reconnect-failed")) {
        if (!reconnectModal.open) reconnectModal.showModal();
        if (classes.contains("components-reconnect-failed")) {
            document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
        }
    }
}

new MutationObserver(updateState).observe(reconnectModal, { attributes: true, attributeFilter: ["class"] });
updateState();

async function retry() {
    if (retryButton.disabled) return;
    retryButton.disabled = true;
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    try {
        if (await Blazor.reconnect()) {
            reconnectModal.close();
        } else {
            // The server no longer retains this circuit. Start a fresh page.
            location.reload();
        }
    } catch {
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } finally {
        retryButton.disabled = false;
    }
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") await retry();
}
