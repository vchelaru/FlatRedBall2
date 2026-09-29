"""Tests for ae-before-after.py's output folder. Run: python3 scripts/test_ae_before_after.py"""
import importlib.util, os, tempfile, unittest

_spec = importlib.util.spec_from_file_location(
    "ae_before_after", os.path.join(os.path.dirname(__file__), "ae-before-after.py"))
script = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(script)


class ResetOutDirTests(unittest.TestCase):
    def test_out_dir_lives_in_the_worktree_and_starts_empty(self):
        with tempfile.TemporaryDirectory() as tree:
            out = script.reset_out_dir(tree)
            self.assertTrue(out.startswith(tree))
            with open(os.path.join(out, "after-stale.png"), "w") as f:
                f.write("x")
            os.makedirs(os.path.join(out, "sub"))

            self.assertEqual(script.reset_out_dir(tree), out)
            self.assertEqual(os.listdir(out), [])


if __name__ == "__main__":
    unittest.main()
