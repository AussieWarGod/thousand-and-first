#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class ExileRulesTests
	{
		[Test]
		public void PublicExileEnumMetadataIsFrozen()
		{
			Assert.AreEqual("ThousandAndFirst.RealmRegard", typeof(RealmRegard).FullName);
			Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(RealmRegard)));
			Assert.AreEqual(0, (int)RealmRegard.Beloved);
			Assert.AreEqual(1, (int)RealmRegard.Trusted);
			Assert.AreEqual(2, (int)RealmRegard.Doubted);
			Assert.AreEqual(3, (int)RealmRegard.Resented);
			Assert.AreEqual(4, (int)RealmRegard.Repudiated);
			Assert.AreEqual("ThousandAndFirst.RegardStep", typeof(RegardStep).FullName);
			Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(RegardStep)));
			Assert.AreEqual(0, (int)RegardStep.Nothing);
			Assert.AreEqual(1, (int)RegardStep.Murmur);
			Assert.AreEqual(2, (int)RegardStep.Warning);
			Assert.AreEqual(3, (int)RegardStep.Expulsion);
			Assert.AreEqual("ThousandAndFirst.ExileVerdict", typeof(ExileVerdict).FullName);
			Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(ExileVerdict)));
			Assert.AreEqual(0, (int)ExileVerdict.Warranted);
			Assert.AreEqual(1, (int)ExileVerdict.NothingFounded);
			Assert.AreEqual(2, (int)ExileVerdict.AlreadyCastOut);
			Assert.AreEqual(3, (int)ExileVerdict.RegardHolds);
			Assert.AreEqual("ThousandAndFirst.ReturnVerdict", typeof(ReturnVerdict).FullName);
			Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(ReturnVerdict)));
			Assert.AreEqual(0, (int)ReturnVerdict.Allowed);
			Assert.AreEqual(1, (int)ReturnVerdict.NeverCastOut);
			Assert.AreEqual(2, (int)ReturnVerdict.FoundedAgain);
			Assert.AreEqual(3, (int)ReturnVerdict.NothingRemembered);
			Assert.AreEqual(4, (int)ReturnVerdict.NotOnTheirGround);
			Assert.AreEqual(5, (int)ReturnVerdict.RegardTooLow);
		}

		// Every boundary is tested from both sides. The ladder copies four vanilla reputation
		// thresholds by value, so an off-by-one here is a silent behaviour change in play; the
		// live half of the same check (that the copies still equal RuleSettings) is asserted by
		// kingdom:selftest, which is the only place the vanilla constants exist.
		[TestCase(2000, RealmRegard.Beloved)]
		[TestCase(700, RealmRegard.Beloved)]
		[TestCase(600, RealmRegard.Beloved)]
		[TestCase(599, RealmRegard.Trusted)]
		[TestCase(250, RealmRegard.Trusted)]
		[TestCase(249, RealmRegard.Doubted)]
		[TestCase(0, RealmRegard.Doubted)]
		[TestCase(-249, RealmRegard.Doubted)]
		[TestCase(-250, RealmRegard.Resented)]
		[TestCase(-599, RealmRegard.Resented)]
		[TestCase(-600, RealmRegard.Repudiated)]
		[TestCase(-601, RealmRegard.Repudiated)]
		[TestCase(-32000, RealmRegard.Repudiated)]
		public void ClassifyRegard(int regard, RealmRegard expected)
		{
			Assert.AreEqual(expected, KingdomExileRules.ClassifyRegard(regard));
		}

		// Mending things properly re-arms the ladder; anything short of that only ever remembers
		// the worst. If this returned Current unconditionally the murmur would repeat forever on
		// jitter; if it returned Spoken unconditionally the ladder could never speak twice.
		[TestCase(RealmRegard.Beloved, RealmRegard.Repudiated, RealmRegard.Beloved)]
		[TestCase(RealmRegard.Trusted, RealmRegard.Resented, RealmRegard.Trusted)]
		[TestCase(RealmRegard.Doubted, RealmRegard.Resented, RealmRegard.Resented)]
		[TestCase(RealmRegard.Doubted, RealmRegard.Beloved, RealmRegard.Doubted)]
		[TestCase(RealmRegard.Resented, RealmRegard.Doubted, RealmRegard.Resented)]
		[TestCase(RealmRegard.Resented, RealmRegard.Resented, RealmRegard.Resented)]
		[TestCase(RealmRegard.Repudiated, RealmRegard.Doubted, RealmRegard.Repudiated)]
		public void RememberedRegard(RealmRegard current, RealmRegard spoken, RealmRegard expected)
		{
			Assert.AreEqual(expected, KingdomExileRules.RememberedRegard(current, spoken));
		}

		[TestCase(RealmRegard.Beloved, RealmRegard.Beloved, RegardStep.Nothing)]
		[TestCase(RealmRegard.Trusted, RealmRegard.Beloved, RegardStep.Nothing)]
		[TestCase(RealmRegard.Doubted, RealmRegard.Beloved, RegardStep.Murmur)]
		[TestCase(RealmRegard.Doubted, RealmRegard.Doubted, RegardStep.Nothing)]
		[TestCase(RealmRegard.Doubted, RealmRegard.Resented, RegardStep.Nothing)]
		[TestCase(RealmRegard.Resented, RealmRegard.Doubted, RegardStep.Warning)]
		[TestCase(RealmRegard.Resented, RealmRegard.Beloved, RegardStep.Warning)]
		[TestCase(RealmRegard.Resented, RealmRegard.Resented, RegardStep.Nothing)]
		[TestCase(RealmRegard.Repudiated, RealmRegard.Repudiated, RegardStep.Expulsion)]
		[TestCase(RealmRegard.Repudiated, RealmRegard.Beloved, RegardStep.Expulsion)]
		public void JudgeRegardStep(RealmRegard current, RealmRegard spoken, RegardStep expected)
		{
			Assert.AreEqual(expected, KingdomExileRules.JudgeRegardStep(current, spoken, AlreadyCastOut: false));
		}

		/// <summary>
		/// A realm that already put someone out has nothing further to say about them, including
		/// putting them out again.
		/// </summary>
		[TestCase(RealmRegard.Repudiated)]
		[TestCase(RealmRegard.Resented)]
		[TestCase(RealmRegard.Doubted)]
		public void JudgeRegardStepSaysNothingOnceCastOut(RealmRegard current)
		{
			Assert.AreEqual(RegardStep.Nothing, KingdomExileRules.JudgeRegardStep(current, RealmRegard.Beloved, AlreadyCastOut: true));
		}

		/// <summary>
		/// The whole ladder as a founder walks down it and back up: each rung speaks exactly once,
		/// jitter across a threshold is silent, and only mending it to Trusted lets the same rung
		/// speak again. A single-threshold implementation, or one without the re-arm, fails here.
		/// </summary>
		[Test]
		public void LadderSpeaksOncePerRungAndRearmsOnlyOnMending()
		{
			int[] walk = new int[9] { 700, 300, 0, -100, -300, -100, -300, 400, 0 };
			RegardStep[] expected = new RegardStep[9]
			{
				RegardStep.Nothing,  // 700 beloved
				RegardStep.Nothing,  // 300 trusted
				RegardStep.Murmur,   // 0 doubted, first fall
				RegardStep.Nothing,  // -100 still doubted
				RegardStep.Warning,  // -300 resented
				RegardStep.Nothing,  // -100 back to doubted: already spoken of worse
				RegardStep.Nothing,  // -300 resented again: no repeat
				RegardStep.Nothing,  // 400 trusted: mended, ladder re-armed
				RegardStep.Murmur    // 0 doubted: spoken of again
			};
			RealmRegard spoken = RealmRegard.Beloved;
			List<RegardStep> actual = new List<RegardStep>();
			for (int i = 0; i < walk.Length; i++)
			{
				RealmRegard current = KingdomExileRules.ClassifyRegard(walk[i]);
				actual.Add(KingdomExileRules.JudgeRegardStep(current, spoken, AlreadyCastOut: false));
				spoken = KingdomExileRules.RememberedRegard(current, spoken);
			}
			CollectionAssert.AreEqual(expected, actual);
		}

		/// <summary>The gate is the only rung that fires without needing to be a step down.</summary>
		[Test]
		public void LadderReachesTheGateFromAnywhere()
		{
			RealmRegard spoken = RealmRegard.Beloved;
			foreach (int regard in new int[3] { -300, -599, -600 })
			{
				RealmRegard current = KingdomExileRules.ClassifyRegard(regard);
				RegardStep step = KingdomExileRules.JudgeRegardStep(current, spoken, AlreadyCastOut: false);
				spoken = KingdomExileRules.RememberedRegard(current, spoken);
				if (regard == -600)
				{
					Assert.AreEqual(RegardStep.Expulsion, step);
				}
				else
				{
					Assert.AreNotEqual(RegardStep.Expulsion, step);
				}
			}
		}

		[TestCase(true, false, RealmRegard.Repudiated, false, ExileVerdict.Warranted)]
		[TestCase(true, false, RealmRegard.Resented, false, ExileVerdict.RegardHolds)]
		[TestCase(true, false, RealmRegard.Beloved, false, ExileVerdict.RegardHolds)]
		[TestCase(true, false, RealmRegard.Beloved, true, ExileVerdict.Warranted)]
		[TestCase(false, false, RealmRegard.Repudiated, true, ExileVerdict.NothingFounded)]
		[TestCase(false, false, RealmRegard.Repudiated, false, ExileVerdict.NothingFounded)]
		[TestCase(false, true, RealmRegard.Repudiated, true, ExileVerdict.AlreadyCastOut)]
		[TestCase(false, true, RealmRegard.Beloved, false, ExileVerdict.AlreadyCastOut)]
		public void JudgeExile(bool founded, bool castOut, RealmRegard current, bool forced, ExileVerdict expected)
		{
			Assert.AreEqual(expected, KingdomExileRules.JudgeExile(founded, castOut, current, forced));
		}

		/// <summary>
		/// A founder who was cast out and founded again may be cast out of the new realm too. The
		/// slot holds one expulsion because the earlier door is already shut for good.
		/// </summary>
		[Test]
		public void HoldingARealmAgainDoesNotMakeYouUnexpellable()
		{
			Assert.AreEqual(ExileVerdict.Warranted, KingdomExileRules.JudgeExile(Founded: true, AlreadyCastOut: true, Current: RealmRegard.Repudiated, Forced: false));
		}

		[TestCase(true, false, true, true, 0, ReturnVerdict.Allowed)]
		[TestCase(true, false, true, true, -249, ReturnVerdict.Allowed)]
		[TestCase(true, false, true, true, -600, ReturnVerdict.RegardTooLow)]
		[TestCase(true, false, true, true, -1000, ReturnVerdict.RegardTooLow)]
		[TestCase(true, false, true, false, 0, ReturnVerdict.NotOnTheirGround)]
		[TestCase(true, false, false, false, 0, ReturnVerdict.NothingRemembered)]
		[TestCase(false, false, true, true, 700, ReturnVerdict.NeverCastOut)]
		public void JudgeReturn(bool castOut, bool foundedAgain, bool groundRemembered, bool onTheirGround, int regard, ReturnVerdict expected)
		{
			Assert.AreEqual(expected, KingdomExileRules.JudgeReturn(castOut, foundedAgain, groundRemembered, onTheirGround, regard));
		}

		/// <summary>
		/// The door founding again shuts is shut wherever the founder is standing and however well
		/// the old realm thinks of them. If this ever ordered the ground check first, a founder
		/// with a realm of their own would be told to go and stand somewhere instead of being told
		/// the truth.
		/// </summary>
		[TestCase(true, true, 700)]
		[TestCase(false, true, 700)]
		[TestCase(true, false, -1000)]
		[TestCase(false, false, 0)]
		public void FoundingAgainShutsTheDoorEverywhere(bool onTheirGround, bool groundRemembered, int regard)
		{
			Assert.AreEqual(ReturnVerdict.FoundedAgain, KingdomExileRules.JudgeReturn(CastOut: true, FoundedAgain: true, GroundRemembered: groundRemembered, OnTheirGround: onTheirGround, Regard: regard));
		}

		/// <summary>Never having founded again is what leaves the door open at all.</summary>
		[Test]
		public void NeverFoundingAgainLeavesTheDoorOpen()
		{
			Assert.AreEqual(ReturnVerdict.Allowed, KingdomExileRules.JudgeReturn(CastOut: true, FoundedAgain: false, GroundRemembered: true, OnTheirGround: true, Regard: 0));
		}

		/// <summary>
		/// The question is put on arrival, silenced by walking away from it, and asked again only
		/// once the founder has actually changed the realm's mind. Nothing here reads a clock, so
		/// no amount of waiting reopens it and no amount of standing there repeats it.
		/// </summary>
		[TestCase(int.MinValue, 0, true)]
		[TestCase(0, 0, false)]
		[TestCase(0, 1, true)]
		[TestCase(0, -100, false)]
		[TestCase(-100, 0, true)]
		[TestCase(400, 300, false)]
		public void ShouldOfferReturn(int askedAtRegard, int regard, bool expected)
		{
			Assert.AreEqual(expected, KingdomExileRules.ShouldOfferReturn(CastOut: true, FoundedAgain: false, GroundRemembered: true, OnTheirGround: true, Regard: regard, AskedAtRegard: askedAtRegard));
		}

		[TestCase(true, false, false, 0)]
		[TestCase(false, true, true, 0)]
		[TestCase(true, true, false, 0)]
		[TestCase(true, false, true, -600)]
		public void ShouldOfferReturnNeverAsksWhenTheReturnIsRefused(bool groundRemembered, bool foundedAgain, bool onTheirGround, int regard)
		{
			Assert.IsFalse(KingdomExileRules.ShouldOfferReturn(CastOut: true, FoundedAgain: foundedAgain, GroundRemembered: groundRemembered, OnTheirGround: onTheirGround, Regard: regard, AskedAtRegard: int.MinValue));
		}

		[Test]
		public void ShouldOfferReturnNeverAsksSomeoneWhoWasNeverCastOut()
		{
			Assert.IsFalse(KingdomExileRules.ShouldOfferReturn(CastOut: false, FoundedAgain: false, GroundRemembered: true, OnTheirGround: true, Regard: 700, AskedAtRegard: int.MinValue));
		}

		// Being taken back raises the founder to indifference and never lowers what they mended.
		[TestCase(-599, 0)]
		[TestCase(-1, 0)]
		[TestCase(0, 0)]
		[TestCase(1, 1)]
		[TestCase(700, 700)]
		public void RegardOnReturn(int regard, int expected)
		{
			Assert.AreEqual(expected, KingdomExileRules.RegardOnReturn(regard));
		}

		/// <summary>
		/// Every refusal has to say a different true thing. A copy-pasted or empty branch reads as
		/// a working feature in play and is invisible in a screenshot.
		/// </summary>
		[Test]
		public void EveryReturnRefusalIsItsOwnSentence()
		{
			ReturnVerdict[] refusals = new ReturnVerdict[5]
			{
				ReturnVerdict.NeverCastOut,
				ReturnVerdict.FoundedAgain,
				ReturnVerdict.NothingRemembered,
				ReturnVerdict.NotOnTheirGround,
				ReturnVerdict.RegardTooLow
			};
			HashSet<string> seen = new HashSet<string>();
			foreach (ReturnVerdict verdict in refusals)
			{
				string line = KingdomExileRules.ReturnRefusal(verdict, "Kavvat", "Sheol");
				Assert.IsFalse(string.IsNullOrEmpty(line), verdict + " has no refusal");
				Assert.IsTrue(seen.Add(line), verdict + " repeats another refusal");
			}
			Assert.AreEqual("", KingdomExileRules.ReturnRefusal(ReturnVerdict.Allowed, "Kavvat", "Sheol"));
		}

		/// <summary>The refusal for a founder who poured again has to name both realms, or it is
		/// telling them something they cannot act on.</summary>
		[Test]
		public void TheClosedDoorNamesBothRealms()
		{
			string line = KingdomExileRules.ReturnRefusal(ReturnVerdict.FoundedAgain, "Kavvat", "Sheol");
			Assert.IsTrue(line.Contains("Kavvat"), "the refusal does not name the realm that shut the door");
			Assert.IsTrue(line.Contains("Sheol"), "the refusal does not name the realm that shut it");
		}

		[TestCase("WaterRitualCurse")]
		[TestCase("WaterRitualHermitOathPunishment")]
		[TestCase("Blasphemy")]
		[TestCase("Worship")]
		[TestCase("Wish")]
		public void EveryNamedDeedHasItsOwnClause(string reputationType)
		{
			string named = KingdomExileRules.DeedClause(reputationType);
			Assert.IsFalse(string.IsNullOrEmpty(named));
			Assert.AreNotEqual(KingdomExileRules.DeedClause(null), named, reputationType + " falls through to the unnamed clause");
		}

		/// <summary>
		/// An unnamed reason still produces a deed clause: the chronicle sentence is built by
		/// concatenation, so an empty one would leave a dated entry that trails off.
		/// </summary>
		[TestCase((string)null)]
		[TestCase("")]
		[TestCase("SomeFutureVanillaReason")]
		public void AnUnnamedDeedStillReads(string reputationType)
		{
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.DeedClause(reputationType)));
		}

		/// <summary>
		/// The two registers must not agree. This is the one thing only a two-register chronicle
		/// can do, and a refactor that routed the expulsion back through the ordinary derived
		/// retelling would quietly lose it.
		/// </summary>
		[Test]
		public void TheTwoRegistersDisagreeAboutTheExpulsion()
		{
			string book = KingdomExileRules.ExileTelling("Kavvat", KingdomExileRules.DeedClause("WaterRitualCurse"));
			string roads = KingdomExileRules.ExileRumour("Kavvat", "Kaviir");
			Assert.AreNotEqual(book, roads);
			Assert.IsTrue(book.Contains("you"), "the founder's own book is not written to the founder");
			Assert.IsFalse(roads.Contains("you "), "the rumour register is written to the founder rather than about them");
			Assert.IsTrue(roads.Contains("Kaviir"), "the rumour register does not name the founder");
		}

		[Test]
		public void TheTwoRegistersDisagreeAboutTheReturn()
		{
			Assert.AreNotEqual(KingdomExileRules.ReturnTelling("Kavvat"), KingdomExileRules.ReturnRumour("Kavvat", "Kaviir"));
		}

		/// <summary>Chronicle clauses are dated and closed by the chronicle, so they must not
		/// arrive with their own trailing stop or a leading capital.</summary>
		[TestCase(RegardStep.Murmur)]
		[TestCase(RegardStep.Warning)]
		public void ChronicleClausesAreClauses(RegardStep step)
		{
			string clause = KingdomExileRules.RegardChronicle(step, "Kavvat");
			Assert.IsFalse(string.IsNullOrEmpty(clause));
			Assert.IsFalse(clause.EndsWith("."), "a chronicle clause must not close itself");
			Assert.AreEqual(char.ToLowerInvariant(clause[0]), clause[0], "a chronicle clause must not capitalise itself");
		}

		[TestCase(RegardStep.Nothing)]
		[TestCase(RegardStep.Expulsion)]
		public void StepsWithTheirOwnTellingSayNothingTwice(RegardStep step)
		{
			Assert.AreEqual("", KingdomExileRules.RegardChronicle(step, "Kavvat"));
			Assert.AreEqual("", KingdomExileRules.RegardSpeech(step, "Kavvat"));
		}

		[TestCase(RegardStep.Murmur)]
		[TestCase(RegardStep.Warning)]
		public void SpokenAndWrittenTellingsAreNotTheSameString(RegardStep step)
		{
			Assert.AreNotEqual(KingdomExileRules.RegardSpeech(step, "Kavvat"), KingdomExileRules.RegardChronicle(step, "Kavvat"));
		}

		/// <summary>
		/// The expulsion modal has to say what survived, because everything that survived is the
		/// reason this is not an ending.
		/// </summary>
		[TestCase(1)]
		[TestCase(2)]
		public void TheExpulsionNoticeSaysWhatSurvives(int cities)
		{
			string notice = KingdomExileRules.ExileNotice("Kavvat", KingdomExileRules.DeedClause("Blasphemy"), cities);
			Assert.IsTrue(notice.Contains("Kavvat"));
			Assert.IsTrue(notice.Contains("charter") || notice.Contains("Charter"));
			Assert.IsTrue(notice.Contains("basin"), "the notice does not tell the founder they may pour again");
		}

		/// <summary>Prose must survive a realm with no name rather than printing a hole in a
		/// sentence the founder is reading at the worst moment of the save.</summary>
		[TestCase((string)null)]
		[TestCase("")]
		public void ProseSurvivesAnUnnamedRealm(string realmName)
		{
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.ExileTelling(realmName, null)));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.ExileRumour(realmName, null)));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.ReturnTelling(realmName)));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.ReturnRumour(realmName, null)));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.ExileNotice(realmName, null, 1)));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.ReturnNotice(realmName, null)));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.DoorClosedLine(realmName, null)));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.RegardSpeech(RegardStep.Murmur, realmName)));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.RegardChronicle(RegardStep.Warning, realmName)));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomExileRules.ReturnRefusal(ReturnVerdict.NotOnTheirGround, realmName, null)));
		}

		[TestCase(RealmRegard.Beloved, "beloved")]
		[TestCase(RealmRegard.Trusted, "trusted")]
		[TestCase(RealmRegard.Doubted, "doubted")]
		[TestCase(RealmRegard.Resented, "resented")]
		[TestCase(RealmRegard.Repudiated, "repudiated")]
		public void RegardName(RealmRegard regard, string expected)
		{
			Assert.AreEqual(expected, KingdomExileRules.RegardName(regard));
		}

		/// <summary>
		/// Nothing in the ladder or either verdict reads elapsed time. This is the pillar the
		/// whole feature is most likely to break by accident, so it is asserted as a shape: the
		/// same inputs give the same answer no matter how much of the game has gone by, because
		/// there is no parameter through which time could arrive.
		/// </summary>
		[Test]
		public void NothingHereTakesATick()
		{
			foreach (System.Reflection.MethodInfo method in typeof(KingdomExileRules).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
			{
				foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
				{
					Assert.AreNotEqual(typeof(long), parameter.ParameterType, method.Name + " takes a tick count; absence must never expel anyone");
					Assert.IsFalse(parameter.Name.ToLowerInvariant().Contains("tick") || parameter.Name.ToLowerInvariant().Contains("elapsed") || parameter.Name.ToLowerInvariant().Contains("day"), method.Name + " takes " + parameter.Name);
				}
			}
		}
	}
}
#endif
