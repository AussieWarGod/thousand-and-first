#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Value tests for <see cref="KingdomTeardownCrewDepartureClaims"/> -- a pure, engine-free
	/// predicate and diagnostic formatter, so every case here is a real behavioural proof, not
	/// a source pin. review-3f010e3-teardown-findings.md finding 3: a genuine roofless-brink
	/// departure previously stalled both teardown cases at Phase 1 with Ok=true and no named
	/// diagnostic; this pins the exact boundary (departed only strictly below the wanted crew
	/// size, never at or above it) and the exact diagnostic text Frame.Check() journals.
	/// </summary>
	public class KingdomTeardownCrewDepartureClaimsTests
	{
		[Test]
		public void HasDepartedIsTrueOnlyStrictlyBelowTheWantedCrewSize()
		{
			Assert.That(KingdomTeardownCrewDepartureClaims.HasDeparted(0, 2), Is.True);
			Assert.That(KingdomTeardownCrewDepartureClaims.HasDeparted(1, 2), Is.True);
			Assert.That(KingdomTeardownCrewDepartureClaims.HasDeparted(2, 2), Is.False,
				"the full crew size itself must never read as departed");
			Assert.That(KingdomTeardownCrewDepartureClaims.HasDeparted(3, 2), Is.False,
				"a roll count above the wanted size must never read as departed");
		}

		[Test]
		public void DiagnosticIsTheExactNamedJournalLineFrameChecksEmits()
		{
			string text = KingdomTeardownCrewDepartureClaims.Diagnostic("fire", 1, 2, 8640L);
			Assert.That(text,
				Is.EqualTo("case=fire outcome=crew-departed onRoll=1 wanted=2 tick=8640"));
		}

		[Test]
		public void DiagnosticNamesTheExactCaseGivenNeverAHardcodedOne()
		{
			Assert.That(
				KingdomTeardownCrewDepartureClaims.Diagnostic("larder", 0, 2, 9600L),
				Is.EqualTo("case=larder outcome=crew-departed onRoll=0 wanted=2 tick=9600"));
		}
	}
}
#endif
