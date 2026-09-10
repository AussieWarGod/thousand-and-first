#if TAF_TESTS
using System;
using System.Collections.Generic;
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
				new string('x', 513) })
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
			StringAssert.Contains("rather than a claim of fully automatic forward recovery",
				TestMain.ReadRepositoryText("CHANGELOG.md"));
			StringAssert.Contains("SAFETY POLICY, not a claim of fully automatic forward recovery",
				TestMain.ReadRepositoryText("Core/KingdomFounderOriginEngine.cs"));
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
			private int Budget = int.MaxValue;

			internal void SetTally(int value) { Tallies[Profile] = value; }
			internal bool HasTally() { return Tallies.ContainsKey(Profile); }
			internal int Tally() { int v; return Tallies.TryGetValue(Profile, out v) ? v : 0; }
			internal void Cut(int writes) { Budget = writes - 1; }

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
				public string CityId { get { return City; } }

				private void Spend()
				{
					if (Where.Budget-- <= 0) throw new PowerCut();
				}

				public bool HasReceipt() { return Where.HasReceipt(Id); }
				public string RawReceipt() { return Where.Receipt(Id); }
				public void WriteReceipt(string Wire) { Spend(); Where.PutReceipt(Id, Wire); }
				public bool HasOrigin() { return Where.HasOrigin(Id); }
				public string RawOrigin() { return Where.Origin(Id); }
				public void WriteOrigin(string Origin) { Spend(); Where.PutOrigin(Id, Origin); }

				public bool TryTally(string Profile, out int Count)
				{
					return Where.Tallies.TryGetValue(Profile, out Count);
				}

				public void WriteTally(string Profile, int Count)
				{
					Spend();
					Where.Tallies[Profile] = Count;
				}
			}
		}
	}
}
#endif
