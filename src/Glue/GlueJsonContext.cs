using System.Collections.Generic;
using System.Text.Json.Serialization;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Source-generated serialization metadata for the Glue save shapes. Source generation rather than
/// reflection because the engine builds with <c>IsAotCompatible</c>.
/// </summary>
/// <remarks>
/// Reading only — nothing here writes Glue files. Round-tripping would need its own design pass,
/// since FRB1's <c>ShouldSerialize</c> conventions and Newtonsoft attributes have no
/// <c>System.Text.Json</c> equivalent and output would diverge from what Glue produces.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(GlueProjectSave))]
[JsonSerializable(typeof(ScreenSave))]
[JsonSerializable(typeof(EntitySave))]
[JsonSerializable(typeof(NamedObjectSave))]
[JsonSerializable(typeof(CustomVariable))]
[JsonSerializable(typeof(ReferencedFileSave))]
[JsonSerializable(typeof(PropertySave))]
[JsonSerializable(typeof(InstructionSave))]
[JsonSerializable(typeof(StateSave))]
[JsonSerializable(typeof(StateSaveCategory))]
[JsonSerializable(typeof(DisplaySettings))]
[JsonSerializable(typeof(GlueElementFileReference))]
[JsonSerializable(typeof(List<PropertySave>))]
internal partial class GlueJsonContext : JsonSerializerContext;
