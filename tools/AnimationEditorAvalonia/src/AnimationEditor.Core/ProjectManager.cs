using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public void LoadAnimationChain(FilePath fileName)
        {
            if (!fileName.Exists())
                throw new FileNotFoundException($"Animation chain file not found: {fileName.FullPath}", fileName.FullPath);

            var rawContent = File.ReadAllText(fileName.FullPath);
            if (IO.AchxConflictMarkerDetector.HasConflictMarkers(rawContent))
                throw new System.IO.InvalidDataException(
                    $"{IO.AchxConflictMarkerDetector.ConflictMarkerMessage} ({fileName.FullPath})");

            var acls = AnimationChainListSave.FromFile(fileName.FullPath);

            AddShapeCollectionsToFrames(acls);

            OnDiskCoordinateType = acls.CoordinateType;
            NormalizeCoordinatesToUv(acls, fileName.GetDirectoryContainingThis().FullPath);

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
        /// parsed directly to avoid pulling an image-decode dependency into Core.
        /// </summary>
        private static void NormalizeCoordinatesToUv(AnimationChainListSave acls, string achxDirectory)
            => ConvertCoordinates(acls, achxDirectory, TextureCoordinateType.UV);

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
                        var path = System.IO.Path.IsPathRooted(frame.TextureName)
                            ? frame.TextureName
                            : System.IO.Path.Combine(achxDirectory, frame.TextureName);

                        var read = TryReadPngSize(path);
                        if (read == null) continue;
                        size = read.Value;
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
