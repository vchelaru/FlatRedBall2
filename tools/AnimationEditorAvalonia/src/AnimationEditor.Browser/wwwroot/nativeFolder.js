// Open Folder write support for Avalonia.Browser:
// 1. index.html early-patches showDirectoryPicker to force mode:'readwrite' before Avalonia's
//    storage.js closes over it (native-file-system-adapter).
// 2. patchAvaloniaStorageProvider wraps selectFolderDialog as belt-and-suspenders.
// 3. Hidden #frb-open-folder is a DOM click fallback when Avalonia's picker path isn't used.
// See docs/BROWSER_OPEN_FOLDER_SAVE_HANDOFF.md.

const OPEN_FOLDER_BUTTON_ID = "frb-open-folder";

/** @type {((dirHandle: FileSystemDirectoryHandle) => void | Promise<void>) | null} */
let folderPickedHandler = null;

export async function pickFolderReadWrite() {
    if (typeof globalThis.showDirectoryPicker !== "function") {
        console.warn("[nativeFolder] showDirectoryPicker not available");
        return null;
    }
    try {
        return await globalThis.showDirectoryPicker({ mode: "readwrite" });
    } catch (e) {
        if (e && e.name === "AbortError") return null;
        console.warn("[nativeFolder] showDirectoryPicker failed:", e && e.name, e && e.message);
        return null;
    }
}

export async function patchAvaloniaStorageProvider() {
    // Diagnostic-only (see docs/BROWSER_OPEN_FOLDER_SAVE_SESSION_HANDOFF.md): this patch's
    // success/failure is currently invisible -- NativeFolderInterop.InitializeAsync swallows any
    // exception here so boot never blocks, and the C# side only logs via Debug.WriteLine, which
    // does not reliably surface in the browser console. These console.log calls are the only way
    // to know whether this ever actually ran.
    const mod = await import("/_framework/storage.js");
    const StorageProvider = mod.StorageProvider;
    const StorageItem = mod.StorageItem;
    if (!StorageProvider) {
        console.log("[OpenFolder] patchAvaloniaStorageProvider: mod.StorageProvider is missing -- storage.js export shape changed?");
        return;
    }
    if (StorageProvider.__frbSelectFolderPatched) {
        console.log("[OpenFolder] patchAvaloniaStorageProvider: already patched, skipping");
        return;
    }

    const original = StorageProvider.selectFolderDialog.bind(StorageProvider);
    StorageProvider.selectFolderDialog = async function (startInItem, preferPolyfill) {
        console.log("[OpenFolder] patched StorageProvider.selectFolderDialog called, preferPolyfill:", preferPolyfill);
        if (preferPolyfill || typeof globalThis.showDirectoryPicker !== "function") {
            console.log("[OpenFolder] patched StorageProvider.selectFolderDialog: falling back to original (preferPolyfill or no native picker)");
            return original(startInItem, preferPolyfill);
        }
        const options = { mode: "readwrite" };
        const startIn =
            (startInItem && (startInItem.wellKnownType || startInItem.handle)) || undefined;
        if (startIn) options.startIn = startIn;
        const handle = await globalThis.showDirectoryPicker(options);
        return StorageItem.createFromHandle(handle);
    };
    StorageProvider.__frbSelectFolderPatched = true;
    console.log("[OpenFolder] patchAvaloniaStorageProvider: patched StorageProvider.selectFolderDialog successfully");
}

export function bindOpenFolderButton() {
    const btn = document.getElementById(OPEN_FOLDER_BUTTON_ID);
    if (!btn || btn.__frbBound) return;
    btn.__frbBound = true;
    // pointer-events:none in CSS — only activated via .click() from Avalonia Load, or we
    // temporarily enable pointer-events when using as primary path.
    btn.addEventListener("click", async () => {
        if (typeof window.showDirectoryPicker !== "function") {
            console.warn("[nativeFolder] showDirectoryPicker not available");
            return;
        }
        let handle;
        try {
            handle = await window.showDirectoryPicker({ mode: "readwrite" });
        } catch (e) {
            if (e && e.name === "AbortError") return;
            console.warn("[nativeFolder] showDirectoryPicker failed:", e && e.name, e && e.message);
            return;
        }
        if (typeof folderPickedHandler === "function") {
            try {
                await folderPickedHandler(handle);
            } catch (e) {
                console.error("[nativeFolder] folderPickedHandler failed:", e);
            }
        }
    });
}

