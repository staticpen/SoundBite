// .NET 9 reports reconnection state through CSS classes, not DOM events.
const reconnectModal = document.getElementById("components-reconnect-modal");
const retryButton = document.getElementById("components-reconnect-button");
// Retries the circuit when the user selects the retry button.
retryButton.addEventListener("click", retry);
// Reloads the page when the user selects the reload button.
document.getElementById("components-reload-button").addEventListener("click", () => location.reload());
// Keeps the reconnect dialog open when its cancel action is requested.
reconnectModal.addEventListener("cancel", event => event.preventDefault());

// Updates the dialog and retry listener to match Blazor's reconnect state.
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

// Watches the reconnect dialog for changes to its state classes.
new MutationObserver(updateState).observe(reconnectModal, { attributes: true, attributeFilter: ["class"] });
updateState();

// Attempts to reconnect and reloads if the circuit cannot be recovered.
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

// Retries automatically when the page becomes visible again.
async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") await retry();
}
