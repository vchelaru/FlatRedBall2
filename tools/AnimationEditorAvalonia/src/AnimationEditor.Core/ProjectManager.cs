using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
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

        /// <summary>Why a tileset cannot be associated while <see cref="IsNativeTsxProject"/>.</summary>
        public const string NativeTsxAssociationRefusal =
            "Associating a tileset applies to an .achx/.achj; a .tsx cannot be a native project and an association target at once.";

        /// <summary>The open native <c>.tsx</c> project's state (issue #1140), or <see langword="null"/> for an achx/achj project.</summary>
        private TsxState? _tsx;

        /// <summary>
        /// What a native <c>.tsx</c> document has beside its chains. Never changed in place, so a captured state
        /// stays as it was; only <paramref name="File"/> is shared by a document's states.
        /// </summary>
        /// <param name="Tileset">The tileset as of the last save.</param>
        internal sealed record TsxState(DotTiled.Tileset Tileset, Tiled.TsxFile File, Tiled.TsxUserProperties UserProperties)
        {
            /// <summary>Each native-tsx chain's own tile id, keyed by chain object reference (survives
            /// a rename, unlike keying by name) -- populated on <see cref="LoadTsxProject"/> from what
            /// the file already said, and kept current after every save so a
            /// brand-new chain's first-save id keeps being reused on every later save instead of being
            /// recomputed (and potentially drifting) from geometry each time. See <see
            /// cref="Tiled.MultiTileToTiledAnimationMapper"/>'s <c>knownEntryTileIds</c> parameter.</summary>
            public required IReadOnlyDictionary<AnimationChainSave, uint> EntryTileIdsByChain { get; init; }

            /// <summary>The satellite equivalent of <see cref="EntryTileIdsByChain"/>: each
            /// chain's satellites' own tile ids, keyed by chain reference then by the satellite's
            /// (Dx, Dy) offset within the footprint. Kept so a save whose mapping fails (bad geometry,
            /// wrong texture) still reports the tiles the chain owns instead of orphaning them, and so
            /// an offset that leaves the footprint (a shrink) and comes back (its Undo) lands on the
            /// same tile. See <see cref="Tiled.MultiTileToTiledAnimationMapper"/>'s
            /// <c>knownSatelliteTileIds</c> parameter.</summary>
            public required IReadOnlyDictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>> SatelliteTileIdsByChain { get; init; }

            /// <summary>Chains whose <see cref="AnimationChainSave.Name"/> is still the synthetic
            /// <c>"ID:{tileId}"</c> placeholder (<see cref="Tiled.TiledAnimationToAchjMapper.SyntheticChainName"/>)
            /// rather than a real, explicitly-given one -- set from real provenance at load time (<see
            /// cref="Tiled.TiledAnimationToAchjMapper.Map"/>'s <c>chainsWithSyntheticName</c> out param:
            /// did the tile carry a <c>Name</c> property at all?), never guessed from whether the current
            /// text happens to *look* like the placeholder format. <see cref="TrySetTsxOwnerTileId"/>
            /// reads this to decide whether to keep a chain's placeholder following its owner tile;
            /// <see cref="MarkChainNameExplicit"/> (called on a real rename) removes a chain once it has
            /// a real name, so it's never auto-renamed again.</summary>
            public required IReadOnlySet<AnimationChainSave> SyntheticNamedChains { get; init; }

            /// <summary>Each chain's own <see cref="AnimationChainSave.Frames"/> contents as of the
            /// most recent save where it had a real (non-null) entry tile id -- i.e. the frame-object
            /// sequence that produced <see cref="EntryTileIdsByChain"/>'s current value for that
            /// chain. Used only to seed a <see cref="DormantTsxHint"/> the moment a chain's frames go
            /// from non-empty to empty (see <see cref="DormantHintsByChain"/>).</summary>
            public required IReadOnlyDictionary<AnimationChainSave, IReadOnlyList<AnimationFrameSave>> LastNonEmptyFramesByChain { get; init; }

            /// <summary>A chain's tile-identity hint, held onto after a save sees
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
            public required IReadOnlyDictionary<AnimationChainSave, DormantTsxHint> DormantHintsByChain { get; init; }

            /// <summary>Which chain each tile's animation in the file belongs to, as of the load or last save. Unlike the hints,
            /// a tile has one owner here, and a chain that left the document or shrank off a tile no longer owns it.</summary>
            public required IReadOnlyDictionary<uint, AnimationChainSave> TileOwners { get; init; }

            /// <summary>See <see cref="TsxChainsNotFullySaved"/>.</summary>
            public required IReadOnlySet<AnimationChainSave> ChainsNotFullySaved { get; init; }
        }

        /// <summary>See <see cref="TsxState.DormantHintsByChain"/>.</summary>
        internal sealed record DormantTsxHint(
            uint EntryTileId,
            IReadOnlyDictionary<(int Dx, int Dy), uint> Satellites,
            IReadOnlyList<AnimationFrameSave> Frames);

        /// <summary>Whether the currently loaded project is a native <c>.tsx</c> project (see
        /// <see cref="LoadTsxProject"/>) rather than an achx/achj project.</summary>
        public bool IsNativeTsxProject => _tsx != null;

        /// <inheritdoc/>
        public Tiled.TilesetAnimationInfo? TsxTileset => _tsx is null ? null : BuildTsxTilesetInfo(_tsx);

        /// <summary>The tsx's own tile grid (tile size, margin, spacing), or <see langword="null"/>
        /// for an achx/achj project. A native tsx project's wireframe grid is always this -- it is
        /// not user-configurable (see issue #1140).</summary>
        public TileGrid? TsxTileGrid =>
            _tsx?.Tileset is not { } tileset ? null : new TileGrid(tileset.TileWidth, tileset.TileHeight, tileset.Margin, tileset.Spacing);

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
        /// of the in-memory representation, which is always pixels.
        /// </summary>
        public TextureCoordinateType OnDiskCoordinateType { get; set; } = TextureCoordinateType.Pixel;

        /// <param name="fileName">The .achx path. Only read from disk when <paramref name="preParsed"/> is null.</param>
        /// <param name="preParsed">Already-parsed content (e.g. a file the caller already read to check
        /// for a UV-to-Pixel conversion), skipping the disk read.</param>
        public void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null)
        {
            // Caller already has the parsed content -- nothing to read from disk, so the
            // existence and conflict-marker checks only apply when reading fileName ourselves.
            var acls = preParsed ?? AnimationFileLoader.Read(fileName);
            OnDiskCoordinateType = AnimationFileLoader.ConvertToEditorCoordinates(acls, fileName);

            AnimationChainListSave = acls;
            FileName = fileName.FullPath;

            // This ProjectManager instance is reused across File > Open calls (one instance per
            // app window/tab-set, not recreated per file -- see TabSwitchCacheTests), so a prior
            // LoadTsxProject's tileset/identity-tracking state must not leak into a now-plain achx
            // project: IsNativeTsxProject must go false, or this achx would be saved as that tileset.
            _tsx = null;

            // Same reused-instance hazard as the tsx state just above: ReferencedPngs is driven
            // entirely by the *current* achx's own ProjectFile reference, so a file with none must
            // clear whatever a previously loaded achx populated here rather than leaving it stale.
            if (!string.IsNullOrEmpty(acls.ProjectFile))
                TryLoadProjectFile(new FilePath(TexturePathHelper.ResolveDisplayPath(acls.ProjectFile, fileName.GetDirectoryContainingThis().FullPath)));
            else
                ReferencedPngs = new FilePath[0];
        }

        /// <summary>
        /// The one writer of a document, the open one or another tab's: writes it to <paramref name="target"/> as the
        /// .tsx it is, or as the .achj (JSON) or .achx (XML) that <paramref name="target"/>'s extension says. Nothing about
        /// <paramref name="document"/> changes when it throws.
        /// </summary>
        /// <returns>What a .tsx save warns of; see <see cref="SaveTsx"/>.</returns>
        internal static IReadOnlyList<string> WriteDocument(TabEntry document, string target)
        {
            if (document.CachedEditorModel is not { } model) return [];
            if (document.CachedTsxState is not TsxState tsx)
            {
                SaveAnimationChainList(model, document.Path, target, document.CachedOnDiskCoordinateType);
                return [];
            }
            document.CachedTsxState = SaveTsx(model, tsx, new FilePath(target), out var warnings);
            return warnings;
        }

        // The document holds pixels, so only a UV write converts, and it is left as it was. ownFile: the file whose folder
        // its relative paths are relative to so far.
        private static void SaveAnimationChainList(
            AnimationChainListSave acls, FilePath ownFile, string targetPath, TextureCoordinateType diskFormat)
        {
            var (from, to) = (ownFile.GetDirectoryContainingThis(), new FilePath(targetPath).GetDirectoryContainingThis());
            var achxDirectory = System.IO.Path.GetDirectoryName(targetPath) ?? string.Empty;
            var textureNames = acls.AnimationChains.SelectMany(chain => chain.Frames)
                .Select(frame => (Frame: frame, frame.TextureName)).ToList();
            var projectFile = acls.ProjectFile;
            void Write() { if (IsJsonPath(targetPath)) acls.SaveJson(targetPath); else acls.Save(targetPath); }

            try
            {
                foreach (var (frame, name) in textureNames) frame.TextureName = TexturePathHelper.Rebase(name, from, to);
                if (projectFile != null && from != to)
                    acls.ProjectFile = TexturePathHelper.NormalizeStoredTextureName(TexturePathHelper.Rebase(projectFile, from, to), achxDirectory);
                NormalizeFrameTextureNames(acls, achxDirectory);
                TextureCoordinateConverter.WriteAs(acls, diskFormat, TextureCoordinateConverter.SizesIn(achxDirectory), Write);
            }
            catch
            {
                // The document is still where it was, and a name relative to the target's folder is another file there.
                foreach (var (frame, name) in textureNames) frame.TextureName = name;
                acls.ProjectFile = projectFile;
                throw;
            }
        }

        /// <summary>True when <paramref name="path"/> has a .achj (JSON) extension; false for .achx or anything else.</summary>
        internal static bool IsJsonPath(string? path) =>
            !string.IsNullOrEmpty(path) && new FilePath(path).Extension == "achj";

        /// <summary>
        /// Resolves <paramref name="textureName"/> (relative to the loaded .achx's directory, or
        /// absolute) to its pixel size by reading the PNG header. Returns <c>null</c> when the name
        /// is empty or the PNG can't be read.
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

            return TextureCoordinateConverter.TryReadPngSize(path);
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
                var projectFile = new FilePath(TexturePathHelper.ResolveDisplayPath(projectFileRelative, achxFolder.FullPath));
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

                if (TextureCoordinateConverter.TryReadPngSize(path) == null)
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
        /// <exception cref="NotSupportedException">The tsx is not UTF-8, or uses a construct (an unwritable
        /// tileset feature) that would be lost on the first save -- see <see cref="Tiled.TsxCompatibilityChecker"/>.</exception>
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

            var (source, tileset) = ReadTsx(fileName);

            var acls = Tiled.TiledAnimationToAchjMapper.Map(
                tileset, out var entryTileIdsByChain, out var satelliteTileIdsByChain, out var syntheticNamedChains);

            // Seeded from what was just loaded, so a chain whose frames are cleared on the very first
            // save after load (no intervening save to have captured this otherwise) can still go
            // dormant instead of losing its hint outright -- see TsxState.DormantHintsByChain.
            var loadedFramesByChain = new Dictionary<AnimationChainSave, IReadOnlyList<AnimationFrameSave>>(ReferenceEqualityComparer.Instance);
            foreach (var chain in acls.AnimationChains)
                if (chain.Frames.Count > 0)
                    loadedFramesByChain[chain] = chain.Frames.ToArray();

            var tileOwners = new Dictionary<uint, AnimationChainSave>();
            foreach (var (chain, tileId) in entryTileIdsByChain)
                tileOwners[tileId] = chain;
            foreach (var (chain, satellites) in satelliteTileIdsByChain)
                foreach (var tileId in satellites.Values)
                    tileOwners[tileId] = chain;

            _tsx = new TsxState(tileset, new Tiled.TsxFile(fileName, source, tileset), Tiled.TsxUserProperties.Read(tileset))
            {
                EntryTileIdsByChain = new Dictionary<AnimationChainSave, uint>(entryTileIdsByChain, ReferenceEqualityComparer.Instance),
                SatelliteTileIdsByChain = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(satelliteTileIdsByChain, ReferenceEqualityComparer.Instance),
                SyntheticNamedChains = new HashSet<AnimationChainSave>(syntheticNamedChains, ReferenceEqualityComparer.Instance),
                LastNonEmptyFramesByChain = loadedFramesByChain,
                DormantHintsByChain = new Dictionary<AnimationChainSave, DormantTsxHint>(ReferenceEqualityComparer.Instance),
                TileOwners = tileOwners,
                ChainsNotFullySaved = new HashSet<AnimationChainSave>(),
            };
            AnimationChainListSave = acls;
            FileName = fileName.FullPath;

            // Same reused-instance hazard LoadAnimationChain resets its own achx-side fields for:
            // this ProjectManager instance is reused across File > Open calls, so a prior achx's
            // ReferencedPngs/OnDiskCoordinateType must not leak into a now-open tsx project. A tsx
            // has no ProjectFile/CoordinateType concept of its own, so CoordinateType goes back to
            // its no-project default. ReferencedPngs is the tileset's single <image> (#1365), so
            // the wireframe shows it on open even when no animation (and so no frame) references it.
            ReferencedPngs = ResolveTsxImagePath(tileset, fileName) is { } imagePath
                ? [imagePath]
                : new FilePath[0];
            OnDiskCoordinateType = TextureCoordinateType.Pixel;
        }

        internal static (Tiled.TsxSource Source, DotTiled.Tileset Tileset) ReadTsx(FilePath fileName)
        {
            try
            {
                var source = Tiled.TsxSource.Load(fileName.FullPath);
                var tileset = Tiled.TsxLoader.ReadTileset(source);
                if (!Tiled.TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var blockingReason))
                    throw new NotSupportedException(blockingReason);
                return (source, tileset);
            }
            catch (NotSupportedException ex)
            {
                throw new NotSupportedException($"Can't open \"{fileName.FullPath}\" as a native AnimationEditor project: {ex.Message}", ex);
            }
        }

        /// <summary>The tileset's single <c>&lt;image source&gt;</c> resolved against the tsx's own
        /// folder (how Tiled resolves it), or <see langword="null"/> when the tileset has no image
        /// source. Not checked for existence: a missing file is reported by the texture load.</summary>
        internal static FilePath? ResolveTsxImagePath(DotTiled.Tileset tileset, FilePath tsxPath)
        {
            if (!tileset.Image.HasValue || !tileset.Image.Value.Source.HasValue) return null;
            var source = tileset.Image.Value.Source.Value;
            if (string.IsNullOrEmpty(source)) return null;

            // Hand-rolled instead of Path.IsPathRooted, which on Linux misses a Windows-authored "C:\..." path.
            bool isRooted = source.StartsWith('/') || source.StartsWith('\\')
                || (source.Length >= 2 && source[1] == ':');
            return isRooted
                ? new FilePath(source)
                : new FilePath(tsxPath.GetDirectoryContainingThis().FullPath + source);
        }

        /// <inheritdoc/>
        public bool TsxLacksOutsideChange => _tsx?.File.LacksOutsideChange == true;

        /// <summary>
        /// The chains the open tsx's last save did not store in full: not written at all (a mapping
        /// warning, or no frames), or written without data the format can't hold.
        /// </summary>
        public IReadOnlySet<AnimationChainSave> TsxChainsNotFullySaved => _tsx?.ChainsNotFullySaved ?? new HashSet<AnimationChainSave>();

        /// <summary>
        /// Writes a tsx document to <paramref name="target"/>, via <see cref="Tiled.MultiTileToTiledAnimationMapper"/> and
        /// <see cref="Tiled.NativeTsxAnimationSync"/>. Returns its state after the save. <paramref name="warnings"/>: every chain's mapping warning -- a
        /// chain that couldn't be mapped (bad geometry, wrong texture, etc.) keeps whatever it last wrote to its tile
        /// untouched rather than being cleared -- plus one per chain carrying data the format
        /// can't hold (<see cref="Tiled.TsxLossyDataCheck"/>; that chain IS written, minus that
        /// data). A caller should surface these to the user instead of assuming the save fully
        /// captured every edit.
        /// </summary>
        /// <remarks>Same all-or-nothing invariant as <see cref="LoadTsxProject"/>: <see
        /// cref="Tiled.NativeTsxAnimationSync.Apply"/> runs against a working copy (<see
        /// cref="Tiled.NativeTsxAnimationSync.CloneForSave"/>), which is in the returned state only
        /// after <see cref="Tiled.TsxWriter"/> has actually
        /// succeeded -- so a write failure (a disk/permissions error)
        /// can't leave the in-memory tileset reflecting computed-but-never-persisted state.</remarks>
        private static TsxState SaveTsx(
            AnimationChainListSave acls, TsxState state, FilePath target, out IReadOnlyList<string> warnings)
        {
            // Revives a dormant hint for this save's Map() call, but only for a chain whose
            // current Frames are reference-sequence-identical to the ones captured when that hint
            // went dormant -- i.e. DeleteFramesCommand.Undo() re-inserting the exact same frame
            // objects it just removed. A chain re-authored with different frame content (even the
            // same count) does not match and is left to recompute fresh, same as today.
            var entryHintsForMap = new Dictionary<AnimationChainSave, uint>(state.EntryTileIdsByChain, ReferenceEqualityComparer.Instance);
            var satelliteHintsForMap = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(state.SatelliteTileIdsByChain, ReferenceEqualityComparer.Instance);
            foreach (var chain in acls.AnimationChains)
            {
                if (chain.Frames.Count == 0) continue;
                if (!state.DormantHintsByChain.TryGetValue(chain, out var dormant)) continue;
                if (!FramesSequenceEqual(dormant.Frames, chain.Frames)) continue;

                entryHintsForMap[chain] = dormant.EntryTileId;
                if (dormant.Satellites.Count > 0)
                    satelliteHintsForMap[chain] = dormant.Satellites;
            }

            var mapped = Tiled.MultiTileToTiledAnimationMapper.Map(
                acls, BuildTsxTilesetInfo(state), entryHintsForMap, satelliteHintsForMap);

            // No "ownership transfer" here: a chain's owner tile is a storage-slot choice (loaded,
            // computed once, or explicitly set), never re-derived from frame geometry after the
            // fact. Resizing/moving frame 0's rect never relocates it -- only an explicit
            // TrySetTsxOwnerTileId call (or the Sync-to-First-Frame button, which just calls that
            // with frame 0's current tile) changes which tile a chain owns.
            mapped = YieldCollidingClaims(mapped, satelliteHintsForMap, state, out var releasedHints);

            var workingTileset = Tiled.NativeTsxAnimationSync.CloneForSave(state.Tileset);
            // The undo history keeps the state of each edit, so one that changed nothing shares the tileset.
            if (!Tiled.NativeTsxAnimationSync.Apply(workingTileset, mapped, state.UserProperties).Changed)
                workingTileset = state.Tileset;
            state.File.Save(workingTileset, target);

            // Commits this save's tile assignments (including a brand-new chain's freshly-chosen
            // id) so the *next* save reuses them instead of recomputing from geometry again -- see
            // TsxState.EntryTileIdsByChain's doc comment for why that matters. Rebuilt primarily from
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
                acls.AnimationChains, ReferenceEqualityComparer.Instance);

            var updatedEntries = new Dictionary<AnimationChainSave, uint>(ReferenceEqualityComparer.Instance);
            foreach (var kvp in state.EntryTileIdsByChain)
                if (!currentChains.Contains(kvp.Key))
                    updatedEntries[kvp.Key] = kvp.Value;

            var updatedSatellites = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(ReferenceEqualityComparer.Instance);
            foreach (var kvp in state.SatelliteTileIdsByChain)
                if (!currentChains.Contains(kvp.Key))
                    updatedSatellites[kvp.Key] = kvp.Value;

            var updatedLastFrames = new Dictionary<AnimationChainSave, IReadOnlyList<AnimationFrameSave>>(ReferenceEqualityComparer.Instance);
            foreach (var kvp in state.LastNonEmptyFramesByChain)
                if (!currentChains.Contains(kvp.Key))
                    updatedLastFrames[kvp.Key] = kvp.Value;

            // Dormant hints for an absent chain are carried forward unchanged, same reasoning as
            // the dictionaries above -- DeleteChainsCommand's own absent-chain carry-forward
            // already handles reinsertion of the chain itself; this only matters if a chain is
            // deleted while it already had a dormant (frames-cleared) hint pending.
            var updatedDormant = new Dictionary<AnimationChainSave, DormantTsxHint>(ReferenceEqualityComparer.Instance);
            foreach (var kvp in state.DormantHintsByChain)
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
                        var merged = state.SatelliteTileIdsByChain.TryGetValue(chain, out var existingSatellites)
                            ? new Dictionary<(int Dx, int Dy), uint>(existingSatellites)
                            : new Dictionary<(int Dx, int Dy), uint>();
                        foreach (var satellite in result.Satellites)
                            merged[satellite.Offset] = satellite.TileId;
                        updatedSatellites[chain] = merged;
                    }
                    else if (state.SatelliteTileIdsByChain.TryGetValue(chain, out var stillHinted))
                    {
                        // This save's footprint has no satellites at all -- e.g. a resize command
                        // shrank every frame down to a single tile. chain.Frames never went to zero
                        // and no AnimationFrameSave object was added or removed (a resize only
                        // mutates existing frames' Left/Top/Right/BottomCoordinate in place), so
                        // there's no frame-reference signal available to gate a dormant-hint-style
                        // revival on. Keeping the old per-offset hints alive unconditionally instead
                        // mirrors how TsxState.EntryTileIdsByChain already behaves for a chain that never
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
                    if (state.DormantHintsByChain.TryGetValue(chain, out var stillDormant))
                        updatedDormant[chain] = stillDormant;
                    else if (state.EntryTileIdsByChain.TryGetValue(chain, out var oldEntry)
                        && state.LastNonEmptyFramesByChain.TryGetValue(chain, out var oldFrames))
                    {
                        state.SatelliteTileIdsByChain.TryGetValue(chain, out var oldSatellites);
                        updatedDormant[chain] = new DormantTsxHint(
                            oldEntry, oldSatellites ?? new Dictionary<(int Dx, int Dy), uint>(), oldFrames);
                    }
                }
                else if (releasedHints.Contains(chain))
                {
                    // Its tile carries another chain now, so it is placed by its frames from here on.
                }
                else if (state.EntryTileIdsByChain.TryGetValue(chain, out var stillActiveEntry))
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
                    updatedLastFrames[chain] = chain.Frames.ToArray();
                    if (state.SatelliteTileIdsByChain.TryGetValue(chain, out var stillActiveSatellites))
                        updatedSatellites[chain] = stillActiveSatellites;
                }
                else if (state.DormantHintsByChain.TryGetValue(chain, out var stillDormantThroughAbort))
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

            var updatedOwners = new Dictionary<uint, AnimationChainSave>();
            foreach (var result in mapped)
                foreach (var tileId in ClaimedTiles(result))
                    updatedOwners[tileId] = result.SourceChain;

            var notFullySaved = mapped
                .Where(r => r.Warnings.Count > 0 || r.EntryTileId == null
                    || Tiled.TsxLossyDataCheck.DroppedKinds(r.SourceChain, acls.TimeMeasurementUnit).Count > 0)
                .Select(r => r.SourceChain).ToHashSet<AnimationChainSave>(ReferenceEqualityComparer.Instance);

            var updatedState = state with
            {
                Tileset = workingTileset,
                EntryTileIdsByChain = updatedEntries,
                SatelliteTileIdsByChain = updatedSatellites,
                LastNonEmptyFramesByChain = updatedLastFrames,
                DormantHintsByChain = updatedDormant,
                TileOwners = updatedOwners,
                ChainsNotFullySaved = notFullySaved,
            };

            warnings = mapped.SelectMany(r => r.Warnings)
                .Concat(Tiled.TsxLossyDataCheck.Warnings(acls))
                .ToList();
            return updatedState;
        }

        /// <inheritdoc/>
        public bool MoveTsxProject(FilePath path)
        {
            if (_tsx == null || AnimationChainListSave == null)
                return false;

            var (from, to) = (_tsx.File.Path.GetDirectoryContainingThis(), path.GetDirectoryContainingThis());
            foreach (var frame in AnimationChainListSave.AnimationChains.SelectMany(chain => chain.Frames))
                frame.TextureName = TexturePathHelper.Rebase(frame.TextureName, from, to);
            _tsx.File.SetPath(path, _tsx.Tileset);
            return from != to;
        }

        /// <summary>
        /// A Tiled tile carries one animation, so two chains wanting the same tile can't both be written. That happens
        /// routinely: duplicating a chain gives the copy the same cells as its source, and the user moves the copy's
        /// frames afterwards. Rather than refusing the whole save (<see cref="Tiled.NativeTsxAnimationSync.Apply"/> would
        /// throw), one chain keeps the tile: the one whose animation the file has there, then one hinted to it, then the
        /// first in chain order. Every other chain is not written, is reported
        /// by name, and like any chain that couldn't be mapped keeps exactly the tiles it has in the file.
        /// </summary>
        /// <param name="releasedHints">Chains that lost a tile and whose owner tile is only a hint. They forget it, so moving
        /// their frames, which the warning asks for, gets them saved.</param>
        private static IReadOnlyList<Tiled.MultiTileMappingResult> YieldCollidingClaims(
            IReadOnlyList<Tiled.MultiTileMappingResult> mapped,
            IReadOnlyDictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>> satelliteHints,
            TsxState state,
            out HashSet<AnimationChainSave> releasedHints)
        {
            var failed = new HashSet<AnimationChainSave>(
                mapped.Where(r => r.Warnings.Count > 0).Select(r => r.SourceChain), ReferenceEqualityComparer.Instance);
            var yielding = new Dictionary<AnimationChainSave, string>(ReferenceEqualityComparer.Instance);

            var claims = new List<(uint TileId, AnimationChainSave Chain, int Rank)>();
            foreach (var r in mapped.Where(r => r.Warnings.Count == 0))
            {
                int Rank(uint tileId, bool hinted) => OwnsTile(state, r.SourceChain, tileId) ? 0 : hinted ? 1 : 2;
                if (r.EntryTileId is { } entry)
                    claims.Add((entry, r.SourceChain, Rank(entry, !r.EntryTileIdIsFreshlyComputed)));
                foreach (var satellite in r.Satellites)
                    claims.Add((satellite.TileId, r.SourceChain, Rank(satellite.TileId,
                        satelliteHints.TryGetValue(r.SourceChain, out var hints)
                        && hints.TryGetValue(satellite.Offset, out var hintedId) && hintedId == satellite.TileId)));
            }
            var claimsByTile = claims
                .GroupBy(c => c.TileId, (tileId, c) => (TileId: tileId, Claims: c.OrderBy(x => x.Rank).ToList()))
                .OrderBy(g => g.Claims[0].Rank).ToList();

            // A chain that yields keeps the tiles it has in the file, which can take one from a chain already passed.
            int yieldingBefore;
            do
            {
                yieldingBefore = yielding.Count;
                foreach (var (tileId, tileClaims) in claimsByTile)
                {
                    var claimants = tileClaims.Select(c => c.Chain).Where(c => !yielding.ContainsKey(c)).ToList();
                    if (claimants.Count == 0) continue;
                    var winner = state.TileOwners.TryGetValue(tileId, out var owner) && (failed.Contains(owner) || yielding.ContainsKey(owner))
                        ? owner : claimants[0];
                    foreach (var loser in claimants.Where(c => !ReferenceEquals(c, winner)))
                        yielding[loser] = $"chain \"{loser.Name}\": Tiled tile {tileId} already carries \"{winner.Name}\" - not saved; move its frames to cells no other chain uses.";
                }
            } while (yielding.Count > yieldingBefore);

            releasedHints = new HashSet<AnimationChainSave>(
                mapped.Where(r => yielding.ContainsKey(r.SourceChain) && !r.EntryTileIdIsFreshlyComputed && !OwnsTile(state, r.SourceChain, r.EntryTileId!.Value))
                    .Select(r => r.SourceChain), ReferenceEqualityComparer.Instance);

            return mapped.Select(r =>
            {
                var warnings = yielding.TryGetValue(r.SourceChain, out var warning) ? [warning] : r.Warnings;
                if (warnings.Count == 0)
                    return r;
                return r with
                {
                    AnchorFrames = [],
                    EntryTileId = null,
                    EntryTileIdIsFreshlyComputed = false,
                    Satellites = state.TileOwners.Where(o => ReferenceEquals(o.Value, r.SourceChain))
                        .Select(o => new Tiled.TiledSatelliteMapping(o.Key, [], default)).ToList(),
                    Warnings = warnings,
                };
            }).ToList();
        }

        private static bool OwnsTile(TsxState state, AnimationChainSave chain, uint tileId) =>
            state.TileOwners.TryGetValue(tileId, out var owner) && ReferenceEquals(owner, chain);

        private static IEnumerable<uint> ClaimedTiles(Tiled.MultiTileMappingResult result) =>
            result.Satellites.Select(s => s.TileId).Concat(result.EntryTileId is { } entry ? [entry] : Array.Empty<uint>());

        /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> hold the exact same
        /// <see cref="AnimationFrameSave"/> objects, in the same order -- the "is this the same
        /// Undo?" test <see cref="TsxState.DormantHintsByChain"/> relies on. Deliberately reference
        /// equality, not value equality: a genuinely re-authored frame with identical-looking
        /// coordinates is still a different object and must NOT match.</summary>
        private static bool FramesSequenceEqual(IReadOnlyList<AnimationFrameSave> a, IReadOnlyList<AnimationFrameSave> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        /// <inheritdoc/>
        public object? CaptureTsxState() => _tsx;

        /// <inheritdoc/>
        public void RestoreTsxState(object? state) => _tsx = state as TsxState;

        /// <inheritdoc/>
        public void ResetToBlankDocument()
        {
            AnimationChainListSave = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
            FileName = null;
            OnDiskCoordinateType = TextureCoordinateType.Pixel;
            RestoreTsxState(null);
            ReferencedPngs = new FilePath[0];
        }

        /// <summary>
        /// The tile id a native-tsx chain's Tiled <c>&lt;animation&gt;</c> block would be written to
        /// on the next save -- an explicit hint set via <see cref="TrySetTsxOwnerTileId"/>, one
        /// loaded from disk, or (when neither exists yet) freshly computed from frame 0's own
        /// top-left cell. Issue #1182: makes what was previously hidden save-time bookkeeping
        /// (<see cref="TsxState.EntryTileIdsByChain"/>) explicit and readable, e.g. for an Inspector
        /// "Placed Tile: N" readout. <see langword="null"/> for an achx/achj project, a chain not in
        /// this project, or a chain whose current frames can't be mapped (empty, or bad geometry).
        /// </summary>
        public uint? GetTsxOwnerTileId(AnimationChainSave chain)
        {
            if (_tsx == null || AnimationChainListSave == null)
                return null;

            return Tiled.MultiTileToTiledAnimationMapper.Map(
                    AnimationChainListSave, BuildTsxTilesetInfo(_tsx), _tsx.EntryTileIdsByChain, _tsx.SatelliteTileIdsByChain)
                .FirstOrDefault(r => ReferenceEquals(r.SourceChain, chain))
                ?.EntryTileId;
        }

        /// <summary>
        /// Explicitly sets which tile id a native-tsx chain's Tiled <c>&lt;animation&gt;</c> is
        /// written to on the next save, overriding whatever <see cref="GetTsxOwnerTileId"/> would
        /// otherwise report -- issue #1182's explicit ownership, e.g. from an Inspector field the
        /// user types into, or a "Sync to First Frame" action that passes <see
        /// cref="GetTsxOwnerTileId"/>'s own frame-0-computed value back in. Validated against the
        /// tileset's own tile count and every tile another chain uses or the save refuses, before committing;
        /// returns a human-readable error and makes no change on failure, or <see langword="null"/>
        /// on success. Drops any satellite-offset hints (<see cref="TsxState.SatelliteTileIdsByChain"/>):
        /// a multi-tile chain's satellites must recompute relative to the new owner position instead
        /// of reusing offsets captured at the old one. There is no auto-follow -- once set (loaded,
        /// computed once, or set here), a chain's owner tile never moves again on its own; only
        /// another call here (e.g. the "Sync to First Frame" button) changes it. If <paramref
        /// name="chain"/> is still tracked in <see cref="TsxState.SyntheticNamedChains"/> -- its name has
        /// never been anything but the synthetic <c>"ID:{tileId}"</c> placeholder, per real
        /// provenance from load time, not a guess from the string's shape -- the name is re-derived
        /// for the new tile too, so an unnamed chain's tree label follows its owner instead of going
        /// stale (and, worse, getting baked in as a permanent explicit name on the next save: <see
        /// cref="Tiled.NativeTsxAnimationSync"/> only omits the <c>Name</c> property when the chain's
        /// current name still matches its current tile's synthetic one).
        /// </summary>
        public string? TrySetTsxOwnerTileId(AnimationChainSave chain, uint tileId)
        {
            if (_tsx == null || AnimationChainListSave == null)
                return "Can't set an owner tile: this isn't a native Tiled (.tsx) project.";
            if (!AnimationChainListSave.AnimationChains.Contains(chain))
                return "Can't set an owner tile: this chain isn't part of the current project.";
            if (tileId >= (uint)_tsx.Tileset.TileCount)
                return $"Tile {tileId} is past this tileset's {Plural.Format(_tsx.Tileset.TileCount, "tile")}.";

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

            var updatedEntries = new Dictionary<AnimationChainSave, uint>(_tsx.EntryTileIdsByChain, ReferenceEqualityComparer.Instance)
            {
                [chain] = tileId
            };
            var updatedSatellites = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(_tsx.SatelliteTileIdsByChain, ReferenceEqualityComparer.Instance);
            updatedSatellites.Remove(chain);

            var tilesetInfo = BuildTsxTilesetInfo(_tsx);
            var own = Tiled.MultiTileToTiledAnimationMapper.Map(AnimationChainListSave, tilesetInfo, updatedEntries, updatedSatellites)
                .First(r => ReferenceEquals(r.SourceChain, chain));
            var claimed = ClaimedTiles(own).ToHashSet();
            var settled = YieldCollidingClaims(
                Tiled.MultiTileToTiledAnimationMapper.Map(AnimationChainListSave, tilesetInfo, _tsx.EntryTileIdsByChain, _tsx.SatelliteTileIdsByChain),
                _tsx.SatelliteTileIdsByChain, _tsx, out _);
            foreach (var other in settled.Where(r => !ReferenceEquals(r.SourceChain, chain)))
                foreach (var taken in ClaimedTiles(other).Where(claimed.Contains))
                    return $"Tile {taken} is already used by \"{other.ChainName}\".";

            // The save refuses a tile achx-push owns or the user put a property on that it would overwrite.
            var name = _tsx.SyntheticNamedChains.Contains(chain) ? Tiled.TiledAnimationToAchjMapper.SyntheticChainName(tileId) : chain.Name;
            if (name != chain.Name && AnimationChainListSave.HasOtherChainNamed(chain, name))
                return $"A chain named '{name}' already exists.";
            try
            {
                Tiled.NativeTsxAnimationSync.Apply(
                    Tiled.NativeTsxAnimationSync.CloneForSave(_tsx.Tileset), [own with { ChainName = name, Warnings = [] }], _tsx.UserProperties);
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }

            _tsx = _tsx with { EntryTileIdsByChain = updatedEntries, SatelliteTileIdsByChain = updatedSatellites };
            chain.Name = name;

            return null;
        }

        /// <inheritdoc/>
        public bool IsChainNameAuto(AnimationChainSave chain) => _tsx?.SyntheticNamedChains.Contains(chain) == true;

        /// <summary>
        /// Reverts a native-tsx chain to its synthetic <c>"ID:{ownerTileId}"</c> name, so the next
        /// save omits the tile's <c>Name</c> property. Returns <see langword="false"/> (no change)
        /// outside a native-tsx project or when the chain has no owner tile to name it after.
        /// Replaces the set wholesale for the same reason as <see cref="MarkChainNameExplicit"/>.
        /// </summary>
        public bool MakeChainNameAuto(AnimationChainSave chain)
        {
            if (_tsx == null || GetTsxOwnerTileId(chain) is not { } ownerTileId)
                return false;
            chain.Name = Tiled.TiledAnimationToAchjMapper.SyntheticChainName(ownerTileId);
            _tsx = _tsx with { SyntheticNamedChains = new HashSet<AnimationChainSave>(_tsx.SyntheticNamedChains, ReferenceEqualityComparer.Instance) { chain } };
            return true;
        }

        /// <summary>
        /// Marks a chain's name as a real, user-given one from now on -- called on an actual rename
        /// (<see cref="AppCommands.RenameChain"/>) so <see cref="TrySetTsxOwnerTileId"/> never
        /// overwrites it again. No-op outside a native-tsx project or for a chain that was already
        /// marked. Returns whether the chain *was* still tracked as synthetic. Replaces the whole
        /// set, because a captured state (<see cref="CaptureTsxState"/>) holds it by reference.
        /// </summary>
        public bool MarkChainNameExplicit(AnimationChainSave chain)
        {
            if (_tsx == null || !_tsx.SyntheticNamedChains.Contains(chain))
                return false;
            var updated = new HashSet<AnimationChainSave>(_tsx.SyntheticNamedChains, ReferenceEqualityComparer.Instance);
            updated.Remove(chain);
            _tsx = _tsx with { SyntheticNamedChains = updated };
            return true;
        }

        /// <summary>
        /// True when a chain is already pinned to <paramref name="tileId"/> as its owner tile with
        /// nothing left for <see cref="TrySetTsxOwnerTileId"/> to change -- no satellite hints
        /// still tracked for it.
        /// </summary>
        public bool IsTsxOwnerTileIdAlreadySet(AnimationChainSave chain, uint tileId) =>
            _tsx != null && _tsx.EntryTileIdsByChain.TryGetValue(chain, out var existing) && existing == tileId
            && !_tsx.SatelliteTileIdsByChain.ContainsKey(chain);

        /// <summary>The Tiled tile id a frame's own pixel rect resolves to against this project's
        /// tileset, or <see langword="null"/> for an achx/achj project or a frame whose rect doesn't
        /// cleanly map to a single whole tile cell. Display-only (Inspector "Tile: N" readout, issue
        /// #1182) -- unrelated to which tile OWNS the chain's animation; see <see
        /// cref="GetTsxOwnerTileId"/> for that.</summary>
        public uint? ComputeFrameTileId(AnimationFrameSave frame)
        {
            if (_tsx == null || AnimationChainListSave == null)
                return null;

            return Tiled.AchjToTiledAnimationMapper.TryGetTileId(
                frame, AnimationChainListSave.CoordinateType, BuildTsxTilesetInfo(_tsx));
        }

        /// <summary>
        /// Names of chains that have a <see cref="Tiled.TsxAnimationValidator"/> issue -- a
        /// multi-tile group whose satellite tile has drifted out of lockstep with its anchor, or a
        /// dangling/chained/backward/incomplete-footprint <c>ParentId</c> (issue #1140). Empty when
        /// no tsx project is loaded or nothing is wrong. Correlates the validator's tile-id-keyed
        /// issues back to chain names by re-running <see cref="Tiled.MultiTileToTiledAnimationMapper"/>
        /// on the current in-memory chains and matching each chain's own computed entry tile id
        /// against either an issue's <c>AnchorTileId</c> or its <c>TileId</c> -- the same id math
        /// a save uses, so this always reflects the chains as they'd actually
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
            if (_tsx == null || AnimationChainListSave == null)
                return Array.Empty<string>();

            var issues = Tiled.TsxAnimationValidator.Validate(_tsx.Tileset);
            if (issues.Count == 0)
                return Array.Empty<string>();

            var flaggedTileIds = issues.SelectMany(i => new[] { i.AnchorTileId, i.TileId }).ToHashSet();

            return Tiled.MultiTileToTiledAnimationMapper.Map(
                    AnimationChainListSave, BuildTsxTilesetInfo(_tsx), _tsx.EntryTileIdsByChain, _tsx.SatelliteTileIdsByChain)
                .Where(r => r.EntryTileId.HasValue && flaggedTileIds.Contains(r.EntryTileId.Value))
                .Select(r => r.ChainName)
                .ToList();
        }

        /// <summary>
        /// Builds a <see cref="Tiled.TilesetAnimationInfo"/> from <paramref name="state"/>, which
        /// both a save and <see cref="GetChainNamesWithTsxIssues"/> need.
        /// </summary>
        private static Tiled.TilesetAnimationInfo BuildTsxTilesetInfo(TsxState state)
        {
            var tileset = state.Tileset;
            var (textureWidth, textureHeight) = Tiled.TiledAnimationToAchjMapper.GetTextureSize(tileset);
            return new Tiled.TilesetAnimationInfo
            {
                TileWidth = tileset.TileWidth,
                TileHeight = tileset.TileHeight,
                ColumnCount = tileset.Columns,
                TileCount = tileset.TileCount,
                Margin = tileset.Margin,
                TileSpacing = tileset.Spacing,
                ImageFileName = state.File.ImageSource,
                Folder = state.File.Path.GetDirectoryContainingThis().FullPath,
                TextureWidth = textureWidth,
                TextureHeight = textureHeight,
            };
        }
    }
}
