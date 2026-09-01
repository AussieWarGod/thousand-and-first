#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomArchitectureExpansionRulesTests
	{
		[TestCase(ArchitectureTransitionMode.AdditiveExpand)]
		[TestCase(ArchitectureTransitionMode.RenovateExpand)]
		public void ExactSizeBindingsMayDifferAcrossAuthorizedExpansion(
			ArchitectureTransitionMode Mode)
		{
			ArchitectureLayoutSnapshot before = Snapshot("plan", "binding-s", "Housing",
				ArchitectureLotSize.Small, ArchitectureTransitionMode.None);
			ArchitectureLayoutSnapshot after = Snapshot("plan", "binding-m", "housing",
				ArchitectureLotSize.Medium, Mode);
			Assert.IsTrue(KingdomArchitectureExpansionRules.SameFrozenLineage(before, after));
		}

		[Test]
		public void ExpansionStillRejectsPlanTypeSizePoseAndModeDrift()
		{
			ArchitectureLayoutSnapshot before = Snapshot("plan", "binding-s", "housing",
				ArchitectureLotSize.Small, ArchitectureTransitionMode.None);
			Assert.IsFalse(KingdomArchitectureExpansionRules.SameFrozenLineage(before,
				Snapshot("other", "binding-m", "housing", ArchitectureLotSize.Medium,
					ArchitectureTransitionMode.RenovateExpand)));
			Assert.IsFalse(KingdomArchitectureExpansionRules.SameFrozenLineage(before,
				Snapshot("plan", "binding-m", "water", ArchitectureLotSize.Medium,
					ArchitectureTransitionMode.RenovateExpand)));
			Assert.IsFalse(KingdomArchitectureExpansionRules.SameFrozenLineage(before,
				Snapshot("plan", "binding-s", "housing", ArchitectureLotSize.Small,
					ArchitectureTransitionMode.RenovateExpand)));
			Assert.IsFalse(KingdomArchitectureExpansionRules.SameFrozenLineage(before,
				Snapshot("plan", "binding-m", "housing", ArchitectureLotSize.Medium,
					ArchitectureTransitionMode.Renovate)));
			ArchitectureLayoutSnapshot turned = Snapshot("plan", "binding-m", "housing",
				ArchitectureLotSize.Medium, ArchitectureTransitionMode.AdditiveExpand);
			turned.Facing = ArchitectureFacing.East;
			Assert.IsFalse(KingdomArchitectureExpansionRules.SameFrozenLineage(before, turned));
		}

		[Test]
		public void ExpansionCannotSkipALotSize()
		{
			ArchitectureLayoutSnapshot before = Snapshot("plan", "binding-s", "housing",
				ArchitectureLotSize.Small, ArchitectureTransitionMode.None);
			ArchitectureLayoutSnapshot skipped = Snapshot("plan", "binding-l", "housing",
				ArchitectureLotSize.Large, ArchitectureTransitionMode.RenovateExpand);
			Assert.IsFalse(KingdomArchitectureExpansionRules.SameFrozenLineage(before, skipped));
		}

		private static ArchitectureLayoutSnapshot Snapshot(string Plan, string Binding,
			string Type, ArchitectureLotSize Size, ArchitectureTransitionMode Mode)
		{
			return new ArchitectureLayoutSnapshot
			{
				PlanKey = Plan, BindingKey = Binding, LotType = Type, LotSize = Size,
				Facing = ArchitectureFacing.North, IncomingTransitionMode = Mode
			};
		}
	}
}
#endif
