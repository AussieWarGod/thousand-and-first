"""Verify every cross-file reference in TAF's XML resolves to something real.

Sibling of check_wiring.py, and the same discipline applied to a different reference class.
STANDARDS section 4 says an asset is not shipped until something proves it is reachable, and that
the check runs in both directions. check_wiring.py does that for tiles. This does it for the
references the game resolves by name at load or roll time, where a wrong name is silent:

  blueprint reference   a Blueprint= naming something neither we nor the game defines
  population merge      a Load="Merge" into a table that does not exist and is not fabricable
  book reference        a Book ID= that no blueprint points at, or a pointer to no book
  upgrade chain         an UpgradesTo= naming a design no <building> declares, or a ring of them
  zoning district       a Districts= token naming ground the founder can never declare
  city style            a Styles= token naming a style no <style> declares -- and, worse, a
                        MIS-SPELLED refusal (Styles="all,!eatr"), which refuses nobody and sends
                        the design everywhere while looking like a restriction
  creed gate            a CreedShare= with no Creed= to take a share of, or a Builders= token
                        naming a kind nothing on the roll can ever supply
  merged design         a footprint, chain link or key that only contradicts itself once the
                        catalogue's <building> elements are folded together by Key
  raising ceremony      a completion path that finishes a building without calling the ceremony
                        by name, so the building rises with no crew, no shared water and no
                        record of who was there -- the one C# seam of exactly this shape, and
                        the one this file's own audit found unjoined for 53 of 57 designs
  research teacher      a TaughtBy/SeededBy/Requires token naming a disk nobody can carry or a
                        machine nobody can haul home, so the node has a source that can never
                        fire and reads, to its author, as a road into the tree
  research grant        two <node> elements minting the same `node:` key, so one of them is a
                        completion nothing notices, because the gate it was written for was
                        already open
  knowledge gate        a Knowledge="node:..." on a <building> naming a key no <node> grants,
                        which is a design gated on research that does not exist -- refused for
                        ever, and refused for a reason the founder is never given

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

TAF_XML = [
    "Books.xml",
    "EmbarkModules.xml",
    "KingdomBuildings.xml",
    "KingdomDeals.xml",
    "KingdomProcedures.xml",
    "KingdomResearch.xml",
    "KingdomYardWorks.xml",
    "ObjectBlueprints.xml",
    "Options.xml",
    "PopulationTables.xml",
]
DEFAULT_BASE = (
    "/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
)


def read(path):
    with io.open(path, encoding="utf-8") as handle:
        return handle.read()


def xml_syntax_problems():
    """Every root the game will stream must be well formed, including data files that do not
    otherwise participate in a cross-reference check below."""
    problems = []
    for path in TAF_XML:
        if not os.path.isfile(path):
            problems.append("required runtime XML is missing: %s" % path)
            continue
        try:
            ET.parse(path)
        except ET.ParseError as error:
            problems.append("%s is not well-formed XML: %s" % (path, error))
    return problems


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
        for match in re.finditer(
            r'<object\s+Name="([^"]+)"', read(os.path.join(folder, name))
        ):
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
        if any(
            part in folder
            for part in (os.sep + ".git", os.sep + "DevTests", os.sep + "obj")
        ):
            continue
        for name in files:
            if name.endswith(".cs") and needle in read(os.path.join(folder, name)):
                return True
    return False


def contents_tables():
    """Population tables the building catalogue furnishes finished plots from. Named in XML and
    rolled through the plot registry, so no .cs file ever holds the name as a literal."""
    if not os.path.isfile("KingdomBuildings.xml"):
        return set()
    root = ET.parse("KingdomBuildings.xml").getroot()
    return {b.get("Contents") for b in root.iter("building") if b.get("Contents")}


def known_districts():
    """The district keys a founder can actually declare, read off the rules rather than copied,
    so this check cannot drift from the menu it is checking against."""
    source = read(os.path.join("Core", "KingdomRules.cs"))
    match = re.search(r"Districts = new string\[\d+\]\s*\{([^}]*)\}", source)
    if not match:
        return set()
    return set(re.findall(r'"([^"]+)"', match.group(1)))


def merged_buildings():
    """Every <building> in our catalogue, folded the way the loader folds it.

    A later element with the same Key MERGES into the earlier one: attributes it names override,
    attributes it omits survive, and <skin> children append with a repeated skin key replacing.
    Reading each element on its own is exactly how a merge-created contradiction hides -- neither
    file is wrong by itself, and only the merged design is.
    """
    order = []
    merged = {}
    if not os.path.isfile("KingdomBuildings.xml"):
        return order, merged
    for building in ET.parse("KingdomBuildings.xml").getroot().iter("building"):
        key = building.get("Key")
        if not key:
            continue
        if key not in merged:
            merged[key] = {"attrs": {}, "skins": {}, "declarations": 0}
            order.append(key)
        entry = merged[key]
        entry["attrs"].update(building.attrib)
        entry["declarations"] += 1
        for skin in building.iter("skin"):
            skin_key = skin.get("Key")
            if skin_key:
                entry["skins"][skin_key] = dict(skin.attrib)
    return order, merged


# The one chain in the catalogue whose plot tier is MEANT to climb from link to link, mirrored
# from KingdomPlotRules.HeartRungKeys. The heart is a single plot that grows with its rung: its
# whole extent is surveyed at the founding rite and each rung takes the next ring of that surveyed
# ground, so basin -> waterstone -> moot yard -> great court is S -> M -> L -> XL by design.
# Everything else in the file climbs inside the envelope the founder staked, and the check below
# still holds it to that. Kept here by name rather than by an authored attribute because the
# catalogue loader hands the plot registry a fixed set of attributes; if a `Heart="yes"` attribute
# ever lands, this list reads it instead.
HEART_RUNG_KEYS = ("heartbasin", "heartwaterstone", "heartmoot", "heartcourt")

# The tokens KingdomPlotRules.TryParseSize accepts, mapped to the tier they name.
PLOT_TIERS = {
    "s": "Small",
    "small": "Small",
    "m": "Medium",
    "medium": "Medium",
    "l": "Large",
    "large": "Large",
    "xl": "Huge",
    "huge": "Huge",
}


def plot_dimensions():
    """Tier dimensions read off KingdomPlotRules rather than copied, so this check cannot drift
    from the geometry it is checking a footprint against."""
    source = read(os.path.join("Growth", "KingdomPlotRules.cs"))
    dims = {}
    for name in ("Small", "Medium", "Large", "Huge"):
        width = re.search(r"public const int %sWidth = (\d+)" % name, source)
        height = re.search(r"public const int %sHeight = (\d+)" % name, source)
        if width and height:
            dims[name] = (int(width.group(1)), int(height.group(1)))
    return dims


def footprint_of(raw):
    """A declared footprint as (width, height), or None for anything this script does not
    recognise. The authoritative parser is the mod's own; guessing here would invent failures
    rather than find them."""
    if not raw:
        return None
    match = re.match(r"^\s*(\d+)\s*[xX,*]\s*(\d+)\s*$", raw)
    if not match:
        return None
    return int(match.group(1)), int(match.group(2))


def building_reference_problems():
    """UpgradesTo resolution, chain cycles, chain plot agreement, footprint against plot,
    mis-spelled merge keys, Districts tokens, skin tiles and Contents tables -- every one of them
    against the MERGED catalogue, because merge-by-key means the design the game builds is not the
    element any one author wrote."""
    order, merged = merged_buildings()
    if not merged:
        return []
    problems = []
    keys = set(order)
    chain = {}
    for key in order:
        successor = merged[key]["attrs"].get("UpgradesTo")
        if successor:
            chain[key] = successor

    def layered(key):
        count = merged.get(key, {}).get("declarations", 1)
        return "" if count < 2 else " (%s is the merge of %d declarations)" % (key, count)

    def plot_of(key):
        raw = (merged[key]["attrs"].get("Plot") or "").strip().lower()
        return PLOT_TIERS.get(raw, raw)

    for key, successor in sorted(chain.items()):
        if successor not in keys:
            problems.append(
                "building %s upgrades into %s, which no <building> in this file declares%s"
                % (key, successor, layered(key))
            )

    # A ring improves each work into the next forever, spending the settlement's whole surplus
    # on going in a circle. TryParseUpgradeAttributes catches the one-step case; only a pass over
    # the whole catalogue can see a longer one, and only a pass over the MERGED catalogue can see
    # one whose last link was added by a later file.
    for start in sorted(chain):
        seen = [start]
        at = chain[start]
        while at in chain and at not in seen:
            seen.append(at)
            at = chain[at]
        if at in seen:
            problems.append("upgrade chain loops: %s -> %s" % (" -> ".join(seen), at))
            break

    # Upgrades climb within a plot; sizes compete across plots. One file may name the chain and a
    # later file re-tier a single link, and neither element is wrong where it stands.
    for key, successor in sorted(chain.items()):
        if successor not in keys or plot_of(key) == plot_of(successor):
            continue
        if key in HEART_RUNG_KEYS and successor in HEART_RUNG_KEYS:
            continue
        problems.append(
            "building %s stands on plot %s and improves into %s, which wants plot %s; an "
            "improvement climbs within its own plot%s%s"
            % (
                key,
                plot_of(key) or "none",
                successor,
                plot_of(successor) or "none",
                layered(key),
                layered(successor),
            )
        )

    # The footprint belongs to the building's tier and the plot is only the envelope it fits
    # inside. A merge that overrides one and not the other is the case no single element shows.
    dims = plot_dimensions()
    for key in order:
        footprint = footprint_of(merged[key]["attrs"].get("Footprint"))
        if not footprint:
            continue
        tier = plot_of(key)
        if tier not in dims:
            problems.append(
                "building %s declares a footprint of %dx%d and no plot to stand it in%s"
                % (key, footprint[0], footprint[1], layered(key))
            )
            continue
        if footprint[0] > dims[tier][0] or footprint[1] > dims[tier][1]:
            problems.append(
                "building %s covers %dx%d and stands on a %s plot, which is %dx%d; a tier's "
                "footprint fits inside its plot or it is never raised%s"
                % (
                    key,
                    footprint[0],
                    footprint[1],
                    tier.lower(),
                    dims[tier][0],
                    dims[tier][1],
                    layered(key),
                )
            )

    # A merge fragment names a key an earlier file declared. One that names a key nothing else
    # declares is a mis-spelling: the loader creates a half-entry, refuses it for want of a
    # Blueprint, and the design the author meant to change is left exactly as it was.
    for key in order:
        if merged[key]["declarations"] > 1:
            continue
        missing = [
            name
            for name in ("DisplayName", "Blueprint", "Cost", "Ticks")
            if not merged[key]["attrs"].get(name)
        ]
        if missing:
            problems.append(
                "building %s is declared once and is missing %s; a merge fragment whose key no "
                "other <building> declares is a mis-spelled key and changes nothing"
                % (key, ", ".join(missing))
            )

    districts = known_districts() | {"none", "all"}
    if districts:
        for key in order:
            declared = merged[key]["attrs"].get("Districts")
            if not declared:
                continue
            for token in declared.split(","):
                token = token.strip().lower()
                if token and token not in districts:
                    problems.append(
                        "building %s wants the district %s, which is not one a founder can "
                        "declare, so nothing can ever be raised on ground that carries it"
                        % (key, token)
                    )

    # A skin only ever names art that already exists. One of ours must exist on disk; a vanilla
    # path cannot be checked here because vanilla tiles live inside the packed Unity assets.
    for key in order:
        for skin_key in sorted(merged[key]["skins"]):
            tile = merged[key]["skins"][skin_key].get("Tile")
            if not tile or not tile.startswith("ThousandAndFirst/"):
                continue
            if not os.path.isfile(os.path.join("Textures", tile)):
                problems.append(
                    "building %s skin %s names the tile %s, which is not in Textures/"
                    % (key, skin_key, tile)
                )

    # A style is a TAG (Addendum 16) and the tag set is OPEN, so a token naming a style nothing
    # declares is not automatically wrong -- a third party may ship the <style> in another file.
    # Inside THIS repository it always is wrong, and the negated form is the one worth catching:
    # `Styles="all,!eatr"` refuses nobody, so the design goes everywhere while reading, to its own
    # author, as a restriction. That is a silent failure of exactly the shape this file exists for.
    declared_styles = set(
        name.strip().lower()
        for name in re.findall(r'<style\s+Name="([^"]+)"', read("KingdomBuildings.xml"))
    )
    if declared_styles:
        for key in order:
            for token in (merged[key]["attrs"].get("Styles") or "").split(","):
                token = token.strip().lower()
                negated = token.startswith("!")
                token = token.lstrip("!").strip()
                if not token or token == "all" or token in declared_styles:
                    continue
                problems.append(
                    "building %s %s the style %s, which no <style> in this file declares, so the "
                    "%s does nothing at all"
                    % (
                        key,
                        "refuses" if negated else "is offered to",
                        token,
                        "refusal" if negated else "offer",
                    )
                )

    # The creed gate, as far as the XML can answer for itself. A share with no creed to take a
    # share of is dropped at load and named in the log; naming it here is cheaper than a log line
    # nobody reads. A Builders kind nothing supplies is a gate that will never open.
    builder_kinds = {"origin", "creed", "kept"}
    for key in order:
        attrs = merged[key]["attrs"]
        if attrs.get("CreedShare") and not attrs.get("Creed"):
            problems.append(
                "building %s declares CreedShare and no Creed, so there is no creed for the share "
                "to be a share of and the attribute is dropped at load" % key
            )
        for token in (attrs.get("Builders") or "").split(","):
            token = token.strip().lower()
            if not token or token == "all" or ":" not in token:
                continue
            kind = token.split(":", 1)[0]
            if kind not in builder_kinds:
                problems.append(
                    "building %s wants builders of the kind %s, which nothing puts on a city's "
                    "roll, so the gate can never open" % (key, kind)
                )

    declared_pops = set()
    if os.path.isfile("PopulationTables.xml"):
        declared_pops = set(
            re.findall(r'<population\s+Name="([^"]+)"', read("PopulationTables.xml"))
        )
    for key in order:
        table = merged[key]["attrs"].get("Contents")
        if table and table not in declared_pops:
            problems.append(
                "building %s furnishes from %s, which no <population> declares, so the plot is "
                "finished empty and nothing says why" % (key, table)
            )
    return problems


# --------------------------------------------------------------------------------------
# The raising ceremony: a C# reference of the same shape as the XML ones above.
# --------------------------------------------------------------------------------------

# Every file that stamps a finished building. Two of them RAISE one and must therefore close
# through the ceremony; the third only adopts what was already standing when the rite was
# poured, which is not a raising and has nobody to gather. A file appearing here that this
# list does not name is a new completion path, and the question it has to answer is the same.
RAISING_PATHS = (
    os.path.join("Growth", "KingdomScaffold.cs"),
    os.path.join("Growth", "KingdomPlot2.cs"),
)
ADOPTING_PATHS = (os.path.join("Core", "KingdomFounding.cs"),)

# Both files that realise a staked plan must carry the surveyor's words onto whatever finishes
# the building, or the chronicle quotes a plan for a wall and never for a house.
PLAN_PATHS = (
    os.path.join("Growth", "KingdomPlanMarker.cs"),
    os.path.join("Growth", "KingdomPlot2.cs"),
)


def raising_ceremony_problems():
    """The completion paths, walked in both directions.

    Silent in play in the way STANDARDS section 4 describes: a building raised without its
    ceremony still rises, so nothing looks broken. It simply rises with nobody there.
    """
    problems = []
    stampers = set()
    for folder, _dirs, files in os.walk("."):
        if any(part in folder for part in (".git", "DevTests", "Art")):
            continue
        for name in sorted(files):
            if not name.endswith(".cs"):
                continue
            path = os.path.join(folder, name)
            if 'SetIntProperty("KingdomBuilt", 1)' in read(path):
                stampers.add(os.path.normpath(path[2:] if path.startswith("./") else path))

    for path in sorted(stampers - set(RAISING_PATHS) - set(ADOPTING_PATHS)):
        problems.append(
            "%s finishes a building but is neither a known raising path nor a known adoption "
            "path; if it raises one it must call KingdomCeremony.OnBuildingRaised" % path
        )
    for path in RAISING_PATHS:
        if not os.path.isfile(path):
            problems.append("raising path %s is gone; this check no longer walks anything" % path)
            continue
        text = read(path)
        if "KingdomCeremony.OnBuildingRaised(" not in text:
            problems.append(
                "%s finishes a building without calling KingdomCeremony.OnBuildingRaised, so it "
                "raises with no crew gathered, no water shared and no record of who was there"
                % path
            )
        if re.search(r'KingdomChronicle\.Record\([^;]*was raised at', text):
            problems.append(
                "%s writes its own raising line instead of letting the ceremony write it; there "
                "is one grammar for a building rising" % path
            )
    for path in PLAN_PATHS:
        if not os.path.isfile(path):
            problems.append("plan path %s is gone; this check no longer walks anything" % path)
            continue
        text = read(path)
        if "PlanQuote(" not in text:
            problems.append(
                "%s realises a staked plan without carrying the surveyor's words onto what "
                "finishes it, so the chronicle can never quote that plan" % path
            )
    return problems


def crop_chain_problems(vanilla):
    """The seed chain, walked in both directions.

    The failure this exists for is the one STANDARDS section 4 describes exactly: every name in
    `KingdomCropRules`' seed and row maps is resolved at RUNTIME by `GameObject.Create`, which
    returns null for a name nothing defines and logs nothing anybody sees. A typo there produces a
    field that refuses to sow for a reason no message can name, and validating either the C# or the
    XML alone cannot see it.

    Four directions:

      seed named, seed missing   `SeedForCrop` names a blueprint ObjectBlueprints.xml lacks
      seed shipped, seed unnamed a blueprint carrying r_KingdomSeed that no crop maps to
      row named, row missing     `RowForCrop` names a blueprint that is not there
      crop named, crop unknown   a crop blueprint neither we nor the game defines

    Plus the two structural facts the sim depends on: every design carrying `food` that is meant to
    GROW declares `r_KingdomCropRows`, and every blueprint carrying that tag also carries the
    `r_KingdomPlot` part that reads it. A rows tag on an object with no field part is a number
    nothing will ever look at.
    """
    problems = []
    rules = read(os.path.join("Growth", "KingdomCropRules.cs"))
    ours = taf_blueprints()

    def pairs(method):
        body = re.search(
            r"public static string " + method + r"\(string [A-Za-z]+\)\s*\{(.*?)\n\t\t\}",
            rules,
            re.S,
        )
        if not body:
            problems.append(
                "KingdomCropRules.%s is gone or reshaped; this check no longer walks anything"
                % method
            )
            return []
        return re.findall(r'case "([^"]+)":\s*\n\s*return "([^"]+)";', body.group(1))

    seed_map = pairs("SeedForCrop")
    row_map = pairs("RowForCrop")
    crop_map = pairs("CropForSeed")

    for crop, seed in seed_map:
        if seed not in ours:
            problems.append(
                "KingdomCropRules.SeedForCrop grows %s from %s, which ObjectBlueprints.xml does "
                "not define; sowing it would create nothing and say nothing" % (crop, seed)
            )
        if crop not in ours and vanilla is not None and crop not in vanilla:
            problems.append(
                "KingdomCropRules.SeedForCrop names crop %s, which neither we nor the game "
                "defines" % crop
            )
    for crop, row in row_map:
        if row not in ours:
            problems.append(
                "KingdomCropRules.RowForCrop stands %s as %s, which ObjectBlueprints.xml does not "
                "define; a sown field would lay no rows at all" % (crop, row)
            )
    # The two maps must agree, or a seed sows a crop that cannot be sown again from its own harvest.
    for seed, crop in crop_map:
        if (crop, seed) not in seed_map:
            problems.append(
                "KingdomCropRules.CropForSeed says %s grows %s, and SeedForCrop does not agree"
                % (seed, crop)
            )

    named_seeds = {seed for _crop, seed in seed_map}
    named_rows = {row for _crop, row in row_map}
    tree = ET.parse("ObjectBlueprints.xml")
    for obj in tree.getroot().iter("object"):
        name = obj.get("Name", "")
        parts = {part.get("Name") for part in obj.iter("part")}
        tags = {tag.get("Name"): tag.get("Value") for tag in obj.iter("tag")}
        if "r_KingdomSeed" in parts and name not in named_seeds:
            problems.append(
                "%s carries r_KingdomSeed but no crop in KingdomCropRules is sown from it, so it "
                "is a seed that grows nothing" % name
            )
        if "Harvestable" in parts and name.startswith("r_KingdomRow") and name not in named_rows:
            problems.append(
                "%s looks like a crop row but no crop in KingdomCropRules stands as it" % name
            )
        if "r_KingdomCropRows" in tags and "r_KingdomPlot" not in inherited_parts(tree, name):
            problems.append(
                "%s declares r_KingdomCropRows but carries no r_KingdomPlot part, so nothing "
                "will ever read the rows it promises" % name
            )

    # And the catalogue side: a design that grows must say how much it grows.
    for building in ET.parse("KingdomBuildings.xml").getroot().iter("building"):
        blueprint = building.get("Blueprint")
        if not blueprint:
            continue
        parts = inherited_parts(tree, blueprint)
        if "r_KingdomPlot" not in parts:
            continue
        if not inherited_tag(tree, blueprint, "r_KingdomCropRows"):
            problems.append(
                "%s is a growing design (%s carries r_KingdomPlot) and declares no "
                "r_KingdomCropRows, so it would sow no rows and carry food it never grows"
                % (building.get("Key", "<unkeyed>"), blueprint)
            )
    return problems


# --------------------------------------------------------------------------------------
# The tree: a node's sources, its grants, and the gates written against them.
# --------------------------------------------------------------------------------------

RESEARCH_XML = "KingdomResearch.xml"

# The three attributes a roster token may legally appear in on a <node>. Forbidden is deliberately
# not among them: a token that makes a node invisible does not have to name anything that exists,
# because refusing on a creed nobody in Qud holds is a refusal, not a dangling reference.
SOURCE_ATTRIBUTES = ("Requires", "TaughtBy", "SeededBy")


def research_nodes():
    """Every <node> in the tree, folded the way the registry folds it.

    A later element with the same Key REPLACES the earlier one whole -- KingdomResearch.cs:166-178
    assigns over the row rather than updating it, which is NOT how <building> merges. So the last
    declaration is the node, and checking the first would check a node the game never builds.
    """
    order, bykey = [], {}
    if not os.path.isfile(RESEARCH_XML):
        return order, bykey
    for node in ET.parse(RESEARCH_XML).getroot().iter("node"):
        key = (node.get("Key") or "").strip().lower()
        if not key:
            continue
        if key not in bykey:
            order.append(key)
        bykey[key] = dict(node.attrib)
    return order, bykey


def roster_tokens(raw):
    """One roster list, split the way the mod splits it.

    Commas separate requirements (KingdomZoningRules.Tokens) and a `|` inside one requirement
    separates arms that are alternatives (KingdomResearchRules.Validate:788, AnyRoadVisible:578).
    Every arm names something on its own, so every arm is checked on its own. Folded to lower
    case because KingdomZoningRules.Fold does, and the roster is matched folded.
    """
    tokens = []
    for token in (raw or "").split(","):
        for arm in token.split("|"):
            arm = arm.strip().lower()
            if arm:
                tokens.append(arm)
    return tokens


def split_key(token):
    """A roster key as (kind, name). An unqualified token has no kind, exactly as
    KingdomZoningRules.KindOf reports it."""
    kind, sep, name = token.partition(":")
    if not sep:
        return None, kind.strip()
    return kind.strip(), name.strip()


# Read with regexes and not with ElementTree, for the same reason vanilla_blueprints above does:
# the shipped blueprint corpus carries character references a conforming XML parser refuses
# (ObjectBlueprints/Creatures.xml:3291 among them), and a reference checker that cannot read the
# game's own files checks nothing. The patterns allow a '>' inside a quoted value, because
# descriptions contain them and a tag cut short there would drop the attributes after it.
_TAG_ATTRS = r'((?:[^>"]|"[^"]*")*)'
_OBJECT_OPEN = re.compile(r"<object\s+" + _TAG_ATTRS + r"(/?)>", re.S)
_PART_TAG = re.compile(r"<part\s+" + _TAG_ATTRS + r"/?>", re.S)
_ATTRIBUTE = re.compile(r'([\w:.-]+)\s*=\s*"([^"]*)"')


def _attributes(raw):
    return dict(_ATTRIBUTE.findall(raw))


def blueprint_corpus(base):
    """Every blueprint the game will hold, ours and vanilla's, with the parts each declares.

    Built once. The checks below walk Inherits, and re-reading three megabytes of vanilla per
    token would turn a reference check into a build step.
    """
    corpus = {}
    paths = ["ObjectBlueprints.xml"]
    folder = os.path.join(base, "ObjectBlueprints") if base else None
    if folder and os.path.isdir(folder):
        paths.extend(
            os.path.join(folder, name)
            for name in sorted(os.listdir(folder))
            if name.endswith(".xml")
        )
    for path in paths:
        if not os.path.isfile(path):
            continue
        text = read(path)
        opens = list(_OBJECT_OPEN.finditer(text))
        for index, match in enumerate(opens):
            attrs = _attributes(match.group(1))
            name = attrs.get("Name")
            if not name:
                continue
            entry = corpus.setdefault(name, {"inherits": None, "parts": {}})
            if attrs.get("Inherits"):
                entry["inherits"] = attrs["Inherits"]
            if match.group(2) == "/":
                continue
            # Objects do not nest, so one object's body runs to the next object's opening tag.
            stop = opens[index + 1].start() if index + 1 < len(opens) else len(text)
            for part in _PART_TAG.finditer(text, match.end(), stop):
                part_attrs = _attributes(part.group(1))
                part_name = part_attrs.get("Name")
                if part_name:
                    entry["parts"].setdefault(part_name, {}).update(part_attrs)
    return corpus


def resolved_part(corpus, name, part_name):
    """A part's parameters as the engine resolves them: an ancestor declares the part, and every
    descendant overrides the parameters it names. None when nothing in the chain declares it."""
    chain, seen, walk = [], set(), name
    while walk and walk in corpus and walk not in seen:
        seen.add(walk)
        chain.append(walk)
        walk = corpus[walk]["inherits"]
    params = {}
    for step in reversed(chain):
        params.update(corpus[step]["parts"].get(part_name, {}))
    return params or None


def buildable_blueprints(corpus):
    """The names a data disk can teach the keepers under.

    A Build recipe is a blueprint whose resolved TinkerItem says CanBuild="true" and names no
    SubstituteBlueprint (D/XRL/World/Parts/TinkerItem.cs:205-236), and the string it teaches under
    is the blueprint's own Name -- KingdomZoning.DiskName returns TinkerData.Blueprint for
    everything that is not a modification, and TinkerData.Blueprint is Blueprint.Name.
    """
    names = set()
    for name in corpus:
        part = resolved_part(corpus, name, "TinkerItem")
        if not part:
            continue
        if (part.get("CanBuild") or "").strip().lower() != "true":
            continue
        if (part.get("SubstituteBlueprint") or "").strip():
            continue
        names.add(name.strip().lower())
    return names


def moddable_display_names(base):
    """The other half of what a disk can teach: a modification's TinkerDisplayName, which is what
    KingdomZoning.DiskName returns for a Type="Mod" recipe. TinkerAllowed defaults to true
    (D/XRL/World/Tinkering/TinkerData.cs:209-223)."""
    if not base:
        return set()
    path = os.path.join(base, "Mods.xml")
    if not os.path.isfile(path):
        return set()
    names = set()
    for mod in ET.parse(path).getroot().iter("mod"):
        if (mod.get("TinkerAllowed") or "true").strip().lower() != "true":
            continue
        shown = mod.get("TinkerDisplayName")
        if shown:
            names.add(shown.strip().lower())
    return names


def research_reference_problems(base):
    """The tree walked in both directions.

    Silent in exactly the way STANDARDS section 4 describes. A node taught by a disk that does not
    exist still loads, still shows, and simply never fires -- the founder carries every disk in
    Qud home and the keepers never once say "we have seen this before". A building gated on a key
    no node grants is refused for ever, and the refusal names a thing that cannot be got.

    What is NOT checked here: a `node:` token in Requires or Reveals naming a node nothing grants.
    KingdomResearchRules.Validate already walks that at load and says it in the log, and a second
    copy of the same rule is a second thing to keep true.
    """
    order, bykey = research_nodes()
    if not bykey:
        return []
    problems = []

    # 1. Two nodes minting the same key. The second completion mints a key the first already
    #    minted, so it opens nothing and nothing anywhere reports a node that did no work.
    granted = {}
    for key in order:
        attrs = bykey[key]
        for token in roster_tokens(attrs.get("Grants") or ("node:" + key)):
            kind, name = split_key(token)
            if kind != "node" or not name:
                continue
            if name in granted and granted[name] != key:
                problems.append(
                    "node %s grants node:%s, which node %s already grants; the second completion "
                    "mints a key that is already minted and opens nothing"
                    % (key, name, granted[name])
                )
                continue
            granted[name] = key

    # 2. Every teacher resolves. Only with --base: a machine or a disk naming one of OUR blueprints
    #    resolves without vanilla, but the tree's teachers are vanilla's and refusing to answer is
    #    better than answering wrongly.
    if base:
        corpus = blueprint_corpus(base)
        blueprints = set(name.strip().lower() for name in corpus)
        buildable = buildable_blueprints(corpus)
        mods = moddable_display_names(base)
        for key in order:
            for attribute in SOURCE_ATTRIBUTES:
                for token in roster_tokens(bykey[key].get(attribute)):
                    kind, name = split_key(token)
                    # A wildcard names no one thing, so there is nothing to resolve it against.
                    if not name or "*" in name:
                        continue
                    if kind == "machine" and name not in blueprints:
                        problems.append(
                            "node %s %s machine:%s in %s, which is no blueprint the game declares, "
                            "so no machine anyone hauls home can ever certify it"
                            % (key, attribute, name, RESEARCH_XML)
                        )
                    elif kind == "disk" and name not in buildable and name not in mods:
                        problems.append(
                            "node %s %s disk:%s in %s, which is neither a blueprint carrying "
                            "TinkerItem CanBuild=true nor a modification's TinkerDisplayName, so "
                            "no data disk in Qud teaches under that name"
                            % (key, attribute, name, RESEARCH_XML)
                        )

    # 3. The other direction: a catalogue gate written against a node key nothing mints. Read off
    #    the MERGED catalogue, because a Knowledge attribute can be added by a later file.
    building_order, merged = merged_buildings()
    for key in building_order:
        for token in roster_tokens(merged[key]["attrs"].get("Knowledge")):
            kind, name = split_key(token)
            if kind != "node" or not name or "*" in name:
                continue
            if name not in granted:
                problems.append(
                    "building %s is gated on node:%s in KingdomBuildings.xml, which no <node> in "
                    "%s grants, so the design is refused for ever and the refusal names a thing "
                    "nobody can get" % (key, name, RESEARCH_XML)
                )
    return problems


def _blueprint_index(tree):
    index = {}
    for obj in tree.getroot().iter("object"):
        index[obj.get("Name", "")] = obj
    return index


def inherited_parts(tree, name):
    """Every part a blueprint carries, walking Inherits the way the engine's loader does."""
    index = _blueprint_index(tree)
    parts, seen, walk = set(), set(), name
    while walk and walk in index and walk not in seen:
        seen.add(walk)
        obj = index[walk]
        parts |= {part.get("Name") for part in obj.iter("part")}
        walk = obj.get("Inherits")
    return parts


