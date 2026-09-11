#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The sealed founding heart whose root climbed a rung. A VALUE case for the identity fold
	/// that makes the stale city-book row unmatchable, and source contracts for the acceptance
	/// itself: the seam it is wired into, every clause it insists on, and the fail-closed halt it
	/// exists to stop firing. What no source pin can prove -- that a real climb recovers on real
	/// ground -- is owed to a native run.
	/// </summary>
	[TestFixture]
	public sealed class KingdomFoundingHeartClimbedRootTests
	{
		private const string Climbed = "Growth/KingdomPlot2.07r.FoundingHeartClimbedRoot.cs";
		private const string Drive = "Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs";
		private const string Settlement = "Growth/KingdomConstruction.Settlement.cs";
		private const string Evidence = "Core/KingdomInheritanceSpatial.Evidence.cs";

		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		/// <summary>
		/// VALUE. Why the sealed anchor stops resolving at all. The spatial evidence matches a
		/// standing object to a city-book row by blueprint AND by the row's work id, which is the
		/// object's own engine identity folded by <see cref="KingdomCityRules.StableId"/>. An
		/// improvement replaces the object, so the identity is a different string and the fold is
		/// a different work id -- the row can never match the successor until the settlement pass
		/// rebuilds it. This is the arithmetic behind "a sealed work root is absent, duplicated,
		/// moved, or changed", and it is why the halt must be fixed where the pass is aborted
		/// rather than by teaching the seal to accept a mismatch.
		/// </summary>
		[Test]
		public void ASuccessorsOwnIdentityFoldsToADifferentWorkIdThanTheSealedRow()
		{
			// Two founding-heart-shaped identities: the sealed final root, and the successor an
			// improvement raises in its place.
			const string sealedRoot = "r_TAF_FoundingHeart:final:9f2c";
			const string successor = "r_TAF_FoundingHeart:final:9f2d";
			int sealedRow = KingdomCityRules.StableId(sealedRoot);
			int successorRow = KingdomCityRules.StableId(successor);
			ClassicAssert.AreNotEqual(sealedRow, successorRow,
				"a replaced root that folded to the same work id would hide the defect");
			// The fold is stable, so the row is not merely unlucky: the same identity always
			// folds to the same id, and only a different identity moves it.
			ClassicAssert.AreEqual(sealedRow, KingdomCityRules.StableId(sealedRoot));
			ClassicAssert.AreEqual(successorRow, KingdomCityRules.StableId(successor));
			// And the evidence really does compare that fold to the row, at the anchor cell.
			string evidence = Read(Evidence);
			StringAssert.Contains("item.Blueprint != Row.Blueprint", evidence);
			StringAssert.Contains("Simulation.City.KingdomCityRules.StableId(item.IDIfAssigned)",
				evidence);
			StringAssert.Contains("!= Row.WorkId", evidence);
			StringAssert.Contains("a sealed work root is absent, duplicated, moved, or changed",
				evidence);
		}

		/// <summary>
		/// The acceptance is wired where the sealed recovery used to refuse, and it is an
		/// ADDITION: the refusal it sits beside is the same refusal, with the same step name.
		/// </summary>
		[Test]
		public void TheSealedRecoveryAcceptsAClimbedRootBesideTheRefusalItKeeps()
		{
			string drive = Read(Drive);
			StringAssert.Contains("return HasClimbedFoundingHeartRoot(System, Z, Context)\n"
				+ "\t\t\t\t\t\t|| HeartRefused(\"sealed: works slot lookup\");", drive);
			StringAssert.Contains("if (!HasFoundingHeartTerminalEvidence(Context.Plan, Z))", drive);
			// The terminal drive itself is untouched by this: a heart with terminal evidence
			// still drives its own terminal, exactly as before.
			StringAssert.Contains(
				"return DriveFoundingHeartTerminal(System, Z, Context, null, null, 0L, null, false);",
				drive);
		}

		/// <summary>
		/// Every clause the acceptance insists on. None of them is a weakening: the sealed
		/// identity must be ABSENT rather than unresolvable, its retirement must be proved by the
		/// same authority the terminal settlement asks for, the zone must record a rung above the
		/// first, exactly one heart plot of this settlement's own lot must stand on the sealed
		/// cell, it must be functionally built, and its design must BE the rung the zone records.
		/// </summary>
		[Test]
		public void TheClimbedRootIsProvedByAbsenceRetirementLotRungAndAuthority()
		{
			string climbed = Read(Climbed);
			foreach (string clause in new[] {
				"FindGlobalFoundingHeartId(FoundingHeartFinalId(plan), out _, out _)\n"
					+ "\t\t\t\t!= KingdomPhysicalLookupState.Absent) return false;",
				"if (!ExactFoundingHeartRetiredAuthority(Z, FoundingHeartFinalId(plan), out _))",
				"int rung = HeartRung(Z);",
				"if (rung < 2) return false;",
				"Cell cell = Z.GetCell(Context.Architecture.MainWorldX, Context.Architecture.MainWorldY);",
				"item.GetIntProperty(HeartPlotProperty) != 1",
				"item.GetStringProperty(PlotIdProperty) != plan.PlotId",
				"if (climbed != null) return false;",
				"!KingdomUpgrade.IsFunctionallyBuilt(climbed)",
				"KingdomPlotRules.HeartRungOf(key) == rung",
				"lot == plan.PlotId" })
				ClassicAssert.IsTrue(climbed.Contains(clause),
					"the climbed-root proof dropped a clause: " + clause);
			// It proves, and never writes: no property is stamped, no identity renamed, no
			// receipt rewritten, nothing created or destroyed.
			foreach (string effect in new[] { "SetIntProperty(", "SetStringProperty(",
				"SetZoneProperty(", "SetObjectGameState(", "IDIfAssigned =", "Destroy(",
				"Obliterate(", "AddObject(" })
				ClassicAssert.IsFalse(climbed.Contains(effect),
					"the climbed-root proof must read, never write: " + effect);
		}

		/// <summary>
		/// The halt this fixes still fails closed. A settlement pass that cannot recover its
		/// founding heart must still refuse to do anything else on that ground -- the fix is that
		/// a lawfully climbed heart IS recovered, never that an unrecovered heart is waved past.
		/// </summary>
		[Test]
		public void TheSettlementPassStillRefusesEverythingWhenTheHeartIsNotRecovered()
		{
			string settlement = Read(Settlement);
			StringAssert.Contains("if (!KingdomPlots.RecoverFoundingHeart(System, Z))", settlement);
			StringAssert.Contains(
				"KingdomLog.Log(\"construction: founding heart recovery requires inspection\");",
				settlement);
			int guard = settlement.IndexOf("if (!KingdomPlots.RecoverFoundingHeart(System, Z))",
				StringComparison.Ordinal);
			int halt = settlement.IndexOf("return;", guard, StringComparison.Ordinal);
			int work = settlement.IndexOf("ReleaseTerminalInputRemaindersOnActiveZone",
				StringComparison.Ordinal);
			ClassicAssert.IsTrue(guard > -1 && halt > guard && work > halt,
				"the founding-heart guard must still stand before any settlement work");
		}
	}
}
#endif
