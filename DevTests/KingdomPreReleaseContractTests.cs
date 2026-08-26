#if TAF_TESTS
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPreReleaseContractTests
	{
		private static void AssertRetired(MemberInfo member, string replacement)
		{
			Assert.IsNotNull(member);
			ObsoleteAttribute obsolete = member.GetCustomAttribute<ObsoleteAttribute>();
			Assert.IsNotNull(obsolete, member.Name + " lacks its retirement marker");
			Assert.IsTrue(obsolete.IsError, member.Name + " still admits new source callers");
			StringAssert.Contains(replacement, obsolete.Message);
		}

		[Test]
		public void SupersededPureSurfacesRemainOnlyAsCompileErrorBinaryAdapters()
		{
			AssertRetired(typeof(KingdomRules).GetField("MaxBuildings"),
				"MaxBuildingsForStage");
			AssertRetired(typeof(KingdomRules).GetMethod("TryAddSkin"),
				"TryMergeSkin");
			AssertRetired(typeof(KingdomQolRules).GetField("CohabitHostility"),
				"RefusalHostility");
			AssertRetired(typeof(KingdomQolRules).GetMethod("JudgeCohabitation"),
				"KingdomLodgingRules.Conflicts");
		}

		[Test]
		public void EngineWrapperCannotReopenTheRetiredFlatCohabitationPath()
		{
			string source = TestMain.ReadRepositoryText("Core/KingdomQolQuestions.cs");
			StringAssert.Contains(
				"[System.Obsolete(\"Retired before public release; use KingdomLodging", source);
			Assert.IsFalse(source.Contains("return KingdomQolRules.JudgeCohabitation("));
			Assert.IsFalse(source.Contains("return KingdomQolRules.IsMatch(JudgeCohabitation("));
		}

		[Test]
		public void WeightedSinglePickPopulationsActuallyDeclareQudsPickOneStyle()
		{
			XDocument document = XDocument.Parse(
				TestMain.ReadRepositoryText("PopulationTables.xml"));
			string[] names = new string[]
			{
				"r_KingdomSettlers", "r_KingdomGuests", "r_KingdomNotableGuests",
				"r_KingdomFurnishings_Dwelling", "r_KingdomFurnishings_Water",
				"r_KingdomFurnishings_Civic", "r_KingdomFurnishings_Comfort",
				"r_KingdomFurnishings_Learning"
			};
			for (int i = 0; i < names.Length; i++)
			{
				XElement found = null;
				foreach (XElement population in document.Root.Elements("population"))
					if ((string)population.Attribute("Name") == names[i])
					{
						found = population;
						break;
					}
				Assert.IsNotNull(found, names[i] + " population is missing");
				Assert.AreEqual("pickone", (string)found.Attribute("Style"),
					names[i] + " uses Weight but Qud will ignore it and emit every row without Style=pickone");
			}
		}

		[Test]
		public void MatureRoadSolverQueuesEveryPlotAndUsesFrozenAuthoredEntrances()
		{
			string source = KingdomRoadsLogicalSource.Read();
			StringAssert.DoesNotContain("MaxHomesConsidered", source);
			StringAssert.DoesNotContain("MaxWorksConsidered", source);
			StringAssert.DoesNotContain("MaxPlotsConsidered", source);
			StringAssert.Contains("KingdomArchitectureRuntime.TryRead(root", source);
			StringAssert.Contains("KingdomArchitectureRuntime.TryWorldAnchor(snapshot, rect, anchor",
				source);
			StringAssert.Contains("anchor.Key == \"entrance:public\"", source);
			StringAssert.Contains("KingdomRoadRules.MaxRoutesPerPass", source);
		}

		[Test]
		public void NativeEvidenceVersionHasOneManifestBoundRuntimeOwner()
		{
			string manifest = TestMain.ReadRepositoryText("manifest.json");
			Match version = Regex.Match(manifest, "\\\"version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
			Assert.IsTrue(version.Success, "manifest version is missing");
			Assert.AreEqual(version.Groups[1].Value, KingdomReleaseInfo.Version);
			foreach (string path in new string[]
			{
				"Debug/KingdomArchitectureGalleryWishes.cs",
				"Debug/KingdomVisualStateWishes.cs"
			})
			{
				string source = TestMain.ReadRepositoryText(path);
				StringAssert.Contains("KingdomReleaseInfo.Version", source, path);
				StringAssert.DoesNotContain("\"0.2.0\"", source, path);
			}
		}

		[Test]
		public void RetiredUnboundCaravanMutationHelpersAreAbsent()
		{
			string source = KingdomTradeLogicalSource.Read();
			StringAssert.DoesNotContain("SpawnCaravan(", source);
			StringAssert.DoesNotContain("DespawnCaravans(", source);
		}
	}
}
#endif