export function setFolderPickedHandler(handler) {
    folderPickedHandler = handler;
}

/** Forwards Avalonia File→Load to the hidden DOM button (best-effort activation). */
export function clickOpenFolderButton() {
    const btn = document.getElementById(OPEN_FOLDER_BUTTON_ID);
    if (!btn) return;
    // Temporarily allow the synthetic click to run the listener; picker still needs activation.
    btn.style.pointerEvents = "auto";
    try {
        btn.click();
    } finally {
        btn.style.pointerEvents = "none";
    }
}

export async function queryWritePermission(dirHandle) {
    if (!dirHandle || typeof dirHandle.queryPermission !== "function") return "no-queryPermission";
    try {
        return await dirHandle.queryPermission({ mode: "readwrite" });
    } catch (e) {
        return "error:" + (e && e.message ? e.message : String(e));
    }
}

export async function ensureReadWrite(dirHandle) {
    if (!dirHandle) return "no-handle";
    if (typeof dirHandle.queryPermission !== "function") return "no-queryPermission-fn";
    const descriptor = { mode: "readwrite" };
    try {
        let state = await dirHandle.queryPermission(descriptor);
        // Diagnostic-only: distinguishes "pick genuinely granted readwrite already" (query ==
        // granted, requestPermission never called) from "query already says denied/prompt" --
        // the latter means the picker itself did not obtain readwrite (patches ineffective) or a
        // prior test stuck this exact handle at denied, either way requestPermission below is
        // very unlikely to show real UI. See docs/BROWSER_OPEN_FOLDER_SAVE_SESSION_HANDOFF.md.
        console.log("[OpenFolder] queryPermission(readwrite) initial state:", state);
        if (state === "granted") return "granted";
        if (typeof dirHandle.requestPermission !== "function") return "no-requestPermission-fn";
        state = await dirHandle.requestPermission(descriptor);
        console.log("[OpenFolder] requestPermission(readwrite) state:", state);
        return state;
    } catch (e) {
        return "error:" + (e && e.name ? e.name : "?") + ":" + (e && e.message ? e.message : String(e));
    }
}

export function directoryName(dirHandle) {
    return dirHandle.name;
}

/**
 * Recursively walks dirHandle so projects that nest textures in subfolders resolve (#768) --
 * `prefix` accumulates the "sub/dir/" segments as recursion descends. Files are returned as
 * root-relative, forward-slash paths (a root-level file's path is unchanged from before this
 * became recursive, so single-folder projects see identical output).
 */
async function collectFileNamesRecursive(dirHandle, prefix, names) {
    for await (const [name, entry] of dirHandle.entries()) {
        const path = prefix + name;
        // Diagnostic-only (see docs/BROWSER_OPEN_FOLDER_SAVE_SESSION_HANDOFF.md): a
        // NotFoundError crashed this exact enumeration once already (2026-07-14, Edge, real
        // readwrite grant) with no indication of which entry triggered it. Logging each entry
        // as it's seen pinpoints exactly where a repeat failure happens.
        console.log("[OpenFolder] listFileNames entry:", path, entry.kind);
        if (entry.kind === "file") names.push(path);
        else if (entry.kind === "directory") await collectFileNamesRecursive(entry, path + "/", names);
    }
}

export async function listFileNames(dirHandle) {
    const names = [];
    try {
        await collectFileNamesRecursive(dirHandle, "", names);
    } catch (e) {
        console.log(
            "[OpenFolder] listFileNames threw after", names.length, "entries -- last successful:",
            names[names.length - 1], "error:", e && e.name, e && e.message);
        throw e;
    }
    return JSON.stringify(names);
}

