#!/usr/bin/env python3
"""Prints the `dotnet test --filter` for one shard of AnimationEditor.App.Tests.

Usage:
  python3 scripts/app_test_shards.py 2     # filter for shard 2 (1-based, SHARDS total)

CI runs one matrix leg per shard (`.github/workflows/tests.yml`). Shards are whole test classes,
because Avalonia headless runs every [AvaloniaFact] on one UI thread per process. Classes are spread
over all shards by file size (a rough proxy for run time). Shards 1..SHARDS-1 name their classes;
the last shard is "everything not named in another shard", so it also picks up a class this script
does not know about (a new file, a class that does not match its file name), exactly once. That only holds if every
shard runs: test_app_test_shards.py checks the CI matrix lists exactly 1..SHARDS.
"""
import os, sys

from run_tests import ROOT

APP_TESTS_DIR = os.path.join(ROOT, "tools", "AnimationEditorAvalonia", "tests", "AnimationEditor.App.Tests")
# Test time is ~4m50s and each shard pays ~1m of checkout/restore/build, so wall-clock is about
# 1m + 4m50s/SHARDS. At 4 shards that is ~2m30s, already under the ~3m engine job; more shards
# cost a runner-minute each and cannot shorten the run.
SHARDS = 4


def test_classes(app_dir=APP_TESTS_DIR):
    """Returns [(file size, class name)] for every *Tests.cs file; the class is named after its file."""
    classes = []
    for folder, _, names in os.walk(app_dir):
        parts = folder.replace("\\", "/")
        if "/bin" in parts or "/obj" in parts:
            continue
        for name in names:
            if name.endswith("Tests.cs"):
                classes.append((os.path.getsize(os.path.join(folder, name)), name[:-3]))
    return classes


def assign_classes(classes, shards):
    """Greedy size balancing of all known classes into `shards` lists of (size, class)."""
    buckets = [[0, []] for _ in range(shards)]
    for size, cls in sorted(classes, reverse=True):
        bucket = min(buckets, key=lambda b: b[0])
        bucket[0] += size
        bucket[1].append((size, cls))
    return [b[1] for b in buckets]


def shard_filters(classes, shards):
    """One filter per shard, matching a class by its '.Cls.' name segment (a nested class has '+', so it
    is never named and always runs in the last shard). Shards 1..N-1 name their classes; shard N
    excludes every class named elsewhere, so it runs its own known classes plus anything unknown."""
    explicit = assign_classes(classes, shards)[:-1]
    filters = ["|".join("FullyQualifiedName~.%s." % c for _, c in shard)
               for shard in explicit]
    listed = [c for shard in explicit for _, c in shard]
    filters.append("&".join("FullyQualifiedName!~.%s." % c for c in listed))
    return filters


def shard_filter(index):
    """The filter for 1-based shard `index` over the real test tree."""
    return shard_filters(test_classes(), SHARDS)[index - 1]


def main(argv):
    try:
        index = int(argv[0]) if len(argv) == 1 else 0
    except ValueError:
        index = 0
    if not 1 <= index <= SHARDS:
        print("REFUSED: pass one shard number from 1 to %d." % SHARDS)
        return 2
    print(shard_filter(index))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
