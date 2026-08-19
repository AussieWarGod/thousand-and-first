"""Verify every cross-file reference in TAF's XML resolves to something real.

Sibling of check_wiring.py, and the same discipline applied to a different reference class.
STANDARDS section 4 says an asset is not shipped until something proves it is reachable, and that
the check runs in both directions. check_wiring.py does that for tiles. This does it for the
references the game resolves by name at load or roll time, where a wrong name is silent:

  blueprint reference   a Blueprint= naming something neither we nor the game defines
  population merge      a Load="Merge" into a table that does not exist and is not fabricable
  book reference        a Book ID= that no blueprint points at, or a pointer to no book

The population case is the one that motivated this. `DynamicObjectsTable:Books` looked like a
vanilla table to merge into. It is not declared anywhere; it is *fabricated* on demand from
blueprints carrying a matching tag (XRL/PopulationManager.cs:189-211). No blueprint in
StreamingAssets carries that tag and no code rolls the table, so the entry was a no-op that read
like content. Worse, declaring the name in XML would have pre-empted fabrication entirely
(`RequireTable` returns early when the key already exists), so if the tag ever did appear, our
dead entry would have suppressed the real table.

Usage, from the repository root:

    python3 Art/check_xml_refs.py [--base <StreamingAssets/Base>]

Without --base, vanilla resolution is skipped and only TAF-internal references are checked, so the
script still runs somewhere without the game installed.
"""

import io
import os
import re
import sys
import xml.etree.ElementTree as ET

TAF_XML = ["ObjectBlueprints.xml", "PopulationTables.xml", "Books.xml", "KingdomBuildings.xml"]
DEFAULT_BASE = "/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"


def read(path):
    with io.open(path, encoding="utf-8") as handle:
        return handle.read()


def taf_blueprints():
    names = set()
    for obj in ET.parse("ObjectBlueprints.xml").getroot().iter("object"):
        name = obj.get("Name")
        if name:
            names.add(name)
    return names


def taf_referenced_blueprints():
    """Every Blueprint= across our XML, with the file that mentions it."""
    refs = {}
    for path in TAF_XML:
        if not os.path.isfile(path):
            continue
        for match in re.finditer(r'Blueprint="([^"]+)"', read(path)):
            refs.setdefault(match.group(1), set()).add(path)
    return refs


def vanilla_blueprints(base):
    names = set()
    folder = os.path.join(base, "ObjectBlueprints")
    if not os.path.isdir(folder):
        return names
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".xml"):
            continue
        for match in re.finditer(r'<object\s+Name="([^"]+)"', read(os.path.join(folder, name))):
            names.add(match.group(1))
    return names


def vanilla_populations(base):
    path = os.path.join(base, "PopulationTables.xml")
    if not os.path.isfile(path):
        return set()
    return set(re.findall(r'<population\s+Name="([^"]+)"', read(path)))


def vanilla_tag_exists(base, tag):
    """Is any blueprint tagged for a fabricated table? Tags live in the blueprint corpus."""
    folder = os.path.join(base, "ObjectBlueprints")
    if not os.path.isdir(folder):
        return False
    for name in sorted(os.listdir(folder)):
        if name.endswith(".xml") and tag in read(os.path.join(folder, name)):
            return True
    return False


def rolled_by_our_code(table):
    """Does any TAF source actually roll this table by name?"""
    needle = '"%s"' % table
    for folder, _dirs, files in os.walk("."):
        if any(part in folder for part in (os.sep + ".git", os.sep + "DevTests", os.sep + "obj")):
            continue
        for name in files:
            if name.endswith(".cs") and needle in read(os.path.join(folder, name)):
                return True
    return False


def main():
    base = None
    if "--base" in sys.argv:
        base = sys.argv[sys.argv.index("--base") + 1]
    elif os.path.isdir(DEFAULT_BASE):
        base = DEFAULT_BASE

    if not os.path.isfile("ObjectBlueprints.xml"):
        sys.exit("run from the repository root")

    problems = []
    ours = taf_blueprints()
    theirs = vanilla_blueprints(base) if base else set()

    # 1. Every referenced blueprint resolves.
    for name, sources in sorted(taf_referenced_blueprints().items()):
        if name in ours:
            continue
        if base and name in theirs:
            continue
        if base:
            problems.append("unresolved blueprint %s, referenced by %s"
                            % (name, ", ".join(sorted(sources))))

    # 2. Every population we merge into exists or is genuinely fabricable.
    if base:
        known = vanilla_populations(base)
        ours_pop = set()
        root = ET.parse("PopulationTables.xml").getroot()
        for pop in root.iter("population"):
            ours_pop.add(pop.get("Name"))
        for pop in root.iter("population"):
            name = pop.get("Name")
            load = (pop.get("Load") or "Merge")
            if load != "Merge" or name in known:
                continue
            if name.startswith("DynamicObjectsTable:") or name.startswith("StaticObjectsTable:"):
                # Fabricated on demand from a matching blueprint tag. Declaring the name in XML
                # pre-empts that fabrication, so a merge is only meaningful if the tag exists.
                if not vanilla_tag_exists(base, name):
                    problems.append(
                        "dead population merge %s: not declared in vanilla, no blueprint carries "
                        "the tag it would be fabricated from, so nothing rolls it and our entry "
                        "pre-empts a table that would never have content" % name)
            elif not rolled_by_our_code(name):
                # A table we define and roll ourselves is a new table, which is fine and is how
                # another mod is meant to add settlers. A table we define and never roll is the
                # defect: Merge into an absent name silently creates one nothing reads.
                problems.append(
                    "population merge %s targets a table that does not exist in vanilla and that "
                    "no TAF code rolls; Merge into an absent name creates a table nothing reads"
                    % name)

    # 3. Book IDs referenced by blueprints exist, and books are reachable.
    if os.path.isfile("Books.xml"):
        book_ids = set(re.findall(r'<book\s+ID="([^"]+)"', read("Books.xml")))
        book_ids |= set(re.findall(r'ID="([^"]+)"', read("Books.xml")))
        pointed = set(re.findall(r'part\s+Name="Book"\s+ID="([^"]+)"', read("ObjectBlueprints.xml")))
        for pointer in sorted(pointed - book_ids):
            problems.append("blueprint points at book ID %s, which Books.xml does not define" % pointer)
        for orphan in sorted(book_ids - pointed):
            problems.append("book %s is defined but no blueprint points at it" % orphan)

    if problems:
        print("XML REFERENCES FAILED")
        for problem in problems:
            print("  " + problem)
        return 1

    scope = "including vanilla resolution" if base else "TAF-internal only (no --base)"
    print("XML REFERENCES CLEAN: %s" % scope)
    return 0


if __name__ == "__main__":
    sys.exit(main())
