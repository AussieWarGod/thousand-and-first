#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The seat swap is where a city can be lost without a message: a capture that drops one
	/// field drops it every time the founder walks between cities, and nothing in the game says
	/// so. These tests reflect over <see cref="KingdomSettlement"/> rather than listing its
	/// fields, so a field added tomorrow is covered today.
	/// </summary>
	public class SettlementSeatTests
	{
		/// <summary>Realm state: one faction, one history. None of this may live on a city, or
		/// walking between cities would rewrite the realm.</summary>
		private static readonly string[] RealmOnlyFields = new string[12]
		{
			"KingdomFactionName", "KingdomDisplayName", "Standings", "ChronicleEntries", "OutsiderEntries",
			"SerializationVersion", "LoadFailed", "HomecomingDays", "ActiveDealKeys", "ActiveDealFactions",
			"DealNextTicks", "Away"
		};

		/// <summary>Fields that must be on a city, named here only so the reflective tests cannot
		/// pass by finding nothing at all.</summary>
		private static readonly string[] SettlementFields = new string[6]
		{
			"SettlementName", "Vocation", "Population", "ClaimedZones", "Ledger", "LastHeartbeatTick"
		};

		[Test]
		public void CaptureAndRestoreCarryEveryFieldACityHolds()
		{
			KingdomSettlement seat = new KingdomSettlement();
			Dictionary<string, object> written = FillEveryField(seat);
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(seat);
			KingdomSettlement restored = new KingdomSettlement();
			captured.WriteTo(restored);
			foreach (FieldInfo field in KingdomSettlement.CarriedFields())
			{
				object expected = written[field.Name];
				object actual = field.GetValue(restored);
				if (field.FieldType.IsValueType)
				{
					Assert.AreEqual(expected, actual, "field " + field.Name + " was not carried through capture and restore");
				}
				else
				{
					Assert.AreSame(expected, actual, "field " + field.Name + " was not carried through capture and restore");
				}
			}
		}

		[Test]
		public void CapturedContainersAreHandedOverNotCloned()
		{
			// The contract the swap depends on: the seat and the record never both stay live, so
			// the rosters and the ledger move by reference rather than being copied every time
			// the founder crosses a zone line.
			KingdomSettlement seat = new KingdomSettlement();
			seat.RosterNames.Add("Ptoh");
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(seat);
			Assert.AreSame(seat.RosterNames, captured.RosterNames);
			Assert.AreSame(seat.Ledger, captured.Ledger);
		}

		[Test]
		public void ASeatMissingAFieldIsRefusedAndNamesIt()
		{
			List<string> mismatches = KingdomSettlement.SeatMismatches(typeof(PartialSeat));
			Assert.AreEqual(KingdomSettlement.CarriedFields().Length - 2, mismatches.Count, "every field the seat cannot carry must be named");
			bool refused = false;
			try
			{
				new KingdomSettlement().ReadFrom(new PartialSeat());
			}
			catch (KingdomSeatMismatchException)
			{
				refused = true;
			}
			Assert.IsTrue(refused, "reading from an incomplete seat must throw rather than silently drop a city");
		}

		[Test]
		public void ARefusedWriteLeavesTheSeatUntouched()
		{
			KingdomSettlement settlement = new KingdomSettlement();
			settlement.SettlementName = "Kavvat";
			settlement.Population = 7;
			PartialSeat seat = new PartialSeat();
			bool refused = false;
			try
			{
				settlement.WriteTo(seat);
			}
			catch (KingdomSeatMismatchException)
			{
				refused = true;
			}
			Assert.IsTrue(refused);
			Assert.IsNull(seat.SettlementName, "a refused write must write nothing at all");
			Assert.AreEqual(0, seat.Population, "a refused write must write nothing at all");
		}

		[Test]
		public void AMistypedCounterpartIsAMismatch()
		{
			List<string> mismatches = KingdomSettlement.SeatMismatches(typeof(MistypedSeat));
			bool named = false;
			foreach (string mismatch in mismatches)
			{
				if (mismatch.StartsWith("Population (expected Int32"))
				{
					named = true;
				}
			}
			Assert.IsTrue(named, "a seat field of the wrong type must be reported, not quietly coerced");
		}

		[Test]
		public void NoRealmStateIsCarriedByACity()
		{
			List<string> carried = CarriedFieldNames();
			foreach (string realmField in RealmOnlyFields)
			{
				Assert.IsFalse(carried.Contains(realmField), realmField + " is realm state and must not travel with a city");
			}
		}

		[Test]
		public void EverySettlementFieldIsCarried()
		{
			List<string> carried = CarriedFieldNames();
			foreach (string settlementField in SettlementFields)
			{
				Assert.IsTrue(carried.Contains(settlementField), settlementField + " belongs to a city and must be carried");
			}
		}

		[Test]
		public void NormalizeRepairsEveryNullContainer()
		{
			KingdomSettlement settlement = new KingdomSettlement();
			foreach (FieldInfo field in KingdomSettlement.CarriedFields())
			{
				if (!field.FieldType.IsValueType)
				{
					field.SetValue(settlement, null);
				}
			}
			settlement.Normalize();
			foreach (FieldInfo field in KingdomSettlement.CarriedFields())
			{
				if (IsContainerField(field))
				{
					Assert.IsNotNull(field.GetValue(settlement), field.Name + " must be repaired by Normalize, not left null for a consumer to trip over");
				}
			}
			Assert.AreEqual("common", settlement.Style);
			Assert.AreEqual(0, settlement.ClaimedZones.Count);
			Assert.IsNotNull(settlement.Ledger.Notes);
		}

		[Test]
		public void NormalizeKeepsANullVocationAndDiscardsAnUnknownOne()
		{
			KingdomSettlement first = new KingdomSettlement();
			first.Normalize();
			Assert.IsNull(first.Vocation, "the realm's first city was founded before there was a purpose to name");
			KingdomSettlement strange = new KingdomSettlement();
			strange.Vocation = "capital-of-the-world";
			strange.Normalize();
			Assert.AreEqual(KingdomSettlement.NeutralVocation, strange.Vocation);
		}

		[Test]
		public void ReadFromRepairsWhatItReads()
		{
			KingdomSettlement seat = new KingdomSettlement();
			seat.RosterNames = null;
			seat.Ledger = null;
			seat.Style = null;
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(seat);
			Assert.IsNotNull(captured.RosterNames);
			Assert.IsNotNull(captured.Ledger);
			Assert.AreEqual("common", captured.Style);
		}

		[TestCase(false, 0, false, false, KingdomSettlement.SecondFoundingVerdict.NothingFoundedYet)]
		[TestCase(true, 1, false, false, KingdomSettlement.SecondFoundingVerdict.Allowed)]
		[TestCase(true, 1, true, false, KingdomSettlement.SecondFoundingVerdict.GroundIsAlreadyOurs)]
		[TestCase(true, 1, false, true, KingdomSettlement.SecondFoundingVerdict.GroundIsTooClose)]
		[TestCase(true, 2, false, false, KingdomSettlement.SecondFoundingVerdict.RealmIsFull)]
		[TestCase(true, 2, false, true, KingdomSettlement.SecondFoundingVerdict.RealmIsFull)]
		[TestCase(true, 3, false, false, KingdomSettlement.SecondFoundingVerdict.RealmIsFull)]
		public void JudgeSecondFounding(bool founded, int held, bool claimed, bool adjacent, KingdomSettlement.SecondFoundingVerdict expected)
		{
			Assert.AreEqual(expected, KingdomSettlement.JudgeSecondFounding(founded, held, claimed, adjacent));
		}

		[Test]
		public void TheCapIsTwoCities()
		{
			Assert.AreEqual(2, KingdomSettlement.MaxSettlements);
			Assert.AreEqual(KingdomSettlement.SecondFoundingVerdict.RealmIsFull, KingdomSettlement.JudgeSecondFounding(true, KingdomSettlement.MaxSettlements, false, false));
		}

		[Test]
		public void EveryRefusalSaysSomethingAndNamesTheRealm()
		{
			foreach (KingdomSettlement.SecondFoundingVerdict verdict in Enum.GetValues(typeof(KingdomSettlement.SecondFoundingVerdict)))
			{
				string refusal = KingdomSettlement.SecondFoundingRefusal(verdict, "Kavvat");
				if (verdict == KingdomSettlement.SecondFoundingVerdict.Allowed)
				{
					Assert.AreEqual("", refusal, "an allowed founding refuses nothing");
				}
				else
				{
					Assert.IsTrue(refusal.Length > 0, verdict + " must tell the founder why");
				}
			}
			Assert.IsTrue(KingdomSettlement.SecondFoundingRefusal(KingdomSettlement.SecondFoundingVerdict.RealmIsFull, "Kavvat").Contains("Kavvat"));
			Assert.IsTrue(KingdomSettlement.SecondFoundingRefusal(KingdomSettlement.SecondFoundingVerdict.RealmIsFull, null).Contains("the realm"));
		}

		[Test]
		public void EveryVocationIsKnownAndSpeaks()
		{
			Assert.AreEqual(KingdomSettlement.Vocations.Length, KingdomSettlement.VocationBlurbs.Length);
			foreach (string vocation in KingdomSettlement.Vocations)
			{
				Assert.IsTrue(KingdomSettlement.IsKnownVocation(vocation));
				Assert.IsTrue(KingdomSettlement.VocationClause(vocation).Length > 0, vocation + " must have a clause");
				Assert.IsTrue(KingdomSettlement.VocationBlurb(vocation).Length > 0, vocation + " must have a blurb");
				Assert.IsTrue(KingdomSettlement.VocationSuffix(vocation).StartsWith(", "));
			}
			Assert.IsTrue(KingdomSettlement.IsKnownVocation(KingdomSettlement.NeutralVocation));
			Assert.IsFalse(KingdomSettlement.IsKnownVocation("capital-of-the-world"));
			Assert.AreEqual("", KingdomSettlement.VocationClause(null));
			Assert.AreEqual("", KingdomSettlement.VocationSuffix(null));
			Assert.AreEqual("", KingdomSettlement.VocationBlurb("capital-of-the-world"));
		}

		[Test]
		public void DescribeNamesTheCityAndItsPurpose()
		{
			KingdomSettlement settlement = new KingdomSettlement();
			settlement.SettlementName = "Sheol";
			settlement.Vocation = "refuge";
			settlement.Population = 4;
			string described = settlement.Describe();
			Assert.IsTrue(described.Contains("Sheol"));
			Assert.IsTrue(described.Contains("refuge"));
			Assert.IsTrue(described.Contains("pop=4"));
			Assert.IsTrue(new KingdomSettlement().Describe().Contains("(unnamed)"));
		}

		private static List<string> CarriedFieldNames()
		{
			List<string> names = new List<string>();
			foreach (FieldInfo field in KingdomSettlement.CarriedFields())
			{
				names.Add(field.Name);
			}
			return names;
		}

		private static bool IsContainerField(FieldInfo Field)
		{
			return Field.FieldType == typeof(List<string>)
				|| Field.FieldType == typeof(Dictionary<string, int>)
				|| Field.FieldType == typeof(Dictionary<string, string>)
				|| Field.FieldType == typeof(KingdomLedger);
		}

		/// <summary>
		/// Writes a distinct, non-default value into every field, so a dropped field shows up as a
		/// default rather than as a coincidence, and returns what was written. Reflective on
		/// purpose: a field added to the settlement is filled here without anyone remembering to.
		/// </summary>
		private static Dictionary<string, object> FillEveryField(KingdomSettlement Settlement)
		{
			Dictionary<string, object> written = new Dictionary<string, object>();
			FieldInfo[] fields = KingdomSettlement.CarriedFields();
			for (int i = 0; i < fields.Length; i++)
			{
				object value = SampleValue(fields[i], i);
				fields[i].SetValue(Settlement, value);
				written[fields[i].Name] = value;
			}
			return written;
		}

		private static object SampleValue(FieldInfo Field, int Index)
		{
			// Vocation is the one string Normalize is entitled to rewrite, so it must be filled
			// with a vocation this build knows or the round trip would fail on a working carry.
			if (Field.Name == "Vocation")
			{
				return KingdomSettlement.Vocations[0];
			}
			Type type = Field.FieldType;
			if (type == typeof(string))
			{
				return Field.Name + "-" + Index;
			}
			if (type == typeof(int))
			{
				return 100 + Index;
			}
			if (type == typeof(long))
			{
				return 1000L + Index;
			}
			if (type == typeof(bool))
			{
				return true;
			}
			if (type.IsEnum)
			{
				Array values = Enum.GetValues(type);
				return values.GetValue(values.Length - 1);
			}
			if (type == typeof(List<string>))
			{
				return new List<string> { Field.Name + "-" + Index };
			}
			if (type == typeof(Dictionary<string, int>))
			{
				Dictionary<string, int> counts = new Dictionary<string, int>();
				counts[Field.Name] = Index;
				return counts;
			}
			if (type == typeof(Dictionary<string, string>))
			{
				Dictionary<string, string> labels = new Dictionary<string, string>();
				labels[Field.Name] = "value-" + Index;
				return labels;
			}
			if (type == typeof(KingdomLedger))
			{
				KingdomLedger ledger = new KingdomLedger();
				ledger.Fetched = Index + 1;
				ledger.Note("note " + Index);
				return ledger;
			}
			Assert.Fail("SettlementSeatTests cannot fill " + Field.Name + " of type " + type.Name + ". Teach SampleValue that type, or the carry test is not covering the field.");
			return null;
		}

		/// <summary>A seat that has room for two of a city's fields and no more. Stands in for the
		/// day someone adds a field to the settlement and not to <c>KingdomSystem</c>.</summary>
		private sealed class PartialSeat
		{
			public string SettlementName;

			public int Population;
		}

		/// <summary>A seat whose field of that name cannot hold what the city keeps there.</summary>
		private sealed class MistypedSeat
		{
			public string Population;
		}
	}
}
#endif
