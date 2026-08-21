#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Lane 2: what a creed makes of the founder's own body, judged only from tables vanilla
	/// already filled in.
	/// <para>
	/// BUILDING-CATALOGUE-BRIEF Addendum 13 lane 2. The mesh contract under test is that the SIGN
	/// of <c>Faction.PartReputation</c> is the whole judgement &mdash; this mod has no opinion of
	/// its own about any mutation &mdash; and that a reaction is a line and never a mechanic.
	/// </para>
	/// </summary>
	internal class KingdomNatureRulesTests
	{
		private static KingdomFounderNature With(string part, int feeling, int chrome, bool revere, bool refuse)
		{
			return new KingdomFounderNature("Mutated Human", chrome, part, feeling, revere, refuse);
		}

		/// <summary>Vanilla's own numbers, as vanilla wrote them: Seekers of the Sightless Way
		/// score <c>MassMind</c> at -200 (B/Factions.xml:1397) and the birds score <c>Wings</c> at
		/// +300 (:362). The sign decides, and nothing else does.</summary>
		[TestCase(-200, KingdomRegard.Unease)]
		[TestCase(-1, KingdomRegard.Unease)]
		[TestCase(300, KingdomRegard.Wonder)]
		[TestCase(1, KingdomRegard.Wonder)]
		public void PartReputation_DecidesBySign(int feeling, KingdomRegard expected)
		{
			Assert.AreEqual(expected, KingdomNatureRules.Judge(With("mass mind", feeling, 0, false, false)));
		}

		/// <summary>A creed that has written a number about a body has said something stronger
		/// than one that has merely listed an interest, so the part table outranks the chrome.</summary>
		[Test]
		public void ThePartTable_OutranksTheChrome()
		{
			Assert.AreEqual(KingdomRegard.Wonder, KingdomNatureRules.Judge(With("wings", 300, 3, false, true)));
			Assert.AreEqual(KingdomRegard.Unease, KingdomNatureRules.Judge(With("mass mind", -200, 3, true, false)));
		}

		/// <summary>
		/// Vanilla's Putus Templar list <c>cybernetics</c> twice &mdash; once inverted under "the
		/// modern world" and once plainly (B/Factions.xml:1271-1272). A faction that sells a thing
		/// it will not carry is not admiring it, so the inverted reading wins.
		/// </summary>
		[Test]
		public void Chrome_RefusalOutranksInterest()
		{
			Assert.AreEqual(KingdomRegard.Unease, KingdomNatureRules.Judge(With(null, 0, 2, true, true)));
			Assert.AreEqual(KingdomRegard.Wonder, KingdomNatureRules.Judge(With(null, 0, 2, true, false)));
		}

		[Test]
		public void NoChromeAndNoOpinion_IsSilence()
		{
			Assert.AreEqual(KingdomRegard.Nothing, KingdomNatureRules.Judge(With(null, 0, 0, true, true)));
			Assert.AreEqual(KingdomRegard.Nothing, KingdomNatureRules.Judge(KingdomFounderNature.Unremarkable));
			Assert.AreEqual(KingdomRegard.Nothing, KingdomNatureRules.Judge(With(null, 0, 3, false, false)),
				"a creed with no interest in chrome says nothing about it");
		}

		/// <summary>A part named with a zero feeling is a table entry that says nothing, and must
		/// not be read as a verdict.</summary>
		[Test]
		public void AZeroFeeling_IsNotAVerdict()
		{
			Assert.AreEqual(KingdomRegard.Nothing, KingdomNatureRules.Judge(With("horns", 0, 0, false, false)));
		}

		// ==================================================================================
		// The key
		// ==================================================================================

		[Test]
		public void Silence_HasNoKey()
		{
			Assert.AreEqual(KingdomNatureRules.NoKey, KingdomNatureRules.RegardKey("Templar", KingdomFounderNature.Unremarkable));
		}

		/// <summary>A different creed, a different part, or a different sign is a different state,
		/// and the city is owed a line about each. Without this the founder would be told once
		/// ever, whatever they became afterwards.</summary>
		[Test]
		public void Keys_ChangeWithTheCreedThePartAndTheSign()
		{
			int a = KingdomNatureRules.RegardKey("Templar", With("mass mind", -200, 0, false, false));
			int b = KingdomNatureRules.RegardKey("Barathrumites", With("mass mind", -200, 0, false, false));
			int c = KingdomNatureRules.RegardKey("Templar", With("wings", -200, 0, false, false));
			int d = KingdomNatureRules.RegardKey("Templar", With("mass mind", 200, 0, false, false));
			Assert.AreNotEqual(a, b);
			Assert.AreNotEqual(a, c);
			Assert.AreNotEqual(a, d);
		}

		[Test]
		public void Keys_AreStableForOneState()
		{
			Assert.AreEqual(
				KingdomNatureRules.RegardKey("Templar", With("mass mind", -200, 1, false, true)),
				KingdomNatureRules.RegardKey("Templar", With("mass mind", -200, 1, false, true)));
		}

		/// <summary>Chrome appearing is a state change even when the part table already had an
		/// opinion, because the founder is not the same person they were.</summary>
		[Test]
		public void GainingChrome_IsAStateChange()
		{
			Assert.AreNotEqual(
				KingdomNatureRules.RegardKey("Templar", With("mass mind", -200, 0, false, false)),
				KingdomNatureRules.RegardKey("Templar", With("mass mind", -200, 2, false, false)));
		}

		// ==================================================================================
		// The prose
		// ==================================================================================

		[Test]
		public void RegardLine_NamesTheCreedAndTheThing()
		{
			string line = KingdomNatureRules.RegardLine(With("mass mind", -200, 0, false, false), "the Seekers of the Sightless Way", "Kavvat");
			Assert.IsTrue(line.Contains("Seekers"), line);
			Assert.IsTrue(line.Contains("mass mind"), line);
			Assert.IsTrue(line.Contains("Kavvat"), line);
		}

		[Test]
		public void RegardLine_SpeaksForACreedWithNoName()
		{
			string line = KingdomNatureRules.RegardLine(With("wings", 300, 0, false, false), null, "Kavvat");
			Assert.AreNotEqual("", line);
			Assert.IsTrue(line.Contains("Kavvat"), line);
		}

		[Test]
		public void RegardLine_SaysNothingWhenThereIsNothingToSay()
		{
			Assert.AreEqual("", KingdomNatureRules.RegardLine(KingdomFounderNature.Unremarkable, "Templar", "Kavvat"));
			Assert.AreEqual("", KingdomNatureRules.RegardTelling(KingdomFounderNature.Unremarkable, "Templar", "Kavvat", "Ereshkigal"));
		}

		/// <summary>The chronicle's clause is third person and carries no trailing period, so the
		/// outsider register can retell it without the founder's own voice getting in.</summary>
		[Test]
		public void RegardTelling_IsAClauseAndNotASentence()
		{
			string telling = KingdomNatureRules.RegardTelling(With("wings", 300, 0, false, false), "the Fungi", "Kavvat", "Ereshkigal");
			Assert.AreNotEqual("", telling);
			Assert.IsFalse(telling.EndsWith("."));
			Assert.IsFalse(telling.Contains("you"));
			Assert.IsFalse(telling.Contains("your"));
		}

		/// <summary>The chrome telling names the founder when it has nothing else to point at.</summary>
		[Test]
		public void RegardTelling_NamesTheFounderForChrome()
		{
			string telling = KingdomNatureRules.RegardTelling(With(null, 0, 2, true, false), "the Barathrumites", "Kavvat", "Ereshkigal");
			Assert.IsTrue(telling.Contains("Ereshkigal"), telling);
			Assert.IsTrue(KingdomNatureRules.RegardTelling(With(null, 0, 2, true, false), "the Barathrumites", "Kavvat", null).Contains("founder"));
		}
	}
}
#endif
