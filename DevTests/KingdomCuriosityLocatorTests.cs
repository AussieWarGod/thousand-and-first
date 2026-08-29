#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The locator grammar, held to the engine's real ranges at every edge.
	/// <para>
	/// Each numeric component gets four cases &mdash; one below its floor, its floor, its ceiling,
	/// and one above it &mdash; because an off-by-one here is not a validation nicety. Past the
	/// ceiling the engine's own <c>Location2D.Get</c> returns null and the note has no position on
	/// the map at all; below the floor <c>ZoneManager</c> wraps to the far side of the world.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomCuriosityLocatorTests
	{
		// ParasangX: floor 0, ceiling 83 (249 / 3, the last column Location2D.Get accepts).
		[TestCase("JoppaWorld.-1.0.0.0.0", false)]
		[TestCase("JoppaWorld.0.0.0.0.0", true)]
		[TestCase("JoppaWorld.83.0.0.0.0", true)]
		[TestCase("JoppaWorld.84.0.0.0.0", false)]
		// ParasangY: floor 0, ceiling 28 (84 / 3).
		[TestCase("JoppaWorld.0.-1.0.0.0", false)]
		[TestCase("JoppaWorld.0.28.0.0.0", true)]
		[TestCase("JoppaWorld.0.29.0.0.0", false)]
		// ZoneX and ZoneY: Definitions.Width and Height are 3, so 0 through 2.
		[TestCase("JoppaWorld.0.0.-1.0.0", false)]
		[TestCase("JoppaWorld.0.0.2.0.0", true)]
		[TestCase("JoppaWorld.0.0.3.0.0", false)]
		[TestCase("JoppaWorld.0.0.0.-1.0", false)]
		[TestCase("JoppaWorld.0.0.0.2.0", true)]
		[TestCase("JoppaWorld.0.0.0.3.0", false)]
		// ZoneZ: Definitions.Layers is 50, so 0 through 49.
		[TestCase("JoppaWorld.0.0.0.0.-1", false)]
		[TestCase("JoppaWorld.0.0.0.0.49", true)]
		[TestCase("JoppaWorld.0.0.0.0.50", false)]
		[TestCase("JoppaWorld.0.0.0.0.255", false)]
		public void EveryComponentIsHeldToItsRealRange(string locator, bool valid)
		{
			Assert.AreEqual(valid, KingdomCuriosityRules.TryFullLocator(locator), locator);
		}

		/// <summary>
		/// The last parasang column is only reachable at zone offset zero, because the bound that
		/// matters is the resolved cell and not either component alone. A per-component check that
		/// stopped at 83 and 2 would accept 83.2, which resolves to column 251 and has no cell.
		/// </summary>
		[TestCase("JoppaWorld.83.0.0.0.10", true)]
		[TestCase("JoppaWorld.83.0.1.0.10", false)]
		[TestCase("JoppaWorld.82.0.2.0.10", true)]
		[TestCase("JoppaWorld.0.28.0.0.10", true)]
		[TestCase("JoppaWorld.0.28.0.1.10", false)]
		[TestCase("JoppaWorld.0.27.0.2.10", true)]
		public void TheResolvedCellIsCheckedAndNotJustEachComponent(string locator, bool valid)
		{
			Assert.AreEqual(valid, KingdomCuriosityRules.TryFullLocator(locator), locator);
		}

		/// <summary>
		/// Lexical refusals. Every one of these parses cleanly under <c>int.TryParse</c>, and
		/// several parse cleanly under the engine's own parser, which is exactly why the rule is
		/// a reassembly identity rather than a parse.
		/// </summary>
		[TestCase(null, TestName = "Locator_Null")]
		[TestCase("", TestName = "Locator_Empty")]
		[TestCase("JoppaWorld.010.20.1.2.10", TestName = "Locator_LeadingZero")]
		[TestCase("JoppaWorld.10.020.1.2.10", TestName = "Locator_LeadingZeroParasangY")]
		[TestCase("JoppaWorld.10.20.1.2.010", TestName = "Locator_LeadingZeroLayer")]
		[TestCase("JoppaWorld.00.20.1.2.10", TestName = "Locator_PaddedZero")]
		[TestCase("JoppaWorld.+10.20.1.2.10", TestName = "Locator_LeadingPlus")]
		[TestCase("JoppaWorld.-0.20.1.2.10", TestName = "Locator_NegativeZero")]
		[TestCase(" JoppaWorld.10.20.1.2.10", TestName = "Locator_LeadingSpace")]
		[TestCase("JoppaWorld.10.20.1.2.10 ", TestName = "Locator_TrailingSpace")]
		[TestCase("JoppaWorld. 10.20.1.2.10", TestName = "Locator_SpaceBeforeComponent")]
		[TestCase("JoppaWorld.10 .20.1.2.10", TestName = "Locator_SpaceAfterComponent")]
		[TestCase("Joppa World.10.20.1.2.10", TestName = "Locator_SpaceInWorld")]
		[TestCase("JoppaWorld.10.20.1.2.10@instance", TestName = "Locator_Suffix")]
		[TestCase("JoppaWorld@Blueprint.10.20.1.2.10", TestName = "Locator_BlueprintWorld")]
		[TestCase("JoppaWorld.10.20.1.2.10.0", TestName = "Locator_SevenParts")]
		[TestCase("JoppaWorld.10.20.1.2", TestName = "Locator_FiveParts")]
		[TestCase(".10.20.1.2.10", TestName = "Locator_EmptyWorld")]
		[TestCase("JoppaWorld.10.20.1..10", TestName = "Locator_EmptyComponent")]
		[TestCase("JoppaWorld.1e1.20.1.2.10", TestName = "Locator_Exponent")]
		[TestCase("JoppaWorld.0x10.20.1.2.10", TestName = "Locator_Hex")]
		[TestCase("JoppaWorld.10.20.1.2.1O", TestName = "Locator_LetterOh")]
		[TestCase("JoppaWorld.99999999999.20.1.2.10", TestName = "Locator_Overflow")]
		[TestCase("the salt dunes", TestName = "Locator_Prose")]
		[TestCase("the well below Joppa", TestName = "Locator_ProsePlace")]
		public void NothingThatMerelyResemblesALocatorIsAccepted(string locator)
		{
			Assert.IsFalse(KingdomCuriosityRules.TryFullLocator(locator), locator ?? "<null>");
		}

		/// <summary>A world segment may be long, but not unbounded, and the whole locator's
		/// character bound is derived from that one choice rather than asserted beside it.</summary>
		[Test]
		public void TheWorldSegmentIsBoundedAndTheLocatorBoundFollowsFromIt()
		{
			string longest = new string('W', KingdomCuriosityRules.MaxWorldIdChars);
			Assert.IsTrue(KingdomCuriosityRules.TryFullLocator(longest + ".83.28.0.0.49"));
			Assert.IsFalse(KingdomCuriosityRules.TryFullLocator(longest + "W.83.28.0.0.49"));
			Assert.AreEqual(KingdomCuriosityRules.MaxWorldIdChars
				+ KingdomCuriosityRules.LocatorSeparators
				+ KingdomCuriosityRules.MaxLocatorNumericChars,
				KingdomCuriosityRules.MaxLocatorChars);
			Assert.AreEqual((longest + ".83.28.0.0.49").Length,
				KingdomCuriosityRules.MaxLocatorChars);
		}

		/// <summary>The components handed back are the ones the caller can act on, and the
		/// reassembly of them is the input.</summary>
		[Test]
		public void AProvenLocatorHandsBackComponentsThatReassembleToItself()
		{
			Assert.IsTrue(KingdomCuriosityRules.TryFullLocator("JoppaWorld.10.20.1.2.11",
				out string world, out int px, out int py, out int zx, out int zy, out int zz));
			Assert.AreEqual("JoppaWorld", world);
			Assert.AreEqual(10, px); Assert.AreEqual(20, py);
			Assert.AreEqual(1, zx); Assert.AreEqual(2, zy); Assert.AreEqual(11, zz);
			Assert.AreEqual("JoppaWorld.10.20.1.2.11",
				KingdomCuriosityRules.Assemble(world, px, py, zx, zy, zz));
		}

		/// <summary>A refused locator hands back nothing a caller could mistake for a place.</summary>
		[Test]
		public void ARefusedLocatorNamesNoPlaceAtAll()
		{
			Assert.IsFalse(KingdomCuriosityRules.TryFullLocator("JoppaWorld.010.20.1.2.10",
				out string world, out int px, out int py, out int zx, out int zy, out int zz));
			Assert.IsNull(world);
			Assert.AreEqual(-1, px); Assert.AreEqual(-1, py);
			Assert.AreEqual(-1, zx); Assert.AreEqual(-1, zy); Assert.AreEqual(-1, zz);
		}

		/// <summary>
		/// The ranges are derived, not typed twice. If the engine's grid ever changes, these
		/// arithmetic identities are what carries the change through instead of a stale literal.
		/// </summary>
		[Test]
		public void EveryRangeIsDerivedFromTheEnginesOwnGrid()
		{
			Assert.AreEqual(3, KingdomCuriosityRules.ZonesPerParasang);
			Assert.AreEqual(2, KingdomCuriosityRules.MaxZoneX);
			Assert.AreEqual(2, KingdomCuriosityRules.MaxZoneY);
			Assert.AreEqual(50, KingdomCuriosityRules.LayerCount);
			Assert.AreEqual(49, KingdomCuriosityRules.MaxZoneZ);
			Assert.AreEqual(250, KingdomCuriosityRules.ResolvedWidth);
			Assert.AreEqual(85, KingdomCuriosityRules.ResolvedHeight);
			Assert.AreEqual(83, KingdomCuriosityRules.MaxParasangX);
			Assert.AreEqual(28, KingdomCuriosityRules.MaxParasangY);
			Assert.AreEqual(77, KingdomCuriosityRules.MaxLocatorChars);
		}

		/// <summary>
		/// Unicode, not ASCII. A world name padded with a non-breaking space, joined with a
		/// zero-width joiner, or steered with a right-to-left override reads identically to a real
		/// one everywhere a founder can see it, and none of those are C0 or DEL.
		/// </summary>
		[TestCase("\u00A0", TestName = "World_NoBreakSpace")]
		[TestCase("\u200B", TestName = "World_ZeroWidthSpace")]
		[TestCase("\u200D", TestName = "World_ZeroWidthJoiner")]
		[TestCase("\u200F", TestName = "World_RightToLeftMark")]
		[TestCase("\u202E", TestName = "World_RightToLeftOverride")]
		[TestCase("\uFEFF", TestName = "World_ByteOrderMark")]
		[TestCase("\u0085", TestName = "World_NextLine")]
		[TestCase("\u3000", TestName = "World_IdeographicSpace")]
		[TestCase("\u0000", TestName = "World_Nul")]
		[TestCase("\u007F", TestName = "World_Delete")]
		public void AWorldNameRefusesEveryInvisibleCharacterNotJustTheAsciiOnes(string intruder)
		{
			Assert.IsFalse(KingdomCuriosityRules.ValidWorldId("Joppa" + intruder + "World"));
			Assert.IsFalse(KingdomCuriosityRules.TryFullLocator(
				"Joppa" + intruder + "World.10.20.1.2.10"));
		}

		/// <summary>
		/// Lone surrogates, built in code rather than declared in an attribute.
		/// <para>
		/// An attribute argument is stored as UTF-8 in metadata, and a lone surrogate has no UTF-8
		/// encoding, so the compiler substitutes the replacement character and a test case written
		/// that way would quietly assert something else entirely. Constructing the char here is
		/// the only way to hand the real thing to the rule under test.
		/// </para>
		/// </summary>
		[Test]
		public void ALoneSurrogateIsRefusedInAWorldNameByEitherGrammar()
		{
			foreach (char lone in new[] { '\uD800', '\uDBFF', '\uDC00', '\uDFFF' })
			{
				string world = "Joppa" + lone + "World";
				Assert.IsFalse(KingdomCuriosityRules.ValidWorldId(world),
					"U+" + ((int)lone).ToString("X4"));
				Assert.IsFalse(KingdomCuriosityRules.TryFullLocator(world + ".10.20.1.2.10"));
				Assert.IsFalse(KingdomCuriosityRules.LegacyFullLocator(world + ".10.20.1.2.10"),
					"the historical grammar was never a door for impossible text");
			}
			string trailing = "JoppaWorld" + '\uD83C';
			Assert.IsFalse(KingdomCuriosityRules.ValidWorldId(trailing));
		}

		[Test]
		public void APairedSurrogateIsOrdinaryTextAndALoneOneIsNot()
		{
			Assert.IsTrue(KingdomCuriosityRules.Utf8Encodable("a \U0001F300 b"));
			Assert.IsTrue(KingdomCuriosityRules.Utf8Encodable(""));
			Assert.IsFalse(KingdomCuriosityRules.Utf8Encodable("a \uD800 b"));
			Assert.IsFalse(KingdomCuriosityRules.Utf8Encodable("a \uDC00 b"));
			Assert.IsFalse(KingdomCuriosityRules.Utf8Encodable("trailing \uD83C"));
			Assert.IsFalse(KingdomCuriosityRules.Utf8Encodable(null));
		}

		/// <summary>
		/// The historical grammar, which existing saves were written against and which this build
		/// still reads. It is wider than the canonical one in exactly the places the first build
		/// was wider, and no wider: prose was never a place then either.
		/// </summary>
		[TestCase("JoppaWorld.10.20.1.2.10", true, TestName = "Legacy_Canonical")]
		[TestCase("JoppaWorld.010.20.1.2.10", true, TestName = "Legacy_LeadingZero")]
		[TestCase("JoppaWorld.99999.20.1.2.10", true, TestName = "Legacy_HugeParasang")]
		[TestCase("JoppaWorld.10.20.1.2.255", true, TestName = "Legacy_Layer255")]
		[TestCase("JoppaWorld.10.20.1.2.256", false, TestName = "Legacy_Layer256")]
		[TestCase("JoppaWorld.10.20.3.2.10", false, TestName = "Legacy_ZoneXOutOfRange")]
		[TestCase("JoppaWorld.-1.20.1.2.10", false, TestName = "Legacy_NegativeParasang")]
		[TestCase("JoppaWorld.10.20.1.2", false, TestName = "Legacy_FiveParts")]
		[TestCase(".10.20.1.2.10", false, TestName = "Legacy_EmptyWorld")]
		[TestCase("the salt dunes", false, TestName = "Legacy_Prose")]
		[TestCase("the well below Joppa", false, TestName = "Legacy_ProsePlace")]
		[TestCase(null, false, TestName = "Legacy_Null")]
		public void TheHistoricalGrammarIsWiderInTheOldPlacesAndNowhereElse(string locator,
			bool valid)
		{
			Assert.AreEqual(valid, KingdomCuriosityRules.LegacyFullLocator(locator),
				locator ?? "<null>");
		}

		/// <summary>
		/// The two grammars never swap doors. A stored revision 1 row may carry either, because
		/// its own build promised it the wider one; anything authored today must be canonical,
		/// whatever version it claims.
		/// </summary>
		[Test]
		public void OnlyAStoredRevisionOneRowMayCarryTheHistoricalGrammar()
		{
			const string legacy = "JoppaWorld.010.20.1.2.255";
			Assert.IsFalse(KingdomCuriosityRules.TryFullLocator(legacy));
			Assert.IsTrue(KingdomCuriosityRules.LegacyFullLocator(legacy));
			Assert.IsTrue(KingdomCuriosityRules.StorableLocator(
				KingdomCuriosityReceipt.FirstVersion, legacy));
			Assert.IsFalse(KingdomCuriosityRules.StorableLocator(
				KingdomCuriosityReceipt.CategoryVersion, legacy),
				"a revision 2 row could only have been written after the grammar tightened");
			Assert.IsFalse(KingdomCuriosityRules.StorableLocator(
				KingdomCuriosityReceipt.FirstVersion, "the salt dunes"),
				"prose was not a place under either grammar");

			// New authorship goes through the note and the cause, and both demand canonical.
			KingdomCuriosityBook book = new KingdomCuriosityBook();
			System.Collections.Generic.List<KingdomCuriosityNote> notes =
				new System.Collections.Generic.List<KingdomCuriosityNote>
				{
					new KingdomCuriosityNote("taf:note:legacy", legacy, "an old place",
						"Historic Sites", true)
				};
			Assert.IsFalse(KingdomCuriosityRules.TryPrepare(book, 0L, LegacyCause(), notes,
				out _, out _),
				"a historical locator must not be accepted as new authorship");
			Assert.AreEqual(0, book.Rows.Count);
			Assert.IsNull(KingdomCivicLeadRules.LeadId("taf:delve:one", legacy));
		}

		private static KingdomCuriosityCause LegacyCause() => new KingdomCuriosityCause
		{
			SourceId = "taf:source:legacy", SourceVersion = 1,
			SettlementId = "taf:settlement:legacy", CuratorResidentId = 1,
			CuratorName = "Ari", CuratorObjectId = "taf:object:ari",
			Reason = "an authored reason", RequiredCategory = "Historic Sites",
			CompletedTick = 1L
		};

		/// <summary>A lead identity is derived from a canonical locator and refuses anything
		/// else, so no prose can ever become a journal note's id.</summary>
		[Test]
		public void ALeadIdentityRefusesEveryLocatorTheGrammarRefuses()
		{
			Assert.IsNotNull(KingdomCivicLeadRules.LeadId("taf:delve:one",
				"JoppaWorld.10.20.1.2.10"));
			Assert.IsNull(KingdomCivicLeadRules.LeadId("taf:delve:one", "the salt dunes"));
			Assert.IsNull(KingdomCivicLeadRules.LeadId("taf:delve:one",
				"JoppaWorld.010.20.1.2.10"));
			Assert.IsNull(KingdomCivicLeadRules.LeadId("taf:delve:one",
				"JoppaWorld.10.20.1.2.50"));
			Assert.IsNull(KingdomCivicLeadRules.LeadId(null, "JoppaWorld.10.20.1.2.10"));
			Assert.AreEqual(KingdomCivicLeadRules.LeadIdChars,
				KingdomCivicLeadRules.LeadId("taf:delve:one", "JoppaWorld.10.20.1.2.10").Length);
		}
	}
}
#endif
