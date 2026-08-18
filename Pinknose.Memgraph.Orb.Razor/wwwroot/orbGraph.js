const scriptLoads = new Map();
const styleLoads = new Map();

function ensureScript(url, integrity) {
    if (!url) {
        throw new Error("An Orb script URL is required.");
    }

    if (!scriptLoads.has(url)) {
        scriptLoads.set(url, new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${url}"]`);
            if (existing) {
                resolve();
                return;
            }

            const script = document.createElement("script");
            script.src = url;
            script.async = true;

            // The browser verifies this hash itself and refuses to execute a
            // mismatched bundle, so no separate fetch-and-hash pass is needed.
            if (integrity) {
                script.integrity = integrity;
                script.crossOrigin = "anonymous";
            }

            script.onload = () => resolve();
            script.onerror = () => reject(new Error(`Failed to load script '${url}'.`));
            document.head.appendChild(script);
        }));
    }

    return scriptLoads.get(url);
}

function ensureStyle(url) {
    if (!url) {
        return;
    }

    if (!styleLoads.has(url)) {
        styleLoads.set(url, new Promise((resolve) => {
            const existing = document.querySelector(`link[href="${url}"]`);
            if (existing) {
                resolve();
                return;
            }

            const link = document.createElement("link");
            link.rel = "stylesheet";
            link.href = url;
            link.onload = () => resolve();
            link.onerror = () => resolve();
            document.head.appendChild(link);
        }));
    }

    return styleLoads.get(url);
}

function parseJson(value) {
    if (!value || !value.trim()) {
        return null;
    }

    return JSON.parse(value);
}

// The script tag resolving is not the same as the UMD bundle having run: a tag
// added by an earlier component may still be in flight when we adopt it above.
async function resolveOrbView(maxRetries = 50, delayMs = 20) {
    for (let i = 0; i < maxRetries; i++) {
        const candidate = globalThis.Orb?.OrbView;
        if (typeof candidate === "function") {
            return candidate;
        }

        if (i < maxRetries - 1) {
            await new Promise((resolve) => setTimeout(resolve, delayMs));
        }
    }

    throw new Error("The 'OrbView' export was not found on the global Orb namespace after script load.");
}

export async function initializeOrb(hostElement, scriptUrl, scriptIntegrity, styleUrl, dataJson, settingsJson) {
    if (!hostElement) {
        throw new Error("Host element is required.");
    }

    await Promise.all([ensureScript(scriptUrl, scriptIntegrity), ensureStyle(styleUrl)]);

    const OrbView = await resolveOrbView();
    const settings = parseJson(settingsJson) ?? undefined;
    const view = new OrbView(hostElement, settings);

    const data = parseJson(dataJson);
    if (data) {
        view.data.setup(data);
    }

    view.render(() => view.recenter());

    return { view, hostElement };
}

export function disposeOrb(handle) {
    if (!handle) {
        return;
    }

    const view = handle.view;
    const hostElement = handle.hostElement;

    if (view && typeof view.destroy === "function") {
        view.destroy();
    }

    if (hostElement) {
        hostElement.innerHTML = "";
    }
}
