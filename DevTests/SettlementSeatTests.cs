#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>Deliberate old-save seam. Tests below must seed and inspect obsolete projection
	/// columns to prove their migration; ordinary tests and production code stay warning-clean.</summary>
	internal static class KingdomLegacyRosterProjectionTestAccess
	{
#pragma warning disable CS0618 // Intentional coverage of serialized compatibility projections.
		internal static List<string> Names(KingdomSettlement Value) => Value.RosterNames;
		internal static void SetNames(KingdomSettlement Value, List<string> Rows) =>
			Value.RosterNames = Rows;
		internal static List<string> Origins(KingdomSettlement Value) => Value.RosterOrigins;
		internal static void SetOrigins(KingdomSettlement Value, List<string> Rows) =>
			Value.RosterOrigins = Rows;
		internal static List<string> Arrivals(KingdomSettlement Value) => Value.RosterArrived;
		internal static void SetArrivals(KingdomSettlement Value, List<string> Rows) =>
			Value.RosterArrived = Rows;
#pragma warning restore CS0618
	}

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
		private static readonly string[] RealmOnlyFields = new string[19]
		{
			"KingdomFactionName", "KingdomDisplayName", "Standings", "RealmPolicyToward",
			"RegardSpilloverRemainders", "RegardSpilloverObservedReputation",
			"ChronicleEntries", "OutsiderEntries",
			"SerializationVersion", "LoadFailed", "ActiveDealKeys", "ActiveDealFactions",
			"DealNextTicks", "Away",
			// LIVING-CITY-ARCHITECTURE §3.8: the binding registry and the id counter under it are
			// realm-scope, because a bound body can be standing in the other city's ground or walked
			// off the map entirely. A registry a seat swap carried would answer for half the realm
			// and lose the other half every time the founder crossed a zone line, and two per-city id
			// counters would hand the same number to two people.
			"Bindings", "ResidentCounter", "DedicationCounter", "RealmId", "CarryBook"
		};

		/// <summary>Fields that must be on a city, named here only so the reflective tests cannot
		/// pass by finding nothing at all.</summary>
		private static readonly string[] SettlementFields = new string[7]
		{
			"SettlementName", "Vocation", "Population", "ClaimedZones", "Ledger", "LastHeartbeatTick",
			"HomecomingDays"
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
				// These names remain in the carried wire shape, but the 2026-09-01 food ruling
				// deliberately normalizes every pre-ruling consequence to its inert projection.
				if (field.Name == "HungerStreak" || field.Name == "MealShade")
				{
					ClassicAssert.AreEqual(0, actual, "retired field " + field.Name + " was reanimated");
					continue;
				}
				if (field.Name == "Famished" || field.Name == "ScrapsAnnounced")
				{
					ClassicAssert.AreEqual(false, actual, "retired field " + field.Name + " was reanimated");
					continue;
				}
				if (field.FieldType.IsValueType)
				{
					ClassicAssert.AreEqual(expected, actual, "field " + field.Name + " was not carried through capture and restore");
				}
				else
				{
					ClassicAssert.AreSame(expected, actual, "field " + field.Name + " was not carried through capture and restore");
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
			KingdomLegacyRosterProjectionTestAccess.Names(seat).Add("Ptoh");
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(seat);
			ClassicAssert.AreSame(KingdomLegacyRosterProjectionTestAccess.Names(seat),
				KingdomLegacyRosterProjectionTestAccess.Names(captured));
			ClassicAssert.AreSame(seat.Ledger, captured.Ledger);
		}

		[Test]
		public void ASeatMissingAFieldIsRefusedAndNamesIt()
		{
			List<string> mismatches = KingdomSettlement.SeatMismatches(typeof(PartialSeat));
			ClassicAssert.AreEqual(KingdomSettlement.CarriedFields().Length - 2, mismatches.Count, "every field the seat cannot carry must be named");
			bool refused = false;
			try
			{
				new KingdomSettlement().ReadFrom(new PartialSeat());
			}
			catch (KingdomSeatMismatchException)
			{
				refused = true;
			}
			ClassicAssert.IsTrue(refused, "reading from an incomplete seat must throw rather than silently drop a city");
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
			ClassicAssert.IsTrue(refused);
			ClassicAssert.IsNull(seat.SettlementName, "a refused write must write nothing at all");
			ClassicAssert.AreEqual(0, seat.Population, "a refused write must write nothing at all");
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
			ClassicAssert.IsTrue(named, "a seat field of the wrong type must be reported, not quietly coerced");
		}

		[Test]
		public void AMultiZoneClaimSurvivesTheSeatSwapWhole()
		{
			// The claim action is the first thing in the mod that puts more than one zone on a
			// city, so the swap has never actually had to carry one. Every parasang travels, in
			// order, and the swap hands the list over rather than copying it.
			KingdomSettlement seat = new KingdomSettlement();
			seat.SettlementName = "Kavvat";
			seat.Stage = GrowthStage.Town;
			seat.ClaimedZones.Add("JoppaWorld.11.22.1.1.10");
			seat.ClaimedZones.Add("JoppaWorld.11.22.2.1.10");
			seat.ClaimedZones.Add("JoppaWorld.11.22.1.1.11");

			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(seat);
			KingdomSettlement restored = new KingdomSettlement();
			captured.WriteTo(restored);

			ClassicAssert.AreEqual(3, restored.ClaimedZones.Count, "a claim the founder made must not be lost by walking between cities");
			ClassicAssert.AreEqual("JoppaWorld.11.22.1.1.10", restored.ClaimedZones[0]);
			ClassicAssert.AreEqual("JoppaWorld.11.22.2.1.10", restored.ClaimedZones[1]);
			ClassicAssert.AreEqual("JoppaWorld.11.22.1.1.11", restored.ClaimedZones[2], "the vertical claim travels like any other");
			ClassicAssert.AreEqual(GrowthStage.Town, restored.Stage, "the rung the ceiling is read against travels with the ground");
			ClassicAssert.AreSame(seat.ClaimedZones, restored.ClaimedZones);
		}

		[Test]
		public void TwoCitiesKeepTheirOwnGroundAcrossASwap()
		{
			// One parasang answers to one city: the seat and the record must never end up
			// sharing a claim list, or a swap would make each city answer for the other's ground.
			KingdomSettlement seat = new KingdomSettlement();
			seat.SettlementName = "Kavvat";
			seat.ClaimedZones.Add("JoppaWorld.11.22.1.1.10");
			KingdomSettlement away = new KingdomSettlement();
			away.SettlementName = "Ezra";
			away.ClaimedZones.Add("JoppaWorld.30.30.1.1.10");

			KingdomSettlement capturedSeat = new KingdomSettlement();
			capturedSeat.ReadFrom(seat);
			KingdomSettlement nowSeated = new KingdomSettlement();
			away.WriteTo(nowSeated);

			ClassicAssert.AreEqual(1, nowSeated.ClaimedZones.Count);
			ClassicAssert.AreEqual("JoppaWorld.30.30.1.1.10", nowSeated.ClaimedZones[0]);
			ClassicAssert.AreEqual("JoppaWorld.11.22.1.1.10", capturedSeat.ClaimedZones[0]);
			ClassicAssert.AreNotSame(capturedSeat.ClaimedZones, nowSeated.ClaimedZones);
		}

		[Test]
		public void TwoCitiesKeepTheirOwnUnreadReportAgeAcrossASwap()
		{
			KingdomSettlement seat = new KingdomSettlement
			{
				SettlementName = "Kavvat",
				HomecomingDays = 5
			};
			seat.Ledger.Note("Kavvat has news.");
			KingdomSettlement away = new KingdomSettlement
			{
				SettlementName = "Ezra",
				HomecomingDays = 2
			};
			away.Ledger.Note("Ezra has different news.");

			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(seat);
			KingdomSettlement nowSeated = new KingdomSettlement();
			away.WriteTo(nowSeated);

			ClassicAssert.AreEqual(5, captured.HomecomingDays);
			ClassicAssert.AreEqual(2, nowSeated.HomecomingDays);
			StringAssert.Contains("Kavvat", captured.Ledger.Notes[0]);
			StringAssert.Contains("Ezra", nowSeated.Ledger.Notes[0]);
		}

		[Test]
		public void NoRealmStateIsCarriedByACity()
		{
			List<string> carried = CarriedFieldNames();
			foreach (string realmField in RealmOnlyFields)
			{
				ClassicAssert.IsFalse(carried.Contains(realmField), realmField + " is realm state and must not travel with a city");
			}
		}

		[Test]
		public void EverySettlementFieldIsCarried()
		{
			List<string> carried = CarriedFieldNames();
			foreach (string settlementField in SettlementFields)
			{
				ClassicAssert.IsTrue(carried.Contains(settlementField), settlementField + " belongs to a city and must be carried");
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
					ClassicAssert.IsNotNull(field.GetValue(settlement), field.Name + " must be repaired by Normalize, not left null for a consumer to trip over");
				}
			}
			ClassicAssert.AreEqual("common", settlement.Style);
			ClassicAssert.AreEqual(0, settlement.ClaimedZones.Count);
			ClassicAssert.IsNotNull(settlement.Ledger.Notes);
		}

		[Test]
		public void NormalizeKeepsANullVocationAndDiscardsAnUnknownOne()
		{
			KingdomSettlement first = new KingdomSettlement();
			first.Normalize();
			ClassicAssert.IsNull(first.Vocation, "the realm's first city was founded before there was a purpose to name");
			KingdomSettlement strange = new KingdomSettlement();
			strange.Vocation = "capital-of-the-world";
			strange.Normalize();
			ClassicAssert.AreEqual(KingdomSettlement.NeutralVocation, strange.Vocation);
		}

		[Test]
		public void NormalizeRetiresLegacyNotableAndPassiveFoodEconomy()
		{
			KingdomSettlement legacy = new KingdomSettlement
			{
				NotableShade = 17,
				MealShade = 1,
				HungerStreak = 4,
				Famished = true,
				ScrapsAnnounced = true
			};
			legacy.Normalize();
			ClassicAssert.AreEqual(0, legacy.NotableShade,
				"title-only civic offices cannot retain a hidden legacy capacity modifier");
			ClassicAssert.AreEqual(0, legacy.MealShade);
			ClassicAssert.AreEqual(0, legacy.HungerStreak);
			ClassicAssert.IsFalse(legacy.Famished);
			ClassicAssert.IsFalse(legacy.ScrapsAnnounced);
		}

		[Test]
		public void SeatCaptureAndRestoreCannotReanimateLegacyNotableEconomy()
		{
			KingdomSettlement legacySeat = new KingdomSettlement
			{
				NotableShade = 17,
				MealShade = 1,
				HungerStreak = 4,
				Famished = true
			};
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(legacySeat);
			KingdomSettlement restored = new KingdomSettlement();
			captured.WriteTo(restored);
			ClassicAssert.AreEqual(0, captured.NotableShade);
			ClassicAssert.AreEqual(0, restored.NotableShade);
			ClassicAssert.AreEqual(0, restored.MealShade);
			ClassicAssert.AreEqual(0, restored.HungerStreak);
			ClassicAssert.IsFalse(restored.Famished);
		}

		[Test]
		public void NormalizeRejectsEveryUnnamedSerializedLifecycleValue()
		{
			KingdomSettlement settlement = new KingdomSettlement
			{
				Stage = (GrowthStage)(-1),
				LastMeal = (KingdomRules.MealVerdict)255,
				Gate = (KingdomRules.GatePolicy)255,
				Stores = (KingdomRules.StoresPolicy)(-1),
				PetitionKind = (KingdomRules.PetitionKind)255,
				PetitionState = (PetitionLifecycle)255,
				RaidState = 255,
				RaidFactionName = "foreign",
				RaidDueTick = 1234L
			};
			settlement.Normalize();
			ClassicAssert.AreEqual(GrowthStage.Camp, settlement.Stage);
			ClassicAssert.AreEqual(KingdomRules.MealVerdict.None, settlement.LastMeal);
			ClassicAssert.AreEqual(KingdomRules.GatePolicy.Open, settlement.Gate);
			ClassicAssert.AreEqual(KingdomRules.StoresPolicy.Plenty, settlement.Stores);
			ClassicAssert.AreEqual(KingdomRules.PetitionKind.None, settlement.PetitionKind);
			ClassicAssert.AreEqual(PetitionLifecycle.None, settlement.PetitionState);
			ClassicAssert.AreEqual(0, settlement.RaidState);
			ClassicAssert.IsNull(settlement.RaidFactionName);
			ClassicAssert.AreEqual(0L, settlement.RaidDueTick);
		}

		[Test]
		public void NormalizeRetainsRaggedLegacyRosterEvidenceForSystemMigration()
		{
			KingdomSettlement settlement = new KingdomSettlement
			{
				DeadNames = new List<string> { "Eresh", "Eresh", "A third" },
				DeadOrigins = new List<string> { "dune", "reef", "salt" },
				DeadArrived = new List<string> { "first" },
				DeadCauses = new List<string> { "age", "stale" }
			};
			KingdomLegacyRosterProjectionTestAccess.SetNames(settlement,
				new List<string> { "Ptoh", "Ptoh", "A third" });
			KingdomLegacyRosterProjectionTestAccess.SetOrigins(settlement,
				new List<string> { "salt", "reef" });
			KingdomLegacyRosterProjectionTestAccess.SetArrivals(settlement,
				new List<string> { "one", "two", "stale", "staler" });
			settlement.Normalize();
			List<string> names = KingdomLegacyRosterProjectionTestAccess.Names(settlement);
			List<string> origins = KingdomLegacyRosterProjectionTestAccess.Origins(settlement);
			List<string> arrivals = KingdomLegacyRosterProjectionTestAccess.Arrivals(settlement);
			ClassicAssert.AreEqual(3, names.Count);
			ClassicAssert.AreEqual(2, origins.Count);
			ClassicAssert.AreEqual(4, arrivals.Count);
			ClassicAssert.AreEqual("Ptoh", names[0]);
			ClassicAssert.AreEqual("Ptoh", names[1],
				"duplicate names are legitimate rows, not a normalization key");
			ClassicAssert.AreEqual("reef", origins[1]);
			ClassicAssert.AreEqual("staler", arrivals[3],
				"settlement normalization cannot destroy unresolved old-save evidence; realm migration owns it");
			ClassicAssert.AreEqual(1, settlement.DeadNames.Count);
			ClassicAssert.AreEqual(1, settlement.DeadOrigins.Count);
			ClassicAssert.AreEqual(1, settlement.DeadArrived.Count);
			ClassicAssert.AreEqual(1, settlement.DeadCauses.Count);
			ClassicAssert.AreEqual("Eresh", settlement.DeadNames[0]);
			ClassicAssert.AreEqual("dune", settlement.DeadOrigins[0]);
			ClassicAssert.AreEqual("first", settlement.DeadArrived[0]);
			ClassicAssert.AreEqual("age", settlement.DeadCauses[0]);
		}

		[Test]
		public void ReadFromRepairsWhatItReads()
		{
			KingdomSettlement seat = new KingdomSettlement();
			KingdomLegacyRosterProjectionTestAccess.SetNames(seat, null);
			seat.Ledger = null;
			seat.Style = null;
			KingdomSettlement captured = new KingdomSettlement();
			captured.ReadFrom(seat);
			ClassicAssert.IsNotNull(KingdomLegacyRosterProjectionTestAccess.Names(captured));
			ClassicAssert.IsNotNull(captured.Ledger);
			ClassicAssert.AreEqual("common", captured.Style);
		}

		[TestCase(false, 0, false, false, KingdomSettlement.SecondFoundingVerdict.NothingFoundedYet)]
		[TestCase(true, 1, false, false, KingdomSettlement.SecondFoundingVerdict.Allowed)]
		[TestCase(true, 1, true, false, KingdomSettlement.SecondFoundingVerdict.GroundIsAlreadyOurs)]
		[TestCase(true, 1, false, true, KingdomSettlement.SecondFoundingVerdict.GroundIsTooClose)]
		[TestCase(true, 2, false, false, KingdomSettlement.SecondFoundingVerdict.Allowed)]
		[TestCase(true, 2, false, true, KingdomSettlement.SecondFoundingVerdict.GroundIsTooClose)]
		[TestCase(true, 3, false, false, KingdomSettlement.SecondFoundingVerdict.RealmIsFull)]
		public void JudgeSecondFounding(bool founded, int held, bool claimed, bool adjacent, KingdomSettlement.SecondFoundingVerdict expected)
		{
			ClassicAssert.AreEqual(expected, KingdomSettlement.JudgeSecondFounding(founded, held, claimed, adjacent));
		}

		[Test]
		public void TheCapIsThreeCities()
		{
			ClassicAssert.AreEqual(3, KingdomSettlement.MaxSettlements);
			ClassicAssert.AreEqual(KingdomSettlement.SecondFoundingVerdict.RealmIsFull, KingdomSettlement.JudgeSecondFounding(true, KingdomSettlement.MaxSettlements, false, false));
		}

		[Test]
		public void EveryRefusalSaysSomethingAndNamesTheRealm()
		{
			foreach (KingdomSettlement.SecondFoundingVerdict verdict in Enum.GetValues(typeof(KingdomSettlement.SecondFoundingVerdict)))
			{
				string refusal = KingdomSettlement.SecondFoundingRefusal(verdict, "Kavvat");
				if (verdict == KingdomSettlement.SecondFoundingVerdict.Allowed)
				{
					ClassicAssert.AreEqual("", refusal, "an allowed founding refuses nothing");
				}
				else
				{
					ClassicAssert.IsTrue(refusal.Length > 0, verdict + " must tell the founder why");
				}
			}
			ClassicAssert.IsTrue(KingdomSettlement.SecondFoundingRefusal(KingdomSettlement.SecondFoundingVerdict.RealmIsFull, "Kavvat").Contains("Kavvat"));
			ClassicAssert.IsTrue(KingdomSettlement.SecondFoundingRefusal(KingdomSettlement.SecondFoundingVerdict.RealmIsFull, null).Contains("the realm"));
		}

		[Test]
		public void EveryVocationIsKnownAndSpeaks()
		{
			ClassicAssert.AreEqual(KingdomSettlement.Vocations.Length, KingdomSettlement.VocationBlurbs.Length);
			foreach (string vocation in KingdomSettlement.Vocations)
			{
				ClassicAssert.IsTrue(KingdomSettlement.IsKnownVocation(vocation));
				ClassicAssert.IsTrue(KingdomSettlement.VocationClause(vocation).Length > 0, vocation + " must have a clause");
				ClassicAssert.IsTrue(KingdomSettlement.VocationBlurb(vocation).Length > 0, vocation + " must have a blurb");
				ClassicAssert.IsTrue(KingdomSettlement.VocationSuffix(vocation).StartsWith(", "));
			}
			ClassicAssert.IsTrue(KingdomSettlement.IsKnownVocation(KingdomSettlement.NeutralVocation));
			ClassicAssert.IsFalse(KingdomSettlement.IsKnownVocation("capital-of-the-world"));
			ClassicAssert.AreEqual("", KingdomSettlement.VocationClause(null));
			ClassicAssert.AreEqual("", KingdomSettlement.VocationSuffix(null));
			ClassicAssert.AreEqual("", KingdomSettlement.VocationBlurb("capital-of-the-world"));
		}

		[Test]
		public void DescribeNamesTheCityAndItsPurpose()
		{
			KingdomSettlement settlement = new KingdomSettlement();
			settlement.SettlementName = "Sheol";
			settlement.Vocation = "refuge";
			settlement.Population = 4;
			string described = settlement.Describe();
			ClassicAssert.IsTrue(described.Contains("Sheol"));
			ClassicAssert.IsTrue(described.Contains("refuge"));
			ClassicAssert.IsTrue(described.Contains("pop=4"));
			ClassicAssert.IsTrue(new KingdomSettlement().Describe().Contains("(unnamed)"));
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
			// These five fields form one receipt. Independent arbitrary longs can describe a
			// completed step which was never started, and Normalize is then required to discard the
			// corrupt group. Give the reflective carry test a valid non-default partial receipt.
			SetSample(fields, Settlement, written, "SemanticPassActive", true);
			SetSample(fields, Settlement, written, "SemanticPassStartedTick", 2400L);
			SetSample(fields, Settlement, written, "SemanticPassZoneId", "JoppaWorld.11.22.1.1.10");
			SetSample(fields, Settlement, written, "SemanticPassStartedMask", 7L);
			SetSample(fields, Settlement, written, "SemanticPassCompletedMask", 3L);
			// Read-compatible only: capture normalization must retire this old economy field.
			SetSample(fields, Settlement, written, "NotableShade", 0);
			return written;
		}

		private static void SetSample(FieldInfo[] Fields, KingdomSettlement Settlement,
			Dictionary<string, object> Written, string Name, object Value)
		{
			for (int i = 0; i < Fields.Length; i++)
			{
				if (Fields[i].Name == Name)
				{
					Fields[i].SetValue(Settlement, Value);
					Written[Name] = Value;
					return;
				}
			}
			Assert.Fail("SettlementSeatTests expected carried field " + Name + ".");
		}

		private static object SampleValue(FieldInfo Field, int Index)
		{
			// Vocation is the one string Normalize is entitled to rewrite, so it must be filled
			// with a vocation this build knows or the round trip would fail on a working carry.
			if (Field.Name == "Vocation")
			{
				return KingdomSettlement.Vocations[0];
			}
			// RaidState is a serialized lifecycle code, not an unconstrained counter. Normalize
			// deliberately rejects unnamed values, so the carry fixture must exercise a valid
			// active state instead of asking corruption repair to preserve 147.
			if (Field.Name == "RaidState")
			{
				return 1;
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
			if (type == typeof(ThousandAndFirst.Simulation.City.KingdomCityBook))
			{
				ThousandAndFirst.Simulation.City.KingdomCityBook book = new ThousandAndFirst.Simulation.City.KingdomCityBook();
				book.SettlementId = Field.Name + "-" + Index;
				book.ProcessedThroughTick = 500L + Index;
				return book;
			}
			if (type == typeof(KingdomLifecycleBook))
			{
				return new KingdomLifecycleBook
				{
					SettlementId = "taf:settlement:v1:" + new string('a', 64),
					PlainGuestNextSequence = 10L + Index
				};
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

		/// <summary>
		/// The seat swap carries the registry intact, which for realm state means it carries it by
		/// NOT TOUCHING IT. LIVING-CITY-ARCHITECTURE §3.8, and the reason the registry does not live
		/// on a settlement: a body bound in the city the founder just walked out of is still bound
		/// after they walk into the other one.
		/// </summary>
		[Test]
		public void ASeatSwapLeavesTheBindingRegistryExactlyAsItFoundIt()
		{
			ThousandAndFirst.Simulation.City.KingdomBindingRegistry registry = new ThousandAndFirst.Simulation.City.KingdomBindingRegistry();
			ThousandAndFirst.Simulation.City.KingdomBindingTable table;
			ThousandAndFirst.Simulation.City.KingdomCityFault fault;
			ClassicAssert.IsTrue(ThousandAndFirst.Simulation.City.KingdomBindingTable.Empty.TryBind(
				7, ThousandAndFirst.Simulation.City.KingdomBindingKind.Resident, "JoppaWorld.11.22.1.1.10", "obj-7", 700L,
				out table, out fault), fault.ToString());
			ClassicAssert.IsTrue(registry.TryPublish(table, out fault), fault.ToString());

			// The whole swap, both directions, over a realm holding two cities.
			KingdomSettlement seat = new KingdomSettlement();
			seat.SettlementName = "Kavvat";
			seat.ClaimedZones.Add("JoppaWorld.11.22.1.1.10");
			KingdomSettlement away = new KingdomSettlement();
			away.SettlementName = "Ezra";
			away.ClaimedZones.Add("JoppaWorld.30.30.1.1.10");
			KingdomSettlement capturedSeat = new KingdomSettlement();
			capturedSeat.ReadFrom(seat);
			KingdomSettlement nowSeated = new KingdomSettlement();
			away.WriteTo(nowSeated);

			// Nothing in the swap can reach the registry: it is not among the fields a city carries.
			ClassicAssert.IsFalse(CarriedFieldNames().Contains("Bindings"));
			ThousandAndFirst.Simulation.City.KingdomBindingTable after;
			ClassicAssert.IsTrue(registry.TryRead(out after, out fault), fault.ToString());
			ThousandAndFirst.Simulation.City.KingdomBinding binding;
			ClassicAssert.IsTrue(after.TryGet(7, ThousandAndFirst.Simulation.City.KingdomBindingKind.Resident, out binding),
				"a body bound in the city the founder walked out of is still bound after they walk into the other");
			ClassicAssert.AreEqual("JoppaWorld.11.22.1.1.10", binding.ZoneId);
			ClassicAssert.AreEqual(700L, binding.MintedTick);
		}

		/// <summary>Every city carries its own book, and the two books are never the same object:
		/// a resident row written in one city must not appear on the other's roll.</summary>
		[Test]
		public void EachCityCarriesItsOwnBookOfResidents()
		{
			KingdomSettlement seat = new KingdomSettlement();
			Enrol(seat, 7);
			KingdomSettlement away = new KingdomSettlement();
			Enrol(away, 9);
			KingdomSettlement capturedSeat = new KingdomSettlement();
			capturedSeat.ReadFrom(seat);
			KingdomSettlement nowSeated = new KingdomSettlement();
			away.WriteTo(nowSeated);
			ClassicAssert.AreNotSame(capturedSeat.City, nowSeated.City);
			ClassicAssert.AreEqual(1, capturedSeat.City.ResidentCount);
			ClassicAssert.AreEqual(1, nowSeated.City.ResidentCount);
			int index;
			ClassicAssert.IsTrue(capturedSeat.City.TryResidentRow(7, out index));
			ClassicAssert.IsFalse(capturedSeat.City.TryResidentRow(9, out index), "one city's roll must never appear on the other's");
			ClassicAssert.IsTrue(nowSeated.City.TryResidentRow(9, out index));
			ClassicAssert.IsFalse(nowSeated.City.TryResidentRow(7, out index));
			ThousandAndFirst.Simulation.City.KingdomCityState capturedState;
			ThousandAndFirst.Simulation.City.KingdomCityState seatedState;
			ThousandAndFirst.Simulation.City.KingdomCityFault fault;
			ClassicAssert.IsTrue(capturedSeat.City.TryRead(out capturedState, out fault), fault.ToString());
			ClassicAssert.IsTrue(nowSeated.City.TryRead(out seatedState, out fault), fault.ToString());
			ClassicAssert.IsTrue(capturedState.TryResident(0,
				out ThousandAndFirst.Simulation.City.KingdomResidentRow captured));
			ClassicAssert.IsTrue(seatedState.TryResident(0,
				out ThousandAndFirst.Simulation.City.KingdomResidentRow seated));
			ClassicAssert.AreEqual("origin-7", captured.Origin);
			ClassicAssert.AreEqual("arrival-7", captured.Arrived);
			ClassicAssert.AreEqual("origin-9", seated.Origin);
			ClassicAssert.AreEqual("arrival-9", seated.Arrived);
		}

		/// <summary>Writes one settler onto a city's book through its only publisher, so the
		/// columns stay square — a book filled a column at a time is a book Normalize truncates,
		/// which is the repair working rather than a test fixture.</summary>
		private static void Enrol(KingdomSettlement city, int residentId)
		{
			ThousandAndFirst.Simulation.City.KingdomCityState state;
			ThousandAndFirst.Simulation.City.KingdomCityFault fault;
			ClassicAssert.IsTrue(city.City.TryRead(out state, out fault), fault.ToString());
			ThousandAndFirst.Simulation.City.KingdomCityState peopled;
			ClassicAssert.IsTrue(state.TryWithResidents(new ThousandAndFirst.Simulation.City.KingdomResidentRow[1]
			{
				new ThousandAndFirst.Simulation.City.KingdomResidentRow(residentId, "Ptoh", 0, 0, 400L, 0, 0, 0,
					ThousandAndFirst.Simulation.City.KingdomDayShape.Hearth,
					ThousandAndFirst.Simulation.City.KingdomResidentStanding.Resident,
					ThousandAndFirst.Simulation.City.KingdomStandingCause.None, "JoppaWorld.11.22.1.1.10",
					ThousandAndFirst.Simulation.City.KingdomBrinkWindow.None,
						ThousandAndFirst.Simulation.City.KingdomBrinkWindow.None, null, 0, null,
						"origin-" + residentId, "arrival-" + residentId)
			}, out peopled, out fault), fault.ToString());
			ClassicAssert.IsTrue(city.City.TryPublish(peopled, out fault), fault.ToString());
		}

		/// <summary>A seat that has room for two of a city's fields and no more. Stands in for the
		/// day someone adds a field to the settlement and not to <c>KingdomSystem</c>.</summary>
		private sealed class PartialSeat
		{
			public string SettlementName = null;

			public int Population = 0;
		}

		/// <summary>A seat whose field of that name cannot hold what the city keeps there.</summary>
		private sealed class MistypedSeat
		{
			public string Population = null;
		}
	}
}
#endif
