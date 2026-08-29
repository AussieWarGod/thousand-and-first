#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Tests;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomBodyHistoryAndJointViewSourceTests
	{
		[Test]
		public void BodyAdapterPinsNonMintingLoadedNativeAnatomy()
		{
			string source = TestMain.ReadRepositoryText("Core/KingdomBodyHistoryRuntime.cs");
			StringAssert.Contains("GameObject.cs:139", source);
			StringAssert.Contains(":424-434", source);
			StringAssert.Contains("Parts/Body.cs:919-923", source);
			StringAssert.Contains("IDIfAssigned", source);
			StringAssert.Contains("part._ID", source);
			StringAssert.Contains("part.Abstract", source);
			StringAssert.Contains("part.GetOrdinalName()", source);
			StringAssert.Contains("part.Cybernetics", source);
			Assert.IsFalse(source.Contains("part.ID"));
			Assert.IsFalse(source.Contains("GetParts(true"));
			Assert.IsFalse(source.Contains("ZoneManager"));
			Assert.IsFalse(source.Contains("GetZone("));
			Assert.IsFalse(source.Contains("Journal"));
			Assert.IsFalse(source.Contains("Damage"));
			Assert.IsFalse(source.Contains("Scar"));
		}

		[Test]
		public void CompletedLabBuilderIsExactReadOnlyAndUnhooked()
		{
			string source = TestMain.ReadRepositoryText(
				"Growth/KingdomLab.BodyHistoryEvidence.cs");
			StringAssert.Contains("KingdomLabJobPhase.Complete", source);
			StringAssert.Contains("KingdomLabRegistryStatus.Complete", source);
			StringAssert.Contains("KingdomLabRules.RegistryAuthority", source);
			StringAssert.Contains("IDIfAssigned", source);
			StringAssert.Contains("ExactLiveBodyPart", source);
			StringAssert.Contains("EffectNonces", source);
			StringAssert.Contains("ValidEvidence", source);
			Assert.IsFalse(source.Contains("TryRecordWitnessedProcedure"));
			Assert.IsFalse(source.Contains("SetString"));
			Assert.IsFalse(source.Contains("RemoveString"));
			Assert.IsFalse(source.Contains("RequirePart"));
			string application = TestMain.ReadRepositoryText("Growth/KingdomLab.Application.cs");
			Assert.IsFalse(application.Contains("TryBuildCompletedBodyHistoryEvidence"));
		}

		[Test]
		public void JointViewUsesSeparateOwnersWithoutStandingOrMutation()
		{
			string adapters = TestMain.ReadRepositoryText(
				"Core/KingdomJointCivicViewAdapters.cs");
			string source = adapters
				+ TestMain.ReadRepositoryText("Core/KingdomJointCivicView.cs")
				+ TestMain.ReadRepositoryText("Core/KingdomJointCivicViewRuntime.cs")
				+ TestMain.ReadRepositoryText("Growth/KingdomAssentingMoot.ReadOnly.cs")
				+ TestMain.ReadRepositoryText("Growth/KingdomHostedArcology.ReadOnly.cs");
			StringAssert.Contains("CovenantMissing", source);
			StringAssert.Contains("No durable exact village-covenant owner", source);
			StringAssert.Contains("KingdomAssentingMootRules.Validate", source);
			StringAssert.Contains("TryReadAuthority", source);
			StringAssert.Contains("Authority.Valid()", source);
			StringAssert.Contains("taf:hosted-enclave:v1:", source);
			Assert.IsFalse(adapters.Contains("SourceReceiptId = Authority.CarrierId"));
			Assert.IsFalse(source.Contains("GetStanding("));
			Assert.IsFalse(source.Contains("UseEnergy"));
			Assert.IsFalse(source.Contains("SetStringProperty"));
			Assert.IsFalse(source.Contains("SetIntProperty"));
			Assert.IsFalse(source.Contains("RecordDeed"));
			Assert.IsFalse(source.Contains("Normalize("));
			Assert.IsFalse(source.Contains("ReconcileRoot("));
			Assert.IsFalse(source.Contains("Operational("));
			Assert.IsFalse(source.Contains("FindByID("));
		}

		[Test]
		public void MissingMootIsReadFromTheLoadedCityBeforeABuildingIsRequired()
		{
			string source = TestMain.ReadRepositoryText(
				"Growth/KingdomAssentingMoot.ReadOnly.cs");
			int city = source.IndexOf("KingdomAssentingMootReceipt stored = book.AssentingMoot;",
				System.StringComparison.Ordinal);
			int absent = source.IndexOf("stored.Phase == KingdomAssentingMootPhase.None",
				System.StringComparison.Ordinal);
			int building = source.IndexOf("!GameObject.Validate(Building)",
				System.StringComparison.Ordinal);
			Assert.GreaterOrEqual(city, 0);
			Assert.Greater(absent, city);
			Assert.Greater(building, absent,
				"an absent receipt is a valid Absent owner, not an invalid missing building");
			StringAssert.Contains("System.OwnedZone(LoadedZone.ZoneID)", source);
			StringAssert.Contains("System.SettlementIdForOwnedZone(LoadedZone.ZoneID)", source);
			StringAssert.Contains("ReferenceEquals(zone, LoadedZone)", source);
		}

		[Test]
		public void RefactoredProductionFilesStayReviewable()
		{
			string[] paths =
			{
				"Core/KingdomBodyHistoryModels.cs",
				"Core/KingdomBodyHistoryRules.cs",
				"Core/KingdomBodyHistoryCodec.cs",
				"Core/KingdomBodyHistoryEnvelope.cs",
				"Core/KingdomBodyHistoryStore.cs",
				"Core/KingdomBodyHistoryRuntime.cs",
				"Core/KingdomJointCivicView.cs",
				"Core/KingdomJointCivicViewAdapters.cs",
				"Core/KingdomJointCivicViewRuntime.cs",
				"Growth/KingdomLab.BodyHistoryEvidence.cs",
				"Growth/KingdomAssentingMoot.ReadOnly.cs",
				"Growth/KingdomHostedArcology.ReadOnly.cs"
			};
			for (int i = 0; i < paths.Length; i++)
			{
				int lines = TestMain.ReadRepositoryText(paths[i]).Split('\n').Length;
				Assert.Less(lines, 300, paths[i]);
			}
		}
	}
}
#endif
