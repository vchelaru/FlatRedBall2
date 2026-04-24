# ai-reference

Skills for game developers building with FlatRedBall2.

Drop the subfolders you want into your project's `.claude/skills/` (or your
user-global `~/.claude/skills/`) to give Claude Code in-context guidance on the
engine — entities, collision, physics, screens, animation, tweening, etc.

These are **3rd-party skills**: they describe how to *use* FlatRedBall2 from a
game project. Skills for working *on* the engine itself (TDD discipline, skill
authoring, sample-project bootstrap, content-boundary philosophy, etc.) live in
`/.claude/skills/` and are not intended for distribution.

In this repo the same files are also surfaced at `/.claude/skills/<name>` via
local symlinks (gitignored) so Claude Code's auto-discovery picks them up while
working on the engine. The canonical copy is here.
