# Reading Raw Animation Data

The FlatRedBall.AnimationChain.Common package can be used to load animation data from AnimationEditor files. This can be used to draw or process the animation data in any C# environment.

## Install the Package

To install the package:

```
dotnet add package FlatRedBall.AnimationChain.Common
```

## Load the Data

The following minimal code shows how to load the data from disk:

```csharp
using var stream = TitleContainer.OpenStream("Content/MyFile.achj");
var animations = AnimationChainListSave.FromStream(stream);

foreach (var chain in _save.AnimationChains)
    if (chain.Name == name)
        // you can store this or do whatever

```