export async function listSubfolderNames(dirHandle) {
    const names = [];
    for await (const [name, entry] of dirHandle.entries()) {
        if (entry.kind === "directory") names.push(name);
    }
    return JSON.stringify(names);
}

export async function getDirectoryHandle(dirHandle, name) {
    return await dirHandle.getDirectoryHandle(name);
}

/**
 * #763 fallback: directory *enumeration* (dirHandle.entries()) can throw NotFoundError on some
 * environments even though named lookups (getFileHandle) on the identical handle keep working
 * (see listFileNames's doc comment above). When that happens, ask the user to pick the .achx
 * directly instead of auto-discovering it via enumeration -- seeded with startIn so the picker
 * opens already inside the folder just granted, no re-navigation required. Returns the picked
 * file's path relative to dirHandle (via dirHandle.resolve(), which walks however many folders
 * deep the user navigated in the OS picker -- #768: without this, a texture referenced relative
 * to a *nested* achx's own folder would be looked up from dirHandle's root instead and never
 * resolve), or the bare name if resolve() can't place it (should not happen since the picker was
 * seeded with dirHandle as startIn). Returns null if the user cancels.
 */
export async function pickAchxFile(dirHandle) {
    if (typeof globalThis.showOpenFilePicker !== "function") return null;
    try {
        const [fileHandle] = await globalThis.showOpenFilePicker({
            startIn: dirHandle,
            types: [{ description: "Animation Chain", accept: { "text/xml": [".achx"] } }],
            excludeAcceptAllOption: true,
        });
        if (!fileHandle) return null;
        try {
            const segments = await dirHandle.resolve(fileHandle);
            if (segments) return segments.join("/");
        } catch (e) {
            console.log("[OpenFolder] pickAchxFile: dirHandle.resolve failed, falling back to bare name:", e && e.message);
        }
        return fileHandle.name;
    } catch (e) {
        if (e && e.name === "AbortError") return null;
        throw e;
    }
}

/**
 * Walks from dirHandle to the FileSystemFileHandle at relativePath, which may contain "/"
 * segments (#768 -- lets textures nested in subfolders resolve). A slash-free name walks zero
 * directories and behaves exactly as the old flat `getFileHandle(name)` call did.
 */
async function resolveFileHandle(dirHandle, relativePath, { create = false } = {}) {
    const parts = relativePath.replace(/\\/g, "/").split("/").filter(p => p.length > 0 && p !== ".");
    let dir = dirHandle;
    for (let i = 0; i < parts.length - 1; i++) dir = await dir.getDirectoryHandle(parts[i], { create });
    return dir.getFileHandle(parts[parts.length - 1], { create });
}

export async function fileInfo(dirHandle, name) {
    const fileHandle = await resolveFileHandle(dirHandle, name);
    const file = await fileHandle.getFile();
    return JSON.stringify({ size: file.size, lastModified: file.lastModified });
}

/** Fast base64 via FileReader (manual btoa loops hang on large PNGs). */
export async function readFileBase64(dirHandle, name) {
    const fileHandle = await resolveFileHandle(dirHandle, name);
    const file = await fileHandle.getFile();
    const dataUrl = await new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(reader.error || new Error("FileReader failed"));
        reader.readAsDataURL(file);
    });
    const comma = dataUrl.indexOf(",");
    return comma >= 0 ? dataUrl.slice(comma + 1) : dataUrl;
}

export async function writeFileBytes(dirHandle, name, bytes) {
    try {
        const fileHandle = await resolveFileHandle(dirHandle, name, { create: true });
        const writable = await fileHandle.createWritable();
        await writable.write(bytes);
        await writable.close();
        return "ok";
    } catch (e) {
        return "error:" + (e && e.name ? e.name : "?") + ":" + (e && e.message ? e.message : String(e));
    }
}
