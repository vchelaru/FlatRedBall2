// Drag-drop write upgrade: dropped files/folders are read-only until requestPermission.
// Open Folder uses nativeFolder.js (HTML button + mode:readwrite) instead.
// See StoragePermissionInterop.cs.

function describe(obj) {
    if (!obj) return "null";
    const ownKeys = Object.getOwnPropertyNames(obj);
    const proto = Object.getPrototypeOf(obj);
    const protoKeys = proto ? Object.getOwnPropertyNames(proto) : [];
    const ctorName = (proto && proto.constructor && proto.constructor.name) || "?";
    return `ctor=${ctorName} own=[${ownKeys.join(",")}] proto=[${protoKeys.join(",")}]`;
}

async function requestOn(nativeHandle) {
    const descriptor = { mode: "readwrite" };
    let state = await nativeHandle.queryPermission(descriptor);
    if (state === "granted") return "granted";
    if (typeof nativeHandle.requestPermission !== "function") return "no-requestPermission-fn";
    state = await nativeHandle.requestPermission(descriptor);
    return state;
}

export async function ensureReadWrite(wrapper) {
    if (!wrapper) return "no-handle";

    const candidates = [wrapper, wrapper.handle, wrapper.file].filter(Boolean);
    for (const candidate of candidates) {
        if (typeof candidate.queryPermission === "function") {
            try {
                return await requestOn(candidate);
            } catch (e) {
                return "error:" + (e && e.message ? e.message : String(e));
            }
        }
    }

    return "no-queryPermission-fn on wrapper(" + describe(wrapper) + ") or wrapper.handle(" + describe(wrapper.handle) + ")";
}
