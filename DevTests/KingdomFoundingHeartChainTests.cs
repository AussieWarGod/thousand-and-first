#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The founding heart's terminal chain, executed rather than pinned. These are the cases the
	/// last attempt at issue #162 lacked: it asserted source strings and shipped a predicate that
	/// could never be true. Here the codec is round-tripped, the digest and the chain link are
	/// tampered with, the two identities that were confused are compared for a real plan, and the
	/// relaxed binding is driven clause by clause with one mutation each.
	/// </summary>
	[TestFixture]
	public sealed class KingdomFoundingHeartChainTests
	{
		private const string Transaction = "0123456789abcdef0123456789abcdef";
		private const string Zone = "JoppaWorld.11.22.1.1.10";
		private const string Seal = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

		private static KingdomFoundingHeartTerminalPlan Terminal(string Predecessor, string Final)
		{
			KingdomFoundingHeartTerminalPlan plan;
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryCreate(Transaction, Seal,
				Zone, Predecessor, Final, "r_KingdomWaterstone", "heartwaterstone", "lot-heart",
				12, 9, out plan), "the terminal record refused a well-formed generation");
			return plan;
		}

		/// <summary>
		/// VALUE. A chained generation round-trips through the SAME sealed blob shape the first
		/// generation uses: no new version, no new field, one record naming the identity it
		/// retires and the identity it binds.
		/// </summary>
		[Test]
		public void AChainedTerminalRoundTripsThroughTheSameSealedBlob()
		{
			KingdomFoundingHeartTerminalPlan first = Terminal("works-slot-id", "final-reserved-id");
			KingdomFoundingHeartTerminalPlan second = Terminal("final-reserved-id", "successor-id");
			string encoded = KingdomFoundingHeartTerminalRules.Encode(second);
			ClassicAssert.IsNotNull(encoded);
			StringAssert.StartsWith("ht1|", encoded, "the chained generation must not change shape");
			ClassicAssert.AreEqual(15, encoded.Split('|').Length);

			KingdomFoundingHeartTerminalPlan read;
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryDecode(encoded, out read));
			ClassicAssert.AreEqual("final-reserved-id", read.PredecessorId);
			ClassicAssert.AreEqual("successor-id", read.FinalId);
			ClassicAssert.AreEqual(encoded, KingdomFoundingHeartTerminalRules.Encode(read));
			// The chain link itself: what the first generation bound is what the second retires.
			ClassicAssert.AreEqual(first.FinalId, read.PredecessorId);
			// And a historical single-generation blob still decodes exactly as it always did.
			string historical = KingdomFoundingHeartTerminalRules.Encode(first);
			KingdomFoundingHeartTerminalPlan back;
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryDecode(historical, out back));
			ClassicAssert.AreEqual("works-slot-id", back.PredecessorId);
			ClassicAssert.AreEqual("final-reserved-id", back.FinalId);
		}

		/// <summary>VALUE. The blob is digest-sealed, so neither the chain link nor anything
		/// beside it can be edited in place: a tampered record does not decode at all.</summary>
		[Test]
		public void ATamperedDigestOrChainLinkRefusesToDecode()
		{
			string encoded = KingdomFoundingHeartTerminalRules.Encode(
				Terminal("final-reserved-id", "successor-id"));
			string[] parts = encoded.Split('|');
			KingdomFoundingHeartTerminalPlan read;

			// The chain link, edited in place, keeping the old digest.
			string[] link = (string[])parts.Clone();
			link[4] = KingdomFoundingHeartTerminalRules.Encode(
				Terminal("someone-elses-id", "successor-id")).Split('|')[4];
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.TryDecode(
				string.Join("|", link), out read), "an edited chain link must not decode");

			// The bound identity, edited in place.
			string[] bound = (string[])parts.Clone();
			bound[5] = KingdomFoundingHeartTerminalRules.Encode(
				Terminal("final-reserved-id", "another-id")).Split('|')[5];
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.TryDecode(
				string.Join("|", bound), out read), "an edited final identity must not decode");

			// The digest itself.
			string[] digest = (string[])parts.Clone();
			digest[14] = new string('0', digest[14].Length);
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.TryDecode(
				string.Join("|", digest), out read), "a forged digest must not decode");

			// Untouched, it still decodes.
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryDecode(encoded, out read));
		}

		/// <summary>
		/// VALUE, and the case whose absence let a dead predicate ship: the works slot and the
		/// final root are DIFFERENT identities for the same plan, so an authority asked about one
		/// can never answer for the other.
		/// </summary>
		[Test]
		public void TheWorksSlotAndTheFinalRootAreDifferentIdentitiesForOnePlan()
		{
			string works = KingdomFoundingHeartRules.StableId(Transaction, Zone,
				"slot-" + KingdomFoundingHeartRules.WorksSlot);
			string final = KingdomFoundingHeartRules.StableId(Transaction, Zone, "final");
			ClassicAssert.IsFalse(string.IsNullOrEmpty(works));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(final));
			ClassicAssert.AreNotEqual(works, final,
				"an authority hard-wired to the works slot cannot retire the final root");
			// Both folds are stable, so this is a property of the roles, not of one run.
			ClassicAssert.AreEqual(works, KingdomFoundingHeartRules.StableId(Transaction, Zone,
				"slot-" + KingdomFoundingHeartRules.WorksSlot));
			ClassicAssert.AreEqual(final,
				KingdomFoundingHeartRules.StableId(Transaction, Zone, "final"));
		}

		/// <summary>
		/// VALUE. The relaxed binding, clause by clause. The first row is the whole proof; every
		/// row after it drops exactly one demand and must refuse. A first-generation terminal is
		/// accepted without any of them, and a stranger identity without all of them.
		/// </summary>
		[Test]
		public void TheRelaxedBindingRefusesWhenAnySingleClauseIsDropped()
		{
			const string planFinal = "final-reserved-id";
			const string successor = "successor-id";
			KingdomFoundingHeartTerminalPlan prior = Terminal("works-slot-id", planFinal);

			// First generation: the reserved identity, no chain asked for.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.BindsGround(planFinal, planFinal,
				null, null, null, false, false, false));
			// A stranger identity with no chain is not a heart.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				null, null, null, false, false, false));
			// The whole chained proof.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				prior, planFinal, successor, true, true, true));

			// One clause dropped per row, and each must refuse.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				null, planFinal, successor, true, true, true), "prior record absent");
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				Terminal("works-slot-id", "someone-elses-final"), planFinal, successor, true, true,
				true), "the prior record bound another identity");
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				prior, "", successor, true, true, true), "no identity was retired");
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				prior, planFinal, "other-id", true, true, true),
				"the receipt names another output");
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				prior, planFinal, successor, false, true, true), "no construction receipt");
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				prior, planFinal, successor, true, false, true), "no removal proof");
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				prior, planFinal, successor, true, true, false), "custody unproved");

			// A record that retires what it binds, and one that loops back onto the prior
			// record's own predecessor, are not generations.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				Terminal("works-slot-id", successor), successor, successor, true, true, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround("works-slot-id",
				planFinal, prior, planFinal, "works-slot-id", true, true, true));
		}

		/// <summary>
		/// VALUE. A caller holding nothing but booleans cannot satisfy the chain. The prior
		/// generation must be the record the plan's own sealed blob decoded to, so a forged or
		/// merely asserted chain -- every flag true, no real prior record, or a prior record from
		/// another heart -- is refused on the evidence rather than on the flags.
		/// </summary>
		[Test]
		public void EveryFlagTrueWithoutTheRecordsIsRefused()
		{
			const string planFinal = "final-reserved-id";
			const string successor = "successor-id";
			// Every proof flag asserted, and nothing to read: refused.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				null, planFinal, successor, true, true, true));
			// A prior record that is not a valid terminal at all: refused.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				new KingdomFoundingHeartTerminalPlan(), planFinal, successor, true, true, true));
			// A well-formed prior record belonging to ANOTHER heart's chain: refused, because the
			// link is read from the record and does not match the identity retired here.
			KingdomFoundingHeartTerminalPlan foreign = Terminal("other-works", "other-final");
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				foreign, planFinal, successor, true, true, true));
			// And the honest chain, for contrast, with the same flags.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				Terminal("works-slot-id", planFinal), planFinal, successor, true, true, true));
		}

		/// <summary>
		/// The halt this chain exists to lift still fails closed, and must keep doing so: a
		/// settlement that cannot recover its founding heart refuses every later pass on that
		/// ground rather than proceeding without one.
		/// </summary>
		[Test]
		public void TheSettlementPassStillRefusesEverythingWhenTheHeartIsNotRecovered()
		{
			string settlement = TestMain.ReadRepositoryText(
				"Growth/KingdomConstruction.Settlement.cs");
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
		/// <summary>
		/// VALUE, and the case whose absence let a hard refusal ship: the heart's reservation
		/// store is keyed by DETERMINISTIC role identities. A row whose id is not the role's own
		/// stable identity cannot be read back at all, and Encode returns null for one, so a chain
		/// clause demanding a reservation for a successor could only ever refuse -- and while it
		/// stood at the settle it refused every heart climb.
		/// </summary>
		[Test]
		public void AReservationCannotNameAnythingButTheRolesOwnStableIdentity()
		{
			string final = KingdomFoundingHeartRules.StableId(Transaction, Zone, "final");
			ClassicAssert.IsFalse(string.IsNullOrEmpty(final));
			ClassicAssert.AreNotEqual("successor-id", final);
			// A well-formed row for the role's own identity is readable only under its own key,
			// and the same row under a successor's key is refused: the id is checked against
			// StableId(transaction, zone, role), which no successor identity can equal.
			string raw = "hr1|" + Convert.ToBase64String(
					System.Text.Encoding.UTF8.GetBytes(Transaction))
				+ "|" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Zone))
				+ "|" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("final"))
				+ "|successor-id|" + new string('0', 64);
			ClassicAssert.IsFalse(KingdomFoundingHeartReservationRules.TryRead(
				KingdomFoundingHeartReservationRules.Prefix + "successor-id", raw, out _, out _,
				out _), "the store must refuse an id that is not the role's stable identity");
		}


		/// <summary>
		/// VALUE. How a witnessed work row is re-linked to a retired identity: the seal holds the
		/// FOLD of an object identity, and the fold of the retired root is what the row carries
		/// until the next check-in rebuilds it. The successor's own fold is a different number,
		/// which is why the row stops matching the world and why the chain has to name the
		/// identity rather than search the cell.
		/// </summary>
		[Test]
		public void AWitnessedRowIsRelinkedByTheFoldOfTheRetiredIdentity()
		{
			const string retired = "r_TAF_FoundingHeart:final:9f2c";
			const string successor = "r_TAF_FoundingHeart:final:9f2d";
			int row = KingdomCityRules.StableId(retired);
			ClassicAssert.AreNotEqual(0, row);
			// The row's id is the retired identity's fold, and only that identity's.
			ClassicAssert.AreEqual(row, KingdomCityRules.StableId(retired));
			ClassicAssert.AreNotEqual(row, KingdomCityRules.StableId(successor));
			ClassicAssert.AreNotEqual(row, KingdomCityRules.StableId("r_TAF_SomeoneElse"));
			// A foreign identity standing at the same anchor cannot borrow the row: its fold is
			// not the row's, so the re-link refuses before any cell is read.
			ClassicAssert.AreNotEqual(row, KingdomCityRules.StableId("r_KingdomWaterstone:stray"));
		}

		/// <summary>
		/// Both sites that follow a climbed root run the SAME proof: the seal's spatial witness
		/// asks through the work-row entry point, which calls the recovery path's own function,
		/// so the two cannot drift apart. And the witness still requires position, exactly once.
		/// </summary>
		[Test]
		public void TheSealAndTheRecoveryShareOneChainProof()
		{
			string chain = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.07s.FoundingHeartClimbedChain.cs");
			StringAssert.Contains("internal static bool TryChainedWorkSuccessor(Zone Z, "
				+ "int RowWorkId,", chain);
			StringAssert.Contains("Simulation.City.KingdomCityRules.StableId(prior.FinalId) "
				+ "!= RowWorkId", chain);
			StringAssert.Contains("return TryChainedFoundingHeartRoot(Z, context, out Successor);",
				chain);

			string evidence = TestMain.ReadRepositoryText(
				"Core/KingdomInheritanceSpatial.Evidence.cs");
			// Added only where the exact count was zero, and the old refusal is untouched.
			StringAssert.Contains("if (count == 0 && TryClimbedRoot(Zone, cell, Row, "
				+ "out GameObject climbed))", evidence);
			StringAssert.Contains("Failure = \"a sealed work root is absent, duplicated, moved, "
				+ "or changed\";", evidence);
			StringAssert.Contains("KingdomPlots.TryChainedWorkSuccessor(Zone, Row.WorkId, "
				+ "out GameObject proved)", evidence);
			// Position still required, and still exactly once.
			StringAssert.Contains("if (object.ReferenceEquals(Cell.Objects[i], proved)) here++;",
				evidence);
			StringAssert.Contains("if (here != 1) return false;", evidence);
			// The witness writes nothing.
			foreach (string write in new[] { "SetStringProperty(", "SetIntProperty(",
				"SetZoneProperty(", "SetObjectGameState(", "Destroy(", "AddObject(" })
				StringAssert.DoesNotContain(write, evidence);
		}

		/// <summary>
		/// The two day-length constants the ordering argument rests on are the same number, so the
		/// seal's daily poll and the settlement's daily cadence measure the same day.
		/// </summary>
		[Test]
		public void TheSealsDayAndTheSettlementsDayAreTheSameLength()
		{
			// Ours, executed. The engine's own constant cannot be executed here -- these suites
			// are engine-free -- so it is pinned where we consume it and verified against the
			// pinned decompile: XRL/World/Calendar.cs:13 declares TurnsPerDay = 1200.
			ClassicAssert.AreEqual(1200L, KingdomRules.TicksPerDay);
			ClassicAssert.AreEqual(1200L, KingdomSemanticClockRules.CadenceTicks);
			StringAssert.Contains("Calendar.TurnsPerDay",
				TestMain.ReadRepositoryText("Core/KingdomSeal.cs"));
			StringAssert.Contains("public const long CadenceTicks = KingdomRules.TicksPerDay;",
				TestMain.ReadRepositoryText("Simulation/City/KingdomSemanticClockRules.cs"));
		}

		/// <summary>
		/// VALUE. The custody corroboration, after native run 19: the improvement route never
		/// writes a predecessor stamp (only the plot finish route does), so an absent stamp must
		/// bind on the receipts, a stamp naming the retired identity must bind, and a stamp naming
		/// anything else must refuse. Receipts missing refuses whatever the stamp says.
		/// </summary>
		[Test]
		public void AnAbsentPredecessorStampBindsAndAContradictingOneRefuses()
		{
			const string retired = "final-reserved-id";
			const string successor = "successor-id";

			// The stamp alone, in its three shapes.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.CorroboratesRetired(null, retired),
				"the improvement route writes no stamp, so absence cannot refuse");
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.CorroboratesRetired("", retired));
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.CorroboratesRetired(retired,
				retired));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.CorroboratesRetired(
				"someone-elses-root", retired),
				"a successor stamped with another predecessor did not replace this root");
			// And it can never supply the identity it is corroborating.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.CorroboratesRetired(retired,
				null));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.CorroboratesRetired(null, ""));

			// In the whole binding: custody proved on the receipts alone still binds, and the
			// receipts missing still refuses -- the stamp cannot rescue it either way.
			KingdomFoundingHeartTerminalPlan prior = Terminal("works-slot-id", retired);
			bool custodyOnReceipts = KingdomFoundingHeartChainRules.CorroboratesRetired(null,
				retired);
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.BindsGround(successor, "other-plan",
				prior, retired, successor, true, true, custodyOnReceipts));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, "other-plan",
				prior, retired, successor, false, true, custodyOnReceipts),
				"no construction receipt refuses however the stamp reads");
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, "other-plan",
				prior, retired, successor, true, false, custodyOnReceipts),
				"no removal proof refuses however the stamp reads");
			// A contradicting stamp collapses custody, and the binding refuses with it.
			bool custodyContradicted = KingdomFoundingHeartChainRules.CorroboratesRetired(
				"someone-elses-root", retired);
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, "other-plan",
				prior, retired, successor, true, true, custodyContradicted));
		}
	}
}
#endif