# Conversions of the user's files

A conversion changes a file the user already has into another form. The rule: **a conversion shows what it will do before it writes anything**, which files change and what else uses them, and afterwards says when the result is not what it showed. Each one is called a conversion in the UI and docs. No version number goes up.

| Conversion | Changes | Can affect | Going back | Shows its plan first |
| --- | --- | --- | --- | --- |
| Convert to .achj (Animations tab, file or folder) | Writes the `.achj`, points the `.seqj` files under the project folder at it, moves the `.achx` and the old `.seqj` to the Recycle Bin, closes the `.achx` tab | `.seqj` outside the project folder, game code and project files that name the `.achx` | Restore the originals from the Recycle Bin and delete the `.achj` | Yes: `AchxConversionPlan` |
| UV to pixels, when a UV file is opened | The open file's coordinates. The next save writes them | Anything that reads the file as UV. | Close without editing, else source control | Only a Yes/No question, with no list |
| `aecli convert` | Writes the other format beside the source, which it keeps | Nothing: no file names the new one yet | Delete the new file | No: it never overwrites and never touches the source |

How Convert to .achj follows the rule (`AnimationEditor.Core/Conversions`):

- `AchxConversionPlan.Build` writes nothing. It converts each file in memory, reads it back and compares every field the `.achx` format holds (`AnimationListComparison`). A file that differs is left alone and listed.
- `AchxConversion.Run` runs that plan: write and compare each `.achj`, then the sequences, then recycle. An `.achx` that a sequence still needs is kept.
- What is on disk afterwards is compared with the plan. A difference is a problem banner.
- An open `.seqj` goes through its document. The conversion is not an undo entry, because undo would name a file that is gone. Earlier entries are rewritten when restored, as Save As does with folders.
