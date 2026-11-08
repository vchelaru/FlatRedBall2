"""Tests for run_tests.py's class-name resolution. Run: python3 scripts/test_run_tests.py"""
import os, sys, unittest

sys.path.insert(0, os.path.dirname(__file__))
import run_tests


class ParseRequestsTests(unittest.TestCase):
    def test_comma_and_space_separated_names_split(self):
        self.assertEqual(run_tests.parse_requests(["A,B C"]), [(None, "A", None), (None, "B", None), (None, "C", None)])

    def test_method_and_project_prefix_are_split_out(self):
        self.assertEqual(run_tests.parse_requests(["Proj.Tests:Cls.Meth_Name"]), [("Proj.Tests", "Cls", "Meth_Name")])


class ResolveTests(unittest.TestCase):
    def setUp(self):
        self.projects = run_tests.find_test_projects()

    def test_engine_class_resolves_to_engine_project_only(self):
        filters, unresolved = run_tests.resolve([(None, "HexGridTests", None)], self.projects)
        self.assertEqual(unresolved, [])
        self.assertEqual(list(filters), ["FlatRedBall2.Tests"])
        self.assertIn("FullyQualifiedName~FlatRedBall2.Tests.Math.HexGridTests.", filters["FlatRedBall2.Tests"])

    def test_namespace_and_unknown_names_are_unresolved(self):
        filters, unresolved = run_tests.resolve([(None, "Collision", None), (None, "NoSuchTests", None)], self.projects)
        self.assertEqual(filters, {})
        self.assertEqual(unresolved, ["Collision", "NoSuchTests"])

    def test_method_filter_names_the_method_not_the_whole_class(self):
        filters, _ = run_tests.resolve([(None, "GridSnapperTests", "Snaps_Up")], self.projects)
        self.assertEqual(filters["AnimationEditor.Core.Tests"],
                         {"FullyQualifiedName~AnimationEditor.Core.Tests.GridSnapperTests.Snaps_Up"})

    def test_opt_in_projects_are_not_auto_candidates(self):
        filters, unresolved = run_tests.resolve([(None, "InstallerSandboxTests", None)], self.projects)
        self.assertEqual((filters, unresolved), ({}, ["InstallerSandboxTests"]))
        filters, _ = run_tests.resolve([("AnimationEditor.Sandbox.Tests", "InstallerSandboxTests", None)], self.projects)
        self.assertEqual(list(filters), ["AnimationEditor.Sandbox.Tests"])


if __name__ == "__main__":
    unittest.main()
