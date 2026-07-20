using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
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

        public FilePath[] ReferencedPngs { get; private set; } = new FilePath[0];

        public string? FileName { get; set; }

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

                acls = AnimationChainListSave.FromFile(fileName.FullPath);
            }

            AddShapeCollectionsToFrames(acls);

            OnDiskCoordinateType = acls.CoordinateType;
            _knownTextureSizes = knownTextureSizes;
            NormalizeCoordinatesToUv(acls, fileName.GetDirectoryContainingThis().FullPath, knownTextureSizes);

            AnimationChainListSave = acls;
            FileName = fileName.FullPath;

            if (!string.IsNullOrEmpty(acls.ProjectFile))
                TryLoadProjectFile(new FilePath(fileName.GetDirectoryContainingThis().FullPath + acls.ProjectFile));
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
        /// Save the current animation chain list to <paramref name="targetPath"/> in the
        /// format specified by <see cref="OnDiskCoordinateType"/>. The editor stores
        /// frame coordinates as UV internally (so the rendering pipeline can render at
        /// any texture size); when writing as Pixel, this method converts just for the
        /// on-disk write and then converts back so the in-memory model stays UV.
        /// </summary>
        public void SaveAnimationChainList(string targetPath)
        {
            var acls = AnimationChainListSave;
            if (acls == null) return;

            var achxDirectory = System.IO.Path.GetDirectoryName(targetPath) ?? string.Empty;
            var diskFormat = OnDiskCoordinateType;

            // No conversion needed when on-disk format matches the in-memory format (UV).
            if (diskFormat == TextureCoordinateType.UV)
            {
                acls.Save(targetPath);
                return;
            }

            var sizes = ConvertCoordinates(acls, achxDirectory, diskFormat);
            try
            {
                acls.Save(targetPath);
            }
            finally
            {
                // Convert back to UV so the in-memory model continues to be UV.
                ConvertCoordinates(acls, achxDirectory, TextureCoordinateType.UV, sizes);
            }
        }

        /// <summary>
        /// Save the current animation chain list to <paramref name="stream"/> in the format
        /// specified by <see cref="OnDiskCoordinateType"/> -- the seam the browser-wasm build
        /// needs, since it has no filesystem to write a path to. When converting UV back to
        /// Pixel, texture sizes come from the <c>knownTextureSizes</c> supplied to the most
        /// recent <see cref="LoadAnimationChain"/> call rather than a disk read: there is no
        /// directory to resolve a relative <see cref="AnimationFrameSave.TextureName"/> against
        /// on this overload. A texture missing from that dictionary is left in UV coordinates,
        /// same as the path-based overload's behavior when a PNG can't be read.
        /// </summary>
        public void SaveAnimationChainList(Stream stream)
        {
            var acls = AnimationChainListSave;
            if (acls == null) return;

            RunWithDiskCoordinateConversion(acls, () => acls.Save(stream));
        }

        /// <summary>
        /// Async counterpart to <see cref="SaveAnimationChainList(Stream)"/> for destination
        /// streams that only support async writes -- the browser-wasm build's
        /// <c>IStorageFile.OpenWriteAsync()</c> stream throws on a synchronous write, which
        /// <see cref="AnimationChainListSave.Save(Stream)"/> would otherwise trigger from inside
        /// <c>XmlWriter.Dispose()</c>. See <see cref="AnimationChainListSave.SaveAsync"/>.
        /// </summary>
        public async Task SaveAnimationChainListAsync(Stream stream)
        {
            var acls = AnimationChainListSave;
            if (acls == null) return;

            await RunWithDiskCoordinateConversionAsync(acls, () => acls.SaveAsync(stream));
        }

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
        private static Dictionary<string, (int W, int H)> ConvertCoordinates(
            AnimationChainListSave acls,
            string achxDirectory,
            TextureCoordinateType target,
            Dictionary<string, (int W, int H)>? sizeCache = null)
        {
            sizeCache ??= new Dictionary<string, (int W, int H)>(System.StringComparer.OrdinalIgnoreCase);

            if (acls.CoordinateType == target) return sizeCache;

            bool toPixel = target == TextureCoordinateType.Pixel;

            foreach (var chain in acls.AnimationChains)
            {
                foreach (var frame in chain.Frames)
                {
                    if (string.IsNullOrEmpty(frame.TextureName)) continue;

                    if (!sizeCache.TryGetValue(frame.TextureName, out var size))
                    {
                        // Browser callers (#768) key knownTextureSizes by the frame's own
                        // TextureName, so the TryGetValue above normally already succeeds. This
                        // bare-filename fallback covers any other caller-supplied sizeCache keyed
                        // by leaf name instead, before assuming a real disk read is possible.
                        var bareName = System.IO.Path.GetFileName(frame.TextureName);
                        if (bareName != frame.TextureName && sizeCache.TryGetValue(bareName, out size))
                        {
                            sizeCache[frame.TextureName] = size;
                        }
                        else
                        {
                            var path = System.IO.Path.IsPathRooted(frame.TextureName)
                                ? frame.TextureName
                                : System.IO.Path.Combine(achxDirectory, frame.TextureName);

                            var read = TryReadPngSize(path);
                            if (read == null) continue;
                            size = read.Value;
                            sizeCache[frame.TextureName] = size;
                        }
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

        private void AddShapeCollectionsToFrames(AnimationChainListSave acls)
        {
            foreach (var chain in acls.AnimationChains)
            {
                foreach (var frame in chain.Frames)
                {
                    frame.ShapesSave ??= new FlatRedBall2.Animation.Content.ShapesSave();
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
    }
}