def inherited_tag(tree, name, tag_name):
    """A tag's value off a blueprint or the nearest ancestor that declares it, or None."""
    index = _blueprint_index(tree)
    seen, walk = set(), name
    while walk and walk in index and walk not in seen:
        seen.add(walk)
        obj = index[walk]
        for tag in obj.iter("tag"):
            if tag.get("Name") == tag_name:
                return tag.get("Value")
        walk = obj.get("Inherits")
    return None


def embark_module_problems():
    """Loader-shape checks the generic XML parser cannot express after ElementTree folds it.

    QudGamemodeModule.HandleGameSystemNode records the Class attribute but, unlike the adjacent
    game-state handlers, does not call DoneWithElement.  A paired start/end form therefore leaves
    an EndElement for HandleNodes and emits a MODWARN.  The semantically identical self-closing
    form is the exact runtime contract.
    """
    path = "EmbarkModules.xml"
    if not os.path.isfile(path):
        return []
    source = read(path)
    problems = []
    if re.search(r"<gamesystem\b[^>]*>\s*</gamesystem\s*>", source, re.I):
        problems.append(
            "EmbarkModules gamesystem uses a paired end tag; Qud's handler does not consume it "
            "and warns Unexpected EndElement, so gamesystem must be self-closing"
        )
    return problems


