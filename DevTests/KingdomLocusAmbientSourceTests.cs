#if TAF_TESTS
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomLocusAmbientSourceTests
	{
		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		[Test]
		public void GatheringBenchIsOneScaledHandAcrossEverySkinAndArchitectureTier()
		{
			XDocument buildings = XDocument.Parse(Read("RuntimeData/KingdomBuildings.xml"));
			XElement[] rows = buildings.Root.Elements("building")
				.Where(row => (string)row.Attribute("Key") == "bench").ToArray();
			Assert.AreEqual(1, rows.Length);
			XElement bench = rows[0];
			Assert.AreEqual("r_KingdomBench", (string)bench.Attribute("Blueprint"));
			Assert.AreEqual("1", (string)bench.Attribute("Staff"));
			Assert.AreEqual("scaled", (string)bench.Attribute("Manning"));
			Assert.AreEqual("timber:4", (string)bench.Attribute("Materials"));
			XElement[] skins = bench.Elements("skin").ToArray();
			Assert.AreEqual(4, skins.Length);
			Assert.That(skins.All(skin => skin.Attribute("Staff") == null
				&& skin.Attribute("Manning") == null), Is.True);

			int tiers = 0;
			string architecture = Path.Combine(TestMain.RepositoryRoot, "Architecture");
			foreach (string path in Directory.EnumerateFiles(architecture, "*.xml"))
			{
				XDocument doc = XDocument.Load(path);
				foreach (XElement tier in doc.Descendants("tier")
					.Where(row => (string)row.Attribute("BuildKey") == "bench"))
				{
					tiers++;
					Assert.IsNull(tier.Attribute("Staff"), path);
					Assert.IsNull(tier.Attribute("Manning"), path);
				}
			}
			Assert.Greater(tiers, 0, "no architecture tier consumes the staffed bench entry");
		}

		[Test]
		public void CompletionSurveyAndKeeperShareTheExactStaffAndPostContract()
		{
			string completion = Read("Growth/KingdomPlot2.27.FinalBuilding.cs")
				+ Read("Growth/KingdomScaffold.SuccessorProof.cs");
			StringAssert.Contains("if (Staff > 0)", completion);
			StringAssert.Contains("SetIntProperty(\"KingdomStaffNeeded\", Staff)", completion);
			string capture = Read("Growth/KingdomSurvey.01.Capture.cs");
			StringAssert.Contains("Item.GetIntProperty(\"KingdomStaffNeeded\") > 0", capture);

			string keeper = Read("Experience/KingdomLocus.z00.Keeper.cs");
			StringAssert.Contains("item.Blueprint == BenchBlueprint", keeper);
			StringAssert.Contains("KingdomUpgrade.IsFunctionallyBuilt(item)", keeper);
			StringAssert.DoesNotContain("item.GetIntProperty(\"KingdomBuilt\")", keeper);
			StringAssert.Contains("bench.GetIntProperty(\"KingdomStaffNeeded\") == 0", keeper);
			StringAssert.Contains("bench.SetIntProperty(\"KingdomStaffNeeded\", 1)", keeper);
			StringAssert.Contains("Survey.ObserveChanged(bench)", keeper);
			StringAssert.Contains("bench.GetIntProperty(\"KingdomStaffed\") == 1", keeper);
			StringAssert.Contains("KingdomStations.PostOf(settler) == WorkId", keeper);
			StringAssert.Contains("KingdomPhysicalHappenings.IsStaged(settler)", keeper);
			StringAssert.DoesNotContain("candidateIDs.Add(Survey.Settlers[i].ID)", keeper);
		}

		[Test]
		public void CityBookSelectsOneGroundAndEveryHookReprovesThatAuthority()
		{
			string keeper = Read("Experience/KingdomLocus.z00.Keeper.cs");
			string runtime = Read("Experience/KingdomLocus.z00b.Ambient.cs");
			StringAssert.Contains("System?.City?.WorkIds", keeper);
			StringAssert.Contains("System?.City?.WorkDesignKeys", keeper);
			StringAssert.Contains("FindBench(benches, locusWorkId, out bool ambiguous)", keeper);
			StringAssert.Contains("Part.WorkId != locusWorkId", runtime);
			StringAssert.Contains("GameObject.FindByID(Part.KeeperObjectId)", runtime);
		}

		[Test]
		public void KeeperTruthPrecedesAndDoesNotDependOnTimedPlainGuestRecovery()
		{
			string source = KingdomLocusLogicalSource.Read();
			int activation = source.IndexOf("public static void OnZoneActivated(",
				StringComparison.Ordinal);
			int keeper = source.IndexOf("RunKeeperPass(System, Z, Survey, timeTicks)",
				activation, StringComparison.Ordinal);
			int option = source.IndexOf("KingdomGuestLifecycle.ObserveOption", activation,
				StringComparison.Ordinal);
			int open = source.IndexOf("KingdomGuestLifecycle.Open", activation,
				StringComparison.Ordinal);
			Assert.Greater(keeper, activation);
			Assert.Greater(option, keeper);
			Assert.Greater(open, option);

			int update = source.IndexOf("UpdateKeeperConversation(System, keeper, TimeTicks)",
				StringComparison.Ordinal);
			int ambient = source.IndexOf("TryObserveConfiguredOptions(System", update,
				StringComparison.Ordinal);
			int accessibility = source.IndexOf("Options.DisableAllIdleTileAnimations", update,
				StringComparison.Ordinal);
			Assert.Greater(update, 0);
			Assert.Greater(ambient, update);
			Assert.Greater(accessibility, ambient,
				"idle-animation settings must not suppress the keeper's direct conversation");
		}

		[Test]
		public void IdleHookHasTwoCuesAndNoBodiesPromptsMovementOutputsOrZoneScan()
		{
			string keeper = Read("Experience/KingdomLocus.z00.Keeper.cs");
			string projection = Read("Experience/KingdomLocus.z00a.KeeperProjection.cs");
			string runtime = Read("Experience/KingdomLocus.z00b.Ambient.cs");
			string part = Read("Experience/r_KingdomLocusAmbient.cs");
			string rules = Read("Experience/KingdomLocusRules.Ambient.cs");
			string combined = keeper + projection + runtime + part + rules;
			StringAssert.Contains("AmbientUseCount = 2", rules);
			StringAssert.Contains("ShareNews = 1", rules);
			StringAssert.Contains("KeepCompany = 2", rules);
			Assert.AreEqual(1, Count(runtime, "ParticleText("));
			StringAssert.Contains("0f, -0.2f", runtime);
			StringAssert.Contains("GameObject.FindByID(Part.KeeperObjectId)", runtime);
			StringAssert.Contains("KingdomStations.PostOf(Actor) != 0", runtime);
			StringAssert.Contains("DistanceTo(Bench)", runtime);
			StringAssert.Contains("[NonSerialized]", part);
			StringAssert.Contains("if (retire) ParentObject?.RemovePart(this)", part);
			StringAssert.Contains("Options.GetOption(KingdomExperienceOptions.AmbientOptionId",
				runtime);
			StringAssert.Contains("Options.DisableAllIdleTileAnimations", runtime);
			StringAssert.Contains("Options.DisableTextAnimationEffects", runtime);

			string[] forbidden =
			{
				"GetObjects(", "GetObjectsWithPart", "Stat.Random",
				"PushGoal", "MoveTo", "UseEnergy", "GameObject.Create", "AddObject",
				"TryReserveAudience", "TryReserveBodies", "Popup", "MessageQueue",
				"KingdomChronicle", "KingdomLog", "AddXP", "Reputation", "JournalAPI"
			};
			for (int i = 0; i < forbidden.Length; i++)
				StringAssert.DoesNotContain(forbidden[i], combined, forbidden[i]);
		}

		[Test]
		public void HookIsRemovedOnEveryInactiveTruthAndReloadCannotRetainAuthority()
		{
			string projection = Read("Experience/KingdomLocus.z00a.KeeperProjection.cs");
			string runtime = Read("Experience/KingdomLocus.z00b.Ambient.cs");
			string part = Read("Experience/r_KingdomLocusAmbient.cs");
			StringAssert.Contains("if (old != null && (!Enabled", projection);
			StringAssert.Contains("Benches[i].RemovePart(old)", projection);
			StringAssert.Contains("if (!Enabled", projection);
			StringAssert.Contains("RequirePart<r_KingdomLocusAmbient>()", projection);
			StringAssert.Contains("if (!sameAuthority)", projection);
			Assert.GreaterOrEqual(Count(part, "[NonSerialized]"), 10);
			StringAssert.Contains("!Part.AuthorityEnabled", runtime);
			StringAssert.Contains("!Enabled || Options.GetOption", runtime);
			StringAssert.Contains("KingdomExperienceRules.CanEmit", runtime);
			StringAssert.DoesNotContain("NextUse", runtime + part);
			StringAssert.DoesNotContain("DueTick", runtime + part);
			StringAssert.DoesNotContain("Backlog", runtime + part);
		}

		private static int Count(string Source, string Token)
		{
			int count = 0;
			int cursor = 0;
			while ((cursor = Source.IndexOf(Token, cursor, StringComparison.Ordinal)) >= 0)
			{
				count++;
				cursor += Token.Length;
			}
			return count;
		}
	}
}
#endif
