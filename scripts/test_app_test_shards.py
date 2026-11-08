"""Tests for app_test_shards.py. Run: python3 scripts/test_app_test_shards.py"""
import io, os, re, sys, unittest
from contextlib import redirect_stdout

sys.path.insert(0, os.path.dirname(__file__))
import app_test_shards
from run_tests import ROOT

WORKFLOW = os.path.join(ROOT, ".github", "workflows", "tests.yml")


def matches(test_filter, full_name):
    """Evaluates the only vstest filter shapes the script emits: 'FQN~a|FQN~b' or 'FQN!~a&FQN!~b'."""
    if "!~" not in test_filter:
        return any(full_name.find(t.split("~", 1)[1]) >= 0 for t in test_filter.split("|"))
    return all(full_name.find(t.split("!~", 1)[1]) < 0 for t in test_filter.split("&"))


def declared_test_names():
    """Every 'Namespace.Class.Test' in the App.Tests tree, found by scanning for test attributes
    rather than through app_test_shards, so a bug in its file discovery cannot hide a dropped class."""
    names = []
    for folder, _, files in os.walk(app_test_shards.APP_TESTS_DIR):
        p = folder.replace("\\", "/")
        if "/obj" in p or "/bin" in p:
            continue
        for f in files:
            if not f.endswith(".cs"):
                continue
            text = open(os.path.join(folder, f), errors="replace").read()
            if not re.search(r"\[(Avalonia)?(Fact|Theory)", text):
                continue
            ns = re.search(r"^\s*namespace\s+([\w.]+)", text, re.M).group(1)
            for cls in re.findall(r"^\s*(?:public |internal )?(?:sealed |static |abstract |partial )*class\s+(\w+)", text, re.M):
                names.append("%s.%s.SomeTest" % (ns, cls))
    return names


class ShardFilterTests(unittest.TestCase):
    CLASSES = [(900, "BigTests"), (500, "MidTests"), (400, "OtherTests"), (100, "SmallTests"), (50, "TinyTests")]

    def test_every_class_matches_exactly_one_shard(self):
        filters = app_test_shards.shard_filters(self.CLASSES, 3)
        for _, cls in self.CLASSES:
            hits = [i for i, f in enumerate(filters) if matches(f, "Ns.%s.Test" % cls)]
            self.assertEqual(len(hits), 1, cls)

    def test_class_not_in_the_listing_lands_only_in_the_last_shard(self):
        filters = app_test_shards.shard_filters(self.CLASSES, 3)
        hits = [i for i, f in enumerate(filters) if matches(f, "Ns.BrandNewTests.Test")]
        self.assertEqual(hits, [2])

    def test_class_name_that_is_a_suffix_of_another_is_not_confused(self):
        filters = app_test_shards.shard_filters([(10, "FooTests"), (5, "BarFooTests")], 2)
        self.assertEqual(sum(matches(f, "Ns.FooTests.T") for f in filters), 1)
        self.assertEqual(sum(matches(f, "Ns.BarFooTests.T") for f in filters), 1)

    def test_nested_class_runs_exactly_once_in_the_last_shard(self):
        filters = app_test_shards.shard_filters(self.CLASSES, 3)
        for _, cls in self.CLASSES:
            hits = [i for i, f in enumerate(filters) if matches(f, "Ns.%s+Inner.Test" % cls)]
            self.assertEqual(hits, [2], cls)

    def test_classes_are_balanced_by_size_across_all_shards_including_the_last(self):
        shards = app_test_shards.assign_classes(self.CLASSES, 3)
        self.assertEqual(sorted(sum(size for size, _ in shard) for shard in shards), [500, 550, 900])
        self.assertTrue(all(shards), "every shard, the last included, gets known classes")

    def test_last_shard_filter_excludes_only_the_classes_of_the_other_shards(self):
        shards = app_test_shards.assign_classes(self.CLASSES, 3)
        filters = app_test_shards.shard_filters(self.CLASSES, 3)
        for _, cls in shards[-1]:
            self.assertTrue(matches(filters[-1], "Ns.%s.Test" % cls), cls)


class RealTreeTests(unittest.TestCase):
    def test_union_of_all_shards_covers_every_test_class_exactly_once(self):
        names = declared_test_names()
        self.assertGreater(len(names), 200, "the scan found suspiciously few test classes")
        filters = [app_test_shards.shard_filter(i) for i in range(1, app_test_shards.SHARDS + 1)]
        for name in names:
            hits = [i + 1 for i, f in enumerate(filters) if matches(f, name)]
            self.assertEqual(len(hits), 1, "%s matched shards %s" % (name, hits))

    def test_every_shard_gets_some_classes(self):
        names = declared_test_names()
        for i in range(1, app_test_shards.SHARDS + 1):
            f = app_test_shards.shard_filter(i)
            self.assertGreater(sum(matches(f, n) for n in names), 10, "shard %d" % i)

    def test_test_class_names_are_unique_because_filters_match_by_name(self):
        classes = [n.split(".")[-2] for n in declared_test_names()]
        dupes = sorted({c for c in classes if classes.count(c) > 1})
        self.assertEqual(dupes, [])

    def test_ci_matrix_runs_exactly_the_shards_the_script_defines(self):
        # The last shard is "everything not in another shard": it only covers every class if every
        # shard actually runs. A matrix shorter than SHARDS would silently drop whole shards.
        text = open(WORKFLOW).read()
        m = re.search(r"^\s*shard:\s*\[([^\]]*)\]", text, re.M)
        self.assertIsNotNone(m, "no 'shard: [..]' matrix in tests.yml")
        self.assertEqual([int(x) for x in m.group(1).split(",")], list(range(1, app_test_shards.SHARDS + 1)))
        self.assertIn("scripts/app_test_shards.py ${{ matrix.shard }}", text)


class CommandLineTests(unittest.TestCase):
    def test_prints_the_filter_for_a_shard(self):
        out = io.StringIO()
        with redirect_stdout(out):
            code = app_test_shards.main(["2"])
        self.assertEqual(code, 0)
        self.assertEqual(out.getvalue().strip(), app_test_shards.shard_filter(2))

    def test_refuses_an_index_outside_the_shard_range(self):
        for bad in ["0", str(app_test_shards.SHARDS + 1), "x"]:
            out = io.StringIO()
            with redirect_stdout(out):
                code = app_test_shards.main([bad])
            self.assertEqual(code, 2, bad)
            self.assertIn("REFUSED", out.getvalue())


if __name__ == "__main__":
    unittest.main()
