#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomMasterAndCivicWrapperSourceTests
	{
		private static XElement Blueprint(XDocument document, string name)
		{
			return document.Root.Elements("object").Single(row =>
				string.Equals((string)row.Attribute("Name"), name, StringComparison.Ordinal));
		}

		private static bool Child(XElement row, string element, string name)
		{
			return row.Elements(element).Any(child =>
				string.Equals((string)child.Attribute("Name"), name, StringComparison.Ordinal));
		}

		private static void AssertBefore(string source, string method, string gate, string work)
		{
			int start = source.IndexOf(method, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, method);
			int gateAt = source.IndexOf(gate, start, StringComparison.Ordinal);
			int workAt = source.IndexOf(work, start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(gateAt, 0, method + " gate");
			Assert.GreaterOrEqual(workAt, 0, method + " work");
			Assert.Less(gateAt, workAt, method + " must gate before allocations/delegates");
		}

		[Test]
		public void MasterOptionAndPersistedLatchArePresentAndDefaultEnabled()
		{
			XDocument options = XDocument.Parse(TestMain.ReadRepositoryText("Options.xml"));
			XElement master = options.Root.Elements("option").Single(row =>
				(string)row.Attribute("ID") == "r_TAF_OptionMaster");
			Assert.AreEqual("Checkbox", (string)master.Attribute("Type"));
			Assert.AreEqual("Yes", (string)master.Attribute("Default"));

			string system = KingdomSystemLogicalSource.Read();
			StringAssert.Contains("public KingdomMasterLatchValue MasterOption;", system);
			StringAssert.Contains("public long MasterOptionTick;", system);
			StringAssert.Contains("public long MasterResumeToken;", system);
			StringAssert.Contains("public long MasterAppliedResumeToken;", system);
			StringAssert.Contains("public bool InheritanceResumePending;", system);
			StringAssert.Contains("public int InheritancePendingLoadKindValue;", system);
			StringAssert.Contains("KingdomMasterRules.WellFormed(MasterOption, MasterOptionTick,", system);
		}

		[Test]
		public void EveryAutomaticKingdomSystemWakeGatesBeforeItsGuardDelegate()
		{
			string source = KingdomSystemLogicalSource.Read();
			AssertBefore(source, "public override bool HandleEvent(EndTurnEvent E)",
				"KingdomMaster.ObserveAutomaticWake", "Guard(\"pump\"");
			AssertBefore(source, "public override bool HandleEvent(ZoneThawedEvent E)",
				"KingdomMaster.ObserveAutomaticWake", "Guard(\"thaw\"");
			AssertBefore(source, "public override bool HandleEvent(SuspendingEvent E)",
				"KingdomMaster.ObserveAutomaticWake", "Guard(\"check-out\"");
			AssertBefore(source, "public override bool HandleEvent(ZoneActivatedEvent E)",
				"KingdomMaster.ObserveAutomaticWake", "Guard(\"seat\"");
			AssertBefore(source, "public override bool HandleEvent(AfterGameLoadedEvent E)",
				"KingdomMaster.ObserveAutomaticWake", "Guard(\"feeling re-assert\"");

			string master = TestMain.ReadRepositoryText("Core/KingdomMaster.cs") + "\n"
				+ TestMain.ReadRepositoryText("Core/KingdomMasterSettlementPlan.cs");
			StringAssert.Contains("if (decision.Transition == KingdomMasterTransition.None)", master);
			StringAssert.Contains("decision.AutomaticWorkAllowed && decision.ChangedAtTick != now", master);
			StringAssert.Contains("// Initialization and both real transitions consume this wake.", master);
			AssertBefore(master, "private static bool TryCreateCore",
				"KingdomHappeningCursorRules.TryRebaseAfterPause", "plan = new SettlementPlan");
			AssertBefore(master, "private static bool TryCreateCore",
				"KingdomBehaviourRules.TryRebaseAfterPause", "plan = new SettlementPlan");
			StringAssert.Contains("city.ExtensionHappeningCursors = ExtensionHappeningCursors;",
				master);
			StringAssert.Contains("city.ExtensionModel = ExtensionModel;", master);
		}

		[Test]
		public void IndependentAndPublicProducerPathsHaveMasterGates()
		{
			string trade = KingdomTradeLogicalSource.Read();
			StringAssert.Contains("if (!KingdomMaster.NewWorkAllowed(System))", trade);
			StringAssert.Contains("if (!KingdomMaster.AutomaticWorkAllowed(System)) return;", trade);
			string petitions = TestMain.ReadRepositoryText("Quests/KingdomPetitions.cs");
			StringAssert.Contains("if (!KingdomMaster.AutomaticWorkAllowed(System)) return;", petitions);
			StringAssert.Contains("if (!KingdomMaster.NewWorkAllowed(System)) return false;", petitions);
			string inquiry = TestMain.ReadRepositoryText("Growth/KingdomInquiry.cs");
			StringAssert.Contains("KingdomMaster.NewWorkAllowed(system)", inquiry);
			StringAssert.Contains("if (!KingdomMaster.AutomaticWorkAllowed(master))", inquiry);
			string research = KingdomResearchLogicalSource.Read();
			StringAssert.Contains("if (!KingdomMaster.AutomaticWorkAllowed(System)) return LabLastWorkedTick;", research);
			StringAssert.Contains("if (!KingdomMaster.NewWorkAllowed(System))", research);
			string succession = KingdomSuccessionLogicalSource.Read();
			Assert.GreaterOrEqual(Occurrences(succession,
				"KingdomMaster.AutomaticWorkAllowed(system)"), 3,
				"death interception plus load/save recovery must honor master-off");
			AssertBefore(succession, "public override bool HandleEvent(AfterDieEvent E)",
				"KingdomMaster.AutomaticWorkAllowed(system)",
				"DeathChroniclePublished = false");
			AssertBefore(succession, "private void HandleFounderDeath(AfterDieEvent E)",
				"KingdomMaster.AutomaticWorkAllowed(system)",
				"KingdomSuccessionRules.SuccessionEnabled");
			string loader = TestMain.ReadRepositoryText("Core/KingdomLoader.cs");
			AssertBefore(loader, "public static void RequireKingdomSystem()",
				"KingdomMaster.AutomaticWorkAllowed(kingdomSystem)", "seal.ReconcileProfile()");
			string seal = KingdomSealLogicalSource.Read();
			AssertBefore(seal, "public static bool TryFoundingCompleted",
				"KingdomMaster.NewWorkAllowed(kingdom)", "TryFlushLiving(\"founding\"");
			int foundingStart = seal.IndexOf("public static bool TryFoundingCompleted",
				StringComparison.Ordinal);
			int foundingEnd = seal.IndexOf("public static bool TryTerminalFromSuccession",
				foundingStart, StringComparison.Ordinal);
			StringAssert.DoesNotContain("KingdomMaster.AutomaticWorkAllowed",
				seal.Substring(foundingStart, foundingEnd - foundingStart),
				"explicit founding must accept the valid Unobserved latch");
			AssertBefore(seal, "public static bool TryStartSuccessorGeneration",
				"KingdomMaster.AutomaticWorkAllowed(kingdom)", "TryAdvanceGeneration");
			AssertBefore(seal, "internal void ReconcileProfile()",
				"KingdomMaster.AutomaticWorkAllowed(kingdom)", "TryReconcileProfile");
			string inheritance = TestMain.ReadRepositoryText(
				"World/KingdomInheritanceLifecycle.cs");
			AssertBefore(inheritance, "public override bool HandleEvent(AfterGameLoadedEvent E)",
				"KingdomInheritancePrimaryLoad.TryConsume", "TryResumePendingLoad(kingdom)");
			StringAssert.Contains("Registrar.Register(EndTurnEvent.ID);", inheritance);
			AssertBefore(inheritance, "private static void TryResumePendingLoad(KingdomSystem kingdom)",
				"KingdomMaster.AutomaticWorkAllowed(kingdom)", "ResumeAfterLoad(loadKind");
			AssertBefore(inheritance, "private static void TryResumePendingLoad(KingdomSystem kingdom)",
				"InheritanceResumePending = false", "ResumeAfterLoad(loadKind");

			string[] independentTicks =
			{
				"Growth/KingdomInquiry.cs",
				"Growth/KingdomLab.cs",
				"Growth/KingdomMirrorGate.cs",
				"Growth/r_KingdomPlot.cs",
				"Growth/KingdomPower.cs",
				"Growth/KingdomScaffold.cs"
			};
			foreach (string path in independentTicks)
			{
				string part = string.Equals(path, "Growth/KingdomLab.cs",
					StringComparison.Ordinal) ? KingdomLabLogicalSource.Read()
					: TestMain.ReadRepositoryText(path);
				StringAssert.Contains("public override void TurnTick(long TimeTick, int Amount)",
					part, path);
				StringAssert.Contains("KingdomMaster.AutomaticWorkAllowed(", part, path);
			}
		}

		[Test]
		public void SealCoordinatorKeepsExactSerializedFieldAndProtocolOrder()
		{
			string seal = KingdomSealLogicalSource.Read();
			StringAssert.Contains("[Serializable]", seal);
			StringAssert.Contains("public sealed partial class KingdomSeal : IPlayerSystem", seal);
			StringAssert.Contains("private const int SerializationMagic = 1413567315;", seal);
			StringAssert.Contains("private const int CurrentSerializationVersion = 1;", seal);

			string[] persisted =
			{
				"private int SerializationVersion = CurrentSerializationVersion;",
				"private string LineageId = \"\";",
				"private string LegacyId = \"\";",
				"private string OriginGameId = \"\";",
				"private int Generation;", "private int Revision;", "private long LastPollTick;",
				"private string SealedLegacyId = \"\";",
				"private string LastAccessionToken = \"\";",
				"private string PendingAccessionToken = \"\";", "private bool SealDisabled;"
			};
			int prior = seal.IndexOf("public sealed partial class KingdomSeal", StringComparison.Ordinal);
			for (int i = 0; i < persisted.Length; i++)
			{
				int at = seal.IndexOf(persisted[i], prior, StringComparison.Ordinal);
				Assert.Greater(at, prior, persisted[i]);
				prior = at;
			}
			int firstTransient = seal.IndexOf("[NonSerialized]", prior, StringComparison.Ordinal);
			Assert.Greater(firstTransient, prior);

			AssertBefore(seal, "public override void Write(SerializationWriter Writer)",
				"Writer.Write(SerializationMagic)", "Writer.WriteNamedFields(this, typeof(KingdomSeal)");
			AssertBefore(seal, "public override void Read(SerializationReader Reader)",
				"Reader.ReadNamedFields(this, typeof(KingdomSeal)", "ValidateSavedState()");
			AssertBefore(seal, "public static bool TryFoundingCompleted",
				"seal.MarkDirty(\"founding\")", "seal.TryFlushLiving(\"founding\"");
			AssertBefore(seal, "private bool TryAdvanceGeneration",
				"PendingAccessionToken = AccessionToken", "store.TryAdvanceGeneration(previous");
			AssertBefore(seal, "private bool TryCapture",
				"SameSpatialBasis(prior, Record)", "KingdomInheritanceSpatial.CopyEvidence");
		}

		[Test]
		public void PlotPartKeepsSerializablePositionalFieldAbi()
		{
			string part = TestMain.ReadRepositoryText("Growth/r_KingdomPlot.cs");
			StringAssert.Contains("[Serializable]", part);
			int type = part.IndexOf("public class r_KingdomPlot : IPart", StringComparison.Ordinal);
			int fields = part.IndexOf('{', type) + 1;
			int methods = part.IndexOf("public override bool WantTurnTick()", fields,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(type, 0);
			Assert.Greater(methods, fields);
			string layout = part.Substring(fields, methods - fields);
			Assert.AreEqual(4, Occurrences(layout, "\t\tpublic "));
			int stage = layout.IndexOf("public KingdomCropRules.PlotStage Stage;",
				StringComparison.Ordinal);
			int next = layout.IndexOf("public long NextStageTick;", StringComparison.Ordinal);
			int crop = layout.IndexOf("public string CropBlueprint;", StringComparison.Ordinal);
			int announced = layout.IndexOf("public bool NoLarderAnnounced;",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(stage, 0);
			Assert.GreaterOrEqual(next, 0);
			Assert.GreaterOrEqual(crop, 0);
			Assert.GreaterOrEqual(announced, 0);
			Assert.Less(stage, next);
			Assert.Less(next, crop);
			Assert.Less(crop, announced);
		}

		private static int Occurrences(string source, string value)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(value, at, StringComparison.Ordinal)) >= 0;
				at += value.Length) count++;
			return count;
		}

		[Test]
		public void CivicWrappersReassertOnlyOwnedFinalCapabilities()
		{
			XDocument document = XDocument.Parse(TestMain.ReadRepositoryText("ObjectBlueprints.xml"));
			XElement campfire = Blueprint(document, "r_KingdomCivicCampfire");
			Assert.AreEqual("Campfire", (string)campfire.Attribute("Inherits"));
			Assert.IsTrue(Child(campfire, "removepart", "Temporary2"));
			Assert.AreEqual("Campfire Remains", (string)campfire.Elements("part").Single(row =>
				(string)row.Attribute("Name") == "Campfire").Attribute("ExtinguishBlueprint"));

			XElement bookshelf = Blueprint(document, "r_KingdomCivicBookshelf");
			Assert.AreEqual("Bookshelf", (string)bookshelf.Attribute("Inherits"));
			Assert.IsTrue(Child(bookshelf, "removebuilder", "RandomTile"));
			Assert.IsTrue(Child(bookshelf, "removepart", "PackagePush2"));
			Assert.IsTrue(Child(bookshelf, "removepart", "PackageMirror"));
			Assert.AreEqual("*delete", (string)bookshelf.Elements("tag").Single(row =>
				(string)row.Attribute("Name") == "InventoryPopulationTable").Attribute("Value"));

			XElement torch = Blueprint(document, "r_KingdomCivicTorchpost");
			Assert.AreEqual("Torchpost", (string)torch.Attribute("Inherits"));
			Assert.IsTrue(Child(torch, "removebuilder", "RandomTile"));
			Assert.IsTrue(Child(torch, "removepart", "PackagePush2"));
			Assert.IsTrue(Child(torch, "removepart", "PackageMirror"));

			XElement hookah = Blueprint(document, "r_KingdomCivicHookah");
			Assert.AreEqual("Hookah", (string)hookah.Attribute("Inherits"));
			Assert.IsTrue(Child(hookah, "removepart", "TinkerItem"));
			Assert.IsTrue(Child(hookah, "removepart", "DiceRollGame"));
			XElement liquid = hookah.Elements("part").Single(row =>
				(string)row.Attribute("Name") == "LiquidVolume");
			Assert.AreEqual("0", (string)liquid.Attribute("Volume"));
			Assert.AreEqual("0", (string)liquid.Attribute("StartVolume"));
			Assert.AreEqual("", (string)liquid.Attribute("InitialLiquid"));

			XElement shelf = Blueprint(document, "r_KingdomFixtureShelfTimber");
			Assert.AreEqual("r_KingdomCivicBookshelf", (string)shelf.Attribute("Inherits"));
		}

		[Test]
		public void ProductionPlacementsNeverBypassCivicWrappers()
		{
			string root = TestMain.RepositoryRoot;
			List<string> files = Directory.EnumerateFiles(root, "*.xml",
				SearchOption.TopDirectoryOnly).ToList();
			files.AddRange(Directory.EnumerateFiles(Path.Combine(root, "Architecture"),
				"*.xml", SearchOption.AllDirectories));
			HashSet<string> raw = new HashSet<string>(StringComparer.Ordinal)
				{ "Campfire", "Bookshelf", "Torchpost", "Hookah" };
			List<string> bypasses = new List<string>();
			foreach (string file in files)
			{
				XDocument document = XDocument.Load(file);
				foreach (XAttribute attribute in document.Descendants().Attributes("Blueprint"))
					if (raw.Contains(attribute.Value))
						bypasses.Add(Path.GetRelativePath(root, file) + ":" + attribute.Value);
			}
			Assert.AreEqual(0, bypasses.Count, string.Join(", ", bypasses));
		}
	}
}
#endif
