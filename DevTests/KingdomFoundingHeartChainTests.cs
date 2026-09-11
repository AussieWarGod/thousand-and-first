#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
		/// accepted without any of them, and a chained one is accepted without all of them.
		/// </summary>
		[Test]
		public void TheRelaxedBindingRefusesWhenAnySingleClauseIsDropped()
		{
			const string planFinal = "final-reserved-id";
			const string successor = "successor-id";
			// First generation: the reserved identity, no chain asked for.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.BindsGround(planFinal, planFinal,
				null, null, null, false, false, false, false));
			// A stranger identity with no chain is not a heart.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				null, null, null, false, false, false, false));

			// The whole chained proof.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				planFinal, planFinal, successor, true, true, true, true));

			// One clause dropped per row, and each must refuse.
			foreach (object[] row in new[]
			{
				new object[] { "prior blob absent", "", planFinal, successor, true, true, true, true },
				new object[] { "chain link broken", "someone-else", planFinal, successor, true, true, true, true },
				new object[] { "predecessor unnamed", planFinal, "", successor, true, true, true, true },
				new object[] { "receipt names another id", planFinal, planFinal, "other-id", true, true, true, true },
				new object[] { "retirement unproved", planFinal, planFinal, successor, false, true, true, true },
				new object[] { "receipt unproved", planFinal, planFinal, successor, true, false, true, true },
				new object[] { "custody unproved", planFinal, planFinal, successor, true, true, false, true },
				new object[] { "reservation unproved", planFinal, planFinal, successor, true, true, true, false }
			})
				ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor,
					planFinal, (string)row[1], (string)row[2], (string)row[3], (bool)row[4],
					(bool)row[5], (bool)row[6], (bool)row[7]),
					"the chain was accepted with a clause missing: " + row[0]);

			// A record that retires what it binds is not a generation.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.BindsGround(successor, planFinal,
				successor, successor, successor, true, true, true, true));
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
	}
}
#endif
