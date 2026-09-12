#if TAF_TESTS
using System;
using System.Text;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>The journal row cap and its truncation marker (native run 49), and the promise
	/// that the rung-3 check row's own reads fit under the cap however many lots were seeded.</summary>
	public class KingdomScenarioJournalRulesTests
	{
		[Test]
		public void AMessageInsideTheCapIsUnchanged()
		{
			string exact = new string('x', KingdomScenarioJournalRules.MaxMessageChars);
			Assert.That(KingdomScenarioJournalRules.Bound(exact, KingdomScenarioJournalRules.MaxMessageChars),
				Is.EqualTo(exact));
			Assert.That(KingdomScenarioJournalRules.Bound("short", 8192), Is.EqualTo("short"));
			Assert.That(KingdomScenarioJournalRules.Bound(null, 8192), Is.EqualTo(""));
		}

		[TestCase(8193)]
		[TestCase(8308)]
		[TestCase(20000)]
		[TestCase(1000000)]
		public void ACutRowEndsInAMarkerNamingTheDroppedCharsAndNeverExceedsTheCap(int Length)
		{
			string message = new string('y', Length);
			string bound = KingdomScenarioJournalRules.Bound(message, KingdomScenarioJournalRules.MaxMessageChars);
			Assert.That(bound.Length, Is.EqualTo(KingdomScenarioJournalRules.MaxMessageChars));
			Assert.That(bound, Does.EndWith(KingdomScenarioJournalRules.TruncatedClose));
			int open = bound.LastIndexOf(KingdomScenarioJournalRules.TruncatedOpen, StringComparison.Ordinal);
			Assert.That(open, Is.GreaterThan(-1));
			string count = bound.Substring(open + KingdomScenarioJournalRules.TruncatedOpen.Length,
				bound.Length - KingdomScenarioJournalRules.TruncatedClose.Length - open
					- KingdomScenarioJournalRules.TruncatedOpen.Length);
			int dropped = int.Parse(count);
			// The kept text plus the dropped count is exactly the original.
			Assert.That(open + dropped, Is.EqualTo(Length));
			Assert.That(bound.Substring(0, open), Is.EqualTo(message.Substring(0, open)));
		}

		[Test]
		public void ATinyCapStillCarriesTheMarker()
		{
			// A cap smaller than the marker keeps nothing but the marker itself: the row still
			// says it was cut, and says how much, rather than pretending to be complete.
			string bound = KingdomScenarioJournalRules.Bound(new string('z', 100), 20);
			Assert.That(bound, Does.StartWith(KingdomScenarioJournalRules.TruncatedOpen));
			Assert.That(bound, Does.EndWith(KingdomScenarioJournalRules.TruncatedClose));
			Assert.That(bound, Is.EqualTo(KingdomScenarioJournalRules.TruncatedOpen + "100"
				+ KingdomScenarioJournalRules.TruncatedClose));
		}

		/// <summary>
		/// The rung-3 check row is the report header, then THIS phase's reads (rung3-standing,
		/// the moot-yard job, job progress, town-held with its tally, the envelope check and the
		/// phase-3 lines), then earlier phases. Built here at every field's worst width -- the
		/// 300-char Bounded texts fully spent, every number at nine digits -- it fits under the
		/// cap with room, so the eighteen seeded lots (their own town-lots row, and the setup
		/// evidence that follows the current phase) can never cut a rung-3 read.
		/// </summary>
		[Test]
		public void TheRungThreeChecksOwnReadsFitUnderTheCapWhateverWasSeeded()
		{
			const int Bounded = 300;
			const string Nine = "999999999";
			string text = new string('t', Bounded);
			StringBuilder row = new StringBuilder();
			row.Append("native-camp-heart cases=1 passed=0 failed=1; evidence retained: ")
				.Append(text)
				.Append("; synthetic-camp=true; synthetic-residents=true; synthetic-store-contents=true")
				.Append("; synthetic-drams=true; synthetic-born-provenance=true")
				.Append("; synthetic-material-identities=true; target-rung=3; synthetic-rung3-bill=True")
				.Append("; synthetic-craft-disks=True; synthetic-town-works=True; synthetic-stage-derived=True")
				.Append("; synthetic-water-identity=true; improvement-notice-premarked=true")
				.Append("; stockpile-refusal-reason-claimed=false; ordinary-acceptance=false")
				.Append("; charter=untested; save-load=untested");
			row.Append("\nrung3-standing tick=").Append(Nine).Append("; key=heartwaterstone; stage=Steading")
				.Append("; population=").Append(Nine).Append("; assigned-crew=").Append(Nine)
				.Append("; free-hands=").Append(Nine).Append("; craft=Workshop; other-improvement-working=False")
				.Append("; stored water=").Append(Nine).Append("; assess valid=True; verdict=NoGroundToGrow")
				.Append("; successor=heartmoot; stage-needed=Town; crew-needed=").Append(Nine)
				.Append("; cost=").Append(Nine).Append("; reserve=").Append(Nine).Append("; shortfall=").Append(Nine)
				.Append("; reason=").Append(text).Append("; announced-verdict=NoGroundToGrow; improvement-working=False");
			row.Append("\nrung3-job=absent");
			row.Append("\nblocked-player-cell=99,99\nblocked-message-count=").Append(Nine);
			for (int i = 0; i < KingdomScenarioJournalRules.BlockedMessagesKept; i++)
				row.Append("\nblocked-message=").Append(new string('m',
					KingdomScenarioJournalRules.BlockedMessageChars)).Append("[truncated]");
			row.Append("\njob-progress tick=").Append(Nine).Append("; turns=").Append(Nine)
				.Append("; job=(no moot-yard job); row=absent");
			row.Append("\nrung3-town-held at the rung-3 boundary tick=").Append(Nine).Append("; stage=Steading")
				.Append("; population=").Append(Nine).Append("; supports water=").Append(Nine).Append(" roof=")
				.Append(Nine).Append(" lift=").Append(Nine).Append("; supported level=").Append(Nine);
			row.Append("\nheart-envelope-check rung=3; match=False; predicted rect=99,99 99,99; live rect=99,99 99,99")
				.Append("; predicted lanes=").Append(Nine).Append("; live lanes=").Append(Nine).Append("; lanes match=False");
			row.Append("\nphase3 footprint=99x99; rect=99,99 99,99");
			row.Append("\nphase3 job=").Append(new string('j', 64)).Append("; phase=Complete; physical=EffectsSettled")
				.Append("; committed water debit=").Append(Nine).Append("; committed material debit=").Append(text)
				.Append("; failure=").Append(text);
			row.Append("\nphase3 rung-marker property=r_TAF_ConstructionHeartEffect; waterstone=").Append(new string('w', 64))
				.Append(" reads 2; moot yard=").Append(new string('y', 64)).Append(" reads 2; distinct bodies=true");
			row.Append("\nphase3 founding heart recovered after second climb=True");
			row.Append("\nphase3 tick=").Append(Nine).Append("; standing=").Append(new string('s', 64))
				.Append("; key=heartmoot; stage=Town; population=").Append(Nine).Append("; craft=Workshop")
				.Append("; zone rung read=3; basin capacity read=160; store=").Append(new string('s', 64))
				.Append("; store raw custody census=").Append(text).Append("; retained unasked units=").Append(text)
				.Append("; fire=").Append(new string('f', 64)).Append("@(9,9)");
			int length = row.Length;
			Assert.That(length, Is.LessThan(KingdomScenarioJournalRules.MaxMessageChars),
				"the rung-3 row's own reads must fit under the cap; they are " + length + " chars");
			// Room for the marker and a whole extra Bounded field before anything is cut.
			Assert.That(length + Bounded + 32, Is.LessThan(KingdomScenarioJournalRules.MaxMessageChars));
		}
	}
}
#endif
