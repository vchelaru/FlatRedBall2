using System.Collections.Generic;
using System.Xml.Serialization;

namespace AnimationEditor.Core.Data
{
    /// <summary>
    /// Project configuration for Tiled tileset sync: the <c>.tsx</c> files this .achx/.achj's
    /// animation chains should be pushed into on save (issue #1133). Persisted as a dedicated
    /// <c>.tiledsync</c> companion file next to the achx/achj -- deliberately separate from
    /// <see cref="AESettingsSave"/>'s <c>.aeproperties</c> file, whose established contract is
    /// personal editor view-state. Unlike that file, a <c>.tiledsync</c> file is project
    /// configuration and should be committed and shared with the team (issue #1136).
    /// </summary>
    public class AETiledSyncSave
    {
        [XmlElement("TiledTilesetPath")]
        public List<string> TiledTilesetPaths { get; set; } = new List<string>();
    }
}