def main():
    base = None
    if "--base" in sys.argv:
        base = sys.argv[sys.argv.index("--base") + 1]
    elif os.path.isdir(DEFAULT_BASE):
        base = DEFAULT_BASE

    if not os.path.isfile("ObjectBlueprints.xml"):
        sys.exit("run from the repository root")

    problems = xml_syntax_problems()
    if problems:
        print("XML REFERENCES FAILED")
        for problem in problems:
            print("  " + problem)
        return 1
    ours = taf_blueprints()
    theirs = vanilla_blueprints(base) if base else set()

    # 1. Every referenced blueprint resolves.
    for name, sources in sorted(taf_referenced_blueprints().items()):
        if name in ours:
            continue
        if base and name in theirs:
            continue
        if base:
            problems.append(
                "unresolved blueprint %s, referenced by %s"
                % (name, ", ".join(sorted(sources)))
            )

    # 2. Every population we merge into exists or is genuinely fabricable.
    if base:
        known = vanilla_populations(base)
        ours_pop = set()
        root = ET.parse("PopulationTables.xml").getroot()
        for pop in root.iter("population"):
            ours_pop.add(pop.get("Name"))
        for pop in root.iter("population"):
            name = pop.get("Name")
            load = pop.get("Load") or "Merge"
            if load != "Merge" or name in known:
                continue
            if name.startswith("DynamicObjectsTable:") or name.startswith(
                "StaticObjectsTable:"
            ):
                # Fabricated on demand from a matching blueprint tag. Declaring the name in XML
                # pre-empts that fabrication, so a merge is only meaningful if the tag exists.
                if not vanilla_tag_exists(base, name):
                    problems.append(
                        "dead population merge %s: not declared in vanilla, no blueprint carries "
                        "the tag it would be fabricated from, so nothing rolls it and our entry "
                        "pre-empts a table that would never have content" % name
                    )
            elif not rolled_by_our_code(name) and name not in contents_tables():
                # A table we define and roll ourselves is a new table, which is fine and is how
                # another mod is meant to add settlers. A table we define and never roll is the
                # defect: Merge into an absent name silently creates one nothing reads.
                problems.append(
                    "population merge %s targets a table that does not exist in vanilla and that "
                    "no TAF code rolls; Merge into an absent name creates a table nothing reads"
                    % name
                )

    # 3. KingdomBuildings cross-references: a design that grows into a name nothing declares,
    #    a chain that loops back on itself, and ground no founder can ever name. All three are
    #    silent-ish in play -- a refusal the player cannot act on, or an improvement that runs
    #    forever -- and none is visible by validating either end alone.
    problems.extend(building_reference_problems())

    # 4. Every completion path closes through the raising ceremony, and every plan-realising
    #    path carries the surveyor's words to it.
    problems.extend(raising_ceremony_problems())

    # 5. The seed chain resolves in both directions, and a design that grows says how much.
    problems.extend(crop_chain_problems(theirs if base else None))

    # 6. Book IDs referenced by blueprints exist, and books are reachable.
    if os.path.isfile("Books.xml"):
        book_ids = set(re.findall(r'<book\s+ID="([^"]+)"', read("Books.xml")))
        book_ids |= set(re.findall(r'ID="([^"]+)"', read("Books.xml")))
        pointed = set(
            re.findall(
                r'part\s+Name="Book"\s+ID="([^"]+)"', read("ObjectBlueprints.xml")
            )
        )
        for pointer in sorted(pointed - book_ids):
            problems.append(
                "blueprint points at book ID %s, which Books.xml does not define"
                % pointer
            )
        for orphan in sorted(book_ids - pointed):
            problems.append("book %s is defined but no blueprint points at it" % orphan)

    # 7. The research tree: teachers that resolve, grants that are minted once, and catalogue
    #    gates written against keys the tree actually mints. Both directions, as above.
    problems.extend(research_reference_problems(base))

    # 8. Embark module nodes whose textual XML form changes Qud's streaming-loader behaviour.
    problems.extend(embark_module_problems())

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
