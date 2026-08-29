#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFoundingHeartRulesTests
	{
		private const string Transaction = "0123456789abcdef0123456789abcdef";

		private static string StakeTruth()
		{
			Assert.IsTrue(KingdomFoundingHeartStakeRules.TryCreate("heartbasin", "first basin",
				"r_KingdomPlotWorks", 38, 11, 42, 13, 0, true, false, null,
				"TAF_HeartBasinContents", 2, true, 3, false, 40, 11, false,
				out KingdomFoundingHeartStakeTruth truth));
			return KingdomFoundingHeartStakeRules.Encode(truth);
		}

		private static KingdomFoundingHeartPlan Plan()
		{
			Assert.IsTrue(KingdomFoundingHeartRules.TryCreate(Transaction, "JoppaWorld.2.2.1.1.10",
				40, 12, 30, 2, 49, 21, 38, 11, 42, 13, 900L, 600L,
				"p4,frozen-authored-payload", StakeTruth(), out KingdomFoundingHeartPlan plan));
			return plan;
		}

		private static KingdomFoundingHeartTerminalPlan Terminal()
		{
			KingdomFoundingHeartPlan heart = Plan();
			for (int i = 0; i < KingdomFoundingHeartRules.SlotCount; i++)
			{
				Assert.IsTrue(KingdomFoundingHeartRules.TryAdvance(heart, i, 0, 1));
				Assert.IsTrue(KingdomFoundingHeartRules.TryAdvance(heart, i, 1, 2));
			}
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryCreate(heart.TransactionId,
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
			Assert.AreEqual(first.PlotId, second.PlotId);
			Assert.AreEqual(KingdomFoundingHeartRules.SlotId(first, 0),
				KingdomFoundingHeartRules.SlotId(second, 0));
			Assert.AreNotEqual(first.PlotId, KingdomFoundingHeartRules.SlotId(first, 0));
			Assert.AreNotEqual(KingdomFoundingHeartRules.SlotId(first, 0),
				KingdomFoundingHeartRules.SlotId(first, 1));
			Assert.Less(first.PlotId.Length, 128);
		}

		[Test]
		public void CodecRoundTripsFrozenTruthAndIndependentStates()
		{
			KingdomFoundingHeartPlan plan = Plan();
			Assert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, 0, 0, 1));
			Assert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, 0, 1, 2));
			Assert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, 1, 0, 1));
			string encoded = KingdomFoundingHeartRules.Encode(plan);
			Assert.IsTrue(KingdomFoundingHeartRules.TryDecode(encoded, out var loaded));
			Assert.AreEqual(plan.TransactionId, loaded.TransactionId);
			Assert.AreEqual(plan.ZoneId, loaded.ZoneId);
			Assert.AreEqual(plan.Payload, loaded.Payload);
			Assert.AreEqual(plan.StakeTruth, loaded.StakeTruth);
			CollectionAssert.AreEqual(plan.States, loaded.States);
			loaded.States[0] = 0;
			Assert.AreEqual(2, plan.States[0]);
		}

		[Test]
		public void TamperAndOutOfOrderCheckpointAreRefused()
		{
			KingdomFoundingHeartPlan plan = Plan();
			Assert.IsFalse(KingdomFoundingHeartRules.TryAdvance(plan, 1, 0, 1));
			string encoded = KingdomFoundingHeartRules.Encode(plan);
			char replacement = encoded[encoded.Length - 1] == '0' ? '1' : '0';
			Assert.IsFalse(KingdomFoundingHeartRules.TryDecode(
				encoded.Substring(0, encoded.Length - 1) + replacement, out _));
			plan.States[1] = 2;
			Assert.IsFalse(KingdomFoundingHeartRules.Valid(plan));
			Assert.IsNull(KingdomFoundingHeartRules.Encode(plan));
		}

		[Test]
		public void EveryFrozenFieldAndTheWholeCursorAreAuthenticated()
		{
			string encoded = KingdomFoundingHeartRules.Encode(Plan());
			string[] fields = encoded.Split('|');
			Assert.AreEqual("h2", fields[0]);
			Assert.AreEqual(20, fields.Length);
			for (int field = 1; field <= 18; field++)
			{
				string[] changed = (string[])fields.Clone();
				changed[field] = changed[field] + (field == 18 ? ".0" : "A");
				Assert.IsFalse(KingdomFoundingHeartRules.TryDecode(
					string.Join("|", changed), out _), "field " + field);
			}
			string[] forgedComplete = (string[])fields.Clone();
			forgedComplete[18] = "2.2.2.2.2.2";
			Assert.IsFalse(KingdomFoundingHeartRules.TryDecode(
				string.Join("|", forgedComplete), out _));
		}

		[Test]
		public void OldPartialAndTrailingEnvelopeShapesAreRejected()
		{
			string encoded = KingdomFoundingHeartRules.Encode(Plan());
			Assert.IsFalse(KingdomFoundingHeartRules.TryDecode(
				encoded.Replace("h2|", "h1|"), out _));
			Assert.IsFalse(KingdomFoundingHeartRules.TryDecode(encoded + "|tail", out _));
			Assert.IsFalse(KingdomFoundingHeartRules.TryDecode(
				encoded.Substring(0, encoded.LastIndexOf('|')), out _));
		}

		[Test]
		public void StakeTruthIsCanonicalAndRejectsDriftedFields()
		{
			string encoded = StakeTruth();
			Assert.IsTrue(KingdomFoundingHeartStakeRules.TryDecode(encoded, out var truth));
			Assert.AreEqual("heartbasin", truth.BuildKey);
			Assert.AreEqual(3, truth.Defence);
			string[] fields = encoded.Split('|');
			for (int field = 1; field < fields.Length; field++)
			{
				string[] changed = (string[])fields.Clone();
				changed[field] += fields[field] == "0" ? "1" : "A";
				Assert.IsFalse(KingdomFoundingHeartStakeRules.TryDecode(
					string.Join("|", changed), out _), "field " + field);
			}
		}

		[Test]
		public void CompletionRequiresAllSixSettledSlots()
		{
			KingdomFoundingHeartPlan plan = Plan();
			for (int i = 0; i < KingdomFoundingHeartRules.SlotCount; i++)
			{
				Assert.IsFalse(KingdomFoundingHeartRules.Complete(plan));
				Assert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, i, 0, 1));
				Assert.IsTrue(KingdomFoundingHeartRules.TryAdvance(plan, i, 1, 2));
			}
			Assert.IsTrue(KingdomFoundingHeartRules.Complete(plan));
			string seal = KingdomFoundingHeartRules.CompletionSeal(plan);
			Assert.IsNotNull(seal);
			Assert.AreEqual(seal, KingdomFoundingHeartRules.CompletionSeal(plan.Copy()));
		}

		[Test]
		public void MalformedAuthorityGeometryAndPayloadNeverMintPlan()
		{
			Assert.IsFalse(KingdomFoundingHeartRules.TryCreate("BAD", "zone", 1, 1,
				0, 0, 2, 2, 0, 0, 2, 2, 0L, 1L, "payload", StakeTruth(), out _));
			Assert.IsFalse(KingdomFoundingHeartRules.TryCreate(Transaction, "zone", 8, 8,
				0, 0, 2, 2, 0, 0, 2, 2, 0L, 1L, "payload", StakeTruth(), out _));
			Assert.IsFalse(KingdomFoundingHeartRules.TryCreate(Transaction, "zone", 1, 1,
				0, 0, 2, 2, 0, 0, 2, 2, 0L, 1L, null, StakeTruth(), out _));
			Assert.IsFalse(KingdomFoundingHeartRules.TryCreate(Transaction, "zone", 0, 0,
				-1, 0, 2, 2, 0, 0, 2, 2, 0L, 1L, "payload", StakeTruth(), out _));
		}

		[Test]
		public void TerminalReceiptAuthenticatesEveryBindingAndReloadState()
		{
			KingdomFoundingHeartTerminalPlan terminal = Terminal();
			string encoded = KingdomFoundingHeartTerminalRules.Encode(terminal);
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryDecode(encoded, out var loaded));
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.SameBinding(terminal, loaded));
			loaded.FinalId += "foreign";
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.SameBinding(terminal, loaded));
			char changed = encoded[encoded.Length - 1] == '0' ? '1' : '0';
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.TryDecode(
				encoded.Substring(0, encoded.Length - 1) + changed, out _));
		}

		[Test]
		public void TerminalPhasesAndSinkDispositionsAreMonotoneAcrossReload()
		{
			KingdomFoundingHeartTerminalPlan terminal = Terminal();
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.OutputPrepared,
				KingdomFoundingHeartTerminalPhase.OutputSettled));
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.OutputSettled,
				KingdomFoundingHeartTerminalPhase.RemovalAttempting));
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.RemovalAttempting,
				KingdomFoundingHeartTerminalPhase.Removed));
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.Removed,
				KingdomFoundingHeartTerminalPhase.EffectsAttempting));
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvanceSink(terminal, false,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting));
			string cut = KingdomFoundingHeartTerminalRules.Encode(terminal);
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryDecode(cut, out terminal));
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvanceSink(terminal, false,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Lost));
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvanceSink(terminal, true,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting));
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvanceSink(terminal, true,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Settled));
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.EffectsAttempting,
				KingdomFoundingHeartTerminalPhase.EffectsSettled));
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.TryAdvancePhase(terminal,
				KingdomFoundingHeartTerminalPhase.EffectsSettled,
				KingdomFoundingHeartTerminalPhase.Removed));
		}

		[Test]
		public void GraveyardRemovalProofUsesCallbackAndIdentityNotRetainedParts()
		{
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, true, false, true, true));
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				false, true, false, true, true));
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, false, false, true, true));
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, true, true, true, true));
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, true, false, false, true));
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.ExactRemovalTombstone(
				true, true, false, true, false));
		}

		[Test]
		public void AddCutSettlesFromExactTopologyNotCallbackReturn()
		{
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.ExactAddCut(
				false, false, true, true), "throw after exact landing is resumable");
			Assert.IsTrue(KingdomFoundingHeartTerminalRules.ExactAddCut(
				true, false, true, true), "foreign return cannot override exact landed custody");
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.ExactAddCut(
				true, true, false, true));
			Assert.IsFalse(KingdomFoundingHeartTerminalRules.ExactAddCut(
				true, true, true, false));
		}

		[Test]
		public void LegacyEffectsReceiptMakesAttemptingHonestlyLostAcrossReload()
		{
			Assert.IsTrue(KingdomPlotLegacyEffectsRules.TryCreate("final", "works", "Building",
				"watermill", "plot", "zone", 40, 12, true, false, true, out var plan));
			Assert.IsTrue(KingdomPlotLegacyEffectsRules.TryAdvance(plan, 0,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting));
			string cut = KingdomPlotLegacyEffectsRules.Encode(plan);
			Assert.IsTrue(KingdomPlotLegacyEffectsRules.TryDecode(cut, out plan));
			Assert.IsTrue(KingdomPlotLegacyEffectsRules.TryAdvance(plan, 0,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Lost));
			Assert.IsTrue(KingdomPlotLegacyEffectsRules.TryAdvance(plan, 2,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting));
			Assert.IsTrue(KingdomPlotLegacyEffectsRules.TryAdvance(plan, 2,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Settled));
			Assert.IsTrue(KingdomPlotLegacyEffectsRules.Complete(plan));
			string encoded = KingdomPlotLegacyEffectsRules.Encode(plan);
			Assert.IsFalse(KingdomPlotLegacyEffectsRules.TryDecode(encoded + "x", out _));
		}
	}
}
#endif
