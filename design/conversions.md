# Conversions of the user's files

A conversion changes a file the user already has into another form. The rule: **a conversion shows what it will do before it writes anything**, which files change and what else uses them, and afterwards says when the result is not what it showed. Each one is called a conversion in the UI and docs. No version number goes up.

| Conversion | Changes | Can affect | Going back | Shows its plan first |
| --- | --- | --- | --- | --- |
| Convert to .achj (Animations tab, file or folder) | Writes the `.achj`, points the `.seqj` files under the project folder at it, moves the `.achx` and the old `.seqj` to the Recycle Bin, closes the `.achx` tab | `.seqj` outside the project folder, game code and project files that name the `.achx` | Restore the originals from the Recycle Bin and delete the `.achj` | Yes: `AchxConversionPlan` |
| UV to pixels, when a UV file is opened | The open file's coordinates. The next save writes them | Anything that reads the file as UV. | Close without editing, else source control | Only a Yes/No question, with no list |
| `aecli convert` | Writes the other format beside the source, which it keeps | Nothing: no file names the new one yet | Delete the new file | No: it never overwrites, never touches the source, and warns about everything the other format does not keep |

How Convert to .achj follows the rule (`AnimationEditor.Core/Conversions`):

- `AchxConversionPlan.Build` writes nothing. For each file it lists what the `.achj` does not keep (`AchxElementCheck.Drops`): every element that is not read (a text is read when one value makes a difference from another, a container when leaving it out makes one; a repeat that is not an item of a list is not, nor is what another element takes the place of, `CircleSaves` beside `Shapes`), every attribute that is not a namespace or `xsi:` one, and what an `.achx` only carries (`AnimationChainListSave.IsOnlyCarried`, beside the fields in AnimationChain.Common: FRB1's `ColorKey`, `ParentFile`, `FileName`, a shape's `Parent`, `RotationZ`, cubes and spheres). The file still converts, and its row lists them, grouped (`ConversionDrop.Describe`). What was read from it, apart from what is only carried, must equal what is read back from its `.achj`, made in memory, as the `.achx` writer writes both, with an empty text in either of its XML forms being the same (`AnimationListComparison`); a file that fails that is left alone, since it would be a writer bug. Both are `ConversionLoss.ToAchj`. Comments are not kept, and the row says so. Not checked: loose text or CDATA between elements (except where a container is written back as other elements, as `Shapes` is), processing instructions, `xsi:` attributes, whether an empty element was there, and how a number was written (1.0 or 1).
- Right before anything is written, every open document catches up with disk and the plan is made again, including the content of each `.achx`. A different plan is shown instead of run.
- `AchxConversion.Run` runs that plan: write and compare each `.achj`, then the sequences, then recycle. Right before that the folder is searched again, and an `.achx` something still plays is kept. The work is off the UI thread, and the dialog stays open and says what is being done.
- A recycled file must really be gone. What is on disk afterwards is compared with the plan. A difference is a problem banner.
- An open `.seqj` goes through its document. The conversion is not an undo entry, because undo would name a file that is gone. Earlier entries are rewritten when restored, as Save As does with folders.

`aecli convert` (`FileConvertEdit`) and its folder batch (`AchxToAchjBatch`) use the same check. What is not kept is one warning each, as comments are, and the exit code stays 0. It writes nothing and fails with `WouldLoseData` only when the animations would come out different:

- to `.achj`: `ConversionLoss.ToAchj`, as in the dialog.
- to `.achx`: `ConversionLoss.ToAchx`. Every property in the text that is not null or empty and is not read is a warning (`AchjPropertyCheck`), and what was read must equal what is read back from the `.achx`, as the `.achj` writer writes both: an `.achx` does not keep text of only spaces or a lone carriage return.

Save As to `.achj` (`AppCommands.SaveCurrentAnimationChainListAsync`) saves, then raises `SaveCompletedWithWarnings` with every element of the document, as an `.achx` writes it, that the `.achj` does not read back (`ConversionLoss.OfSavingAsAchj`, by comparison, not by the list of carried fields), shown in the problem banner as a `.tsx` save's warnings are.
