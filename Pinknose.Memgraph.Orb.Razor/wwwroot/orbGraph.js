const SCRIPT_URL = "./_content/Pinknose.Memgraph.Orb.Razor/vendor/memgraph/orb/1.0.2/orb.min.js";
const SCRIPT_INTEGRITY = "sha384-/bdC+Sgoda/KpkiTPljaZXPEpNJg712oGud22zh7zsoVZch3PRsvcjfqNLRCaIoT";

let scriptLoad = null;

function ensureScript() {
    if (scriptLoad) {
        return scriptLoad;
    }

    scriptLoad = new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[src="${SCRIPT_URL}"]`);
        if (existing) {
            resolve();
            return;
        }

        const script = document.createElement("script");
        script.src = SCRIPT_URL;
        script.async = true;
        // The browser verifies this itself and refuses to execute a mismatched bundle.
        script.integrity = SCRIPT_INTEGRITY;
        script.crossOrigin = "anonymous";
        script.onload = () => resolve();
        script.onerror = () => {
            // Without this, one transient network failure poisons scriptLoad for the life
            // of the page: every later component would await the same rejected promise
            // and fail immediately, with no way to recover. Clearing it lets the next
            // ensureScript() call start a fresh attempt.
            scriptLoad = null;
            reject(new Error(`Failed to load Orb from '${SCRIPT_URL}'.`));
        };
        document.head.appendChild(script);
    });

    return scriptLoad;
}

// A resolved script tag is not the same as the UMD bundle having run: a tag added by an
// earlier component may still be in flight when we adopt it above.
async function resolveOrbView(maxRetries = 50, delayMs = 20) {
    for (let i = 0; i < maxRetries; i++) {
        if (typeof globalThis.Orb?.OrbView === "function") {
            return globalThis.Orb.OrbView;
        }

        if (i < maxRetries - 1) {
            await new Promise((resolve) => setTimeout(resolve, delayMs));
        }
    }

    throw new Error("The 'OrbView' export was not found on the global Orb namespace.");
}

function parseJson(value) {
    return value && value.trim() ? JSON.parse(value) : null;
}

// Orb's _applyStyle() skips any node that already has a style, so setDefaultStyle would
// apply exactly once per node and never repaint a changed style. Push them explicitly.
//
// Always call setStyle, even when the payload carries no style (node.style is undefined).
// Orb's setStyle() replaces the node's _style wholesale and merge() never touches it, so
// skipping the call when a style is cleared (e.g. a consumer's Style/Label goes from
// non-null back to null) would leave the previously-applied style painted forever -- there
// would be no way back to the unstyled look.
//
// Pushing {} is NOT safe here: OrbView's constructor unconditionally installs
// setDefaultStyle(getDefaultGraphStyle()) (views/orb-view.js), and Graph.setup()/merge()
// call _applyStyle() synchronously, right before this function runs, which paints that
// default onto anything whose hasStyle() is false. setStyle replaces _style wholesale, so
// {} would wipe that default and leave the node at getRadius() === 0 (invisible and
// unhittable, since hit-testing uses the radius) and the edge at getWidth() === 0 (never
// drawn -- see renderer/canvas/edge/base.js). Push Orb's own default back instead, so a
// cleared style reverts to Orb's look rather than vanishing.
function pushStyles(view, payload) {
    const graph = view.data;
    const defaults = globalThis.Orb.getDefaultGraphStyle();

    // A projected style is partial by nature: it carries only the properties the caller set,
    // plus the label, which Orb keeps inside the style object rather than beside it. Orb's
    // setStyle() REPLACES the style wholesale, so pushing that partial object on its own
    // costs the target every default it had -- and both defaults that matter are load-bearing:
    // a node's size feeds getRadius() (0 renders it invisible and unhittable) and an edge's
    // width feeds getWidth() (0 means the renderer returns before drawing it). Merging over
    // Orb's own defaults keeps everything the caller did not speak to, including for a target
    // with no projected style at all, where the spread of null leaves the defaults untouched.
    for (const node of payload.nodes) {
        const target = graph.getNodeById(node.id);
        if (target) {
            target.setStyle(
                { ...defaults.getNodeStyle(target), ...node.style },
                { isNotifySkipped: true });
        }
    }

    for (const edge of payload.edges) {
        const target = graph.getEdgeById(edge.id);
        if (target) {
            target.setStyle(
                { ...defaults.getEdgeStyle(target), ...edge.style },
                { isNotifySkipped: true });
        }
    }
}

function pointsOf(event) {
    return {
        localX: event.localPoint?.x ?? 0,
        localY: event.localPoint?.y ?? 0,
        globalX: event.globalPoint?.x ?? 0,
        globalY: event.globalPoint?.y ?? 0
    };
}

function subscribe(handle, subscribedEvents) {
    const wanted = new Set(subscribedEvents ?? []);
    const { view, dotNetRef } = handle;

    const send = (type, id, event) =>
        dotNetRef.invokeMethodAsync("HandleOrbEvent", type, { id, ...pointsOf(event) });

    const classify = (subject) => {
        if (!subject) return null;
        if (globalThis.Orb.isNode(subject)) return "node";
        if (globalThis.Orb.isEdge(subject)) return "edge";
        return null;
    };

    const forwardSubject = (orbEvent, nodeType, edgeType) => {
        view.events.on(orbEvent, (e) => {
            const kind = classify(e.subject ?? e.node ?? e.edge);
            const subject = e.subject ?? e.node ?? e.edge;
            if (kind === "node" && wanted.has(nodeType)) send(nodeType, String(subject.id), e);
            else if (kind === "edge" && wanted.has(edgeType)) send(edgeType, String(subject.id), e);
            else if (!kind && wanted.has("background-click") && orbEvent === "mouse-click") {
                send("background-click", null, e);
            }
        });
    };

    forwardSubject("mouse-click", "node-click", "edge-click");
    forwardSubject("mouse-double-click", "node-double-click", "edge-double-click");
    forwardSubject("mouse-right-click", "node-right-click", "edge-right-click");

    const hoverWanted = ["node-hover-enter", "node-hover-leave",
                         "edge-hover-enter", "edge-hover-leave"].some((t) => wanted.has(t));

    if (hoverWanted) {
        // Orb's DefaultEventStrategy.onMouseMove only ever calls getNearestNode — it never
        // hit-tests edges. So mouse-move's `subject` is node-or-nothing, and the native
        // edge-hover event is gated on that same strategy and never fires either. Verified
        // empirically and in orb's strategy.js (Task 1 spike). We therefore hit-test
        // ourselves off the public view.data facade.
        let hoveredId = null;
        let hoveredKind = null;

        view.events.on("mouse-move", (e) => {
            const point = e.localPoint;
            let subject = point ? view.data.getNearestNode(point) : null;
            let kind = subject ? "node" : null;

            if (!subject && point) {
                // getNearestEdge(point, minDistance = 3) — the default threshold means
                // empty space resolves to nothing rather than the nearest edge on screen.
                // Do not pass a threshold; Orb's default is what its own click path uses.
                subject = view.data.getNearestEdge(point);
                kind = subject ? "edge" : null;
            }

            const id = subject ? String(subject.id) : null;

            if (id === hoveredId) {
                return;
            }

            if (hoveredId) {
                const leave = `${hoveredKind}-hover-leave`;
                if (wanted.has(leave)) send(leave, hoveredId, e);
            }

            if (id) {
                const enter = `${kind}-hover-enter`;
                if (wanted.has(enter)) send(enter, id, e);
            }

            hoveredId = id;
            hoveredKind = kind;
        });
    }
}

export async function initializeOrb(host, dotNetRef, settingsJson, dataJson, subscribedEvents) {
    if (!host) {
        throw new Error("Host element is required.");
    }

    await ensureScript();
    const OrbView = await resolveOrbView();

    // Built bare on purpose, then given the caller's settings a line later. Orb's
    // setSettings() merges into whatever is already there, so once a setting has been applied
    // there is no value that means "unset" -- clearing has to re-apply the defaults instead.
    // Snapshotting them off a bare view is the only faithful source for those: getSettings()
    // hands back a deep copy, and the alternative, hardcoding Orb's defaults here, would rot
    // silently the first time Orb changed one.
    const settings = parseJson(settingsJson);
    const positions = new Map();
    const view = new OrbView(host, {
        // Read on every setup and merge, for the nodes being added. This is what decides where a
        // node ENTERS the simulation -- setNodePositions writes the rendered position after the
        // fact and the simulator overwrites it.
        getPosition: (node) => positions.get(String(node.getId())),
    });
    const handle = { view, host, dotNetRef, positions, defaultSettings: view.getSettings() };

    if (settings) {
        view.setSettings(settings);
    }

    const payload = parseJson(dataJson);
    if (payload) {
        view.data.setup(payload);
        pushStyles(view, payload);
    }

    subscribe(handle, subscribedEvents);
    view.render(() => view.recenter());
    installPositionHook(handle);

    // Opt-in test seam: the browser suite needs to inspect graph state and node positions,
    // but this must never ship live on a real page. Setting it unconditionally would pin
    // the OrbView (and everything it references -- the emitter, our subscribe closures, the
    // DotNetObjectReference, the canvas) for the document's lifetime, and with two
    // components on one page it would be last-writer-wins. The host element opts in via
    // data-orb-test, which the sample page sets through AdditionalAttributes.
    if (host.hasAttribute("data-orb-test")) {
        globalThis.__orbTestView = view;
    }

    return handle;
}

// Re-asserts the position hook after any settings change. setSettings merges and resetSettings
// re-applies a snapshot; neither is guaranteed to carry a function member through Orb's own
// copying. Re-installing is cheap and removes the question entirely.
function installPositionHook(handle) {
    handle.view.setSettings({
        getPosition: (node) => handle.positions.get(String(node.getId())),
    });
}

export function updateData(handle, dataJson, removedNodeIds, removedEdgeIds) {
    if (!handle) return;

    const payload = parseJson(dataJson);
    if (payload) {
        handle.view.data.merge(payload);
    }

    if (removedNodeIds?.length || removedEdgeIds?.length) {
        handle.view.data.remove({
            nodeIds: removedNodeIds ?? [],
            edgeIds: removedEdgeIds ?? []
        });
    }

    if (payload) {
        pushStyles(handle.view, payload);
    }

    handle.view.render();
}

export function applySettings(handle, settingsJson) {
    const settings = parseJson(settingsJson);
    if (handle && settings) {
        handle.view.setSettings(settings);
        installPositionHook(handle);
    }
}

// Settings going back to null: re-apply the snapshot taken before anything was ever applied.
export function resetSettings(handle) {
    if (handle) {
        handle.view.setSettings(handle.defaultSettings);
        installPositionHook(handle);
    }
}

// Merges rather than replaces, so a caller seeding newly arrived nodes need not restate the ones
// already placed -- which is the accumulating case this exists for.
export function setSeedPositions(handle, positions) {
    if (!handle || !positions) return;

    for (const p of positions) {
        handle.positions.set(String(p.id), { x: p.x, y: p.y });
    }
}

export function clearSeedPositions(handle) {
    handle?.positions.clear();
}

// Writes the rendered position directly. The simulator never sees this -- it is not read by
// getPosition or _assignPositions, so it holds even under a hot simulation (measured in Task 1).
export function setNodePositions(handle, positions) {
    if (!handle || !positions?.length) return;

    handle.view.data.setNodePositions(positions);
    handle.view.render();
}

export function clearNodePositions(handle) {
    if (!handle) return;

    handle.view.data.clearPositions();
    handle.view.render();
}

export function recenter(handle)      { handle?.view.recenter(); }
export function zoomIn(handle)        { handle?.view.zoomIn(); }
export function zoomOut(handle)       { handle?.view.zoomOut(); }
export function fixNodes(handle)      { handle?.view.fixNodes(); }
export function releaseNodes(handle)  { handle?.view.releaseNodes(); }
export function unselectAll(handle)   { handle?.view.interaction.unselectAll(); handle?.view.render(); }
export function selectNode(handle, id) { handle?.view.interaction.selectNodeById(id); handle?.view.render(); }
export function selectEdge(handle, id) { handle?.view.interaction.selectEdgeById(id); handle?.view.render(); }
export function getSvg(handle)        { return handle ? handle.view.getSVG() : ""; }

export function disposeOrb(handle) {
    if (!handle) return;

    // Clear the opt-in test hook if it still points at this handle's view, so a disposed
    // component's view (and everything it roots) does not outlive the component.
    if (globalThis.__orbTestView === handle.view) {
        delete globalThis.__orbTestView;
    }

    // Drop our subscribe() closures (and the DotNetObjectReference they capture) before
    // destroy(), rather than relying on destroy() to have done it -- removeAllListeners()
    // makes that explicit instead of implicit.
    handle.view?.events?.removeAllListeners?.();
    handle.view?.destroy();

    if (handle.host) {
        handle.host.innerHTML = "";
    }

    handle.view = null;
    handle.dotNetRef = null;
    handle.positions?.clear();
}
