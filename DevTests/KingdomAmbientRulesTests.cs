#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The ambient lane: what the city says about the hour, and the rule that keeps it from
	/// saying it again.
	/// <para>
	/// BUILDING-CATALOGUE-BRIEF Addendum 13 lane 3. The two contracts a regression would break are
	/// both here: <b>a stopped work outranks every texture line</b>, and <b>a line repeats across
	/// a day boundary and never inside one</b>.
	/// </para>
	/// </summary>
	internal class KingdomAmbientRulesTests
	{
		private static KingdomAmbientReading Busy
		{
			get { return new KingdomAmbientReading(3, 0, false, 0, 0, 0, false); }
		}

		private static KingdomAmbientReading Stopped
		{
			get { return new KingdomAmbientReading(1, 2, true, 4, 4, 4, false); }
		}

		[Test]
		public void ASilentWheel_OutranksEveryTexture()
		{
			// Every band, because a work that stopped in the small hours is still a work that
			// stopped, and a texture line that beat it would bury the one line the founder can act
			// on (STANDARDS 7b).
			foreach (KingdomDayBand band in System.Enum.GetValues(typeof(KingdomDayBand)))
			{
				string line;
				int key;
				Assert.IsTrue(KingdomAmbientRules.TryLine(Stopped, band, out line, out key), band.ToString());
				Assert.IsTrue(line.Contains("stopped") || line.Contains("quiet"), band + " said: " + line);
			}
		}

		[Test]
		public void ADryCity_SaysSoBeforeItSaysAnythingElse()
		{
			KingdomAmbientReading dry = new KingdomAmbientReading(3, 0, true, 2, 2, 2, true);
			string line;
			int key;
			Assert.IsTrue(KingdomAmbientRules.TryLine(dry, KingdomDayBand.SaltSun, out line, out key));
			Assert.IsTrue(line.Contains("cisterns"), line);
		}

		[Test]
		public void EveryBand_HasSomethingToSay()
		{
			foreach (KingdomDayBand band in System.Enum.GetValues(typeof(KingdomDayBand)))
			{
				string line;
				int key;
				Assert.IsTrue(KingdomAmbientRules.TryLine(Busy, band, out line, out key), band.ToString());
				Assert.AreNotEqual("", line);
				Assert.AreNotEqual(KingdomAmbientRules.NoKey, key);
			}
		}

		/// <summary>
		/// A line and a band together make the key, so the same words at a different hour are a
		/// different line and two different hours never collide.
		/// </summary>
		[Test]
		public void Keys_DistinguishBandAndLine()
		{
			string a;
			string b;
			int keyA;
			int keyB;
			Assert.IsTrue(KingdomAmbientRules.TryLine(Busy, KingdomDayBand.Rising, out a, out keyA));
			Assert.IsTrue(KingdomAmbientRules.TryLine(Busy, KingdomDayBand.Hindsun, out b, out keyB));
			Assert.AreNotEqual(keyA, keyB);
		}

		/// <summary>The state-change rule, which is the whole of "never per slice": the same line
		/// inside one day is refused, and the same line tomorrow is allowed.</summary>
		[Test]
		public void Speakable_OncePerStateChangeOrPerDay()
		{
			Assert.IsTrue(KingdomAmbientRules.Speakable(7, 0, 3L, -1L), "a book that has said nothing may speak");
			Assert.IsFalse(KingdomAmbientRules.Speakable(7, 7, 3L, 3L), "the same line inside one day is silence");
			Assert.IsTrue(KingdomAmbientRules.Speakable(7, 7, 4L, 3L), "the same line tomorrow is a new day");
			Assert.IsTrue(KingdomAmbientRules.Speakable(8, 7, 3L, 3L), "a different line is a state change");
		}

		[Test]
		public void Speakable_NeverSpeaksNothing()
		{
			Assert.IsFalse(KingdomAmbientRules.Speakable(KingdomAmbientRules.NoKey, 4, 9L, 1L));
		}

		[TestCase(0L, 0L)]
		[TestCase(1199L, 0L)]
		[TestCase(1200L, 1L)]
		[TestCase(2400L, 2L)]
		public void DayOrdinal_CutsOnTheEnginesOwnDay(long tick, long expected)
		{
			Assert.AreEqual(expected, KingdomAmbientRules.DayOrdinal(tick));
			Assert.AreEqual(1200L, KingdomHappeningRules.TicksPerDay);
		}

		/// <summary>An empty city is not a silent one: a place with nothing standing in it still
		/// has a sky over it, and a caller that got an empty string would say nothing forever.</summary>
		[Test]
		public void AnEmptyCity_StillHasALine()
		{
			string line;
			int key;
			Assert.IsTrue(KingdomAmbientRules.TryLine(KingdomAmbientReading.Empty, KingdomDayBand.BeetleMoon, out line, out key));
			Assert.AreNotEqual("", line);
		}

		/// <summary>Bread-smell is a rendering of a work having run, not of the hour. Take the
		/// running away and the line changes.</summary>
		[Test]
		public void BreadSmell_ComesFromAWorkHavingRun()
		{
			string cooked;
			string cold;
			int a;
			int b;
			Assert.IsTrue(KingdomAmbientRules.TryLine(new KingdomAmbientReading(2, 0, true, 0, 0, 0, false), KingdomDayBand.SaltSun, out cooked, out a));
			Assert.IsTrue(KingdomAmbientRules.TryLine(new KingdomAmbientReading(2, 0, false, 0, 0, 0, false), KingdomDayBand.SaltSun, out cold, out b));
			Assert.AreNotEqual(cooked, cold);
			Assert.IsTrue(cooked.Contains("Bread"), cooked);
		}

		/// <summary>The shrine's hour is the shrine's, and only when somebody is keeping it.</summary>
		[Test]
		public void TheShrinesHour_NeedsSomebodyAtTheShrine()
		{
			string kept;
			string empty;
			int a;
			int b;
			Assert.IsTrue(KingdomAmbientRules.TryLine(new KingdomAmbientReading(1, 0, false, 2, 0, 0, false), KingdomDayBand.Hindsun, out kept, out a));
			Assert.IsTrue(KingdomAmbientRules.TryLine(new KingdomAmbientReading(1, 0, false, 0, 0, 0, false), KingdomDayBand.Hindsun, out empty, out b));
			Assert.IsTrue(kept.Contains("shrine"), kept);
			Assert.IsFalse(empty.Contains("shrine"), empty);
		}
	}
}
#endif
