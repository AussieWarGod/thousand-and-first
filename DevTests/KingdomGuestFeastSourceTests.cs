#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGuestFeastSourceTests
	{
		private static readonly string[] PureFiles = new string[]
		{
			"Experience/KingdomCommunalRiteModels.cs",
			"Experience/KingdomCommunalRiteRules.cs",
			"Experience/KingdomCommunalRiteCodec.cs",
			"Experience/KingdomCommunalRiteCodec.Payload.cs",
			"Experience/KingdomCommunalRiteCodec.Primitives.cs",
			"Experience/KingdomGuestFeastModels.cs",
			"Experience/KingdomGuestFeastRules.cs",
			"Experience/KingdomGuestFeastRules.Copy.cs",
			"Experience/KingdomGuestFeastRules.Transitions.cs",
			"Experience/KingdomGuestFeastRules.Pointers.cs",
			"Experience/KingdomGuestFeastCodec.cs",
			"Experience/KingdomGuestFeastCodec.Payload.cs",
			"Experience/KingdomGuestFeastCodec.Primitives.cs"
		};

		private static string Combined()
		{
			string source = "";
			for (int i = 0; i < PureFiles.Length; i++)
				source += TestMain.ReadRepositoryText(PureFiles[i]) + "\n";
			return source;
		}

		[Test]
		public void PureCoordinationDefinesNoSecondPersistenceOwnerQueueOrReward()
		{
			string source = Combined();
			string[] forbidden = new string[] { "IGameSystem", "IComposite", "KingdomSystem.z",
				"Popup.", "PickOption", "JournalAPI", "CookingGameState", "CookingRecipe",
				"LearnRecipe", "AddXP", "Reputation", "Buff", "Calendar",
				"GameObject.Create", "AddObject(", "KingdomGovernanceScope.Commit",
				"TryReserveAudience", "TryReserveBodies", "Audiences.Add",
				"BodyReservations.Add", "Queue<", "Timer" };
			for (int i = 0; i < forbidden.Length; i++)
				StringAssert.DoesNotContain(forbidden[i], source, forbidden[i]);
			StringAssert.Contains("++row.HomeCycles == 3", source);
		}

		[Test]
		public void D8ProjectionIsExternalZeroSinkAndUsesOnlyExistingResidents()
		{
			string physical = KingdomPhysicalHappeningsLogicalSource.Read();
			StringAssert.Contains("KingdomPhysicalHappeningKind.CommunalRite", physical);
			StringAssert.Contains("QueueCommunalRite", physical);
			StringAssert.Contains("r_KingdomBench", physical);
			StringAssert.Contains("r_KingdomFirstBasin", physical);
			StringAssert.Contains("TryReadCommunalRiteProof", physical);
			StringAssert.Contains("ExactResidents(system, zone,", physical);
			StringAssert.Contains("protectCivicRoles", physical);
			StringAssert.DoesNotContain("UseCharge: true)\n\t\t\t\t\t&& RadiatesHeatEvent.Check",
				TestMain.ReadRepositoryText(
					"Simulation/City/KingdomPhysicalHappenings.07.CommunalRite.cs"));
			string d8 = "";
			for (int i = 0; i < 5; i++) d8 += TestMain.ReadRepositoryText(PureFiles[i]);
			StringAssert.DoesNotContain("KingdomExperienceAudienceReceipt", d8);
			StringAssert.DoesNotContain("KingdomExperienceBodyReservation", d8);
			string terminal = TestMain.ReadRepositoryText(
				"Experience/KingdomCommunalRiteRuntime.Terminal.cs");
			StringAssert.Contains("KingdomExperienceBodyReservation", terminal);
			StringAssert.Contains("TryReserveBodies", terminal);
			StringAssert.Contains("TryReleaseBodies", terminal);
			StringAssert.Contains("BodyLeaseProof", terminal);
			StringAssert.Contains("taf:experience-body:", terminal);
		}

		[Test]
		public void O11OwnsOnlyFiniteReferencesCyclesAndOptionalPointers()
		{
			string source = Combined();
			StringAssert.Contains("KingdomGuestFeastPhase.Exhausted", source);
			StringAssert.Contains("++row.HomeCycles == 3", source);
			StringAssert.Contains("row.AwayArmed = false", source);
			StringAssert.Contains("TryAttachCuratorPointer", source);
			StringAssert.Contains("TryAttachCivicLeadPointer", source);
			StringAssert.Contains("row.PointerSourceId == row.PracticeId", source);
			StringAssert.Contains("KingdomFirstFeastRules.IsAffirmative", source);
			StringAssert.Contains("KingdomGrowthFirstGuestIdentityRules.OpportunityId", source);
			StringAssert.Contains("KingdomGuestFeastPhase.GuestDeparted", source);
			StringAssert.Contains("KingdomGrowthArrivalDisposition.Departed", source);
			StringAssert.Contains("&& noPractice && noLocus", source);
			StringAssert.Contains("TAF-GUEST-FEAST-ENVELOPE-V4", source);
		}

		[Test]
		public void EveryNewProductionShardStaysBelowThreeHundredLines()
		{
			for (int i = 0; i < PureFiles.Length; i++)
			{
				int lines = TestMain.ReadRepositoryText(PureFiles[i]).Split('\n').Length;
				Assert.Less(lines, 300, PureFiles[i] + " has " + lines + " lines");
			}
			Assert.Less(TestMain.ReadRepositoryText(
				"Simulation/City/KingdomPhysicalHappenings.07.CommunalRite.cs").Split('\n').Length,
				300);
		}
	}
}
#endif
