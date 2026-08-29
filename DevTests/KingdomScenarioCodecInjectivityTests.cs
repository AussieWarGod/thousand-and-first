#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Codec injectivity: a record encodes to text its own decoder accepts, and no in-memory value
	/// serialises as a different one.
	/// <para>
	/// Split from the grammar fixture only to hold the house line cap. Both defects here were
	/// invisible because the fixture meant to catch them never exercised the field: the sound record
	/// left PlanDigest unassigned, so the round-trip test encoded an empty field it then failed to
	/// decode, and nothing compared a reparsed record against the one it came from.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioCodecInjectivityTests
	{
		private const string DigestA =
			"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
		private const string DigestB =
			"fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
		private const string DigestC =
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff";
		private const string Mod = "0.2.0";
		private const string Core = "2.0.211.51";

		private static KingdomScenarioProvenance Sound()
		{
			return new KingdomScenarioProvenance
			{
				ScenarioKey = "arch-gallery-slice",
				AuthorityClass = "architecture-stamper",
				Verbs = "foundfirst+armcheckpoint",
				AnchorId = "anchor-arch-01",
				KeySetDigest = DigestB,
				Seed = "#4242",
				ModVersion = Mod,
				QudCoreVersion = Core,
				DefinitionDigest = DigestA,
				PlanDigest = DigestC,
				Synthetic = false
			};
		}

		/// <summary>
		/// Encode used to write PlanDigest unchecked while TryDecode demanded 64 hex, so a record
		/// could encode to text only a failing decode would discover. The fixture that was meant to
		/// catch it never assigned the field.
		/// </summary>
		[TestCase(null)]
		[TestCase("")]
		[TestCase("0123456789abcdef")]
		[TestCase("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
		[TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde|")]
		[TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdefa")]
		public void AMalformedPlanDigestNeverEncodes(string digest)
		{
			KingdomScenarioProvenance record = Sound();
			record.PlanDigest = digest;
			Assert.IsNull(KingdomScenarioProvenanceRules.Encode(record));
		}

		// ----- RED 19 item 6: the optional sentinel is reserved ----------------------------------

		/// <summary>
		/// AnchorId="-" passed the token rule, encoded byte-identically to null, and reparsed as
		/// null: one in-memory value serialising as another.
		/// </summary>
		[Test]
		public void ThePresentSentinelValueNeverEncodes()
		{
			KingdomScenarioProvenance record = Sound();
			record.AnchorId = "-";
			Assert.IsNull(KingdomScenarioProvenanceRules.Encode(record),
				"a present '-' must not serialise as absent");
			KingdomScenarioProvenance absent = Sound();
			absent.AnchorId = null;
			Assert.IsNotNull(KingdomScenarioProvenanceRules.Encode(absent));
		}

		[Test]
		public void AnOptionalFieldRoundTripsToItself()
		{
			foreach (string anchor in new string[] { null, "anchor-arch-01" })
			{
				KingdomScenarioProvenance record = Sound();
				record.AnchorId = anchor;
				string wire = KingdomScenarioProvenanceRules.Encode(record);
				Assert.IsNotNull(wire, "anchor " + (anchor ?? "<null>"));
				KingdomScenarioProvenance back;
				string failure;
				Assert.IsTrue(KingdomScenarioProvenanceRules.TryDecode(wire, out back, out failure),
					failure);
				Assert.AreEqual(anchor, back.AnchorId);
				Assert.AreEqual(wire, KingdomScenarioProvenanceRules.Encode(back));
			}
		}

		/// <summary>A record that does not round-trip to itself is not a shape that may be judged.</summary>
		[Test]
		public void StampShapeRequiresAnExactRoundTrip()
		{
			string failure;
			Assert.IsTrue(KingdomScenarioProvenanceRules.TryValidateStampShape(Sound(), DigestA,
				Mod, Core, out failure), failure);
			KingdomScenarioProvenance sentinel = Sound();
			sentinel.AnchorId = "-";
			Assert.IsFalse(KingdomScenarioProvenanceRules.TryValidateStampShape(sentinel, DigestA,
				Mod, Core, out failure));
		}
	}
}
#endif
