"""Tests for push-pr-screenshots.py's markdown layout. Run: python3 scripts/test_push_pr_screenshots.py"""
import importlib.util, os, unittest

_spec = importlib.util.spec_from_file_location(
    "push_pr_screenshots", os.path.join(os.path.dirname(__file__), "push-pr-screenshots.py"))
uploader = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(uploader)


class FormatMarkdownTests(unittest.TestCase):
    def test_pair_puts_before_left_of_after_regardless_of_input_order(self):
        md = uploader.format_markdown([("after-menu.png", "u/after"), ("before-menu.png", "u/before")])
        self.assertEqual(md, "**menu**\n\n| Before | After |\n| --- | --- |\n"
                             "| ![before-menu](u/before) | ![after-menu](u/after) |")

    def test_each_pair_gets_its_own_labeled_table(self):
        md = uploader.format_markdown([("after-b.png", "1"), ("after-a.png", "2"),
                                       ("before-a.png", "3"), ("before-b.png", "4")])
        self.assertLess(md.index("**a**"), md.index("**b**"))
        self.assertEqual(md.count("| Before | After |"), 2)

    def test_unpaired_images_print_as_plain_lines_after_pairs(self):
        md = uploader.format_markdown([("zoom.png", "z"), ("after-x.png", "ax"), ("before-x.png", "bx"),
                                       ("before-lonely.png", "bl")])
        self.assertTrue(md.endswith("![before-lonely](bl)\n\n![zoom](z)"))
        self.assertIn("| ![before-x](bx) | ![after-x](ax) |", md)


if __name__ == "__main__":
    unittest.main()
