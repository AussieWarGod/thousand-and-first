#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFoundingHeartRulesTests
	{
		private const string Transaction = "0123456789abcdef0123456789abcdef";

		private static string StakeTruth()
		{
			ClassicAssert.IsTrue(KingdomFoundingHeartStakeRules.TryCreate("heartbasin", "first basin",
				"r_KingdomPlotWorks", 38, 11, 42, 13, 0, true, false, null,
				"TAF_HeartBasinContents", 2, true, 3, false, 40, 11, false,
				out KingdomFoundingHeartStakeTruth truth));
			return KingdomFoundingHeartStakeRules.Encode(truth);
		}

		private static KingdomFoundingHeartPlan Plan()
		{
			ClassicAssert.IsTrue(KingdomFoundingHeartRules.TryCreate(Transaction, "JoppaWorld.2.2.1.1.10",
				40, 12, 30, 2, 49, 21, 38, 11, 42, 13, 900L, 600L,
				"p4,frozen-authored-payload", StakeTruth(), out KingdomFoundingHeartPlan plan));
			return plan;
		}

		private static KingdomFoundingHeartTerminalPlan Terminal()
		{
			KingdomFoundingHeartPlan heart = Plan();
			for (int i = 0; i < KingdomFoundingHeartRules.SlotCount; i++)
			{
				ClassicAssert.IsTrue(KingdomFoundingHeartRules.TryAdvance(heart, i, 0, 1));
				ClassicAssert.IsTrue(KingdomFoundingHeartRules.TryAdvance(heart, i, 1, 2));
			}
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryCreate(heart.TransactionId,
				KingdomFoundingHeartRules.CompletionSeal(heart), heart.ZoneId,
				KingdomFoundingHeartRules.SlotId(heart, KingdomFoundingHeartRules.WorksSlot),
				KingdomFoundingHeartRules.StableId(heart.TransactionId, heart.ZoneId, "final"),
				"r_KingdomPlotWorks", "heartbasin", heart.PlotId, 40, 12, out var terminal));
			return terminal;
		}

		[Test]
		public void IdentityIsDeterministicDomainSeparatedAndBounded()
		{
			KingdomFoundingHeartPlan first = Plan();
			KingdomFoundingHeartPlan second = Plan();
			ClassicAssert.AreEqual(first.PlotId, second.PlotId);
			ClassicAssert.AreEqual(KingdomFoundingHeartRules.SlotId(first, 0),
				KingdomFoundingHeartRules.SlotId(second, 0));
			ClassicAssert.AreNotEqual(first.PlotId, KingdomFoundingHeartRules.SlotId(first, 0));
			ClassicAssert.AreNotEqual(KingdomFoundingHeartRules.SlotId(first, 0),
				KingdomFoundingHeartRules.SlotId(first, 1));
			ClassicAssert.Less(first.PlotId.Length, 128);
		}

		[Test]
		public void CodecRoundTripsFrozenTruthAndIndependentStates()
		{
			KingdomFoundingHeartPlan plan = Plan();
			ClassicAssert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, 0, 0, 1));
			ClassicAssert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, 0, 1, 2));
			ClassicAssert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, 1, 0, 1));
			string encoded = KingdomFoundingHeartRules.Encode(plan);
			ClassicAssert.IsTrue(KingdomFoundingHeartRules.TryDecode(encoded, out var loaded));
			ClassicAssert.AreEqual(plan.TransactionId, loaded.TransactionId);
			ClassicAssert.AreEqual(plan.ZoneId, loaded.ZoneId);
			ClassicAssert.AreEqual(plan.Payload, loaded.Payload);
			ClassicAssert.AreEqual(plan.StakeTruth, loaded.StakeTruth);
			CollectionAssert.AreEqual(plan.States, loaded.States);
			loaded.States[0] = 0;
			ClassicAssert.AreEqual(2, plan.States[0]);
		}

		[Test]
		public void TamperAndOutOfOrderCheckpointAreRefused()
		{
			KingdomFoundingHeartPlan plan = Plan();
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryAdvance(plan, 1, 0, 1));
			string encoded = KingdomFoundingHeartRules.Encode(plan);
			char replacement = encoded[encoded.Length - 1] == '0' ? '1' : '0';
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryDecode(
				encoded.Substring(0, encoded.Length - 1) + replacement, out _));
			plan.States[1] = 2;
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.Valid(plan));
			ClassicAssert.IsNull(KingdomFoundingHeartRules.Encode(plan));
		}

		[Test]
		public void EveryFrozenFieldAndTheWholeCursorAreAuthenticated()
		{
			string encoded = KingdomFoundingHeartRules.Encode(Plan());
			string[] fields = encoded.Split('|');
			ClassicAssert.AreEqual("h2", fields[0]);
			ClassicAssert.AreEqual(20, fields.Length);
			for (int field = 1; field <= 18; field++)
			{
				string[] changed = (string[])fields.Clone();
				changed[field] = changed[field] + (field == 18 ? ".0" : "A");
				ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryDecode(
					string.Join("|", changed), out _), "field " + field);
			}
			string[] forgedComplete = (string[])fields.Clone();
			forgedComplete[18] = "2.2.2.2.2.2";
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryDecode(
				string.Join("|", forgedComplete), out _));
		}

		[Test]
		public void OldPartialAndTrailingEnvelopeShapesAreRejected()
		{
			string encoded = KingdomFoundingHeartRules.Encode(Plan());
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryDecode(
				encoded.Replace("h2|", "h1|"), out _));
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryDecode(encoded + "|tail", out _));
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryDecode(
				encoded.Substring(0, encoded.LastIndexOf('|')), out _));
		}

		[Test]
		public void StakeTruthIsCanonicalAndRejectsDriftedFields()
		{
			string encoded = StakeTruth();
			ClassicAssert.IsTrue(KingdomFoundingHeartStakeRules.TryDecode(encoded, out var truth));
			ClassicAssert.AreEqual("heartbasin", truth.BuildKey);
			ClassicAssert.AreEqual(3, truth.Defence);
			string[] fields = encoded.Split('|');
			for (int field = 1; field < fields.Length; field++)
			{
				string[] changed = (string[])fields.Clone();
				changed[field] += fields[field] == "0" ? "1" : "A";
				ClassicAssert.IsFalse(KingdomFoundingHeartStakeRules.TryDecode(
					string.Join("|", changed), out _), "field " + field);
			}
		}

		[Test]
		public void CompletionRequiresAllSixSettledSlots()
		{
			KingdomFoundingHeartPlan plan = Plan();
			for (int i = 0; i < KingdomFoundingHeartRules.SlotCount; i++)
			{
				ClassicAssert.IsFalse(KingdomFoundingHeartRules.Complete(plan));
				ClassicAssert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, i, 0, 1));
				ClassicAssert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, i, 1, 2));
			}
			ClassicAssert.IsTrue(KingdomFoundingHeartRules.Complete(plan));
			string seal = KingdomFoundingHeartRules.CompletionSeal(plan);
			ClassicAssert.IsNotNull(seal);
			ClassicAssert.AreEqual(seal, KingdomFoundingHeartRules.CompletionSeal(plan.Copy()));
		}

		[Test]
		public void MalformedAuthorityGeometryAndPayloadNeverMintPlan()
		{
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryCreate("BAD", "zone", 1, 1,
				0, 0, 2, 2, 0, 0, 2, 2, 0L, 1L, "payload", StakeTruth(), out _));
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryCreate(Transaction, "zone", 8, 8,
				0, 0, 2, 2, 0, 0, 2, 2, 0L, 1L, "payload", StakeTruth(), out _));
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryCreate(Transaction, "zone", 1, 1,
				0, 0, 2, 2, 0, 0, 2, 2, 0L, 1L, null, StakeTruth(), out _));
			ClassicAssert.IsFalse(KingdomFoundingHeartRules.TryCreate(Transaction, "zone", 0, 0,
				-1, 0, 2, 2, 0, 0, 2, 2, 0L, 1L, "payload", StakeTruth(), out _));
		}

		[Test]
		public void TerminalReceiptAuthenticatesEveryBindingAndReloadState()
		{
			KingdomFoundingHeartTerminalPlan terminal = Terminal();
			string encoded = KingdomFoundingHeartTerminalRules.Encode(terminal);
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryDecode(encoded, out var loaded));
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.SameBinding(terminal, loaded));
			loaded.FinalId += "foreign";
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.SameBinding(terminal, loaded));
			char changed = encoded[encoded.Length - 1] == '0' ? '1' : '0';
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.TryDecode(
				encoded.Substring(0, encoded.Length - 1) + changed, out _));
		}

		[Test]
		public void TerminalPhasesAndSinkDispositionsAreMonotoneAcrossReload()
		{
			KingdomFoundingHeartTerminalPlan terminal = Terminal();
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.OutputPrepared,
				KingdomFoundingHeartTerminalPhase.OutputSettled));
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.OutputSettled,
				KingdomFoundingHeartTerminalPhase.RemovalAttempting));
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.RemovalAttempting,
				KingdomFoundingHeartTerminalPhase.Removed));
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.Removed,
				KingdomFoundingHeartTerminalPhase.EffectsAttempting));
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvanceSink(terminal, false,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting));
			string cut = KingdomFoundingHeartTerminalRules.Encode(terminal);
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryDecode(cut, out terminal));
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvanceSink(terminal, false,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Lost));
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvanceSink(terminal, true,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting));
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvanceSink(terminal, true,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Settled));
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.EffectsAttempting,
				KingdomFoundingHeartTerminalPhase.EffectsSettled));
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.EffectsSettled,
				KingdomFoundingHeartTerminalPhase.Removed));
		}

		[Test]
		public void GraveyardRemovalProofUsesCallbackAndIdentityNotRetainedParts()
		{
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, true, false, true, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				false, true, false, true, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, false, false, true, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, true, true, true, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, true, false, false, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, true, false, true, false));
		}

		[Test]
		public void AddCutSettlesFromExactTopologyNotCallbackReturn()
		{
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.ExactAddCut(
				false, false, true, true), "throw after exact landing is resumable");
			ClassicAssert.IsTrue(KingdomFoundingHeartTerminalRules.ExactAddCut(
				true, false, true, true), "foreign return cannot override exact landed custody");
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.ExactAddCut(
				true, true, false, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartTerminalRules.ExactAddCut(
				true, true, true, false));
		}

		[Test]
		public void LegacyEffectsReceiptMakesAttemptingHonestlyLostAcrossReload()
		{
			ClassicAssert.IsTrue(KingdomPlotLegacyEffectsRules.TryCreate("final", "works", "Building",
				"watermill", "plot", "zone", 40, 12, true, false, true, out var plan));
			ClassicAssert.IsTrue(KingdomPlotLegacyEffectsRules.TryAdvance(plan, 0,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting));
			string cut = KingdomPlotLegacyEffectsRules.Encode(plan);
			ClassicAssert.IsTrue(KingdomPlotLegacyEffectsRules.TryDecode(cut, out plan));
			ClassicAssert.IsTrue(KingdomPlotLegacyEffectsRules.TryAdvance(plan, 0,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Lost));
			ClassicAssert.IsTrue(KingdomPlotLegacyEffectsRules.TryAdvance(plan, 2,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting));
			ClassicAssert.IsTrue(KingdomPlotLegacyEffectsRules.TryAdvance(plan, 2,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Settled));
			ClassicAssert.IsTrue(KingdomPlotLegacyEffectsRules.Complete(plan));
			string encoded = KingdomPlotLegacyEffectsRules.Encode(plan);
			ClassicAssert.IsFalse(KingdomPlotLegacyEffectsRules.TryDecode(encoded + "x", out _));
		}
	}
}
#endif
