#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomMirrorGateRulesTests
	{
		private const string KeyA = "r_TAF_MirrorGate_JoppaWorld.11.22.1.1.10_20,10";
		private const string KeyB = "r_TAF_MirrorGate_JoppaWorld.14.19.2.0.10_5,7";
		private const string KeyC = "r_TAF_MirrorGate_JoppaWorld.09.31.0.2.10_31,4";

		[Test]
		public void GateDeclarationsKeepTheirWireAbiAndCopyDefaults()
		{
			ClassicAssert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(KingdomGateVerdict)));
			CollectionAssert.AreEqual(new byte[8] { 0, 1, 2, 3, 4, 5, 6, 7 }, new byte[8]
			{
				(byte)KingdomGateVerdict.Offered,
				(byte)KingdomGateVerdict.Joined,
				(byte)KingdomGateVerdict.Released,
				(byte)KingdomGateVerdict.RefusedCityKeyed,
				(byte)KingdomGateVerdict.RefusedAlreadyKeyed,
				(byte)KingdomGateVerdict.RefusedUnkeyed,
				(byte)KingdomGateVerdict.RefusedFull,
				(byte)KingdomGateVerdict.RefusedNamed
			});
			ClassicAssert.AreEqual(typeof(byte), System.Enum.GetUnderlyingType(typeof(KingdomGateHold)));
			CollectionAssert.AreEqual(new byte[3] { 0, 1, 2 }, new byte[3]
			{
				(byte)KingdomGateHold.Unchanged,
				(byte)KingdomGateHold.Held,
				(byte)KingdomGateHold.Lost
			});

			ClassicAssert.AreEqual("ThousandAndFirst.KingdomGateRow", typeof(KingdomGateRow).FullName);
			System.Reflection.FieldInfo[] fields = typeof(KingdomGateRow).GetFields(
				System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			CollectionAssert.AreEqual(new string[3] { "Key", "City", "Partner" },
				new string[3] { fields[0].Name, fields[1].Name, fields[2].Name });
			for (int i = 0; i < fields.Length; i++)
			{
				ClassicAssert.AreEqual(typeof(string), fields[i].FieldType);
				ClassicAssert.IsTrue(fields[i].IsInitOnly);
			}

			KingdomGateRow blank = new KingdomGateRow(null, null, null);
			ClassicAssert.AreEqual("", blank.Key);
			ClassicAssert.AreEqual("", blank.City);
			ClassicAssert.AreEqual("", blank.Partner);
			KingdomGateRow partnered = new KingdomGateRow("key", "city", "first").WithPartner("second");
			ClassicAssert.AreEqual("key", partnered.Key);
			ClassicAssert.AreEqual("city", partnered.City);
			ClassicAssert.AreEqual("second", partnered.Partner);
		}

		private static KingdomGateRow[] Register(params KingdomGateRow[] rows)
		{
			return rows;
		}

		[Test]
		public void MayRemove_RequiresAValidKeyAbsentFromTheExactRegister()
		{
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, "Kavvat", KeyB),
				new KingdomGateRow(KeyB, "Ossuary Reach", KeyA));
			ClassicAssert.IsFalse(KingdomMirrorGateRules.MayRemove(rows, KeyA));
			ClassicAssert.IsFalse(KingdomMirrorGateRules.MayRemove(rows, KeyB));
			ClassicAssert.IsTrue(KingdomMirrorGateRules.MayRemove(rows, KeyC));
			ClassicAssert.IsFalse(KingdomMirrorGateRules.MayRemove(rows, null));
			ClassicAssert.IsFalse(KingdomMirrorGateRules.MayRemove(rows, ""));
		}

		// --- ComposeLocationKey: the ground names the arch, and names it the same way twice ------

		[Test]
		public void ComposeLocationKey_IsStableForOneCell()
		{
			// Stability is the whole contract: the key survives a reload because it was never
			// stored, only recomputed, and an arch rebuilt on the same cell inherits the crossing
			// rather than orphaning it.
			ClassicAssert.AreEqual(
				KingdomMirrorGateRules.ComposeLocationKey("JoppaWorld.11.22.1.1.10", 20, 10),
				KingdomMirrorGateRules.ComposeLocationKey("JoppaWorld.11.22.1.1.10", 20, 10));
		}

		[Test]
		public void ComposeLocationKey_SeparatesGroundThatIsNotTheSame()
		{
			string here = KingdomMirrorGateRules.ComposeLocationKey("JoppaWorld.11.22.1.1.10", 20, 10);
			ClassicAssert.AreNotEqual(here, KingdomMirrorGateRules.ComposeLocationKey("JoppaWorld.11.22.1.1.10", 10, 20));
			ClassicAssert.AreNotEqual(here, KingdomMirrorGateRules.ComposeLocationKey("JoppaWorld.11.22.1.1.11", 20, 10));
		}

		[TestCase(null, 1, 1)]
		[TestCase("", 1, 1)]
		[TestCase("Joppa|World.1.1.1.1.10", 1, 1)]
		[TestCase("Joppa^World.1.1.1.1.10", 1, 1)]
		[TestCase("JoppaWorld.1.1.1.1.10", -1, 1)]
		[TestCase("JoppaWorld.1.1.1.1.10", 1, -1)]
		public void ComposeLocationKey_RefusesGroundTheRegisterCouldNotStore(string zoneId, int x, int y)
		{
			// A key carrying one of the register's own separators would come back out of the
			// register as two columns, so it is refused where it is made rather than escaped where
			// it is read.
			ClassicAssert.IsNull(KingdomMirrorGateRules.ComposeLocationKey(zoneId, x, y));
		}

		[TestCase(null, false)]
		[TestCase("", false)]
		[TestCase("Kavvat", true)]
		[TestCase("Kav|vat", false)]
		[TestCase("Kav^vat", false)]
		public void Storable_RefusesAnythingTheRegisterWouldGiveBackWrong(string text, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMirrorGateRules.Storable(text));
		}

		// --- the register: read, write, and repair --------------------------------------------

		[TestCase(null)]
		[TestCase("")]
		public void TryParseRegister_NoArchesIsNotAFault(string text)
		{
			KingdomGateRow[] rows;
			int dropped;
			ClassicAssert.IsTrue(KingdomMirrorGateRules.TryParseRegister(text, out rows, out dropped));
			ClassicAssert.AreEqual(0, rows.Length);
			ClassicAssert.AreEqual(0, dropped);
		}

		[Test]
		public void TryParseRegister_RoundTripsWhatFormatWrote()
		{
			KingdomGateRow[] written = Register(
				new KingdomGateRow(KeyA, "Kavvat", KeyB),
				new KingdomGateRow(KeyB, "Ossuary Reach", KeyA));
			KingdomGateRow[] read;
			int dropped;
			ClassicAssert.IsTrue(KingdomMirrorGateRules.TryParseRegister(KingdomMirrorGateRules.FormatRegister(written), out read, out dropped));
			ClassicAssert.AreEqual(0, dropped);
			ClassicAssert.AreEqual(2, read.Length);
			ClassicAssert.AreEqual(KeyA, read[0].Key);
			ClassicAssert.AreEqual("Kavvat", read[0].City);
			ClassicAssert.AreEqual(KeyB, read[0].Partner);
			ClassicAssert.AreEqual(KeyB, read[1].Key);
			ClassicAssert.AreEqual("Ossuary Reach", read[1].City);
			ClassicAssert.AreEqual(KeyA, read[1].Partner);
		}

		[Test]
		public void TryParseRegister_DropsAnUnreadableRowAndKeepsTheRest()
		{
			// One corrupt row must not cost the founder a crossing that is standing perfectly well
			// at the other end, and the drop must be reported rather than absorbed.
			KingdomGateRow[] rows;
			int dropped;
			ClassicAssert.IsFalse(KingdomMirrorGateRules.TryParseRegister(KeyA + "^Kavvat^" + "|" + "nonsense" + "|" + KeyB + "^Ossuary Reach^", out rows, out dropped));
			ClassicAssert.AreEqual(1, dropped);
			ClassicAssert.AreEqual(2, rows.Length);
			ClassicAssert.AreEqual(KeyA, rows[0].Key);
			ClassicAssert.AreEqual(KeyB, rows[1].Key);
		}

		[Test]
		public void TryParseRegister_DropsARowWithNoCity()
		{
			KingdomGateRow[] rows;
			int dropped;
			ClassicAssert.IsFalse(KingdomMirrorGateRules.TryParseRegister(KeyA + "^^", out rows, out dropped));
			ClassicAssert.AreEqual(1, dropped);
			ClassicAssert.AreEqual(0, rows.Length);
		}

		[Test]
		public void TryParseRegister_DropsARepeatedKeyRatherThanLettingItWinEveryLookup()
		{
			KingdomGateRow[] rows;
			int dropped;
			ClassicAssert.IsFalse(KingdomMirrorGateRules.TryParseRegister(KeyA + "^Kavvat^|" + KeyA + "^Somewhere Else^", out rows, out dropped));
			ClassicAssert.AreEqual(1, dropped);
			ClassicAssert.AreEqual(1, rows.Length);
			ClassicAssert.AreEqual("Kavvat", rows[0].City);
		}

		[Test]
		public void TryParseRegister_RefusesPastTheCapRatherThanGrowingForever()
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder();
			for (int i = 0; i <= KingdomMirrorGateRules.MaxGates; i++)
			{
				if (i > 0)
				{
					text.Append(KingdomMirrorGateRules.RowSeparator);
				}
				text.Append("key" + i).Append(KingdomMirrorGateRules.FieldSeparator).Append("city" + i).Append(KingdomMirrorGateRules.FieldSeparator);
			}
			KingdomGateRow[] rows;
			int dropped;
			ClassicAssert.IsFalse(KingdomMirrorGateRules.TryParseRegister(text.ToString(), out rows, out dropped));
			ClassicAssert.AreEqual(KingdomMirrorGateRules.MaxGates, rows.Length);
			ClassicAssert.AreEqual(1, dropped);
		}

		[Test]
		public void FormatRegister_IsEmptyForNoArches()
		{
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.FormatRegister(null));
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.FormatRegister(new KingdomGateRow[0]));
		}

		// --- the register's wire: version token, pre-version saves, and newer builds -------------

		private const string LegacyPair = KeyA + "^Kavvat^" + KeyB + "|" + KeyB + "^Ossuary Reach^" + KeyA;

		[Test]
		public void FormatRegister_OpensWithTheVersionTokenAndChangesNothingElse()
		{
			// Golden text: the exact bytes a v1 register carries. A mutation to the token, the
			// separators, or the column order shows up here and nowhere subtler.
			KingdomGateRow[] written = Register(
				new KingdomGateRow(KeyA, "Kavvat", KeyB),
				new KingdomGateRow(KeyB, "Ossuary Reach", KeyA));
			string text = KingdomMirrorGateRules.FormatRegister(written);
			ClassicAssert.AreEqual("v1", KingdomMirrorGateRules.RegisterVersionToken);
			StringAssert.StartsWith("v1|", text);
			ClassicAssert.AreEqual("v1|" + LegacyPair, text);
			ClassicAssert.AreEqual(LegacyPair, KingdomMirrorGateRules.LegacyRegisterText(written));
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.LegacyRegisterText(null));
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.LegacyRegisterText(new KingdomGateRow[0]));
		}

		[Test]
		public void TryParseRegister_ReadsAPreVersionRegisterExactlyAsBefore()
		{
			// An existing save has no token. It must read with version-1 semantics, unchanged, and
			// only a genuine write may ever carry it forward into the versioned shape.
			KingdomGateRow[] rows;
			int dropped;
			bool future;
			ClassicAssert.IsTrue(KingdomMirrorGateRules.TryParseRegister(LegacyPair, out rows, out dropped, out future));
			ClassicAssert.IsFalse(future);
			ClassicAssert.AreEqual(0, dropped);
			AssertPair(rows);
			ClassicAssert.AreEqual("v1|" + LegacyPair, KingdomMirrorGateRules.FormatRegister(rows));
		}

		[Test]
		public void TryParseRegister_RoundTripsAVersionedRegister()
		{
			KingdomGateRow[] rows;
			int dropped;
			bool future;
			ClassicAssert.IsTrue(KingdomMirrorGateRules.TryParseRegister("v1|" + LegacyPair, out rows, out dropped, out future));
			ClassicAssert.IsFalse(future);
			ClassicAssert.AreEqual(0, dropped);
			AssertPair(rows);
		}

		[TestCase("v1")]
		[TestCase("v1|")]
		public void TryParseRegister_AVersionTokenAloneIsNoArches(string text)
		{
			KingdomGateRow[] rows;
			int dropped;
			bool future;
			ClassicAssert.IsTrue(KingdomMirrorGateRules.TryParseRegister(text, out rows, out dropped, out future));
			ClassicAssert.IsFalse(future);
			ClassicAssert.AreEqual(0, rows.Length);
			ClassicAssert.AreEqual(0, dropped);
		}

		[TestCase("v2|")]
		[TestCase("v10|")]
		[TestCase("v2")]
		public void TryParseRegister_RefusesANewerBuildsRegisterWholeRatherThanRepairingIt(string token)
		{
			// The whole point of the token: a later build's register is not corruption. Nothing is
			// read, nothing is dropped, and the flag is distinct so no caller can mistake the
			// refusal for a damaged row and rewrite the text.
			KingdomGateRow[] rows;
			int dropped;
			bool future;
			string text = token.EndsWith("|") ? token + LegacyPair : token;
			ClassicAssert.IsFalse(KingdomMirrorGateRules.TryParseRegister(text, out rows, out dropped, out future));
			ClassicAssert.IsTrue(future);
			ClassicAssert.AreEqual(0, rows.Length);
			ClassicAssert.AreEqual(0, dropped);
			// The two-count reading refuses the same way, with nothing to repair.
			ClassicAssert.IsFalse(KingdomMirrorGateRules.TryParseRegister(text, out rows, out dropped));
			ClassicAssert.AreEqual(0, rows.Length);
			ClassicAssert.AreEqual(0, dropped);
		}

		[TestCase("v|")]
		[TestCase("V1|")]
		[TestCase("v1x|")]
		[TestCase("vv|")]
		[TestCase("v123456789|")]
		public void TryParseRegister_OnlyAWholeTokenIsATokenAndAnythingElseIsARow(string prefix)
		{
			// Something that is not quite a token is what it always was: an unreadable row of a
			// pre-version register, dropped and counted, never a reason to refuse the whole thing.
			KingdomGateRow[] rows;
			int dropped;
			bool future;
			ClassicAssert.IsFalse(KingdomMirrorGateRules.TryParseRegister(prefix + LegacyPair, out rows, out dropped, out future));
			ClassicAssert.IsFalse(future);
			ClassicAssert.AreEqual(1, dropped);
			AssertPair(rows);
		}

		[Test]
		public void TryParseRegister_StillDropsAndCountsAMalformedVersionedRow()
		{
			KingdomGateRow[] rows;
			int dropped;
			bool future;
			ClassicAssert.IsFalse(KingdomMirrorGateRules.TryParseRegister("v1|" + KeyA + "^Kavvat^|nonsense|" + KeyB + "^Ossuary Reach^", out rows, out dropped, out future));
			ClassicAssert.IsFalse(future);
			ClassicAssert.AreEqual(1, dropped);
			ClassicAssert.AreEqual(2, rows.Length);
			ClassicAssert.AreEqual(KeyA, rows[0].Key);
			ClassicAssert.AreEqual(KeyB, rows[1].Key);
		}

		[Test]
		public void TryParseRegister_TheCapCountsRowsAndNotTheToken()
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder("v1");
			for (int i = 0; i <= KingdomMirrorGateRules.MaxGates; i++)
			{
				text.Append(KingdomMirrorGateRules.RowSeparator);
				text.Append("key" + i).Append(KingdomMirrorGateRules.FieldSeparator).Append("city" + i).Append(KingdomMirrorGateRules.FieldSeparator);
			}
			KingdomGateRow[] rows;
			int dropped;
			bool future;
			ClassicAssert.IsFalse(KingdomMirrorGateRules.TryParseRegister(text.ToString(), out rows, out dropped, out future));
			ClassicAssert.IsFalse(future);
			ClassicAssert.AreEqual(KingdomMirrorGateRules.MaxGates, rows.Length);
			ClassicAssert.AreEqual("key0", rows[0].Key);
			ClassicAssert.AreEqual(1, dropped);
		}

		[Test]
		public void FutureVersionLine_SaysTheRegisterIsLeftAloneAndWhy()
		{
			StringAssert.Contains("newer version", KingdomMirrorGateRules.FutureVersionLine);
			StringAssert.Contains("left exactly as it was", KingdomMirrorGateRules.FutureVersionLine);
		}

		private static void AssertPair(KingdomGateRow[] rows)
		{
			ClassicAssert.AreEqual(2, rows.Length);
			ClassicAssert.AreEqual(KeyA, rows[0].Key);
			ClassicAssert.AreEqual("Kavvat", rows[0].City);
			ClassicAssert.AreEqual(KeyB, rows[0].Partner);
			ClassicAssert.AreEqual(KeyB, rows[1].Key);
			ClassicAssert.AreEqual("Ossuary Reach", rows[1].City);
			ClassicAssert.AreEqual(KeyA, rows[1].Partner);
		}

		// --- lookups ---------------------------------------------------------------------------

		[Test]
		public void IndexOfCity_ReadsCitiesTheWayAFounderDoes()
		{
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			ClassicAssert.AreEqual(0, KingdomMirrorGateRules.IndexOfCity(rows, "kavvat"));
			ClassicAssert.AreEqual(0, KingdomMirrorGateRules.IndexOfCity(rows, "KAVVAT"));
			ClassicAssert.AreEqual(-1, KingdomMirrorGateRules.IndexOfCity(rows, "Kavva"));
			ClassicAssert.AreEqual(-1, KingdomMirrorGateRules.IndexOfCity(rows, null));
		}

		[Test]
		public void IndexOfKey_MatchesAKeyExactlyAndNothingElse()
		{
			// Keys are machine-made and case is meaning in a zone id, so this one is ordinal where
			// the city lookup beside it is not.
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			ClassicAssert.AreEqual(0, KingdomMirrorGateRules.IndexOfKey(rows, KeyA));
			ClassicAssert.AreEqual(-1, KingdomMirrorGateRules.IndexOfKey(rows, KeyA.ToUpperInvariant()));
			ClassicAssert.AreEqual(-1, KingdomMirrorGateRules.IndexOfKey(rows, ""));
			ClassicAssert.AreEqual(-1, KingdomMirrorGateRules.IndexOfKey(null, KeyA));
		}

		[Test]
		public void PartnerOf_IsEmptyForAnArchNothingAnswers()
		{
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.PartnerOf(rows, KeyA));
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.PartnerOf(rows, KeyB));
		}

		// --- TryDedicate ------------------------------------------------------------------------

		[Test]
		public void TryDedicate_FirstArchWaits()
		{
			KingdomGateRow[] next;
			string partner;
			ClassicAssert.AreEqual(KingdomGateVerdict.Offered, KingdomMirrorGateRules.TryDedicate(new KingdomGateRow[0], KeyA, "Kavvat", out next, out partner));
			ClassicAssert.AreEqual("", partner);
			ClassicAssert.AreEqual(1, next.Length);
			ClassicAssert.AreEqual(KeyA, next[0].Key);
			ClassicAssert.AreEqual("", next[0].Partner);
		}

		[Test]
		public void TryDedicate_SecondArchInAnotherCityJoinsBothEnds()
		{
			KingdomGateRow[] first;
			string ignored;
			KingdomMirrorGateRules.TryDedicate(new KingdomGateRow[0], KeyA, "Kavvat", out first, out ignored);
			KingdomGateRow[] next;
			string partner;
			ClassicAssert.AreEqual(KingdomGateVerdict.Joined, KingdomMirrorGateRules.TryDedicate(first, KeyB, "Ossuary Reach", out next, out partner));
			ClassicAssert.AreEqual(KeyA, partner);
			ClassicAssert.AreEqual(2, next.Length);
			// Both ends, or neither: an arch answering something that does not answer back is a
			// one-way crossing, which is not a thing this design has.
			ClassicAssert.AreEqual(KeyB, KingdomMirrorGateRules.PartnerOf(next, KeyA));
			ClassicAssert.AreEqual(KeyA, KingdomMirrorGateRules.PartnerOf(next, KeyB));
		}

		[Test]
		public void TryDedicate_RefusesASecondArchInACityThatAlreadyKeepsOne()
		{
			// A crossing is between two of the founder's cities (END-STATE-CITIES-RESEARCH §4.4);
			// a second arch in the same city would answer the ground the first one answers.
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			KingdomGateRow[] next;
			string partner;
			ClassicAssert.AreEqual(KingdomGateVerdict.RefusedCityKeyed, KingdomMirrorGateRules.TryDedicate(rows, KeyB, "kavvat", out next, out partner));
			ClassicAssert.AreSame(rows, next);
		}

		[Test]
		public void TryDedicate_RefusesAnArchAlreadyInTheRegister()
		{
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			KingdomGateRow[] next;
			string partner;
			ClassicAssert.AreEqual(KingdomGateVerdict.RefusedAlreadyKeyed, KingdomMirrorGateRules.TryDedicate(rows, KeyA, "Kavvat", out next, out partner));
			ClassicAssert.AreSame(rows, next);
		}

		[TestCase(null, "Kavvat")]
		[TestCase("", "Kavvat")]
		[TestCase(KeyA, null)]
		[TestCase(KeyA, "")]
		[TestCase(KeyA, "Kav|vat")]
		[TestCase("key^one", "Kavvat")]
		public void TryDedicate_RefusesWhatItCouldNotWriteDown(string key, string city)
		{
			KingdomGateRow[] next;
			string partner;
			ClassicAssert.AreEqual(KingdomGateVerdict.RefusedNamed, KingdomMirrorGateRules.TryDedicate(new KingdomGateRow[0], key, city, out next, out partner));
		}

		[Test]
		public void TryDedicate_RefusesPastTheRegisterCap()
		{
			KingdomGateRow[] rows = new KingdomGateRow[KingdomMirrorGateRules.MaxGates];
			for (int i = 0; i < rows.Length; i++)
			{
				rows[i] = new KingdomGateRow("key" + i, "city" + i, "key" + i + "x");
			}
			KingdomGateRow[] next;
			string partner;
			ClassicAssert.AreEqual(KingdomGateVerdict.RefusedFull, KingdomMirrorGateRules.TryDedicate(rows, KeyA, "Kavvat", out next, out partner));
			ClassicAssert.AreSame(rows, next);
		}

		[Test]
		public void TryDedicate_LeavesTheRegisterItWasGivenAlone()
		{
			// Copy-on-write: the caller holds the register it read and must be able to trust that a
			// refused act changed nothing under it.
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			KingdomGateRow[] next;
			string partner;
			KingdomMirrorGateRules.TryDedicate(rows, KeyB, "Ossuary Reach", out next, out partner);
			ClassicAssert.AreEqual(1, rows.Length);
			ClassicAssert.AreEqual("", rows[0].Partner);
			ClassicAssert.AreEqual(KeyB, next[0].Partner);
		}

		[Test]
		public void TryDedicate_JoinsTheArchThatHasWaitedLongestAndNotOneAlreadyAnswered()
		{
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, "Kavvat", KeyB),
				new KingdomGateRow(KeyB, "Ossuary Reach", KeyA));
			KingdomGateRow[] next;
			string partner;
			// Three cities, two of them already crossing: the third waits rather than stealing an
			// end off a crossing that already exists.
			ClassicAssert.AreEqual(KingdomGateVerdict.Offered, KingdomMirrorGateRules.TryDedicate(rows, KeyC, "Sallow Ford", out next, out partner));
			ClassicAssert.AreEqual(KeyB, KingdomMirrorGateRules.PartnerOf(next, KeyA));
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.PartnerOf(next, KeyC));
		}

		// --- TryPair: the re-keying seam (QUESTION-BACKLOG QB-1) ---------------------------------

		[Test]
		public void TryPair_PointsTwoArchesAtEachOther()
		{
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, "Kavvat", ""),
				new KingdomGateRow(KeyB, "Ossuary Reach", ""));
			KingdomGateRow[] next;
			ClassicAssert.AreEqual(KingdomGateVerdict.Joined, KingdomMirrorGateRules.TryPair(rows, KeyA, KeyB, out next));
			ClassicAssert.AreEqual(KeyB, KingdomMirrorGateRules.PartnerOf(next, KeyA));
			ClassicAssert.AreEqual(KeyA, KingdomMirrorGateRules.PartnerOf(next, KeyB));
		}

		[Test]
		public void TryPair_ReleasesWhateverWasAnsweringEitherEnd()
		{
			// Nothing may be left holding a key that now answers somebody else, or a founder walks
			// into an arch that says it is open and arrives nowhere.
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, "Kavvat", KeyB),
				new KingdomGateRow(KeyB, "Ossuary Reach", KeyA),
				new KingdomGateRow(KeyC, "Sallow Ford", ""));
			KingdomGateRow[] next;
			ClassicAssert.AreEqual(KingdomGateVerdict.Joined, KingdomMirrorGateRules.TryPair(rows, KeyA, KeyC, out next));
			ClassicAssert.AreEqual(KeyC, KingdomMirrorGateRules.PartnerOf(next, KeyA));
			ClassicAssert.AreEqual(KeyA, KingdomMirrorGateRules.PartnerOf(next, KeyC));
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.PartnerOf(next, KeyB));
		}

		[Test]
		public void TryPair_LosesNoArchWhenTheRealmIsRekeyed()
		{
			// QB-1's whole requirement: the hub topology the capital wave will bring is a rewrite of
			// the partner column, so re-keying must never cost a row. Every arch that stood before
			// the rewrite is standing after it.
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, "Kavvat", KeyB),
				new KingdomGateRow(KeyB, "Ossuary Reach", KeyA),
				new KingdomGateRow(KeyC, "Sallow Ford", ""));
			KingdomGateRow[] next;
			KingdomMirrorGateRules.TryPair(rows, KeyA, KeyC, out next);
			KingdomGateRow[] again;
			KingdomMirrorGateRules.TryPair(next, KeyB, KeyC, out again);
			ClassicAssert.AreEqual(3, again.Length);
			ClassicAssert.GreaterOrEqual(KingdomMirrorGateRules.IndexOfKey(again, KeyA), 0);
			ClassicAssert.GreaterOrEqual(KingdomMirrorGateRules.IndexOfKey(again, KeyB), 0);
			ClassicAssert.GreaterOrEqual(KingdomMirrorGateRules.IndexOfKey(again, KeyC), 0);
		}

		[Test]
		public void TryPair_RefusesAnArchThatIsNotInTheRegister()
		{
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			KingdomGateRow[] next;
			ClassicAssert.AreEqual(KingdomGateVerdict.RefusedUnkeyed, KingdomMirrorGateRules.TryPair(rows, KeyA, KeyB, out next));
			ClassicAssert.AreSame(rows, next);
		}

		[Test]
		public void TryPair_RefusesAnArchAnsweringItself()
		{
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			KingdomGateRow[] next;
			ClassicAssert.AreEqual(KingdomGateVerdict.RefusedNamed, KingdomMirrorGateRules.TryPair(rows, KeyA, KeyA, out next));
			ClassicAssert.AreSame(rows, next);
		}

		[Test]
		public void TryPair_RefusesTwoArchesInOneCity()
		{
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, "Kavvat", ""),
				new KingdomGateRow(KeyB, "Kavvat", ""));
			KingdomGateRow[] next;
			ClassicAssert.AreEqual(KingdomGateVerdict.RefusedCityKeyed, KingdomMirrorGateRules.TryPair(rows, KeyA, KeyB, out next));
			ClassicAssert.AreSame(rows, next);
		}

		// --- TryRelease -------------------------------------------------------------------------

		[Test]
		public void TryRelease_TakesTheArchOutAndUnkeysWhatAnsweredIt()
		{
			KingdomGateRow[] rows = Register(
				new KingdomGateRow(KeyA, "Kavvat", KeyB),
				new KingdomGateRow(KeyB, "Ossuary Reach", KeyA));
			KingdomGateRow[] next;
			string orphan;
			ClassicAssert.AreEqual(KingdomGateVerdict.Released, KingdomMirrorGateRules.TryRelease(rows, KeyA, out next, out orphan));
			ClassicAssert.AreEqual(KeyB, orphan);
			ClassicAssert.AreEqual(1, next.Length);
			ClassicAssert.AreEqual(KeyB, next[0].Key);
			ClassicAssert.AreEqual("", next[0].Partner);
		}

		[Test]
		public void TryRelease_ReportsNoOrphanWhenNothingWasAnswering()
		{
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			KingdomGateRow[] next;
			string orphan;
			ClassicAssert.AreEqual(KingdomGateVerdict.Released, KingdomMirrorGateRules.TryRelease(rows, KeyA, out next, out orphan));
			ClassicAssert.AreEqual("", orphan);
			ClassicAssert.AreEqual(0, next.Length);
		}

		[Test]
		public void TryRelease_RefusesAnArchThatWasNeverKeyed()
		{
			KingdomGateRow[] rows = Register(new KingdomGateRow(KeyA, "Kavvat", ""));
			KingdomGateRow[] next;
			string orphan;
			ClassicAssert.AreEqual(KingdomGateVerdict.RefusedUnkeyed, KingdomMirrorGateRules.TryRelease(rows, KeyB, out next, out orphan));
			ClassicAssert.AreSame(rows, next);
		}

		// --- the standing draw ------------------------------------------------------------------

		[Test]
		public void OpenChargePerDay_IsARealPriceInTheCurrencyTheReportUses()
		{
			// A mutation to zero would make the crossing free, which is the one thing Addendum 22
			// A2 rules it must not be.
			ClassicAssert.AreEqual(3 * KingdomPowerRules.PostDailyNeedCharge, KingdomMirrorGateRules.OpenChargePerDay);
			ClassicAssert.Greater(KingdomMirrorGateRules.OpenChargePerDay, KingdomPowerRules.WaterWheelChargePerDay);
		}

		[TestCase(0L, 0)]
		[TestCase(-1L, 0)]
		[TestCase(-4000L, 0)]
		public void DrawForDays_OwesNothingForNoDays(long days, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomMirrorGateRules.DrawForDays(days));
		}

		[TestCase(1L)]
		[TestCase(2L)]
		[TestCase(7L)]
		[TestCase(100L)]
		public void DrawForDays_ScalesWithTheWholeElapsedAndIsNeverForgiven(long days)
		{
			// STANDARDS §8: a cap is a cost that quietly stops scaling. A hundred days away costs a
			// hundred days, and what bounds it is the city's own salt rather than a ceiling here.
			ClassicAssert.AreEqual((int)(days * KingdomMirrorGateRules.OpenChargePerDay), KingdomMirrorGateRules.DrawForDays(days));
		}

		[Test]
		public void DrawForDays_SaturatesRatherThanWrapping()
		{
			// A wrapped total would come back as a small number and light an arch nobody paid for.
			ClassicAssert.AreEqual(int.MaxValue, KingdomMirrorGateRules.DrawForDays(long.MaxValue / 2L));
			ClassicAssert.AreEqual(int.MaxValue, KingdomMirrorGateRules.DrawForDays(1000000L));
		}

		[Test]
		public void JudgeHold_DecidesNothingWhereNoDayTurnedOver()
		{
			// An arch is not closed by being looked at between days.
			ClassicAssert.AreEqual(KingdomGateHold.Unchanged, KingdomMirrorGateRules.JudgeHold(0, 0));
			ClassicAssert.AreEqual(KingdomGateHold.Unchanged, KingdomMirrorGateRules.JudgeHold(-1, 999999));
		}

		[Test]
		public void JudgeHold_PaysInFullOrNotAtAll()
		{
			// Works stop whole: a half-lit arch is not a thing a founder can see or reason about,
			// which is the same ruling KingdomPower.Deliver makes about a half-lit forge. The
			// off-by-one either side is the mutation this is here to catch.
			int owed = KingdomMirrorGateRules.OpenChargePerDay;
			ClassicAssert.AreEqual(KingdomGateHold.Held, KingdomMirrorGateRules.JudgeHold(owed, owed));
			ClassicAssert.AreEqual(KingdomGateHold.Held, KingdomMirrorGateRules.JudgeHold(owed, owed + 1));
			ClassicAssert.AreEqual(KingdomGateHold.Lost, KingdomMirrorGateRules.JudgeHold(owed, owed - 1));
			ClassicAssert.AreEqual(KingdomGateHold.Lost, KingdomMirrorGateRules.JudgeHold(owed, 0));
		}

		// --- what the founder is told -----------------------------------------------------------

		[Test]
		public void RefusalLine_EveryRefusalSaysSomething()
		{
			// A refusal that returned an empty string would be a silent stall, which is the one
			// thing 7b exists to forbid.
			KingdomGateVerdict[] refusals = new KingdomGateVerdict[5]
			{
				KingdomGateVerdict.RefusedCityKeyed,
				KingdomGateVerdict.RefusedAlreadyKeyed,
				KingdomGateVerdict.RefusedUnkeyed,
				KingdomGateVerdict.RefusedFull,
				KingdomGateVerdict.RefusedNamed
			};
			for (int i = 0; i < refusals.Length; i++)
			{
				ClassicAssert.IsNotEmpty(KingdomMirrorGateRules.RefusalLine(refusals[i], "Kavvat"), refusals[i].ToString());
			}
		}

		[Test]
		public void RefusalLine_NothingIsSaidAboutTheAbsenceOfAProblem()
		{
			// STANDARDS 7b's other half: a settlement that announced its successes as refusals
			// would be a settlement nobody reads.
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.RefusalLine(KingdomGateVerdict.Offered, "Kavvat"));
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.RefusalLine(KingdomGateVerdict.Joined, "Kavvat"));
			ClassicAssert.AreEqual("", KingdomMirrorGateRules.RefusalLine(KingdomGateVerdict.Released, "Kavvat"));
		}

		[Test]
		public void RefusalLine_TheCityInTheWayIsNamed()
		{
			StringAssert.Contains("Kavvat", KingdomMirrorGateRules.RefusalLine(KingdomGateVerdict.RefusedCityKeyed, "Kavvat"));
		}

		[Test]
		public void WentDarkLine_NamesTheArrestAndNotOnlyTheDoom()
		{
			// STANDARDS §5 clause 4 and 7b: a brink sentence names what to DO about it.
			string line = KingdomMirrorGateRules.WentDarkLine("Kavvat");
			StringAssert.Contains("Kavvat", line);
			StringAssert.Contains(KingdomMirrorGateRules.OpenChargePerDay.ToString(), line);
			StringAssert.Contains("wheel", line);
		}

		[Test]
		public void DedicationPrompt_DisclosesTheWholeCostBeforeAnythingIsCommitted()
		{
			string prompt = KingdomMirrorGateRules.DedicationPrompt("Kavvat");
			StringAssert.Contains(KingdomMirrorGateRules.OpenChargePerDay.ToString(), prompt);
			// Addendum 22 A2: the draw IS the price of the crossing, and the founder is told that
			// in the same breath so they never go looking for a second toll.
			StringAssert.Contains("Crossing costs nothing beyond that", prompt);
		}

		[Test]
		public void DescriptionLine_SaysWhichOfTheFourStatesTheArchIsIn()
		{
			string unkeyed = KingdomMirrorGateRules.DescriptionLine(false, null, false);
			string waiting = KingdomMirrorGateRules.DescriptionLine(true, null, false);
			string open = KingdomMirrorGateRules.DescriptionLine(true, "Kavvat", false);
			string dark = KingdomMirrorGateRules.DescriptionLine(true, "Kavvat", true);
			ClassicAssert.AreNotEqual(unkeyed, waiting);
			ClassicAssert.AreNotEqual(waiting, open);
			ClassicAssert.AreNotEqual(open, dark);
			StringAssert.Contains("Kavvat", open);
			StringAssert.Contains("Kavvat", dark);
		}

		[Test]
		public void Named_FallsBackToAWordRatherThanAnEmptyGap()
		{
			ClassicAssert.AreEqual("the city", KingdomMirrorGateRules.Named(null));
			ClassicAssert.AreEqual("the city", KingdomMirrorGateRules.Named(""));
			ClassicAssert.AreEqual("Kavvat", KingdomMirrorGateRules.Named("  Kavvat  "));
		}

		[Test]
		public void JoinedLine_NamesBothCities()
		{
			string line = KingdomMirrorGateRules.JoinedLine("Kavvat", "Ossuary Reach");
			StringAssert.Contains("Kavvat", line);
			StringAssert.Contains("Ossuary Reach", line);
		}
	}
}
#endif
