"""Execute removal inventories against disposable trees and the tracked read-only check."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "Tools" / "generate-removal-coverage.py"
SPEC = importlib.util.spec_from_file_location("removal_coverage_generator", SCRIPT)
GENERATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GENERATOR)


class RemovalCoverageFixtureTest(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="taf-removal-coverage-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        authority = patch.object(GENERATOR, "ROOT", self.root)
        authority.start()
        self.addCleanup(authority.stop)

    def write(self, relative: str, text: str):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def test_production_csharp_and_xml_properties_and_blueprints_are_included(self):
        self.write("Core/Fixture.cs", '''
const string OwnerProperty = "KingdomProductionDeclared";
body.SetStringProperty("r_TAF_ProductionWritten", "owned");
body.SetIntProperty("ForeignProperty", 1);
const string MainBlueprint = "r_KingdomProductionSource";
''')
        self.write("RuntimeData/Fixture.xml", '''
<objects><object Name="r_FounderProductionXml">
<property Name="TAFProductionXml" Value="yes" />
<intproperty Name="KingdomProductionXmlInt" Value="1" />
<property Name="ForeignXmlProperty" Value="no" />
</object></objects>
''')
        self.assertEqual(sorted(GENERATOR.MANUAL_OBJECT_PROPERTIES | {
            "KingdomProductionDeclared", "r_TAF_ProductionWritten",
            "TAFProductionXml", "KingdomProductionXmlInt",
        }), GENERATOR.collect())
        self.assertEqual(sorted(GENERATOR.MANUAL_BLUEPRINTS | {
            "r_KingdomProductionSource", "r_FounderProductionXml",
        }), GENERATOR.collect_blueprints())

    def test_harness_csharp_and_xml_never_enter_production_inventories(self):
        self.write("Core/Fixture.cs", 'const string OwnerKey = "KingdomProduction";')
        self.write("Harness/Fixture.cs", '''
const string RegistryKey = "r_TAF_ChronicleEventRegistry_v1";
const string FaultKey = "r_TAF_ChronicleEventRegistryFault_v3";
body.SetStringProperty("KingdomHarnessWritten", "dev");
const string TestBlueprint = "r_KingdomHarnessSource";
''')
        self.write("Harness/Fixture.xml", '''
<objects><object Name="r_FounderHarnessXml">
<property Name="KingdomHarnessXml" Value="dev" />
<intproperty Name="TAFHarnessXmlInt" Value="1" />
</object></objects>
''')
        self.assertEqual(sorted(GENERATOR.MANUAL_OBJECT_PROPERTIES | {
            "KingdomProduction",
        }), GENERATOR.collect())
        self.assertEqual(sorted(GENERATOR.MANUAL_BLUEPRINTS), GENERATOR.collect_blueprints())
        self.assertEqual(["Core/Fixture.cs"], [
            path.relative_to(self.root).as_posix()
            for path in GENERATOR.production_files(".cs")
        ])
        self.assertEqual([], list(GENERATOR.production_files(".xml")))

    def test_production_custom_part_census_follows_indirect_and_split_inheritance(self):
        self.write("Core/Parts.cs", '''
class ProductionBase : XRL.World.IPart {}
class r_KingdomDirect : ProductionBase {}
class r_FounderIndirect : r_KingdomDirect {}
partial class r_KingdomSplit : IDisposable {}
class KingdomCharterPart : TeleporterPair {}
''')
        self.write("Growth/Split.cs", 'partial class r_KingdomSplit : ProductionBase {}')
        self.assertEqual([
            "KingdomCharterPart", "r_FounderIndirect", "r_KingdomDirect", "r_KingdomSplit",
        ], GENERATOR.collect_custom_parts())

    def test_harness_classes_cannot_enter_or_supply_production_inheritance(self):
        self.write("Core/Parts.cs", '''
class r_KingdomProduction : IPart {}
class r_KingdomHarnessBridge : HarnessOnlyBase {}
partial class r_FounderMixed : IDisposable {}
''')
        self.write("Harness/Parts.cs", '''
class HarnessOnlyBase : IPart {}
class r_KingdomHarnessPart : IPart {}
class r_FounderHarnessIndirect : r_KingdomProduction {}
partial class r_FounderMixed : IPart {}
''')
        self.assertEqual(["r_KingdomProduction"], GENERATOR.collect_custom_parts())


class TrackedRemovalCoverageTest(unittest.TestCase):
    def test_tracked_render_and_real_check_are_byte_current_without_writes(self):
        output = ROOT / "Core" / "KingdomRemovalCoverage.Generated.cs"
        before = output.read_bytes()
        rendered = GENERATOR.render(GENERATOR.collect(), GENERATOR.collect_blueprints(),
                                    GENERATOR.collect_custom_parts()).encode("utf-8")
        self.assertEqual(before, rendered)
        result = subprocess.run([sys.executable, str(SCRIPT), "--check"], cwd=ROOT,
                                text=True, capture_output=True, check=False)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(before, output.read_bytes())


if __name__ == "__main__":
    unittest.main()
