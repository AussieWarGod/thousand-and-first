#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Proof that the envelope's caps were derived rather than chosen, and that they bind.
	/// <para>
	/// Each family's maximum is recomputed here from the primitive constants in that family's own
	/// frozen source &mdash; row counts, row sizes, header and frame widths &mdash; and compared
	/// against the mirror in <c>KingdomCivicMemoryLimits</c>. A family that grows a row, widens a
	/// row, or adds a book breaks this test at the exact line that says what it used to be, which
	/// is the only way a comment claiming "derived from" stays true after somebody edits the
	/// thing it was derived from.
	/// </para>
	/// </summary>
	[TestFixture]
	public class KingdomCivicMemoryCapsTests
	{
		private static string Source(params string[] Parts)
		{
			return TestMain.ReadRepositoryText(Path.Combine(Parts));
		}

		/// <summary>The integer literal a named constant is declared with, in that source text.</summary>
		private static int Constant(string SourceText, string Name)
		{
			Match match = Regex.Match(SourceText, @"\b" + Regex.Escape(Name) + @"\s*=\s*(\d+)\b");
			Assert.IsTrue(match.Success, "constant " + Name + " is no longer declared as a literal");
			return int.Parse(match.Groups[1].Value);
		}

		private static int Book(string CodecText, string RulesText, string Header, string Row)
		{
			return Constant(CodecText, Header)
				+ Constant(RulesText, "MaxRows") * (4 + Constant(CodecText, Row));
		}

		[Test]
		public void CivicArtifactsCapIsDerivedFromItsTwoFrozenBooks()
		{
			string codec = Source("Core", "KingdomCivicArtifactsCodec.cs");
			int witness = Book(Source("Experience", "KingdomWitnessWorkCodec.cs"),
				Source("Experience", "KingdomWitnessWorkRules.cs"),
				"BookHeaderBytes", "MaxRowEncodedBytes");
			int recognition = Book(Source("Core", "KingdomArtifactRecognitionCodec.cs"),
				Source("Core", "KingdomArtifactRecognitionRules.cs"),
				"BookHeaderBytes", "MaxRowEncodedBytes");
			int derived = Constant(codec, "IdentityFramingBytes")
				+ Constant(codec, "NestedFramingBytes") + witness + recognition
				+ Constant(codec, "EnvelopeOverheadBytes");

			Assert.AreEqual(32820, witness);
			Assert.AreEqual(32820, recognition);
			Assert.AreEqual(65774, derived);
			Assert.AreEqual(derived, KingdomCivicMemoryLimits.MaxCivicArtifactsBytes);
		}

		[Test]
		public void CivicPracticeCapIsDerivedFromItsSiteAndServiceBooks()
		{
			string codec = Source("Core", "KingdomCivicPracticeCodec.cs");
			int header = Constant(codec, "HeaderBytes");
			int row = Constant(codec, "MaxRowBytes");
			int sites = header + Constant(Source("Core", "KingdomSitePracticeRules.cs"), "MaxRows")
				* (4 + row);
			int services = header
				+ Constant(Source("Core", "KingdomVocationServiceRules.cs"), "MaxRows") * (4 + row);
			int derived = Constant(codec, "IdentityFramingBytes")
				+ Constant(codec, "NestedFramingBytes") + sites + services
				+ Constant(codec, "EnvelopeOverheadBytes");

			Assert.AreEqual(32820, sites);
			Assert.AreEqual(196820, services);
			Assert.AreEqual(229774, derived);
			Assert.AreEqual(derived, KingdomCivicMemoryLimits.MaxCivicPracticeBytes);
		}

		[Test]
		public void BodyHistoryCapIsDerivedFromItsSingleBook()
		{
			string codec = Source("Core", "KingdomBodyHistoryCodec.cs");
			int derived = Constant(codec, "IdentityFramingBytes") + Constant(codec, "HeaderBytes")
				+ Constant(Source("Core", "KingdomBodyHistoryRules.cs"), "MaxRows")
					* (4 + Constant(codec, "MaxRowBytes"))
				+ Constant(codec, "EnvelopeOverheadBytes");

			Assert.AreEqual(32946, derived);
			Assert.AreEqual(derived, KingdomCivicMemoryLimits.MaxBodyHistoryBytes);
		}

		/// <summary>
		/// O6 and D7 are one family but two sections. That codec states a maximum per book and
		/// no maximum for the pair &mdash; its own <c>MaxBookBytes</c> is the larger of the two,
		/// not their sum &mdash; so one section per book keeps both caps quotations.
		/// </summary>
		[Test]
		public void CuriosityAndCivicLeadCapsAreEachTakenFromTheirOwnFrozenBook()
		{
			string codec = Source("Experience", "KingdomCuriosityLeadCodec.cs");
			Assert.AreEqual(Constant(codec, "MaxCuriosityBookBytes"),
				KingdomCivicMemoryLimits.MaxCuriosityBytes);
			Assert.AreEqual(Constant(codec, "MaxLeadBookBytes"),
				KingdomCivicMemoryLimits.MaxCivicLeadsBytes);
			Assert.AreEqual(22031, KingdomCivicMemoryLimits.MaxCuriosityBytes);
			Assert.AreEqual(37708, KingdomCivicMemoryLimits.MaxCivicLeadsBytes);
			Assert.AreNotEqual(KingdomCivicMemoryLimits.MaxCuriosityBytes,
				KingdomCivicMemoryLimits.MaxCivicLeadsBytes,
				"the two books have different maxima; one shared cap would under-bound one of them");
		}

		[Test]
		public void TreatyCapIsTheFrozenLedgerEnvelope()
		{
			Assert.AreEqual(
				Constant(Source("Treaty", "KingdomTreatyCodec.cs"), "MaxEnvelopeBytes"),
				KingdomCivicMemoryLimits.MaxTreatyBytes);
			Assert.AreEqual(241384, KingdomCivicMemoryLimits.MaxTreatyBytes);
		}

		[Test]
		public void CommunalRiteAndGuestFeastCapsAreDerivedFromTheirFrozenRows()
		{
			string rites = Source("Experience", "KingdomCommunalRiteCodec.cs");
			string feasts = Source("Experience", "KingdomGuestFeastCodec.cs");
			int maxRows = Constant(Source("Experience", "KingdomExperienceRules.Validation.cs"),
				"MaxSettlements");
			int rite = Constant(rites, "PayloadHeaderBytes")
				+ maxRows * Constant(rites, "RowBytes")
				+ Constant(rites, "EnvelopeOverheadBytes");
			int feast = Constant(feasts, "PayloadHeaderBytes")
				+ maxRows * Constant(feasts, "RowBytes")
				+ Constant(feasts, "EnvelopeOverheadBytes");

			Assert.AreEqual(1214, rite);
			Assert.AreEqual(12083, feast);
			Assert.AreEqual(rite, KingdomCivicMemoryLimits.MaxCommunalRiteBytes);
			Assert.AreEqual(feast, KingdomCivicMemoryLimits.MaxGuestFeastBytes);
		}

		[Test]
		public void RealmBoundFamilyDecodersOwnOneIngressSnapshot()
		{
			string[] codecs =
			{
				Source("Core", "KingdomCivicArtifactsCodec.cs"),
				Source("Core", "KingdomCivicPracticeCodec.cs"),
				Source("Core", "KingdomBodyHistoryCodec.cs")
			};
			for (int i = 0; i < codecs.Length; i++)
			{
				int cap = codecs[i].IndexOf("Length > MaxEnvelopeBytes",
					StringComparison.Ordinal);
				int clone = codecs[i].IndexOf(".Clone();", cap,
					StringComparison.Ordinal);
				int parse = codecs[i].IndexOf("new MemoryStream(snapshot, false)", clone,
					StringComparison.Ordinal);
				Assert.GreaterOrEqual(cap, 0, "ingress must be bounded before allocation");
				Assert.Greater(clone, cap, "decoder must own one stable ingress snapshot");
				Assert.Greater(parse, clone, "digest and parse must use only that snapshot");
			}
			StringAssert.DoesNotContain("Decode((byte[])",
				Source("Core", "KingdomCivicArtifactsStore.cs"));
			StringAssert.DoesNotContain("Decode((byte[])",
				Source("Core", "KingdomCivicPracticeStore.cs"));
			StringAssert.DoesNotContain("Decode((byte[])",
				Source("Core", "KingdomBodyHistoryStore.cs"));
		}

		[Test]
		public void CumulativeCapIsExactlyTheSumOfTheNineSectionCaps()
		{
			Assert.AreEqual(65774 + 229774 + 32946 + 22031 + 37708 + 241384 + 1214 + 12083 + 196946,
				KingdomCivicMemoryLimits.MaxCumulativePayloadBytes);
			Assert.AreEqual(839860, KingdomCivicMemoryLimits.MaxCumulativePayloadBytes);
			Assert.AreEqual(840048, KingdomCivicMemoryLimits.MaxEnvelopeBytes);
			Assert.AreEqual(KingdomCivicMemoryLimits.EnvelopeOverheadBytes
				+ KingdomCivicMemoryLimits.MaxSections * KingdomCivicMemoryLimits.SectionFramingBytes
				+ KingdomCivicMemoryLimits.MaxCumulativePayloadBytes,
				KingdomCivicMemoryLimits.MaxEnvelopeBytes);
		}

		private static KingdomCivicMemorySection AtCap(int Id, int Extra)
		{
			return new KingdomCivicMemorySection(Id,
				new byte[KingdomCivicMemoryLimits.SectionCap(Id) + Extra]);
		}

		[Test]
		public void EachSectionAcceptsExactlyItsCapAndRefusesOneByteMore()
		{
			int[] ids =
			{
				KingdomCivicMemoryLimits.SectionCivicArtifacts,
				KingdomCivicMemoryLimits.SectionCivicPractice,
				KingdomCivicMemoryLimits.SectionBodyHistory,
				KingdomCivicMemoryLimits.SectionCuriosity,
				KingdomCivicMemoryLimits.SectionCivicLeads,
				KingdomCivicMemoryLimits.SectionTreaty,
				KingdomCivicMemoryLimits.SectionCommunalRite,
				KingdomCivicMemoryLimits.SectionGuestFeast,
				KingdomCivicMemoryLimits.SectionVillageCovenant
			};
			for (int i = 0; i < ids.Length; i++)
			{
				int id = ids[i];
				List<KingdomCivicMemorySection> atCap =
					new List<KingdomCivicMemorySection> { AtCap(id, 0) };
				Assert.DoesNotThrow(
					() => KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(atCap, 0L)),
					"section " + id + " must accept exactly its cap");
				List<KingdomCivicMemorySection> overCap =
					new List<KingdomCivicMemorySection> { AtCap(id, 1) };
				Assert.Throws<InvalidDataException>(
					() => KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(overCap, 0L)),
					"section " + id + " must refuse its cap plus one byte");
			}
		}

		[Test]
		public void AFullSetOfMaximalSectionsFitsTheCumulativeCapExactly()
		{
			List<KingdomCivicMemorySection> full = new List<KingdomCivicMemorySection>();
			long total = 0L;
			for (int id = KingdomCivicMemoryLimits.FirstKnownSection;
				id <= KingdomCivicMemoryLimits.LastKnownSection; id++)
			{
				full.Add(AtCap(id, 0));
				total += KingdomCivicMemoryLimits.SectionCap(id);
			}
			Assert.AreEqual(KingdomCivicMemoryLimits.MaxCumulativePayloadBytes, total,
				"the nine caps must add up to the cumulative cap with nothing spare");

			byte[] encoded = null;
			Assert.DoesNotThrow(
				() => encoded = KingdomCivicMemoryCodec.Encode(
					KingdomCivicMemoryState.Of(full, 0L)),
				"every family at its own maximum at once must still be writable");
			Assert.LessOrEqual(encoded.Length, KingdomCivicMemoryLimits.MaxEnvelopeBytes);
			Assert.AreEqual(full.Count, KingdomCivicMemoryCodec.Decode(encoded, 0L).Count);
		}

		[Test]
		public void RefusesACumulativeTotalOverTheBudgetEvenWhenEverySectionIsWithinItsOwnCap()
		{
			// Future ids are held to the widest known cap, so enough of them can each be lawful
			// while together exceeding what the nine known sections could ever occupy.
			List<KingdomCivicMemorySection> sections = new List<KingdomCivicMemorySection>();
			int count = KingdomCivicMemoryLimits.MaxCumulativePayloadBytes
				/ KingdomCivicMemoryLimits.MaxTreatyBytes + 1;
			Assert.AreEqual(4, count,
				"current nine-family arithmetic requires four maximal future sections to overflow");
			Assert.LessOrEqual(count, KingdomCivicMemoryLimits.MaxSections,
				"the section-count cap must leave this cumulative-overflow case reachable");
			for (int i = 0; i < count; i++)
				sections.Add(new KingdomCivicMemorySection(
					KingdomCivicMemoryLimits.LastKnownSection + 1 + i,
					new byte[KingdomCivicMemoryLimits.MaxTreatyBytes]));
			foreach (KingdomCivicMemorySection section in sections)
				Assert.LessOrEqual(section.Length,
					KingdomCivicMemoryLimits.SectionCap(section.Id));
			Assert.Throws<InvalidDataException>(() => KingdomCivicMemoryCodec.Encode(
				KingdomCivicMemoryState.Of(sections, 0L)),
				"four maximal future sections exceed the cumulative budget and must be refused");
		}
	}
}
#endif
