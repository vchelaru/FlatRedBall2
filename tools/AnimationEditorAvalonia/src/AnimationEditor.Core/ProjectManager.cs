using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core
{
    public class ProjectManager : IProjectManager
    {
        static TileMapInformationList mTileMapInformationList = new TileMapInformationList();

        public AnimationChainListSave? AnimationChainListSave { get; set; }

        public TileMapInformationList TileMapInformationList
        {
            get => mTileMapInformationList;
            set => mTileMapInformationList = value;
        }

        public FilePath[] ReferencedPngs { get; set; } = new FilePath[0];

        public string? FileName { get; set; }

        /// <summary>
        /// The tileset behind a native <c>.tsx</c> project (issue #1140), kept in memory (rather
        /// than re-read from disk) so a load-then-save round trip preserves any tileset content
        /// AnimationEditor doesn't understand -- same reasoning as <see cref="TilesetAnimationSync"/>
        /// for the achj-push feature. <see langword="null"/> for an achx/achj project.
        /// </summary>
        private DotTiled.Tileset? _tsxTileset;

        /// <summary>Each native-tsx chain's own tile id, keyed by chain object reference (survives
        /// a rename, unlike keying by name) -- populated on <see cref="LoadTsxProject"/> from what
        /// the file already said, and kept current after every <see cref="SaveTsxProject"/> so a
        /// brand-new chain's first-save id keeps being reused on every later save instead of being
        /// recomputed (and potentially drifting) from geometry each time. See <see
        /// cref="Tiled.MultiTileToTiledAnimationMapper"/>'s <c>knownEntryTileIds</c> parameter.</summary>
        private Dictionary<AnimationChainSave, uint> _tsxEntryTileIdsByChain = new(ReferenceEqualityComparer.Instance);

        /// <summary>The satellite equivalent of <see cref="_tsxEntryTileIdsByChain"/>: each
        /// chain's satellites' own tile ids, keyed by chain reference then by the satellite's
        /// (Dx, Dy) offset within the footprint. Kept so a save whose mapping fails (bad geometry,
        /// wrong texture) still reports the tiles the chain owns instead of orphaning them, and so
        /// an offset that leaves the footprint (a shrink) and comes back (its Undo) lands on the
        /// same tile. See <see cref="Tiled.MultiTileToTiledAnimationMapper"/>'s
        /// <c>knownSatelliteTileIds</c> parameter.</summary>
        private Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>> _tsxSatelliteTileIdsByChain = new(ReferenceEqualityComparer.Instance);

        /// <summary>Chains whose <see cref="AnimationChainSave.Name"/> is still the synthetic
        /// <c>"ID:{tileId}"</c> placeholder (<see cref="Tiled.TiledAnimationToAchjMapper.SyntheticChainName"/>)
        /// rather than a real, explicitly-given one -- set from real provenance at load time (<see
        /// cref="Tiled.TiledAnimationToAchjMapper.Map"/>'s <c>chainsWithSyntheticName</c> out param:
        /// did the tile carry a <c>Name</c> property at all?), never guessed from whether the current
        /// text happens to *look* like the placeholder format. <see cref="TrySetTsxOwnerTileId"/>
        /// reads this to decide whether to keep a chain's placeholder following its owner tile;
        /// <see cref="MarkChainNameExplicit"/> (called on a real rename) removes a chain once it has
        /// a real name, so it's never auto-renamed again.</summary>
        private HashSet<AnimationChainSave> _tsxSyntheticNamedChains = new(ReferenceEqualityComparer.Instance);

        /// <summary>Each chain's own <see cref="AnimationChainSave.Frames"/> contents as of the
        /// most recent save where it had a real (non-null) entry tile id -- i.e. the frame-object
        /// sequence that produced <see cref="_tsxEntryTileIdsByChain"/>'s current value for that
        /// chain. Used only to seed a <see cref="DormantTsxHint"/> the moment a chain's frames go
        /// from non-empty to empty (see <see cref="_tsxDormantHintsByChain"/>).</summary>
        private Dictionary<AnimationChainSave, IReadOnlyList<AnimationFrameSave>> _tsxLastNonEmptyFramesByChain = new(ReferenceEqualityComparer.Instance);

        /// <summary>A chain's tile-identity hint, held onto after <see cref="SaveTsxProject"/> sees
        /// its <see cref="AnimationChainSave.Frames"/> go empty, so a later save can tell apart two
        /// scenarios that otherwise look identical (chain still present in <see
        /// cref="AnimationChainListSave"/>, entry tile id null on the intervening save):
        /// <c>DeleteFramesCommand</c>'s <c>Undo()</c> re-inserting the exact same frame objects it
        /// just removed (must restore the original tile), vs. the user genuinely re-authoring the
        /// chain with different frame content afterward (must
        /// compute fresh, per <c>SaveTsxProject_AllFramesDeletedFromChain_...</c>). Neither <see
        /// cref="AnimationFrameSave"/> nor <see cref="AnimationChainSave"/> is ever cloned by
        /// undo/redo in this codebase, so comparing the current frame list against <see cref="
        /// DormantTsxHint.Frames"/> by object reference (not value) is a reliable "is this the same
        /// Undo?" test.</summary>
        private Dictionary<AnimationChainSave, DormantTsxHint> _tsxDormantHintsByChain = new(ReferenceEqualityComparer.Instance);

        /// <summary>See <see cref="_tsxDormantHintsByChain"/>.</summary>
        private sealed record DormantTsxHint(
            uint EntryTileId,
            IReadOnlyDictionary<(int Dx, int Dy), uint> Satellites,
            IReadOnlyList<AnimationFrameSave> Frames);

        /// <summary>Whether the currently loaded project is a native <c>.tsx</c> project (see
        /// <see cref="LoadTsxProject"/>) rather than an achx/achj project.</summary>
        public bool IsNativeTsxProject => _tsxTileset != null;

        /// <summary>Guards every achx/achj-format save method (<see
        /// cref="SaveAnimationChainList(string)"/>, its <see cref="Stream"/> overload, and <see
        /// cref="SaveAnimationChainListAsync"/>) against being called on a native tsx project --
        /// defense-in-depth alongside <c>AppCommands.SaveCurrentAnimationChainList</c>'s own branch
        /// on <see cref="IsNativeTsxProject"/>, for any caller that reaches <see
        /// cref="ProjectManager"/> directly instead.</summary>
        private void ThrowIfNativeTsxProject()
        {
            if (IsNativeTsxProject)
                throw new InvalidOperationException(
                    "Cannot save a native tsx project via SaveAnimationChainList -- use SaveTsxProject instead.");
        }

        /// <summary>The tsx's own tile grid (tile size, margin, spacing), or <see langword="null"/>
        /// for an achx/achj project. A native tsx project's wireframe grid is always this -- it is
        /// not user-configurable (see issue #1140).</summary>
        public TileGrid? TsxTileGrid =>
            _tsxTileset is null ? null : new TileGrid(_tsxTileset.TileWidth, _tsxTileset.TileHeight, _tsxTileset.Margin, _tsxTileset.Spacing);

        /// <summary>
        /// The folder explicitly picked via File → Open Project Folder (or restored from
        /// <c>LastProjectFolderPath</c> at startup). Unlike <see cref="ResolveFilesPanelRoot"/>,
        /// this is never inferred from the open .achx -- it stays set (and the Files panel's
        /// Project scope keeps working) even with zero tabs open. Null until a project folder has
        /// ever been opened this session.
        /// </summary>
        public string? ProjectFolderPath { get; set; }

        /// <summary>
        /// The coordinate format the .achx should be written with. Set from the loaded
        /// file so a UV-format file round-trips as UV; defaults to <see cref="TextureCoordinateType.Pixel"/>
        /// for new files, since that is the preferred format going forward. Independent
        /// of the in-memory representation, which is always UV so the rendering pipeline
        /// can render at any texture size.
        /// </summary>
        public TextureCoordinateType OnDiskCoordinateType { get; set; } = TextureCoordinateType.Pixel;

        /// <summary>
        /// Texture sizes supplied to the most recent <see cref="LoadAnimationChain"/> call, kept
        /// around so <see cref="SaveAnimationChainList(Stream)"/> can convert back to Pixel
        /// coordinates without a filesystem to re-read PNG headers from (the browser-wasm build
        /// has no disk at all, unlike <see cref="SaveAnimationChainList(string)"/>'s directory).
        /// Plain per-load instance state with no public getter, same shape as the tsx fields
        /// below -- see <see cref="CaptureTextureSizeState"/>/<see cref="RestoreTextureSizeState"/>
        /// for why a tab-switch cache also needs to round-trip this.
        /// </summary>
        private IReadOnlyDictionary<string, (int Width, int Height)>? _knownTextureSizes;

        /// <param name="fileName">The .achx path. Only read from disk when <paramref name="preParsed"/> is null.</param>
        /// <param name="preParsed">Already-parsed content (e.g. fetched over HTTP on the browser-wasm build), skipping the disk read.</param>
        /// <param name="knownTextureSizes">
        /// Pixel dimensions for textures the caller has already decoded (keyed by <see cref="AnimationFrameSave.TextureName"/>),
        /// used instead of reading a PNG header from disk when converting Pixel coordinates to UV. Needed on the
        /// browser-wasm build, which has no filesystem to read texture headers from but already decodes every
        /// dropped/picked PNG into memory. Sizes not present here still fall back to a disk read.
        /// </param>
        public void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null)
        {
            AnimationChainListSave acls;
            if (preParsed != null)
            {
                // Caller already has the parsed content (e.g. fetched over HTTP with no local
                // filesystem, as on the browser-wasm build) — nothing to read from disk, so the
                // existence check below only applies to the "read fileName ourselves" path.
                acls = preParsed;
            }
            else
            {
                if (!fileName.Exists())
                    throw new FileNotFoundException($"Animation chain file not found: {fileName.FullPath}", fileName.FullPath);

                var rawContent = File.ReadAllText(fileName.FullPath);
                if (IO.AchxConflictMarkerDetector.HasConflictMarkers(rawContent))
                    throw new System.IO.InvalidDataException(
                        $"{IO.AchxConflictMarkerDetector.ConflictMarkerMessage} ({fileName.FullPath})");

                acls = AnimationChainListSave.FromString(rawContent);
            }

            OnDiskCoordinateType = acls.CoordinateType;
            _knownTextureSizes = knownTextureSizes;
            NormalizeCoordinatesToUv(acls, fileName.GetDirectoryContainingThis().FullPath, knownTextureSizes);

            AnimationChainListSave = acls;
            FileName = fileName.FullPath;

            // This ProjectManager instance is reused across File > Open calls (one instance per
            // app window/tab-set, not recreated per file -- see TabSwitchCacheTests), so a prior
            // LoadTsxProject's tileset/identity-tracking state must not leak into a now-plain achx
            // project: IsNativeTsxProject must go false, or SaveCurrentAnimationChainList would
            // route this achx's save through SaveTsxProject against the stale tileset.
            _tsxTileset = null;
            _tsxEntryTileIdsByChain = new Dictionary<AnimationChainSave, uint>(ReferenceEqualityComparer.Instance);
            _tsxSatelliteTileIdsByChain = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(ReferenceEqualityComparer.Instance);
            _tsxSyntheticNamedChains = new HashSet<AnimationChainSave>(ReferenceEqualityComparer.Instance);
            _tsxLastNonEmptyFramesByChain = new Dictionary<AnimationChainSave, IReadOnlyList<AnimationFrameSave>>(ReferenceEqualityComparer.Instance);
            _tsxDormantHintsByChain = new Dictionary<AnimationChainSave, DormantTsxHint>(ReferenceEqualityComparer.Instance);

            // Same reused-instance hazard as the tsx fields just above: ReferencedPngs is driven
            // entirely by the *current* achx's own ProjectFile reference, so a file with none must
            // clear whatever a previously loaded achx populated here rather than leaving it stale.
            if (!string.IsNullOrEmpty(acls.ProjectFile))
                TryLoadProjectFile(new FilePath(fileName.GetDirectoryContainingThis().FullPath + acls.ProjectFile));
            else
                ReferencedPngs = new FilePath[0];
        }

        /// <summary>
        /// The editor's rendering and inspector code assumes UV (0–1) frame coordinates
        /// throughout. .achx files saved with <c>CoordinateType=Pixel</c> store raw pixel
        /// coordinates instead, so we read the texture dimensions for each unique
        /// <see cref="AnimationFrameSave.TextureName"/> and divide. PNG headers are
        /// parsed directly to avoid pulling an image-decode dependency into Core, except
        /// for sizes already supplied via <paramref name="knownTextureSizes"/>.
        /// </summary>
        private static void NormalizeCoordinatesToUv(
            AnimationChainListSave acls,
            string achxDirectory,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null)
        {
            Dictionary<string, (int W, int H)>? seedCache = null;
            if (knownTextureSizes != null)
            {
                seedCache = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in knownTextureSizes)
                    seedCache[entry.Key] = (entry.Value.Width, entry.Value.Height);
            }

            ConvertCoordinates(acls, achxDirectory, TextureCoordinateType.UV, seedCache);
        }

        /// <summary>
        /// Save the current animation chain list to <paramref name="targetPath"/>, as .achj
        /// (JSON) or .achx (XML) depending on <paramref name="targetPath"/>'s extension, in the
        /// coordinate format specified by <see cref="OnDiskCoordinateType"/>. The editor stores
        /// frame coordinates as UV internally (so the rendering pipeline can render at
        /// any texture size); when writing as Pixel, this method converts just for the
        /// on-disk write and then converts back so the in-memory model stays UV.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="IsNativeTsxProject"/> is true --
        /// <see cref="AnimationChainListSave"/> is a view over Tiled tileset data for a native tsx
        /// project, not a real achx/achj document, so writing it through this method would produce
        /// malformed/misleading content. Use <see cref="SaveTsxProject"/> instead.</exception>
        public void SaveAnimationChainList(string targetPath)
        {
            ThrowIfNativeTsxProject();

            var acls = AnimationChainListSave;
            if (acls == null) return;
            SaveAnimationChainList(acls, targetPath, OnDiskCoordinateType);
        }

        /// <inheritdoc cref="IProjectManager.SaveAnimationChainList(AnimationChainListSave, string, TextureCoordinateType)"/>
        public void SaveAnimationChainList(AnimationChainListSave acls, string targetPath, TextureCoordinateType diskFormat)
        {
            var achxDirectory = System.IO.Path.GetDirectoryName(targetPath) ?? string.Empty;
            NormalizeFrameTextureNames(acls, achxDirectory);
            void Write() { if (IsJsonPath(targetPath)) acls.SaveJson(targetPath); else acls.Save(targetPath); }

            // No conversion needed when on-disk format matches the in-memory format (UV).
            if (diskFormat == TextureCoordinateType.UV)
            {
                Write();
                return;
            }

            var sizes = ConvertCoordinates(acls, achxDirectory, diskFormat);
            try
            {
                Write();
            }
            finally
            {
                // Convert back to UV so the in-memory model continues to be UV.
                ConvertCoordinates(acls, achxDirectory, TextureCoordinateType.UV, sizes);
            }
        }

        /// <summary>
        /// Save the current animation chain list to <paramref name="stream"/> -- the seam the
        /// browser-wasm build needs, since it has no filesystem to write a path to. Written as
        /// .achj (JSON) when <see cref="FileName"/> ends in .achj, otherwise .achx (XML), in the
        /// coordinate format specified by <see cref="OnDiskCoordinateType"/>. When converting UV
        /// back to Pixel, texture sizes come from the <c>knownTextureSizes</c> supplied to the
        /// most recent <see cref="LoadAnimationChain"/> call rather than a disk read: there is no
        /// directory to resolve a relative <see cref="AnimationFrameSave.TextureName"/> against
        /// on this overload. A texture missing from that dictionary is left in UV coordinates,
        /// same as the path-based overload's behavior when a PNG can't be read.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="IsNativeTsxProject"/> is true --
        /// see <see cref="SaveAnimationChainList(string)"/>'s matching exception doc.</exception>
        public void SaveAnimationChainList(Stream stream)
        {
            ThrowIfNativeTsxProject();

            var acls = AnimationChainListSave;
            if (acls == null) return;

            RunWithDiskCoordinateConversion(acls, () =>
            {
                if (IsJsonPath(FileName)) acls.SaveJson(stream); else acls.Save(stream);
            });
        }

        /// <summary>
        /// Async counterpart to <see cref="SaveAnimationChainList(Stream)"/> for destination
        /// streams that only support async writes -- the browser-wasm build's
        /// <c>IStorageFile.OpenWriteAsync()</c> stream throws on a synchronous write, which
        /// <see cref="AnimationChainListSave.Save(Stream)"/> would otherwise trigger from inside
        /// <c>XmlWriter.Dispose()</c>. See <see cref="AnimationChainListSave.SaveAsync"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="IsNativeTsxProject"/> is true --
        /// see <see cref="SaveAnimationChainList(string)"/>'s matching exception doc.</exception>
        public async Task SaveAnimationChainListAsync(Stream stream)
        {
            ThrowIfNativeTsxProject();

            var acls = AnimationChainListSave;
            if (acls == null) return;

            await RunWithDiskCoordinateConversionAsync(acls,
                () => IsJsonPath(FileName) ? acls.SaveJsonAsync(stream) : acls.SaveAsync(stream));
        }

        /// <summary>True when <paramref name="path"/> has a .achj (JSON) extension; false for .achx or anything else.</summary>
        internal static bool IsJsonPath(string? path) =>
            !string.IsNullOrEmpty(path) && new FilePath(path).Extension == "achj";

        /// <summary>
        /// Converts <paramref name="acls"/> to <see cref="OnDiskCoordinateType"/> (using
        /// <see cref="_knownTextureSizes"/> in place of a directory read), runs
        /// <paramref name="save"/>, then converts back to UV so the in-memory model is
        /// unaffected. Skips the conversion round-trip entirely when the disk format is already
        /// UV. Shared by the sync and async stream-save overloads.
        /// </summary>
        private void RunWithDiskCoordinateConversion(AnimationChainListSave acls, Action save)
        {
            var diskFormat = OnDiskCoordinateType;
            if (diskFormat == TextureCoordinateType.UV)
            {
                save();
                return;
            }

            var sizes = ConvertCoordinates(acls, achxDirectory: string.Empty, diskFormat, BuildSeedCache());
            try
            {
                save();
            }
            finally
            {
                ConvertCoordinates(acls, achxDirectory: string.Empty, TextureCoordinateType.UV, sizes);
            }
        }

        /// <summary>Async twin of <see cref="RunWithDiskCoordinateConversion"/>.</summary>
        private async Task RunWithDiskCoordinateConversionAsync(AnimationChainListSave acls, Func<Task> save)
        {
            var diskFormat = OnDiskCoordinateType;
            if (diskFormat == TextureCoordinateType.UV)
            {
                await save();
                return;
            }

            var sizes = ConvertCoordinates(acls, achxDirectory: string.Empty, diskFormat, BuildSeedCache());
            try
            {
                await save();
            }
            finally
            {
                ConvertCoordinates(acls, achxDirectory: string.Empty, TextureCoordinateType.UV, sizes);
            }
        }

        private Dictionary<string, (int W, int H)>? BuildSeedCache()
        {
            if (_knownTextureSizes == null) return null;

            var seedCache = new Dictionary<string, (int W, int H)>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _knownTextureSizes)
                seedCache[entry.Key] = (entry.Value.Width, entry.Value.Height);
            return seedCache;
        }

        /// <summary>
        /// Convert <paramref name="acls"/> to <paramref name="target"/> coordinate space
        /// in place and return the per-texture size cache used (so a paired round-trip
        /// can reuse it). No-op if already in <paramref name="target"/> space.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Converting toward <see cref="TextureCoordinateType.Pixel"/> (i.e. preparing a persisted
        /// on-disk write) needs every referenced texture's pixel size resolved up front: writing a
        /// file with <c>CoordinateType=Pixel</c> while a frame's texture size couldn't be resolved
        /// would leave that frame's coordinates un-converted (still UV-scale) under a header that
        /// claims otherwise, corrupting the file (#1135). Converting toward UV -- on load, or when
        /// reversing an in-memory model back after a successful Pixel save -- stays lenient: a
        /// texture that hasn't been built yet is an expected, tolerated state while editing, and no
        /// persisted artifact is at risk.
        /// </exception>
        private static Dictionary<string, (int W, int H)> ConvertCoordinates(
            AnimationChainListSave acls,
            string achxDirectory,
            TextureCoordinateType target,
            Dictionary<string, (int W, int H)>? sizeCache = null)
        {
            sizeCache ??= new Dictionary<string, (int W, int H)>(System.StringComparer.OrdinalIgnoreCase);

            if (acls.CoordinateType == target) return sizeCache;

            bool toPixel = target == TextureCoordinateType.Pixel;

            if (toPixel)
            {
                var unresolved = new List<string>();
                foreach (var chain in acls.AnimationChains)
                {
                    foreach (var frame in chain.Frames)
                    {
                        if (string.IsNullOrEmpty(frame.TextureName)) continue;
                        if (sizeCache.ContainsKey(frame.TextureName)) continue;

                        if (TryResolveTextureSize(frame.TextureName, achxDirectory, sizeCache, out var size))
                            sizeCache[frame.TextureName] = size;
                        else
                            unresolved.Add(frame.TextureName);
                    }
                }

                if (unresolved.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Cannot save with CoordinateType=Pixel: texture size could not be resolved " +
                        $"for: {string.Join(", ", unresolved.Distinct())}. Fix or rebuild the missing " +
                        "texture(s), or switch the on-disk coordinate format to UV.");
                }
            }

            foreach (var chain in acls.AnimationChains)
            {
                foreach (var frame in chain.Frames)
                {
                    if (string.IsNullOrEmpty(frame.TextureName)) continue;

                    if (!sizeCache.TryGetValue(frame.TextureName, out var size))
                    {
                        // Only reached when target == UV -- the toPixel branch above already
                        // guaranteed every frame's texture is in sizeCache or threw.
                        if (!TryResolveTextureSize(frame.TextureName, achxDirectory, sizeCache, out size))
                            continue;
                        sizeCache[frame.TextureName] = size;
                    }

                    if (size.W <= 0 || size.H <= 0) continue;

                    if (toPixel)
                    {
                        frame.LeftCoordinate   *= size.W;
                        frame.RightCoordinate  *= size.W;
                        frame.TopCoordinate    *= size.H;
                        frame.BottomCoordinate *= size.H;
                    }
                    else
                    {
                        frame.LeftCoordinate   /= size.W;
                        frame.RightCoordinate  /= size.W;
                        frame.TopCoordinate    /= size.H;
                        frame.BottomCoordinate /= size.H;
                    }
                }
            }

            acls.CoordinateType = target;
            return sizeCache;
        }

        /// <summary>
        /// Resolves <paramref name="textureName"/> to a pixel size via <paramref name="sizeCache"/>
        /// (including the bare-filename fallback for callers -- e.g. #768's browser path -- that key
        /// the cache by leaf name instead of the frame's own <c>TextureName</c>), falling back to a
        /// PNG header read under <paramref name="achxDirectory"/>. Returns <see langword="false"/>
        /// without touching <paramref name="sizeCache"/> when neither resolves.
        /// </summary>
        private static bool TryResolveTextureSize(
            string textureName,
            string achxDirectory,
            Dictionary<string, (int W, int H)> sizeCache,
            out (int W, int H) size)
        {
            if (sizeCache.TryGetValue(textureName, out size)) return true;

            var bareName = System.IO.Path.GetFileName(textureName);
            if (bareName != textureName && sizeCache.TryGetValue(bareName, out size)) return true;

            var path = System.IO.Path.IsPathRooted(textureName)
                ? textureName
                : System.IO.Path.Combine(achxDirectory, textureName);

            var read = TryReadPngSize(path);
            if (read == null)
            {
                size = default;
                return false;
            }

            size = read.Value;
            return true;
        }

        /// <summary>
        /// Resolves <paramref name="textureName"/> (relative to the loaded .achx's directory, or
        /// absolute) to its pixel size by reading the PNG header. Returns <c>null</c> when the name
        /// is empty or the PNG can't be read. Used by exporters that need pixel rects while the
        /// in-memory model holds UV coordinates.
        /// </summary>
        public (int Width, int Height)? GetTextureSizeInPixels(string textureName)
        {
            if (string.IsNullOrEmpty(textureName)) return null;

            var dir = string.IsNullOrEmpty(FileName)
                ? string.Empty
                : System.IO.Path.GetDirectoryName(FileName) ?? string.Empty;
            var path = System.IO.Path.IsPathRooted(textureName)
                ? textureName
                : System.IO.Path.Combine(dir, textureName);

            var size = TryReadPngSize(path);
            return size == null ? null : (size.Value.W, size.Value.H);
        }

        private static (int W, int H)? TryReadPngSize(string path)
        {
            try
            {
                using var fs = System.IO.File.OpenRead(path);
                System.Span<byte> hdr = stackalloc byte[24];
                if (fs.Read(hdr) != 24) return null;

                // PNG signature: 89 50 4E 47 0D 0A 1A 0A, then 8 bytes IHDR header,
                // then width (BE int32) and height (BE int32).
                if (hdr[0] != 0x89 || hdr[1] != 0x50 || hdr[2] != 0x4E || hdr[3] != 0x47)
                    return null;

                int w = (hdr[16] << 24) | (hdr[17] << 16) | (hdr[18] << 8) | hdr[19];
                int h = (hdr[20] << 24) | (hdr[21] << 16) | (hdr[22] << 8) | hdr[23];
                return (w, h);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Heals every frame's <see cref="AnimationFrameSave.TextureName"/> against
        /// <paramref name="achxDirectory"/> before writing, so a texture path stored absolute (or
        /// OS-native-slashed) before this project had a folder to relativize against -- e.g. a
        /// texture assigned to an untitled, never-yet-saved project -- becomes a portable
        /// forward-slash relative path once a folder is known. Runs on every save, not just the
        /// first, so a file with pre-existing stale paths self-heals the next time it's saved (#936).
        /// </summary>
        private static void NormalizeFrameTextureNames(AnimationChainListSave acls, string achxDirectory)
        {
            foreach (var chain in acls.AnimationChains)
            {
                foreach (var frame in chain.Frames)
                {
                    frame.TextureName = TexturePathHelper.NormalizeStoredTextureName(frame.TextureName, achxDirectory);
                }
            }
        }

        /// <summary>
        /// Root folder the Files panel should browse, in order: (1) if
        /// <see cref="AnimationChainListSave.ProjectFile"/> resolves to a directory that
        /// exists, that directory (or its <c>Content</c> subfolder, if present) — the
        /// referenced project file itself need not exist, since a relative link authored
        /// against a source layout commonly goes stale once the .achx is copied to a
        /// build-output folder, so only the *directory* is required to resolve; (2)
        /// otherwise the nearest ancestor folder literally named <c>Content</c>, walking up
        /// from the loaded .achx's folder — the convention every FlatRedBall content
        /// pipeline (FRB1 and FRB2) copies assets into; (3) otherwise the folder containing
        /// the .achx itself. Returns <c>null</c> when no .achx is loaded/saved yet.
        /// </summary>
        public string? ResolveFilesPanelRoot()
        {
            if (string.IsNullOrEmpty(FileName)) return null;

            var achxFolder = new FilePath(FileName).GetDirectoryContainingThis();

            var projectFileRelative = AnimationChainListSave?.ProjectFile;
            if (!string.IsNullOrEmpty(projectFileRelative))
            {
                var projectFile = new FilePath(achxFolder.FullPath + projectFileRelative);
                var projectDirectory = projectFile.GetDirectoryContainingThis();
                if (projectDirectory.Exists())
                {
                    var contentDirectory = new FilePath(projectDirectory.FullPath + "Content/");
                    return (contentDirectory.Exists() ? contentDirectory : projectDirectory).FullPath;
                }
            }

            return FindContentAncestor(achxFolder.FullPath) ?? achxFolder.FullPath;
        }

        private static string? FindContentAncestor(string folderFullPath)
        {
            // Deliberately no StringSplitOptions.RemoveEmptyEntries: a Unix-style absolute
            // path ("/tmp/...") splits with a leading empty entry, and dropping it would
            // make the reconstructed join lose its leading '/' — turning an absolute path
            // into a relative one that FilePath then resolves against the current directory.
            var segments = folderFullPath.Split('/');

            for (int i = segments.Length - 1; i >= 0; i--)
            {
                if (string.Equals(segments[i], "Content", StringComparison.OrdinalIgnoreCase))
                    return string.Join("/", segments, 0, i + 1) + "/";
            }

            return null;
        }

        private void TryLoadProjectFile(FilePath projectFile)
        {
            if (projectFile?.Exists() != true)
            {
                ReferencedPngs = new FilePath[0];
                return;
            }

            // Assume content folder; adjust for Android if needed
            var projectDirectory = projectFile.GetDirectoryContainingThis().FullPath + "Content/";

            var files = new HashSet<FilePath>();

            void AddRfs(XElement? referencedFiles)
            {
                if (referencedFiles != null)
                {
                    foreach (var file in referencedFiles.Elements())
                    {
                        var nameDescendant = file.Elements("Name").FirstOrDefault();
                        if (nameDescendant != null)
                        {
                            var name = nameDescendant.Value;
                            if (Path.GetExtension(name).TrimStart('.').ToLowerInvariant() == "png")
                            {
                                files.Add(new FilePath(projectDirectory + name));
                            }
                        }
                    }
                }
            }

            XElement? xElement = null;
            try
            {
                xElement = XElement.Load(projectFile.FullPath);
            }
            catch
            {
                // Could not load — possibly a .gluj format we can't parse yet
            }

            if (xElement != null)
            {
                var screens = xElement.Elements("Screens").FirstOrDefault();
                if (screens != null)
                {
                    foreach (var screen in screens.Elements())
                    {
                        AddRfs(screen.Elements("ReferencedFiles").FirstOrDefault());
                    }
                }

                var entities = xElement.Elements("Entities").FirstOrDefault();
                if (entities != null)
                {
                    foreach (var entity in entities.Elements())
                    {
                        AddRfs(entity.Elements("ReferencedFiles").FirstOrDefault());
                    }
                }

                AddRfs(xElement.Elements("GlobalFiles").FirstOrDefault());

                ReferencedPngs = files.ToArray();
            }
            else
            {
                // No parseable project file — fall back to all .png files relative to project dir
                // Directory.EnumerateFiles' "*.png" filter is case-sensitive on Linux — it would
                // miss "Hero.PNG". Match the extension ourselves so Windows-authored sheets
                // (which commonly use mixed casing) are picked up on every platform.
                ReferencedPngs = Directory.Exists(projectDirectory)
                    ? Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories)
                        .Where(f => Path.GetExtension(f).Equals(".png", StringComparison.OrdinalIgnoreCase))
                        .Select(item => new FilePath(item))
                        .ToArray()
                    : new FilePath[0];
            }
        }

        public IReadOnlyList<string> FindMissingTextures(AnimationChainListSave acls, string achxDirectory)
        {
            var missing = new List<string>();
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var chain in acls.AnimationChains)
            foreach (var frame in chain.Frames)
            {
                if (string.IsNullOrEmpty(frame.TextureName)) continue;
                if (!seen.Add(frame.TextureName)) continue;

                var path = System.IO.Path.IsPathRooted(frame.TextureName)
                    ? frame.TextureName
                    : System.IO.Path.Combine(achxDirectory, frame.TextureName);

                if (TryReadPngSize(path) == null)
                    missing.Add(frame.TextureName);
            }

            return missing;
        }

        internal void LoadTileMapInformation(string fileName)
        {
            TileMapInformationList = XmlFile.Deserialize<TileMapInformationList>(fileName);
        }

        /// <summary>
        /// Opens <paramref name="fileName"/> as a native AnimationEditor project (issue #1140):
        /// the tsx's own per-tile animations become <see cref="AnimationChainListSave"/> chains via
        /// <see cref="Tiled.TiledAnimationToAchjMapper"/>, editable the same way achx chains are.
        /// </summary>
        /// <exception cref="NotSupportedException">The tsx uses a construct (wangsets,
        /// transformations, per-tile object layers, unsupported property types) that would be lost
        /// on the first save -- see <see cref="Tiled.TsxCompatibilityChecker"/>.</exception>
        /// <exception cref="InvalidOperationException">The tsx is corrupt in a way <see
        /// cref="Tiled.TiledAnimationToAchjMapper.Map"/> can't tolerate (e.g. <c>Columns &lt;= 0</c>
        /// or a duplicate tile id) -- or some achx/achj already has a <c>.tiledsync</c> association
        /// (see <see cref="IO.IoManager.AddAssociatedTiledTilesetPath"/>) pointing achx-push at this
        /// same tsx (issue #1147): a tsx can't be both a native-tsx project and an achx-push target
        /// at the same time, since the two features' save paths would silently fight over the same
        /// file.</exception>
        /// <remarks>The project is left unchanged when either exception is thrown -- <see
        /// cref="AnimationChainListSave"/> is mapped into local variables first and only committed
        /// to this instance's fields after every step that can throw has already succeeded, so a
        /// rejected load can never leave <see cref="IsNativeTsxProject"/> pointing at a tileset with
        /// no matching chain data.</remarks>
        public void LoadTsxProject(FilePath fileName)
        {
            var conflictingOwners = Tiled.TiledSyncAssociationScanner.FindAssociationsTargeting(fileName.FullPath);
            if (conflictingOwners.Count > 0)
                throw new InvalidOperationException(
                    $"Cannot open \"{fileName.FullPath}\" as a native AnimationEditor project -- " +
                    $"it is already associated as a Tiled sync target from \"{conflictingOwners[0]}\" " +
                    "(Associate Tiled Tileset). A .tsx cannot be both a native-tsx project and an " +
                    "achx-push target at the same time.");

            var tileset = Tiled.TsxLoader.LoadTileset(fileName.FullPath);

            if (!Tiled.TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var blockingReason))
                throw new NotSupportedException(
                    $"Can't open \"{fileName.FullPath}\" as a native AnimationEditor project: {blockingReason}");

            var acls = Tiled.TiledAnimationToAchjMapper.Map(
                tileset, out var entryTileIdsByChain, out var satelliteTileIdsByChain, out var syntheticNamedChains);

            _tsxTileset = tileset;
            AnimationChainListSave = acls;
            _tsxEntryTileIdsByChain = new Dictionary<AnimationChainSave, uint>(entryTileIdsByChain, ReferenceEqualityComparer.Instance);
            _tsxSatelliteTileIdsByChain = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(satelliteTileIdsByChain, ReferenceEqualityComparer.Instance);
            _tsxSyntheticNamedChains = new HashSet<AnimationChainSave>(syntheticNamedChains, ReferenceEqualityComparer.Instance);
            FileName = fileName.FullPath;

            // Seeds _tsxLastNonEmptyFramesByChain from what was just loaded, so a chain whose
            // frames are cleared on the very first save after load (no intervening save to have
            // captured this otherwise) can still go dormant instead of losing its hint outright --
            // see _tsxDormantHintsByChain's doc comment.
            _tsxLastNonEmptyFramesByChain = new Dictionary<AnimationChainSave, IReadOnlyList<AnimationFrameSave>>(ReferenceEqualityComparer.Instance);
            foreach (var chain in acls.AnimationChains)
                if (chain.Frames.Count > 0)
                    _tsxLastNonEmptyFramesByChain[chain] = chain.Frames.ToArray();
            _tsxDormantHintsByChain = new Dictionary<AnimationChainSave, DormantTsxHint>(ReferenceEqualityComparer.Instance);

            // Same reused-instance hazard LoadAnimationChain resets its own achx-side fields for:
            // this ProjectManager instance is reused across File > Open calls, so a prior achx's
            // ReferencedPngs/OnDiskCoordinateType must not leak into a now-open tsx project. A tsx
            // has no ProjectFile/CoordinateType concept of its own, so these just go back to their
            // no-project defaults rather than being recomputed from the tsx.
            ReferencedPngs = new FilePath[0];
            OnDiskCoordinateType = TextureCoordinateType.Pixel;
        }

        /// <summary>
        /// Saves the current <see cref="AnimationChainListSave"/> back to the tsx opened by <see
        /// cref="LoadTsxProject"/>, via <see cref="Tiled.MultiTileToTiledAnimationMapper"/> and <see
        /// cref="Tiled.NativeTsxAnimationSync"/>. No-op (returns an empty list) if no tsx project is
        /// loaded. Returns every chain's mapping warning from this save -- a chain that couldn't be
        /// mapped (bad geometry, wrong texture, etc.) keeps whatever it last wrote to its tile
        /// untouched rather than being cleared -- plus one per chain carrying data the format
        /// can't hold (<see cref="Tiled.TsxLossyDataCheck"/>; that chain IS written, minus that
        /// data). A caller should surface these to the user instead of assuming the save fully
        /// captured every edit.
        /// </summary>
        /// <remarks>Same all-or-nothing invariant as <see cref="LoadTsxProject"/>: <see
        /// cref="Tiled.NativeTsxAnimationSync.Apply"/> runs against a working copy (<see
        /// cref="Tiled.NativeTsxAnimationSync.CloneForSave"/>), and this instance only adopts it
        /// after <see cref="Tiled.TsxWriter.Write(DotTiled.Tileset, string)"/> has actually
        /// succeeded -- so a write failure (an unsupported construct, a disk/permissions error)
        /// can't leave the in-memory tileset reflecting computed-but-never-persisted state.</remarks>
        public IReadOnlyList<string> SaveTsxProject(string? targetPath = null)
        {
            if (_tsxTileset == null || AnimationChainListSave == null)
                return [];

            // Revives a dormant hint for this save's Map() call, but only for a chain whose
            // current Frames are reference-sequence-identical to the ones captured when that hint
            // went dormant -- i.e. DeleteFramesCommand.Undo() re-inserting the exact same frame
            // objects it just removed. A chain re-authored with different frame content (even the
            // same count) does not match and is left to recompute fresh, same as today.
            var entryHintsForMap = _tsxEntryTileIdsByChain;
            var satelliteHintsForMap = _tsxSatelliteTileIdsByChain;
            if (_tsxDormantHintsByChain.Count > 0)
            {
                foreach (var chain in AnimationChainListSave.AnimationChains)
                {
                    if (chain.Frames.Count == 0) continue;
                    if (!_tsxDormantHintsByChain.TryGetValue(chain, out var dormant)) continue;
                    if (!FramesSequenceEqual(dormant.Frames, chain.Frames)) continue;

                    if (ReferenceEquals(entryHintsForMap, _tsxEntryTileIdsByChain))
                        entryHintsForMap = new Dictionary<AnimationChainSave, uint>(_tsxEntryTileIdsByChain, ReferenceEqualityComparer.Instance);
                    entryHintsForMap[chain] = dormant.EntryTileId;

                    if (dormant.Satellites.Count > 0)
                    {
                        if (ReferenceEquals(satelliteHintsForMap, _tsxSatelliteTileIdsByChain))
                            satelliteHintsForMap = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(_tsxSatelliteTileIdsByChain, ReferenceEqualityComparer.Instance);
                        satelliteHintsForMap[chain] = dormant.Satellites;
                    }
                }
            }

            var mapped = Tiled.MultiTileToTiledAnimationMapper.Map(
                AnimationChainListSave, BuildTsxTilesetInfo(_tsxTileset), entryHintsForMap, satelliteHintsForMap);

            // No "ownership transfer" here: a chain's owner tile is a storage-slot choice (loaded,
            // computed once, or explicitly set), never re-derived from frame geometry after the
            // fact. Resizing/moving frame 0's rect never relocates it -- only an explicit
            // TrySetTsxOwnerTileId call (or the Sync-to-First-Frame button, which just calls that
            // with frame 0's current tile) changes which tile a chain owns.
            mapped = YieldCollidingFreshClaims(mapped, entryHintsForMap, satelliteHintsForMap);

            var workingTileset = Tiled.NativeTsxAnimationSync.CloneForSave(_tsxTileset);
            Tiled.NativeTsxAnimationSync.Apply(workingTileset, mapped);
            Tiled.TsxWriter.Write(workingTileset, targetPath ?? FileName!);
            _tsxTileset = workingTileset;

            // Commits this save's tile assignments (including a brand-new chain's freshly-chosen
            // id) so the *next* save reuses them instead of recomputing from geometry again -- see
            // _tsxEntryTileIdsByChain's doc comment for why that matters. Rebuilt primarily from
            // `mapped` (rather than just adding to it) so a chain that's still in the project but
            // now has zero frames doesn't leave a stale hint a later re-populated save could wrongly
            // reuse (see SaveTsxProject_AllFramesDeletedFromChain_... below).
            //
            // Hints for chains that are entirely ABSENT from AnimationChainListSave right now
            // (never reached `mapped` at all) are carried forward unchanged rather than dropped.
            // A Delete-chain command autosaves immediately after removing the chain, and its Undo
            // re-inserts the exact same AnimationChainSave object and autosaves again -- carrying
            // the hint forward is what lets that reinsertion land back on the chain's original
            // tile instead of being recomputed from frame[0] as if it were brand new. This can't
            // reintroduce the zero-frame-chain hazard above: that hazard is about a chain staying
            // present while its EntryTileId goes null, which this branch never touches.
            var currentChains = new HashSet<AnimationChainSave>(
                AnimationChainListSave.AnimationChains, ReferenceEqualityComparer.Instance);

            var updatedEntries = new Dictionary<AnimationChainSave, uint>(ReferenceEqualityComparer.Instance);
            foreach (var kvp in _tsxEntryTileIdsByChain)
                if (!currentChains.Contains(kvp.Key))
                    updatedEntries[kvp.Key] = kvp.Value;

            var updatedSatellites = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(ReferenceEqualityComparer.Instance);
            foreach (var kvp in _tsxSatelliteTileIdsByChain)
                if (!currentChains.Contains(kvp.Key))
                    updatedSatellites[kvp.Key] = kvp.Value;

            var updatedLastFrames = new Dictionary<AnimationChainSave, IReadOnlyList<AnimationFrameSave>>(ReferenceEqualityComparer.Instance);
            foreach (var kvp in _tsxLastNonEmptyFramesByChain)
                if (!currentChains.Contains(kvp.Key))
                    updatedLastFrames[kvp.Key] = kvp.Value;

            // Dormant hints for an absent chain are carried forward unchanged, same reasoning as
            // the dictionaries above -- DeleteChainsCommand's own absent-chain carry-forward
            // already handles reinsertion of the chain itself; this only matters if a chain is
            // deleted while it already had a dormant (frames-cleared) hint pending.
            var updatedDormant = new Dictionary<AnimationChainSave, DormantTsxHint>(ReferenceEqualityComparer.Instance);
            foreach (var kvp in _tsxDormantHintsByChain)
                if (!currentChains.Contains(kvp.Key))
                    updatedDormant[kvp.Key] = kvp.Value;

            foreach (var result in mapped)
            {
                var chain = result.SourceChain;
                if (result.EntryTileId is { } entryTileId)
                {
                    // A live hint again this save (freshly computed, or a dormant one just
                    // revived above) -- not dormant, and remembers these frames as the sequence
                    // that produced it.
                    updatedEntries[chain] = entryTileId;
                    updatedLastFrames[chain] = chain.Frames.ToArray();

                    if (result.Satellites.Count > 0)
                    {
                        // Merged onto whatever this chain's satellite hints already were (rather
                        // than replacing the whole per-chain dictionary), so an offset that isn't
                        // part of *this* save's footprint keeps whatever hint it had -- see the
                        // branch below for why that matters.
                        var merged = _tsxSatelliteTileIdsByChain.TryGetValue(chain, out var existingSatellites)
                            ? new Dictionary<(int Dx, int Dy), uint>(existingSatellites)
                            : new Dictionary<(int Dx, int Dy), uint>();
                        foreach (var satellite in result.Satellites)
                            merged[satellite.Offset] = satellite.TileId;
                        updatedSatellites[chain] = merged;
                    }
                    else if (_tsxSatelliteTileIdsByChain.TryGetValue(chain, out var stillHinted))
                    {
                        // This save's footprint has no satellites at all -- e.g. a resize command
                        // shrank every frame down to a single tile. chain.Frames never went to zero
                        // and no AnimationFrameSave object was added or removed (a resize only
                        // mutates existing frames' Left/Top/Right/BottomCoordinate in place), so
                        // there's no frame-reference signal available to gate a dormant-hint-style
                        // revival on. Keeping the old per-offset hints alive unconditionally instead
                        // mirrors how _tsxEntryTileIdsByChain already behaves for a chain that never
                        // empties: once a (chain, offset) pair claims a tile, later saves keep
                        // reusing it for as long as the chain has any frames at all, so a save right
                        // after Undo restores the satellite to its original tile instead of
                        // recomputing it from geometry.
                        updatedSatellites[chain] = stillHinted;
                    }
                }
                else if (chain.Frames.Count == 0)
                {
                    // Genuinely empty this save. Prefer an already-dormant hint (so a chain left
                    // empty across several consecutive saves keeps the same dormant hint/
                    // fingerprint instead of losing it after the first "still empty" resave),
                    // falling back to freshly demoting whatever was active just before this save
                    // -- the very first "cleared" save after a real hint existed. A chain with no
                    // prior hint at all (e.g. a brand-new chain deleted before its first save) has
                    // nothing to preserve, so no dormant entry is created.
                    if (_tsxDormantHintsByChain.TryGetValue(chain, out var stillDormant))
                        updatedDormant[chain] = stillDormant;
                    else if (_tsxEntryTileIdsByChain.TryGetValue(chain, out var oldEntry)
                        && _tsxLastNonEmptyFramesByChain.TryGetValue(chain, out var oldFrames))
                    {
                        _tsxSatelliteTileIdsByChain.TryGetValue(chain, out var oldSatellites);
                        updatedDormant[chain] = new DormantTsxHint(
                            oldEntry, oldSatellites ?? new Dictionary<(int Dx, int Dy), uint>(), oldFrames);
                    }
                }
                else if (_tsxEntryTileIdsByChain.TryGetValue(chain, out var stillActiveEntry))
                {
                    // The chain still has frames, but this save's mapping aborted with a warning
                    // (a mismatched texture name, a misaligned/negative-origin rect, a footprint
                    // that overflows the tileset, non-zero margin/spacing, etc.) rather than the
                    // chain being genuinely cleared above. Without this branch, a command whose
                    // Do() introduces one of these warnings and whose Undo() fixes it again (e.g.
                    // SetFrameTextureNameCommand pointing a frame at the wrong texture, then back)
                    // would silently drop the chain's entry/satellite hints on the Do() save --
                    // neither the "live hint" branch above nor the "genuinely empty" branch here
                    // applies -- so the very next successful save (the Undo()) would recompute the
                    // entry tile fresh from frame[0], relocating an "owner isn't its own first
                    // frame" hand-authored chain exactly like the already-fixed bugs this whole file
                    // is themed around. Keeping the existing hints untouched here mirrors the
                    // "chain absent from the ACLS" carry-forward above -- a transient, recoverable
                    // save state, not a real identity change.
                    updatedEntries[chain] = stillActiveEntry;
                    if (_tsxLastNonEmptyFramesByChain.TryGetValue(chain, out var stillActiveFrames))
                        updatedLastFrames[chain] = stillActiveFrames;
                    if (_tsxSatelliteTileIdsByChain.TryGetValue(chain, out var stillActiveSatellites))
                        updatedSatellites[chain] = stillActiveSatellites;
                }
                else if (_tsxDormantHintsByChain.TryGetValue(chain, out var stillDormantThroughAbort))
                {
                    // The dormant sibling of the branch above: this chain was DORMANT (not live)
                    // before this save, and its refill this save was neither a reference-match
                    // revival (handled by the pre-map injection above) nor a genuine re-emptying
                    // (Frames.Count > 0 here) -- it was refilled with new content that itself hit
                    // a mapping abort (mismatched texture, misaligned rect, etc.). Without this
                    // branch the dormant hint is simply dropped, even though a later save could
                    // still legitimately revive it (e.g. the abort gets fixed, or the refill is
                    // undone back to the exact original frame objects). Keep it parked exactly as
                    // it was, same "transient, recoverable save state" treatment as the live case.
                    updatedDormant[chain] = stillDormantThroughAbort;
                }
            }
            _tsxEntryTileIdsByChain = updatedEntries;
            _tsxSatelliteTileIdsByChain = updatedSatellites;
            _tsxLastNonEmptyFramesByChain = updatedLastFrames;
            _tsxDormantHintsByChain = updatedDormant;

            return mapped.SelectMany(r => r.Warnings)
                .Concat(Tiled.TsxLossyDataCheck.Warnings(AnimationChainListSave))
                .ToList();
        }

        /// <summary>
        /// A Tiled tile carries one animation, so two chains computing the same tile id can't both
        /// be written. That happens routinely, not just by mistake: duplicating a chain gives the
        /// copy the same cells as its source, and the user moves the copy's frames afterwards.
        /// Rather than refusing the whole save (<see cref="Tiled.NativeTsxAnimationSync.Apply"/>
        /// would throw), the chain whose claim on the tile is freshly computed this save yields to
        /// the one that already owned it (a hinted claim), and is reported by name like any other
        /// chain that couldn't be mapped -- keeping whatever tiles it owned before untouched, and
        /// picked up on the first save after its frames stop overlapping. Two fresh claims on one
        /// tile (two brand-new chains on the same cells) keep the first in chain order.
        /// </summary>
        private IReadOnlyList<Tiled.MultiTileMappingResult> YieldCollidingFreshClaims(
            IReadOnlyList<Tiled.MultiTileMappingResult> mapped,
            IReadOnlyDictionary<AnimationChainSave, uint> entryHints,
            IReadOnlyDictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>> satelliteHints)
        {
            // Every claim this save makes: (tile, owning chain, is it a hint or fresh?).
            var claims = new List<(uint TileId, Tiled.MultiTileMappingResult Result, bool IsHinted)>();
            foreach (var r in mapped)
            {
                if (r.Warnings.Count > 0) continue;
                if (r.EntryTileId is { } entry)
                    claims.Add((entry, r, !r.EntryTileIdIsFreshlyComputed));
                foreach (var satellite in r.Satellites)
                {
                    var hinted = satelliteHints.TryGetValue(r.SourceChain, out var hints)
                        && hints.TryGetValue(satellite.Offset, out var hintedId) && hintedId == satellite.TileId;
                    claims.Add((satellite.TileId, r, hinted));
                }
            }

            var yielding = new Dictionary<AnimationChainSave, string>(ReferenceEqualityComparer.Instance);
            foreach (var group in claims.GroupBy(c => c.TileId))
            {
                var claimants = group.Select(c => c.Result).Distinct(ReferenceEqualityComparer.Instance).Cast<Tiled.MultiTileMappingResult>().ToList();
                if (claimants.Count < 2) continue;
                var winner = group.FirstOrDefault(c => c.IsHinted).Result ?? claimants[0];
                foreach (var loser in claimants.Where(c => !ReferenceEquals(c, winner)))
                    yielding.TryAdd(loser.SourceChain,
                        $"chain \"{loser.ChainName}\": Tiled tile {group.Key} already carries \"{winner.ChainName}\" - not saved; move its frames to cells no other chain uses.");
            }
            if (yielding.Count == 0)
                return mapped;

            return mapped.Select(r =>
            {
                if (!yielding.TryGetValue(r.SourceChain, out var warning))
                    return r;
                // Same shape MultiTileToTiledAnimationMapper's own Empty(warning) produces: the
                // chain's previously-owned tiles are still reported so the stale-clearing step
                // leaves them alone, but nothing new is written.
                uint? ownedEntry = entryHints.TryGetValue(r.SourceChain, out var e) ? e
                    : _tsxEntryTileIdsByChain.TryGetValue(r.SourceChain, out var prior) ? prior : null;
                var ownedSatellites = satelliteHints.TryGetValue(r.SourceChain, out var sh) ? sh
                    : _tsxSatelliteTileIdsByChain.TryGetValue(r.SourceChain, out var priorSh) ? priorSh : null;
                return r with
                {
                    AnchorFrames = [],
                    EntryTileId = ownedEntry,
                    EntryTileIdIsFreshlyComputed = false,
                    Satellites = ownedSatellites?.Select(kv => new Tiled.TiledSatelliteMapping(kv.Value, [], kv.Key)).ToList() ?? [],
                    Warnings = [warning],
                };
            }).ToList();
        }

        /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> hold the exact same
        /// <see cref="AnimationFrameSave"/> objects, in the same order -- the "is this the same
        /// Undo?" test <see cref="_tsxDormantHintsByChain"/> relies on. Deliberately reference
        /// equality, not value equality: a genuinely re-authored frame with identical-looking
        /// coordinates is still a different object and must NOT match.</summary>
        private static bool FramesSequenceEqual(IReadOnlyList<AnimationFrameSave> a, IReadOnlyList<AnimationFrameSave> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        /// <summary>Opaque snapshot type returned by <see cref="CaptureTsxState"/> -- holds
        /// direct references to this instance's tsx-specific fields at capture time, safe to
        /// share without cloning because <see cref="LoadTsxProject"/>/<see cref="SaveTsxProject"/>
        /// always replace these fields wholesale rather than mutating them in place.</summary>
        private sealed record TsxState(
            DotTiled.Tileset Tileset,
            Dictionary<AnimationChainSave, uint> EntryTileIdsByChain,
            Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>> SatelliteTileIdsByChain,
            HashSet<AnimationChainSave> SyntheticNamedChains,
            Dictionary<AnimationChainSave, IReadOnlyList<AnimationFrameSave>> LastNonEmptyFramesByChain,
            Dictionary<AnimationChainSave, DormantTsxHint> DormantHintsByChain);

        /// <inheritdoc/>
        public object? CaptureTsxState() =>
            _tsxTileset is null ? null : new TsxState(
                _tsxTileset, _tsxEntryTileIdsByChain, _tsxSatelliteTileIdsByChain, _tsxSyntheticNamedChains,
                _tsxLastNonEmptyFramesByChain, _tsxDormantHintsByChain);

        /// <inheritdoc/>
        public void RestoreTsxState(object? state)
        {
            if (state is TsxState tsxState)
            {
                _tsxTileset = tsxState.Tileset;
                _tsxEntryTileIdsByChain = tsxState.EntryTileIdsByChain;
                _tsxSatelliteTileIdsByChain = tsxState.SatelliteTileIdsByChain;
                _tsxSyntheticNamedChains = tsxState.SyntheticNamedChains;
                _tsxLastNonEmptyFramesByChain = tsxState.LastNonEmptyFramesByChain;
                _tsxDormantHintsByChain = tsxState.DormantHintsByChain;
            }
            else
            {
                _tsxTileset = null;
                _tsxEntryTileIdsByChain = new Dictionary<AnimationChainSave, uint>(ReferenceEqualityComparer.Instance);
                _tsxSatelliteTileIdsByChain = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(ReferenceEqualityComparer.Instance);
                _tsxSyntheticNamedChains = new HashSet<AnimationChainSave>(ReferenceEqualityComparer.Instance);
                _tsxLastNonEmptyFramesByChain = new Dictionary<AnimationChainSave, IReadOnlyList<AnimationFrameSave>>(ReferenceEqualityComparer.Instance);
                _tsxDormantHintsByChain = new Dictionary<AnimationChainSave, DormantTsxHint>(ReferenceEqualityComparer.Instance);
            }
        }

        /// <inheritdoc/>
        public object? CaptureTextureSizeState() => _knownTextureSizes;

        /// <inheritdoc/>
        public void RestoreTextureSizeState(object? state) =>
            _knownTextureSizes = state as IReadOnlyDictionary<string, (int Width, int Height)>;

        /// <inheritdoc/>
        public void ResetToBlankDocument()
        {
            AnimationChainListSave = new AnimationChainListSave();
            FileName = null;
            OnDiskCoordinateType = TextureCoordinateType.Pixel;
            RestoreTsxState(null);
            RestoreTextureSizeState(null);
            ReferencedPngs = new FilePath[0];
        }

        /// <summary>
        /// The tile id a native-tsx chain's Tiled <c>&lt;animation&gt;</c> block would be written to
        /// on the next save -- an explicit hint set via <see cref="TrySetTsxOwnerTileId"/>, one
        /// loaded from disk, or (when neither exists yet) freshly computed from frame 0's own
        /// top-left cell. Issue #1182: makes what was previously hidden save-time bookkeeping
        /// (<see cref="_tsxEntryTileIdsByChain"/>) explicit and readable, e.g. for an Inspector
        /// "Placed Tile: N" readout. <see langword="null"/> for an achx/achj project, a chain not in
        /// this project, or a chain whose current frames can't be mapped (empty, or bad geometry).
        /// </summary>
        public uint? GetTsxOwnerTileId(AnimationChainSave chain)
        {
            if (_tsxTileset == null || AnimationChainListSave == null)
                return null;

            return Tiled.MultiTileToTiledAnimationMapper.Map(
                    AnimationChainListSave, BuildTsxTilesetInfo(_tsxTileset), _tsxEntryTileIdsByChain, _tsxSatelliteTileIdsByChain)
                .FirstOrDefault(r => ReferenceEquals(r.SourceChain, chain))
                ?.EntryTileId;
        }

        /// <summary>
        /// Explicitly sets which tile id a native-tsx chain's Tiled <c>&lt;animation&gt;</c> is
        /// written to on the next save, overriding whatever <see cref="GetTsxOwnerTileId"/> would
        /// otherwise report -- issue #1182's explicit ownership, e.g. from an Inspector field the
        /// user types into, or a "Sync to First Frame" action that passes <see
        /// cref="GetTsxOwnerTileId"/>'s own frame-0-computed value back in. Validated against the
        /// tileset's own tile count and every other chain's current owner tile before committing;
        /// returns a human-readable error and makes no change on failure, or <see langword="null"/>
        /// on success. Drops any satellite-offset hints (<see cref="_tsxSatelliteTileIdsByChain"/>):
        /// a multi-tile chain's satellites must recompute relative to the new owner position instead
        /// of reusing offsets captured at the old one. There is no auto-follow -- once set (loaded,
        /// computed once, or set here), a chain's owner tile never moves again on its own; only
        /// another call here (e.g. the "Sync to First Frame" button) changes it. If <paramref
        /// name="chain"/> is still tracked in <see cref="_tsxSyntheticNamedChains"/> -- its name has
        /// never been anything but the synthetic <c>"ID:{tileId}"</c> placeholder, per real
        /// provenance from load time, not a guess from the string's shape -- the name is re-derived
        /// for the new tile too, so an unnamed chain's tree label follows its owner instead of going
        /// stale (and, worse, getting baked in as a permanent explicit name on the next save: <see
        /// cref="Tiled.NativeTsxAnimationSync"/> only omits the <c>Name</c> property when the chain's
        /// current name still matches its current tile's synthetic one).
        /// </summary>
        public string? TrySetTsxOwnerTileId(AnimationChainSave chain, uint tileId)
        {
            if (_tsxTileset == null || AnimationChainListSave == null)
                return "Can't set an owner tile: this isn't a native Tiled (.tsx) project.";
            if (!AnimationChainListSave.AnimationChains.Contains(chain))
                return "Can't set an owner tile: this chain isn't part of the current project.";
            if (tileId >= (uint)_tsxTileset.TileCount)
                return $"Tile {tileId} is past this tileset's {_tsxTileset.TileCount} tile(s).";

            foreach (var other in AnimationChainListSave.AnimationChains)
            {
                if (ReferenceEquals(other, chain)) continue;
                if (GetTsxOwnerTileId(other) == tileId)
                    return $"Tile {tileId} is already the owner tile for \"{other.Name}\".";
            }

            // Nothing would actually change -- skip the mutation entirely so a repeated "Sync to
            // First Frame" click (or any other caller re-committing the same already-explicit
            // value) doesn't push a spurious undo entry (#1182 follow-up).
            if (IsTsxOwnerTileIdAlreadySet(chain, tileId))
                return null;

            var updatedEntries = new Dictionary<AnimationChainSave, uint>(_tsxEntryTileIdsByChain, ReferenceEqualityComparer.Instance)
            {
                [chain] = tileId
            };
            _tsxEntryTileIdsByChain = updatedEntries;

            if (_tsxSatelliteTileIdsByChain.ContainsKey(chain))
            {
                var updatedSatellites = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(_tsxSatelliteTileIdsByChain, ReferenceEqualityComparer.Instance);
                updatedSatellites.Remove(chain);
                _tsxSatelliteTileIdsByChain = updatedSatellites;
            }

            if (_tsxSyntheticNamedChains.Contains(chain))
                chain.Name = Tiled.TiledAnimationToAchjMapper.SyntheticChainName(tileId);

            return null;
        }

        /// <summary>
        /// Marks a chain's name as a real, user-given one from now on -- called on an actual rename
        /// (<see cref="AppCommands.RenameChain"/>) so <see cref="TrySetTsxOwnerTileId"/> never
        /// overwrites it again. No-op outside a native-tsx project or for a chain that was already
        /// marked. Returns whether the chain *was* still tracked as synthetic. Replaces the whole
        /// set rather than calling <see cref="HashSet{T}.Remove"/> on the existing instance in
        /// place: <see cref="CaptureTsxState"/> hands out this exact set by reference on the promise
        /// (see its own doc comment) that it's only ever replaced wholesale, never mutated -- an
        /// in-place <c>Remove</c> would silently corrupt an already-taken "before" snapshot (e.g.
        /// <see cref="CommandsAndState.Commands.RenameChainCommand"/>'s own <c>_before</c>, captured
        /// one line earlier in the same <c>Do()</c>), erasing the chain from it too and making Undo
        /// unable to restore synthetic tracking.
        /// </summary>
        public bool IsChainNameAuto(AnimationChainSave chain) => _tsxSyntheticNamedChains.Contains(chain);

        /// <summary>
        /// Reverts a native-tsx chain to its synthetic <c>"ID:{ownerTileId}"</c> name, so the next
        /// save omits the tile's <c>Name</c> property. Returns <see langword="false"/> (no change)
        /// outside a native-tsx project or when the chain has no owner tile to name it after.
        /// Replaces the set wholesale for the same reason as <see cref="MarkChainNameExplicit"/>.
        /// </summary>
        public bool MakeChainNameAuto(AnimationChainSave chain)
        {
            if (GetTsxOwnerTileId(chain) is not { } ownerTileId)
                return false;
            chain.Name = Tiled.TiledAnimationToAchjMapper.SyntheticChainName(ownerTileId);
            _tsxSyntheticNamedChains = new HashSet<AnimationChainSave>(_tsxSyntheticNamedChains, ReferenceEqualityComparer.Instance) { chain };
            return true;
        }

        public bool MarkChainNameExplicit(AnimationChainSave chain)
        {
            if (!_tsxSyntheticNamedChains.Contains(chain))
                return false;
            var updated = new HashSet<AnimationChainSave>(_tsxSyntheticNamedChains, ReferenceEqualityComparer.Instance);
            updated.Remove(chain);
            _tsxSyntheticNamedChains = updated;
            return true;
        }

        /// <summary>
        /// True when a chain is already pinned to <paramref name="tileId"/> as its owner tile with
        /// nothing left for <see cref="TrySetTsxOwnerTileId"/> to change -- no satellite hints
        /// still tracked for it.
        /// </summary>
        public bool IsTsxOwnerTileIdAlreadySet(AnimationChainSave chain, uint tileId) =>
            _tsxEntryTileIdsByChain.TryGetValue(chain, out var existing) && existing == tileId
            && !_tsxSatelliteTileIdsByChain.ContainsKey(chain);

        /// <summary>The Tiled tile id a frame's own pixel rect resolves to against this project's
        /// tileset, or <see langword="null"/> for an achx/achj project or a frame whose rect doesn't
        /// cleanly map to a single whole tile cell. Display-only (Inspector "Tile: N" readout, issue
        /// #1182) -- unrelated to which tile OWNS the chain's animation; see <see
        /// cref="GetTsxOwnerTileId"/> for that.</summary>
        public uint? ComputeFrameTileId(AnimationFrameSave frame)
        {
            if (_tsxTileset == null || AnimationChainListSave == null)
                return null;

            return Tiled.AchjToTiledAnimationMapper.TryGetTileId(
                frame, AnimationChainListSave.CoordinateType, BuildTsxTilesetInfo(_tsxTileset));
        }

        /// <summary>
        /// Names of chains that have a <see cref="Tiled.TsxAnimationValidator"/> issue -- a
        /// multi-tile group whose satellite tile has drifted out of lockstep with its anchor, or a
        /// dangling/chained/backward/incomplete-footprint <c>ParentId</c> (issue #1140). Empty when
        /// no tsx project is loaded or nothing is wrong. Correlates the validator's tile-id-keyed
        /// issues back to chain names by re-running <see cref="Tiled.MultiTileToTiledAnimationMapper"/>
        /// on the current in-memory chains and matching each chain's own computed entry tile id
        /// against either an issue's <c>AnchorTileId</c> or its <c>TileId</c> -- the same id math
        /// <see cref="SaveTsxProject"/> uses, so this always reflects the chains as they'd actually
        /// be written, not just as they were on load.
        /// </summary>
        /// <remarks>
        /// Matching only <c>AnchorTileId</c> is correct for a lockstep-mismatch issue (the satellite
        /// itself is folded into its anchor's chain, never its own) but wrong for a broken-ParentId
        /// issue (dangling/chained/backward/incomplete-footprint): <see
        /// cref="Tiled.TiledAnimationToAchjMapper.Map"/> makes the *referencing* tile (<c>TileId</c>)
        /// its own independent chain in those cases, not a satellite of <c>AnchorTileId</c>'s chain --
        /// matching only <c>AnchorTileId</c> either misses the actually-broken chain entirely (a
        /// chained ParentId, where the immediate parent is itself just a normal folded-in satellite
        /// with no chain of its own) or flags an unrelated, perfectly consistent chain instead (a
        /// backward ParentId, when the tile it names happens to be a real anchor with its own
        /// chain). Matching either id catches the actually-broken chain in every case, at the cost of
        /// also (correctly, not just incidentally) flagging the referenced anchor's chain too when it
        /// happens to have one -- reasonable, since one of its would-be satellites failing to attach
        /// is worth surfacing on the anchor as well.
        /// </remarks>
        public IReadOnlyList<string> GetChainNamesWithTsxIssues()
        {
            if (_tsxTileset == null || AnimationChainListSave == null)
                return Array.Empty<string>();

            var issues = Tiled.TsxAnimationValidator.Validate(_tsxTileset);
            if (issues.Count == 0)
                return Array.Empty<string>();

            var flaggedTileIds = issues.SelectMany(i => new[] { i.AnchorTileId, i.TileId }).ToHashSet();

            return Tiled.MultiTileToTiledAnimationMapper.Map(
                    AnimationChainListSave, BuildTsxTilesetInfo(_tsxTileset), _tsxEntryTileIdsByChain, _tsxSatelliteTileIdsByChain)
                .Where(r => r.EntryTileId.HasValue && flaggedTileIds.Contains(r.EntryTileId.Value))
                .Select(r => r.ChainName)
                .ToList();
        }

        /// <summary>
        /// Builds a <see cref="Tiled.TilesetAnimationInfo"/> from <paramref name="tileset"/>,
        /// including <c>TextureWidth</c>/<c>TextureHeight</c> so <see
        /// cref="Tiled.MultiTileToTiledAnimationMapper"/> can convert the achx model's UV
        /// coordinates back to pixels -- both <see cref="SaveTsxProject"/> and <see
        /// cref="GetChainNamesWithTsxIssues"/> need this identically; omitting the texture size
        /// here was a real shipped bug (issue #1140 follow-up): every frame silently fell into the
        /// <c>UvMissingPixelSize</c> skip path instead of mapping.
        /// </summary>
        private static Tiled.TilesetAnimationInfo BuildTsxTilesetInfo(DotTiled.Tileset tileset)
        {
            var image = tileset.Image;
            // Same size the load side used to build the UV rects (falls back to the tile grid's
            // extent when <image> carries no width/height), so UV -> pixel conversion on save
            // inverts it exactly instead of dereferencing a missing size.
            var (textureWidth, textureHeight) = Tiled.TiledAnimationToAchjMapper.GetTextureSize(tileset);
            return new Tiled.TilesetAnimationInfo
            {
                TileWidth = tileset.TileWidth,
                TileHeight = tileset.TileHeight,
                ColumnCount = tileset.Columns,
                TileCount = tileset.TileCount,
                Margin = tileset.Margin,
                TileSpacing = tileset.Spacing,
                ImageFileName = image.HasValue && image.Value.Source.HasValue ? image.Value.Source.Value : string.Empty,
                TextureWidth = textureWidth,
                TextureHeight = textureHeight,
            };
        }
    }
}
