#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomIdentityApiSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		[Test]
		public void IdentityContractIsRegisteredWithoutRetiringVersionOneSources()
		{
			string source = KingdomExtensionsLogicalSource.Read();
			StringAssert.Contains("extension is IKingdomIdentitySource", source);
			StringAssert.Contains("return mod.ID ?? \"\";", source);
			StringAssert.DoesNotContain("mod.DisplayTitleStripped", source);
			StringAssert.Contains("RefuseNamespaceCollisions(bound, refused);", source);
			StringAssert.Contains("collidedOwners.Contains(binding.ModName)", source);
			string rules = KingdomApiRulesLogicalSource.Read();
			StringAssert.Contains("public const int Version = 3;", rules);
			StringAssert.Contains("public const int MinSupportedVersion = 1;", rules);
		}

		[Test]
		public void BothIdentityCallsCrossTheExecutorAndFaultOnlyTheirSource()
		{
			string source = KingdomExtensionsLogicalSource.Read();
			StringAssert.Contains(
				"KingdomComputeResult<string[]> result = KingdomCity.Seam.Submit(Reading, job);",
				source);
			StringAssert.Contains(
				"KingdomComputeResult<int> result = KingdomCity.Seam.Submit(request, job);",
				source);
			StringAssert.Contains("Fault(binding.ModName, \"identity keys\"", source);
			StringAssert.Contains("Fault(binding.ModName, \"identity affinity\"", source);
			StringAssert.Contains("AnnouncedFaults", source);
			StringAssert.Contains("MessageQueue.AddPlayerMessage", source);
			StringAssert.Contains("The city is unaffected; the log names the fault", source);
		}

		[Test]
		public void RuntimeCapsKeysAndClampsAffinityWithoutATierSurface()
		{
			string source = KingdomExtensionsLogicalSource.Read();
			StringAssert.Contains("KingdomApiRules.MaxIdentityKeysPerSource", source);
			StringAssert.Contains("KingdomApiRules.MaxIdentityKeyCandidatesPerSource", source);
			StringAssert.Contains("KingdomApiRules.IdentityKey(owner, source[i])", source);
			StringAssert.Contains("KingdomApiRules.IdentityAffinityFromDelta", source);
			StringAssert.Contains("affinityDelta += KingdomApiRules.IdentityAffinity", source);
			string contract = Source(Path.Combine("Api", "KingdomApiContracts.cs"));
			int start = contract.IndexOf("public interface IKingdomIdentitySource",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0);
			string body = contract.Substring(start);
			StringAssert.DoesNotContain("Tier(", body);
			StringAssert.DoesNotContain("Tier {", body);
		}

		[Test]
		public void QolReadsExactSpeciesAndFeedsGenericSelfTagLane()
		{
			string reader = Source(Path.Combine("Core", "KingdomQolResidents.cs"));
			StringAssert.Contains("truth.Species = Resident.GetSpecies();", reader);
			string lodging = Source(Path.Combine("Growth", "KingdomLodging.cs"));
			StringAssert.Contains("KingdomQolRules.SelfTags(Profile)", lodging);
		}

		[Test]
		public void IdentityAdapterUsesQudsExactOpenAccessors()
		{
			string adapter = Source(Path.Combine("Api", "KingdomIdentity.cs"));
			StringAssert.Contains("Resident.GetCulture()", adapter);
			StringAssert.Contains("Resident.GetSpecies()", adapter);
			StringAssert.Contains("Resident.GetGenotype()", adapter);
			StringAssert.Contains("KingdomCreed.CreedProperty", adapter);
		}
	}
}
#endif
