#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The capture-digest law. A comparison over a partial key set is not a comparison, and a key
	/// the other path cannot produce would make the differential impossible rather than pending.
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioCaptureRulesTests
	{
		private const string Authority = "architecture-stamper";
		private const string KeySetDigest =
			"fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
		private const string DefinitionDigest =
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

		// ----- capture digest law -------------------------------------------------------------

		private static Dictionary<string, string> Capture()
		{
			Dictionary<string, string> captured =
				new Dictionary<string, string>(StringComparer.Ordinal);
			IList<string> keys = KingdomScenarioAnchorRules.KeySet(Authority);
			for (int i = 0; i < keys.Count; i++) captured[keys[i]] = "v" + i;
			return captured;
		}

		[Test]
		public void CaptureOverTheWholeDeclaredKeySetDigests()
		{
			string digest;
			string failure;
			Assert.IsTrue(KingdomScenarioAnchorRules.TryDigest(Authority, Capture(), out digest,
				out failure), failure);
			Assert.AreEqual(64, digest.Length);
		}

		[Test]
		public void CaptureMissingADeclaredKeyIsRefusedRatherThanShortened()
		{
			Dictionary<string, string> captured = Capture();
			captured.Remove(KingdomScenarioAnchorRules.KeySet(Authority)[0]);
			string digest;
			string failure;
			Assert.IsFalse(KingdomScenarioAnchorRules.TryDigest(Authority, captured, out digest,
				out failure));
			StringAssert.Contains("not a comparison", failure);
		}

		[Test]
		public void CaptureCarryingAnUndeclaredKeyIsRefused()
		{
			Dictionary<string, string> captured = Capture();
			captured["architecture.rogue"] = "x";
			string digest;
			string failure;
			Assert.IsFalse(KingdomScenarioAnchorRules.TryDigest(Authority, captured, out digest,
				out failure));
			StringAssert.Contains("undeclared key", failure);
		}

		[Test]
		public void UnknownAuthorityClassHasNoKeySetAndCannotDigest()
		{
			string digest;
			string failure;
			Assert.IsFalse(KingdomScenarioAnchorRules.TryDigest("no-such", Capture(), out digest,
				out failure));
			Assert.IsFalse(KingdomScenarioAnchorRules.IsKnownAuthorityClass("no-such"));
		}

		[Test]
		public void AnAnchorMayNotBeFoundedFromAScenarioBuiltState()
		{
			string failure;
			Assert.IsFalse(KingdomScenarioAnchorRules.TryFoundAnchor(
				KingdomScenarioAnchorRules.Provenance.ScenarioBuilt, Authority, KeySetDigest,
				out failure));
			StringAssert.Contains("cannot anchor itself", failure);
		}

		[Test]
		public void JudgeWithoutAnAnchorIsNoAnchorRatherThanAPass()
		{
			string detail;
			Assert.AreEqual(KingdomScenarioAnchorRules.Verdict.NoAnchor,
				KingdomScenarioAnchorRules.Judge(null, DefinitionDigest, KeySetDigest,
					DefinitionDigest, out detail));
			Assert.IsFalse(KingdomScenarioAnchorRules.Signs(
				KingdomScenarioAnchorRules.Verdict.NoAnchor));
			Assert.IsFalse(KingdomScenarioAnchorRules.Signs(
				KingdomScenarioAnchorRules.Verdict.Divergent));
			Assert.IsFalse(KingdomScenarioAnchorRules.Signs(
				KingdomScenarioAnchorRules.Verdict.Stale));
			Assert.IsTrue(KingdomScenarioAnchorRules.Signs(
				KingdomScenarioAnchorRules.Verdict.Matched));
		}
	}
}
#endif
