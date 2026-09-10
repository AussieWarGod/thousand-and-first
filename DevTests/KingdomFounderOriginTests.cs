#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The founder origin accounting, driven as a running adapter rather than read as source.
	/// <para>
	/// The settlement's per-profile origin tally is a SHARED aggregate: ordinary arrivals raise the
	/// same dictionary inside their own before/after protocol, and a founding cohort can be seeded
	/// long after some of them arrived. So the whole of this fixture is about one question — can
	/// this transaction add exactly one, exactly once, without ever assuming the tally is its own?
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomFounderOriginTests
	{
		private const string Profile = "marsh";
		private const string City = "JoppaWorld.8.22.1.1.10";

		// --- the arithmetic the defect got wrong -------------------------------------------------

		[Test]
		public void AnEmptyTallyPlusFourFoundersIsFour()
		{
			World world = new World();
			for (int i = 0; i < 4; i++)
				ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied,
					world.Account("founder-" + i));
			ClassicAssert.AreEqual(4, world.Tally());
		}

		[Test]
		public void FivePriorCitizensPlusFourFoundersIsNineAndNotFive()
		{
			// The defect this replaces set the tally to max(recorded, founders marked), so five
			// unrelated citizens plus four founders stayed five.
			World world = new World();
			world.SetTally(5);
			for (int i = 0; i < 4; i++)
				ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied,
					world.Account("founder-" + i));
			ClassicAssert.AreEqual(9, world.Tally());
		}

		[Test]
		public void ARepeatedFounderAndARepeatedCohortNeverIncrementTwice()
		{
			World world = new World();
			world.SetTally(2);
			for (int pass = 0; pass < 3; pass++)
				for (int i = 0; i < 4; i++)
				{
					KingdomFounderOriginOutcome outcome = world.Account("founder-" + i);
					ClassicAssert.AreEqual(pass == 0 ? KingdomFounderOriginOutcome.Applied
						: KingdomFounderOriginOutcome.AlreadySettled, outcome,
						"pass " + pass + " founder " + i);
				}
			ClassicAssert.AreEqual(6, world.Tally());
		}

		[Test]
		public void OneBodyNamedTwiceInACohortIsCountedOnce()
		{
			World world = new World();
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, world.Account("twin"));
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.AlreadySettled,
				world.Account("twin"));
			ClassicAssert.AreEqual(1, world.Tally());
		}

		// --- interruption at every seam ----------------------------------------------------------

		[Test]
		public void AnInterruptionBeforePreparationLeavesNothingAndResumesExactly()
		{
			World world = new World();
			world.SetTally(3);
			world.Cut(1);                       // the preparation write itself never lands
			ClassicAssert.IsTrue(world.Interrupted("f"));
			ClassicAssert.IsFalse(world.HasReceipt("f"));
			ClassicAssert.IsFalse(world.HasOrigin("f"));
			ClassicAssert.AreEqual(3, world.Tally());
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, world.Account("f"));
			ClassicAssert.AreEqual(4, world.Tally());
		}

		[Test]
		public void AnInterruptionAfterPreparationResumesAndCountsExactlyOnce()
		{
			World world = new World();
			world.SetTally(3);
			world.Cut(2);                       // prepared, then the origin write never lands
			ClassicAssert.IsTrue(world.Interrupted("f"));
			ClassicAssert.IsTrue(world.HasReceipt("f"));
			ClassicAssert.IsFalse(world.HasOrigin("f"));
			ClassicAssert.AreEqual(3, world.Tally());
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, world.Account("f"));
			ClassicAssert.AreEqual(4, world.Tally());
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.AlreadySettled, world.Account("f"));
			ClassicAssert.AreEqual(4, world.Tally());
		}

		[Test]
		public void AnInterruptionBetweenTheOriginAndTheCounterRefusesInTheOpenAndNeverGuesses()
		{
			// The label is written immediately before the increment, so its presence proves
			// nothing about the counter. Nothing in the world can now say whether this founder was
			// counted, and the tally cannot be asked - an unrelated arrival moves it too. The
			// honest answer is a visible refusal, not a guess in either direction.
			World world = new World();
			world.SetTally(3);
			world.Cut(3);                       // prepared, origin written, counter never lands
			ClassicAssert.IsTrue(world.Interrupted("f"));
			ClassicAssert.IsTrue(world.HasOrigin("f"));
			ClassicAssert.AreEqual(3, world.Tally());
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(3, world.Tally(), "a refusal never counts");
			// Terminal, and said exactly once: a later attempt is silent.
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.AlreadyQuarantined,
				world.Account("f"));
			ClassicAssert.AreEqual(3, world.Tally());
		}

		[Test]
		public void AnInterruptionAfterTheCounterNeverCountsASecondTime()
		{
			World world = new World();
			world.SetTally(3);
			world.Cut(4);                       // prepared, origin, counter, completion lost
			ClassicAssert.IsTrue(world.Interrupted("f"));
			ClassicAssert.AreEqual(4, world.Tally(), "the increment did land");
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(4, world.Tally(), "and is never applied a second time");
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.AlreadyQuarantined,
				world.Account("f"));
			ClassicAssert.AreEqual(4, world.Tally());
		}

		[Test]
		public void AnInterruptionAfterCompletionIsSettledAndSilent()
		{
			World world = new World();
			world.SetTally(3);
			// Four writes is the whole transaction; a cut placed past the last one never fires,
			// which is exactly the "interrupted after completion publication" case.
			world.Cut(5);
			ClassicAssert.IsFalse(world.Interrupted("f"), "the fifth write does not exist");
			ClassicAssert.AreEqual(4, world.Tally());
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.AlreadySettled, world.Account("f"));
			ClassicAssert.AreEqual(4, world.Tally());
		}

		// --- the shared aggregate ---------------------------------------------------------------

		[Test]
		public void AnUnrelatedArrivalBetweenAttemptsIsAddedToRatherThanOverwritten()
		{
			World world = new World();
			world.SetTally(5);
			world.Cut(2);                                 // prepared with a before image of five
			ClassicAssert.IsTrue(world.Interrupted("f"));
			world.SetTally(6);                            // somebody else walks in and is counted
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, world.Account("f"));
			ClassicAssert.AreEqual(7, world.Tally(), "six plus this founder, not back to six");
		}

		[Test]
		public void ACoincidentalAfterCountNeverAuthorizesCompletion()
		{
			// The nastiest shape there is: the tally now reads EXACTLY what a completed attempt
			// would have left, but it reads that because somebody else arrived. If counter equality
			// were allowed to authorise completion, this founder would silently never be counted.
			World world = new World();
			world.SetTally(3);
			world.Cut(3);                                 // prepared(before=3), origin written
			ClassicAssert.IsTrue(world.Interrupted("f"));
			world.SetTally(4);                            // an arrival makes it the planned after
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(4, world.Tally(), "no silent completion, and no silent count");
		}

		[Test]
		public void ACoincidentalAfterCountBeforeTheLabelIsStillAddedTo()
		{
			// Same coincidence, but the label is absent, which independently proves the increment
			// never happened. Here replay is authorised, and it re-freezes against the NEW tally.
			World world = new World();
			world.SetTally(3);
			world.Cut(2);                                 // prepared(before=3), no origin written
			ClassicAssert.IsTrue(world.Interrupted("f"));
			world.SetTally(4);
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, world.Account("f"));
			ClassicAssert.AreEqual(5, world.Tally());
		}

		[Test]
		public void ATallyThatMovesBetweenPreparationAndItsIncrementIsReProvedAndRefused()
		{
			// The before image is re-proved immediately before the mutation, so the last thing read
			// before the write is the thing the write depends on. An arrival that lands in that
			// instant makes the frozen image untrue, and an untrue image is refused rather than
			// written through.
			World world = new World();
			world.SetTally(5);
			world.MeddleAfter(1);           // somebody arrives the moment the obligation is taken
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(6, world.Tally(), "the arrival stands; the founder is refused");
			ClassicAssert.IsFalse(world.HasOrigin("f"), "and no label was written on a bad image");
		}

		// --- foreign, corrupt and conflicting readings -------------------------------------------

		[Test]
		public void AReceiptNamingAnotherBodyProfileOrCityIsRefusedAndLeftAlone()
		{
			foreach (string[] foreign in new[]
			{
				new[] { "somebody-else", Profile, City },
				new[] { "f", "canyon", City },
				new[] { "f", Profile, "JoppaWorld.14.17.1.1.10" }
			})
			{
				World world = new World();
				string wire = KingdomFounderOriginCodec.Encode(new KingdomFounderOriginReceipt(
					foreign[0], foreign[1], foreign[2], 0,
					KingdomFounderOriginState.Completed));
				ClassicAssert.IsNotNull(wire);
				world.PutReceipt("f", wire);
				ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined,
					world.Account("f"), foreign[0] + "/" + foreign[1] + "/" + foreign[2]);
				ClassicAssert.AreEqual(0, world.Tally());
				// Somebody else's obligation is not overwritten to make this attempt tidy.
				ClassicAssert.AreEqual(wire, world.Receipt("f"));
			}
		}

		[TestCase("")]
		[TestCase("not a receipt at all")]
		[TestCase("fo1|x|y|z")]
		[TestCase("fo2|dGVzdA==|dGVzdA==|dGVzdA==|0|1|00")]
		public void AMalformedOrWrongShapedReceiptIsRefusedAndRecordedTerminal(string wire)
		{
			World world = new World();
			world.SetTally(2);
			world.PutReceipt("f", wire);
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(2, world.Tally());
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.AlreadyQuarantined,
				world.Account("f"));
		}

		[Test]
		public void AnExplicitlyEmptyOriginIsNotTheSameFactAsAMissingOne()
		{
			World missing = new World();
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, missing.Account("f"));
			ClassicAssert.AreEqual(1, missing.Tally());

			World empty = new World();
			empty.PutOrigin("f", "");
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, empty.Account("f"));
			ClassicAssert.AreEqual(0, empty.Tally());
		}

		[Test]
		public void AConflictingExistingOriginIsRefused()
		{
			World world = new World();
			world.PutOrigin("f", "canyon");
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(0, world.Tally());
			ClassicAssert.AreEqual("canyon", world.Origin("f"), "a foreign label is not rewritten");
		}

		[TestCase(-1)]
		[TestCase(int.MinValue)]
		public void ACorruptNegativeTallyIsRefusedRatherThanAddedTo(int corrupt)
		{
			World world = new World();
			world.SetTally(corrupt);
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(corrupt, world.Tally());
		}

		[Test]
		public void ATallyAtItsCeilingIsRefusedBeforeTheAdditionRatherThanWrapped()
		{
			World world = new World();
			world.SetTally(int.MaxValue);
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(int.MaxValue, world.Tally());
			World nearly = new World();
			nearly.SetTally(int.MaxValue - 1);
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, nearly.Account("f"));
			ClassicAssert.AreEqual(int.MaxValue, nearly.Tally());
		}

		[Test]
		public void AnAbsentTallyEntryIsZeroAndIsCreatedByTheFirstFounder()
		{
			World world = new World();
			ClassicAssert.IsFalse(world.HasTally());
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, world.Account("f"));
			ClassicAssert.IsTrue(world.HasTally());
			ClassicAssert.AreEqual(1, world.Tally());
		}

		[Test]
		public void TheAccountingNeverTouchesAnotherProfilesTally()
		{
			World world = new World();
			world.SetTally(2);
			world.Tallies["canyon"] = 7;
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, world.Account("f"));
			ClassicAssert.AreEqual(3, world.Tally());
			ClassicAssert.AreEqual(7, world.Tallies["canyon"]);
		}

		[TestCase("receipt")]
		[TestCase("origin")]
		public void ANameHeldInTheNumberTableIsNotAbsentAndStopsEverything(string which)
		{
			// Text and number properties are two separate tables under one namespace. Asking only
			// the text table would read a number-table collision as "absent", write this
			// accounting's own value beside it, and count the founder a second time.
			string name = which == "receipt" ? KingdomFounderOriginCodec.ReceiptProperty
				: "KingdomOrigin";
			World world = new World();
			world.SetTally(4);
			world.PutNumber("f", name, 7);
			ClassicAssert.AreEqual(KingdomFounderPropertyShape.Number, world.Shape("f", name));
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(4, world.Tally(), "nothing is counted");
			ClassicAssert.IsFalse(world.HasOrigin("f"), "and no label is written");
			// The wrong-typed value IS the evidence. It is left exactly where it stands, and this
			// accounting does not even write its own terminal record beside it.
			ClassicAssert.IsTrue(world.HasNumber("f", name));
			ClassicAssert.IsFalse(world.HasReceipt("f"));
		}

		[TestCase("receipt")]
		[TestCase("origin")]
		public void ANameHeldInBothTablesIsAmbiguousAndStopsEverything(string which)
		{
			string name = which == "receipt" ? KingdomFounderOriginCodec.ReceiptProperty
				: "KingdomOrigin";
			World world = new World();
			world.SetTally(4);
			world.PutNumber("f", name, 7);
			if (which == "receipt")
			{
				string wire = KingdomFounderOriginCodec.Encode(new KingdomFounderOriginReceipt(
					"f", Profile, City, 4, KingdomFounderOriginState.Completed));
				world.PutReceipt("f", wire);
			}
			else world.PutOrigin("f", Profile);
			ClassicAssert.AreEqual(KingdomFounderPropertyShape.Both, world.Shape("f", name));
			// Even a perfectly good completed receipt does not settle anything while the same name
			// also stands in the other table: which of the two is the proof cannot be said.
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"));
			ClassicAssert.AreEqual(4, world.Tally());
			ClassicAssert.IsTrue(world.HasNumber("f", name), "the evidence is retained");
		}

		[Test]
		public void TwoIncarnationsOnTheSameGroundCannotShareOneProof()
		{
			// The obligation binds the canonical settlement identity, not the ground it stands on.
			// A later incarnation seated on the same first-claimed zone is a different settlement
			// with its own tally, and one incarnation's completed proof must not silence the
			// other's obligation.
			World world = new World();
			world.Settlement = "settlement-1";
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, world.Account("f"));
			ClassicAssert.AreEqual(1, world.Tally());
			world.Settlement = "settlement-2";
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, world.Account("f"),
				"the first incarnation's proof is not this one's");
			ClassicAssert.AreEqual(1, world.Tally());
		}

		// --- the wire ----------------------------------------------------------------------------

		[Test]
		public void TheObligationWireRoundTripsAndRefusesEveryShapeItDidNotMint()
		{
			KingdomFounderOriginReceipt receipt = new KingdomFounderOriginReceipt("body", Profile,
				City, 41, KingdomFounderOriginState.Prepared);
			string wire = KingdomFounderOriginCodec.Encode(receipt);
			ClassicAssert.IsNotNull(wire);
			ClassicAssert.AreEqual(7, wire.Split('|').Length);
			StringAssert.StartsWith("fo1|", wire);
			ClassicAssert.IsTrue(KingdomFounderOriginCodec.TryDecode(wire, out var read));
			ClassicAssert.AreEqual("body", read.BodyId);
			ClassicAssert.AreEqual(41, read.Before);
			ClassicAssert.AreEqual(KingdomFounderOriginState.Prepared, read.State);
			ClassicAssert.AreEqual(wire, KingdomFounderOriginCodec.Encode(read));

			char swap = wire[wire.Length - 1] == '0' ? '1' : '0';
			ClassicAssert.IsFalse(KingdomFounderOriginCodec.TryDecode(
				wire.Substring(0, wire.Length - 1) + swap, out _), "a bad digest");
			ClassicAssert.IsFalse(KingdomFounderOriginCodec.TryDecode(wire + "|x", out _));
			ClassicAssert.IsFalse(KingdomFounderOriginCodec.TryDecode("fo2" + wire.Substring(3),
				out _), "a foreign tag");
			ClassicAssert.IsFalse(KingdomFounderOriginCodec.TryDecode(null, out _));
			// A hand-built wire whose base64 carries bytes that are not valid UTF-8, WITH a freshly
			// computed digest over the malformed body. Without recomputing, this would only prove
			// the digest check works; with it, the decoder's own strict reading is what refuses.
			string malformed = "fo1|" + Convert.ToBase64String(new byte[] { 0xC3, 0x28 })
				+ "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Profile))
				+ "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(City)) + "|0|1";
			ClassicAssert.IsFalse(KingdomFounderOriginCodec.TryDecode(
				malformed + "|" + Sha256Hex(malformed), out _));
			// And the digest guard itself still works on the same body.
			ClassicAssert.IsFalse(KingdomFounderOriginCodec.TryDecode(malformed + "|deadbeef",
				out _));
		}

		[TestCase(0xD800)]
		[TestCase(0xDBFF)]
		[TestCase(0xDC00)]
		[TestCase(0xDFFF)]
		public void ARawWireCarryingALoneSurrogateIsRefusedAndNeverThrows(int codeUnit)
		{
			// Built at RUNTIME from an integer: a literal surrogate written into a [TestCase]
			// attribute is normalised through attribute metadata into U+FFFD, so the test would
			// pass while proving nothing about the case it names.
			string lone = new string((char)codeUnit, 1);
			ClassicAssert.IsTrue(char.IsSurrogate(lone[0]), "the fixture really is a surrogate");
			// Hashing is itself an encoding step, so the digest must be taken inside the refusal
			// boundary: a decoder that throws on a malformed reading cannot be asked about one.
			string wire = "fo1|" + lone + "|cA==|Yw==|0|1|bad";
			bool refused = false;
			Assert.DoesNotThrow(() =>
				refused = !KingdomFounderOriginCodec.TryDecode(wire, out _));
			ClassicAssert.IsTrue(refused);
			// The same lone surrogate as an identity is refused by Encode before it is written.
			ClassicAssert.IsNull(KingdomFounderOriginCodec.Encode(
				new KingdomFounderOriginReceipt(lone, Profile, City, 0,
					KingdomFounderOriginState.Prepared)));
			ClassicAssert.IsNull(KingdomFounderOriginCodec.Encode(
				new KingdomFounderOriginReceipt("lead" + lone + "trail", Profile, City, 0,
					KingdomFounderOriginState.Prepared)));
		}

		[TestCase(0x09)]
		[TestCase(0x0A)]
		[TestCase(0x0D)]
		[TestCase(0x00)]
		[TestCase(0x1B)]
		[TestCase(0x7F)]
		public void AControlCharacterIsRefusedByPolicyRatherThanByEncoding(int codeUnit)
		{
			// A control round-trips through UTF-8 perfectly well and collides with nothing; it is
			// refused because a pipe-delimited, line-oriented wire is no place to smuggle one, and
			// because an object id carrying one is not an id this settlement minted.
			string control = new string((char)codeUnit, 1);
			ClassicAssert.IsTrue(char.IsControl(control[0]));
			ClassicAssert.AreEqual(control,
				Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(control)),
				"a control is encodable; this is a policy refusal, not an encoding one");
			ClassicAssert.IsNull(KingdomFounderOriginCodec.Encode(
				new KingdomFounderOriginReceipt("body" + control, Profile, City, 0,
					KingdomFounderOriginState.Prepared)));
		}

		[Test]
		public void TheObligationRefusesEveryUnboundedOrOutOfRangeField()
		{
			ClassicAssert.IsNull(KingdomFounderOriginCodec.Encode(
				new KingdomFounderOriginReceipt("body", Profile, City, -1,
					KingdomFounderOriginState.Prepared)), "a negative before count");
			ClassicAssert.IsNull(KingdomFounderOriginCodec.Encode(
				new KingdomFounderOriginReceipt("body", Profile, City, 0,
					(KingdomFounderOriginState)9)), "an unknown state");
			foreach (string bad in new[] { null, "", "   ", "has|pipe", "has\nnewline",
				new string('x', 513),
				// Root review: strict UTF-8. The replacement-fallback encoder turns EVERY one of
				// these into U+FFFD, so two different bad identities would encode to the same
				// bytes and the same digest - an identity that cannot be told from another one.
				"has\ttab", "\ud800", "\udc00", "lead\ud800trail", "a\u0001b", "\u007f" })
			{
				ClassicAssert.IsNull(KingdomFounderOriginCodec.Encode(
					new KingdomFounderOriginReceipt(bad, Profile, City, 0,
						KingdomFounderOriginState.Prepared)), "body " + (bad ?? "null"));
				ClassicAssert.IsNull(KingdomFounderOriginCodec.Encode(
					new KingdomFounderOriginReceipt("body", bad, City, 0,
						KingdomFounderOriginState.Prepared)), "profile " + (bad ?? "null"));
				ClassicAssert.IsNull(KingdomFounderOriginCodec.Encode(
					new KingdomFounderOriginReceipt("body", Profile, bad, 0,
						KingdomFounderOriginState.Prepared)), "city " + (bad ?? "null"));
			}
			ClassicAssert.IsTrue(KingdomFounderOriginCodec.Valid(
				new KingdomFounderOriginReceipt("body", Profile, City, int.MaxValue,
					KingdomFounderOriginState.Completed)));
		}

		[Test]
		public void TheAccountingPropertyIsNamespacedVersionedAndOwned()
		{
			ClassicAssert.AreEqual("r_TAF_FounderOriginAccount_v1",
				KingdomFounderOriginCodec.ReceiptProperty);
			ClassicAssert.IsTrue(KingdomRemovalCoverage.IsOwnedObjectProperty(
				KingdomFounderOriginCodec.ReceiptProperty));
		}

		[Test]
		public void ACompletedReceiptMustProveItsBindingOnEveryRead()
		{
			// A completed receipt is the ONLY thing that forbids a second increment, so it is
			// never taken on its state alone: it must name this body, this profile and this city
			// on every single read, or it is somebody else's proof and proves nothing here.
			World world = new World();
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Applied, world.Account("f"));
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.AlreadySettled, world.Account("f"));
			string mine = world.Receipt("f");
			ClassicAssert.IsTrue(KingdomFounderOriginCodec.TryDecode(mine, out var read));
			ClassicAssert.AreEqual(KingdomFounderOriginState.Completed, read.State);
			ClassicAssert.IsTrue(read.Binds("f", Profile, City));
			ClassicAssert.IsFalse(read.Binds("g", Profile, City));
			ClassicAssert.IsFalse(read.Binds("f", "canyon", City));
			ClassicAssert.IsFalse(read.Binds("f", Profile, "elsewhere"));
			// Move that exact completed receipt onto another body: it does not settle them.
			World other = new World();
			other.PutReceipt("g", mine);
			ClassicAssert.AreEqual(KingdomFounderOriginOutcome.Quarantined, other.Account("g"));
			ClassicAssert.AreEqual(0, other.Tally());
		}

		[Test]
		public void TheGuidesSayThisIsASafetyPolicyAndNotAutomaticRecovery()
		{
			// The refusal is a policy the player is owed a straight account of, not a bug and not
			// a promise that everything recovers by itself.
			string quickstart = TestMain.ReadRepositoryText("docs/QUICKSTART.md");
			StringAssert.Contains("deliberate safety policy, not a promise of fully automatic "
				+ "recovery", quickstart);
			StringAssert.Contains("five people who were already here plus four founders is nine",
				quickstart);
			StringAssert.Contains("of fully automatic forward recovery: the world stays playable",
				TestMain.ReadRepositoryText("CHANGELOG.md"));
			// And it does not overclaim in the other direction either: an unresolved accounting is
			// not a proven shortfall, because an interruption after the increment retains it.
			StringAssert.Contains("It is not declared SHORT",
				TestMain.ReadRepositoryText("CHANGELOG.md"));
			StringAssert.Contains("it does not say the tally is short", quickstart);
			StringAssert.Contains("SAFETY POLICY, not a claim of fully automatic forward recovery",
				TestMain.ReadRepositoryText("Core/KingdomFounderOriginEngine.cs"));
		}

		private static string Sha256Hex(string value)
		{
			byte[] digest;
			using (System.Security.Cryptography.SHA256 sha
				= System.Security.Cryptography.SHA256.Create())
				digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
			StringBuilder text = new StringBuilder(64);
			foreach (byte piece in digest)
				text.Append(piece.ToString("x2",
					System.Globalization.CultureInfo.InvariantCulture));
			return text.ToString();
		}

		// --- the fake world ----------------------------------------------------------------------

		/// <summary>
		/// A settlement and a body, with nothing in them but the exact facts the seam exposes:
		/// property presence, property text, and one per-profile counter. Writes are counted so a
		/// test can cut the power at any seam, and nothing here dispatches, exactly as the live
		/// adapter's two <c>SetStringProperty</c> calls and dictionary assignment do not.
		/// </summary>
		private sealed class World
		{
			internal readonly Dictionary<string, int> Tallies = new Dictionary<string, int>();
			private readonly Dictionary<string, Dictionary<string, string>> Bodies
				= new Dictionary<string, Dictionary<string, string>>();
			// The engine keeps text and number properties in two separate tables under one
			// namespace, so the fake body does too. A test can put a name in either, or in both.
			private readonly Dictionary<string, Dictionary<string, int>> Numbers
				= new Dictionary<string, Dictionary<string, int>>();
			internal string Settlement = City;
			private int Budget = int.MaxValue;
			private int MeddleAt = int.MaxValue;
			private int Writes;

			internal void SetTally(int value) { Tallies[Profile] = value; }
			internal bool HasTally() { return Tallies.ContainsKey(Profile); }
			internal int Tally() { int v; return Tallies.TryGetValue(Profile, out v) ? v : 0; }
			internal void Cut(int writes) { Budget = writes - 1; }

			/// <summary>An unrelated arrival is counted the instant after the given write, so the
			/// shared tally moves underneath an attempt that is already in flight.</summary>
			internal void MeddleAfter(int writes) { MeddleAt = writes; }

			private void Wrote()
			{
				if (++Writes != MeddleAt) return;
				int held;
				Tallies[Profile] = (Tallies.TryGetValue(Profile, out held) ? held : 0) + 1;
			}

			private Dictionary<string, string> Body(string id)
			{
				Dictionary<string, string> body;
				if (!Bodies.TryGetValue(id, out body))
				{
					body = new Dictionary<string, string>();
					Bodies[id] = body;
				}
				return body;
			}

			internal void PutReceipt(string id, string wire)
			{
				Body(id)[KingdomFounderOriginCodec.ReceiptProperty] = wire;
			}

			internal void PutOrigin(string id, string origin) { Body(id)["KingdomOrigin"] = origin; }

			private Dictionary<string, int> Number(string id)
			{
				Dictionary<string, int> body;
				if (!Numbers.TryGetValue(id, out body))
				{
					body = new Dictionary<string, int>();
					Numbers[id] = body;
				}
				return body;
			}

			/// <summary>Puts a name in the NUMBER table, where a text-only question cannot see it.
			/// </summary>
			internal void PutNumber(string id, string name, int value) { Number(id)[name] = value; }

			internal bool HasNumber(string id, string name) { return Number(id).ContainsKey(name); }

			internal KingdomFounderPropertyShape Shape(string id, string name)
			{
				bool text = Body(id).ContainsKey(name);
				bool number = Number(id).ContainsKey(name);
				if (text && number) return KingdomFounderPropertyShape.Both;
				if (number) return KingdomFounderPropertyShape.Number;
				return text ? KingdomFounderPropertyShape.Text
					: KingdomFounderPropertyShape.Absent;
			}

			internal bool HasReceipt(string id)
			{
				return Body(id).ContainsKey(KingdomFounderOriginCodec.ReceiptProperty);
			}

			internal bool HasOrigin(string id) { return Body(id).ContainsKey("KingdomOrigin"); }

			internal string Receipt(string id)
			{
				string v;
				return Body(id).TryGetValue(KingdomFounderOriginCodec.ReceiptProperty, out v)
					? v : null;
			}

			internal string Origin(string id)
			{
				string v;
				return Body(id).TryGetValue("KingdomOrigin", out v) ? v : null;
			}

			internal KingdomFounderOriginOutcome Account(string id)
			{
				return KingdomFounderOriginEngine.Account(new Host(this, id), Profile, out _);
			}

			/// <summary>Runs one attempt with the power cut at the configured write, and reports
			/// that the cut actually happened.</summary>
			internal bool Interrupted(string id)
			{
				try
				{
					KingdomFounderOriginEngine.Account(new Host(this, id), Profile, out _);
				}
				catch (PowerCut)
				{
					Budget = int.MaxValue;
					return true;
				}
				Budget = int.MaxValue;
				return false;
			}

			private sealed class PowerCut : Exception { }

			private sealed class Host : IKingdomFounderOriginHost
			{
				private readonly World Where;
				private readonly string Id;

				internal Host(World where, string id) { Where = where; Id = id; }

				public string BodyId { get { return Id; } }
				public string CityId { get { return Where.Settlement; } }

				private void Spend()
				{
					if (Where.Budget-- <= 0) throw new PowerCut();
				}

				private void Wrote() { Where.Wrote(); }

				public KingdomFounderPropertyShape ReceiptShape()
				{
					return Where.Shape(Id, KingdomFounderOriginCodec.ReceiptProperty);
				}
				public string RawReceipt() { return Where.Receipt(Id); }
				public void WriteReceipt(string Wire)
				{
					Spend();
					Where.PutReceipt(Id, Wire);
					Wrote();
				}
				public KingdomFounderPropertyShape OriginShape()
				{
					return Where.Shape(Id, "KingdomOrigin");
				}
				public string RawOrigin() { return Where.Origin(Id); }
				public void WriteOrigin(string Origin)
				{
					Spend();
					Where.PutOrigin(Id, Origin);
					Wrote();
				}

				public bool TryTally(string Profile, out int Count)
				{
					return Where.Tallies.TryGetValue(Profile, out Count);
				}

				public void WriteTally(string Profile, int Count)
				{
					Spend();
					Where.Tallies[Profile] = Count;
					Wrote();
				}
			}
		}
	}
}
#endif
