#if TAF_TESTS
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomBenefitInspectionTests
	{
		[Test]
		public void EmptyDesignationSaysItProvidesZero()
		{
			KingdomBenefitReading reading = Reading();
			string detail = KingdomBenefitInspectionText.BuildingDetail(reading, "reed dormitory");
			StringAssert.Contains("Active now: none", detail);
			StringAssert.Contains("designation alone provides zero", detail);
			StringAssert.Contains("roof 4", detail);
			StringAssert.Contains("Unfilled capacity: roof 4", detail);
			StringAssert.Contains("Missing qualities: taf:quiet", detail);
			StringAssert.Contains("12 exact cells at 3,4–6,6", detail);
		}

		[Test]
		public void WrongRoleAndTrueSaturationRemainDistinct()
		{
			KingdomBenefitInspection wrong = new KingdomBenefitInspection {
				Fault = KingdomBenefitFault.UnacceptedBenefit,
				OutsideDesignationContract = true };
			Assert.That(KingdomBenefitInspectionText.Status(wrong), Is.EqualTo("wrong role"));
			StringAssert.Contains("Role mismatch:",
				KingdomBenefitInspectionText.ProviderDetail(wrong, "foreign fixture"));
			KingdomBenefitInspection capped = new KingdomBenefitInspection {
				Fault = KingdomBenefitFault.ProviderCap,
				SaturatedByDesignation = true };
			Assert.That(KingdomBenefitInspectionText.Status(capped), Is.EqualTo("capped"));
			StringAssert.Contains("At capacity:",
				KingdomBenefitInspectionText.ProviderDetail(capped, "extra bed"));
		}

		[Test]
		public void ZeroCreditMixedRefusalNamesWrongRoleAndSaturation()
		{
			KingdomBenefitInspection mixed = new KingdomBenefitInspection {
				Fault = KingdomBenefitFault.UnacceptedBenefit,
				OutsideDesignationContract = true,
				SaturatedByDesignation = true,
				LimitedByDesignation = true };
			Assert.That(KingdomBenefitInspectionText.Status(mixed),
				Is.EqualTo("wrong role and capped"));
			string detail = KingdomBenefitInspectionText.ProviderDetail(mixed, "mixed fixture");
			StringAssert.Contains("Role mismatch:", detail);
			StringAssert.Contains("At capacity:", detail);
		}

		[Test]
		public void ProviderTellsNominalCreditedOperationAndPartialCapApart()
		{
			KingdomBenefitInspection row = new KingdomBenefitInspection {
				ProviderIdentity = "bed-1#part:0", ProviderKey = "taf:bed",
				DesignationIdentity = "authored:dorm", OperationPercent = 75,
				LimitedByDesignation = true,
				Detail = "some offered supply is outside the cap" };
			row.Offered.Add(new KindAmount("roof", 2));
			row.Credited.Add(new KindAmount("roof", 1));
			Assert.AreEqual("partly active; capped", KingdomBenefitInspectionText.Status(row));
			string detail = KingdomBenefitInspectionText.ProviderDetail(row, "woven bed");
			StringAssert.Contains("Operating now: 75%", detail);
			StringAssert.Contains("Nominal offer: roof 2", detail);
			StringAssert.Contains("Counted now: roof 1", detail);
		}

		[TestCase(KingdomBenefitFault.ProviderCap, "capped")]
		[TestCase(KingdomBenefitFault.MissingDesignation, "missing")]
		[TestCase(KingdomBenefitFault.Inoperable, "inactive")]
		[TestCase(KingdomBenefitFault.WrongScope, "ineligible")]
		[TestCase(KingdomBenefitFault.SourceFault, "source fault")]
		[TestCase(KingdomBenefitFault.ObservationLimit, "over limit")]
		public void FaultStatesUsePlayerFacingTerms(KingdomBenefitFault fault, string expected)
		{
			Assert.AreEqual(expected, KingdomBenefitInspectionText.Status(
				new KingdomBenefitInspection { Fault = fault }));
		}

		[Test]
		public void RuntimeIsExactLoadedReadOnlyAndUsesOneSnapshot()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine("Core",
				"KingdomCharterPart.BenefitInspection.cs"));
			StringAssert.Contains("System.OwnedZone(zone.ZoneID)", source);
			StringAssert.Contains("KingdomSurvey.TakeCustodyOnly(zone)", source);
			StringAssert.Contains("survey.BindPass()", source);
			StringAssert.Contains("survey.TryBenefits(out index", source);
			StringAssert.Contains("KingdomConstruction.FindExactId(Zone, Id", source);
			StringAssert.DoesNotContain("GameObject.FindByID", source);
			StringAssert.DoesNotContain("ZoneManager", source);
			StringAssert.DoesNotContain("GetZone(", source);
			StringAssert.DoesNotContain("SetIntProperty", source);
			StringAssert.DoesNotContain("SetStringProperty", source);
			StringAssert.DoesNotContain("KingdomGovernanceScope.Commit", source);
			StringAssert.DoesNotContain("IDIfAssigned", source);
		}

		[Test]
		public void EvaluatorAttachesFailuresAndRecordsCreditedSupply()
		{
			string evaluate = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomBenefitIndex.Evaluate.cs"));
			string allocation = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomBenefitIndex.Allocation.cs"));
			int attach = evaluate.IndexOf("aggregate.Reading.Providers.Add(inspection)");
			int operation = evaluate.IndexOf("OperationPercent(Item, aggregate.Root");
			Assert.GreaterOrEqual(attach, 0);
			Assert.Greater(operation, attach, "an assigned but broken provider stays on its building");
			StringAssert.Contains("aggregate.Pending.Add", evaluate);
			StringAssert.Contains("inspection.Credited.Add", allocation);
			StringAssert.Contains("inspection.CreditedTags.Add", allocation);
		}

		[Test]
		public void PhysicalCoverHasAVisibleInspectionRowAndBound()
		{
			string aggregate = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomBenefitIndex.Aggregation.cs"));
			StringAssert.Contains("ProviderKey = \"taf:physical-cover\"", aggregate);
			StringAssert.Contains("inspection.Tags.Add", aggregate);
			StringAssert.Contains("inspection.CreditedTags.Add", aggregate);
			StringAssert.Contains("TrackInspection(inspection", aggregate);
			string allocation = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomBenefitAllocationRules.cs"));
			StringAssert.Contains("2 * KingdomDesignationRules.MaxDesignationsPerZone", allocation);
		}

		private static KingdomBenefitReading Reading()
		{
			KingdomBenefitDesignation designation = new KingdomBenefitDesignation {
				ProviderId = "taf.architecture", ProviderVersion = "1", Identity = "authored:dorm",
				Revision = "rev", ZoneId = "zone", RootId = "root", BuildingKey = "dorm" };
			designation.Caps.Add(new KindAmount("roof", 4));
			designation.AcceptedTags.Add("taf:quiet");
			for (int y = 4; y <= 6; y++) for (int x = 3; x <= 6; x++)
				designation.Cells.Add(new KingdomBenefitCell(x, y, KingdomBenefitCellUse.Building));
			return new KingdomBenefitReading { Designation = designation };
		}
	}
}
#endif
