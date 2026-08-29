using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteGrowthScarcity(BinaryWriter w,
			KingdomGrowthScarcitySnapshot x)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.DryStreak); w.Write(x.Withered); w.Write(x.HungerStreak);
			w.Write(x.Famished); w.Write((int)x.LastMeal); w.Write(x.MealShade);
			w.Write(x.ScrapsAnnounced); w.Write(x.ElapsedTicks); w.Write(x.Days);
			w.Write(x.Population); w.Write(x.Stage); w.Write(x.UpkeepRequested);
			w.Write(x.WaterAvailable);
			w.Write(x.RationsAvailable); w.Write(x.Foraged); w.Write(x.Eaten);
			w.Write(x.FromDish); w.Write(x.Kitchens); S(w, x.DishName, false, true);
			S(w, x.DishText, false, true); S(w, x.DishStaple, false, true);
			S(w, x.DishSource, false, true);
			w.Write((byte)x.ComposedBite);
			w.Write(x.RequestedWater); w.Write(x.ProvedWater); w.Write(x.RequestedRations);
			w.Write(x.ProvedRations); w.Write(x.StoresPolicy); w.Write(x.DistrictPercent);
			w.Write((byte)x.ThirstOutcome); w.Write((byte)x.HungerOutcome);
			w.Write(x.Thirsting); w.Write(x.Starving); w.Write(x.Withering);
			w.Write(x.Famishing); w.Write(x.Healthy);
		}

		private static KingdomGrowthScarcitySnapshot ReadGrowthScarcity(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomGrowthScarcitySnapshot
			{
				DryStreak = r.ReadInt32(), Withered = ReadExactBoolean(r),
				HungerStreak = r.ReadInt32(), Famished = ReadExactBoolean(r),
				LastMeal = (KingdomRules.MealVerdict)r.ReadInt32(), MealShade = r.ReadInt32(),
				ScrapsAnnounced = ReadExactBoolean(r), ElapsedTicks = r.ReadInt64(),
				Days = r.ReadInt32(), Population = r.ReadInt32(), Stage = r.ReadInt32(),
				UpkeepRequested = r.ReadInt32(), WaterAvailable = r.ReadInt32(),
				RationsAvailable = r.ReadInt32(), Foraged = r.ReadInt32(), Eaten = r.ReadInt32(),
				FromDish = r.ReadInt32(), Kitchens = r.ReadInt32(),
				DishName = S(r, false, true), DishText = S(r, false, true),
				DishStaple = S(r, false, true), DishSource = S(r, false, true),
				ComposedBite = (KingdomGrowthComposedBite)r.ReadByte(),
				RequestedWater = r.ReadInt32(), ProvedWater = r.ReadInt32(),
				RequestedRations = r.ReadInt32(), ProvedRations = r.ReadInt32(),
				StoresPolicy = r.ReadInt32(), DistrictPercent = r.ReadInt32(),
				ThirstOutcome = (KingdomGrowthThirstOutcome)r.ReadByte(),
				HungerOutcome = (KingdomGrowthHungerOutcome)r.ReadByte(),
				Thirsting = ReadExactBoolean(r), Starving = ReadExactBoolean(r),
				Withering = ReadExactBoolean(r), Famishing = ReadExactBoolean(r),
				Healthy = ReadExactBoolean(r)
			};
		}

		private static void WriteGrowthAccounting(BinaryWriter w,
			KingdomGrowthAccountingSnapshot x)
		{
			w.Write(x != null); if (x == null) return;
			w.Write(x.Fetched); w.Write(x.UpkeepDrawn); w.Write(x.ArrivalCost);
			w.Write(x.Delivered); w.Write(x.Harvested); w.Write(x.Foraged);
			w.Write(x.RationsDrawn); w.Write(x.Milled); w.Write(x.HarvestLost);
			w.Write(x.Plundered); w.Write(x.Arrivals); w.Write(x.Departures);
		}

		private static KingdomGrowthAccountingSnapshot ReadGrowthAccounting(BinaryReader r)
		{
			if (!ReadExactBoolean(r)) return null;
			return new KingdomGrowthAccountingSnapshot
			{
				Fetched = r.ReadInt32(), UpkeepDrawn = r.ReadInt32(), ArrivalCost = r.ReadInt32(),
				Delivered = r.ReadInt32(), Harvested = r.ReadInt32(), Foraged = r.ReadInt32(),
				RationsDrawn = r.ReadInt32(), Milled = r.ReadInt32(),
				HarvestLost = r.ReadInt32(), Plundered = r.ReadInt32(), Arrivals = r.ReadInt32(),
				Departures = r.ReadInt32()
			};
		}

		private static void WriteGrowthOutboxEvent(BinaryWriter w, KingdomGrowthOutboxEvent x,
			int wireVersion)
		{
			if (x == null) throw new InvalidDataException("null growth outbox event");
			S(w, x.EventId, true); S(w, x.Kind, false); w.Write(x.ChronicleBeforeCount);
			w.Write(x.ChronicleDeclaredAfterCount); w.Write(x.ChronicleObservedCount);
			S(w, x.ChronicleBeforeHash, true); S(w, x.ChronicleDeclaredAfterHash, true);
			S(w, x.ChronicleObservedHash, true);
			if (wireVersion >= KingdomLifecycleRules.PreviousGrowthFormatVersion)
			{
				w.Write(x.LegacySingleRegisterChronicle); w.Write(x.OutsiderBeforeCount);
				w.Write(x.OutsiderDeclaredAfterCount); w.Write(x.OutsiderObservedCount);
				S(w, x.OutsiderBeforeHash, true); S(w, x.OutsiderDeclaredAfterHash, true);
				S(w, x.OutsiderObservedHash, true);
				S(w, x.ChronicleOfficial, false, true);
				S(w, x.ChronicleOutsider, false, true);
			}
			w.Write(x.LedgerBeforeCount);
			w.Write(x.LedgerDeclaredAfterCount); w.Write(x.LedgerObservedCount);
			S(w, x.LedgerBeforeHash, true); S(w, x.LedgerDeclaredAfterHash, true);
			S(w, x.LedgerObservedHash, true); WriteOutbox(w, x.Outbox);
		}

		private static KingdomGrowthOutboxEvent ReadGrowthOutboxEvent(BinaryReader r,
			int wireVersion)
		{
			KingdomGrowthOutboxEvent result = new KingdomGrowthOutboxEvent
			{
				EventId = S(r, true), Kind = S(r, false), ChronicleBeforeCount = r.ReadInt32(),
				ChronicleDeclaredAfterCount = r.ReadInt32(), ChronicleObservedCount = r.ReadInt32(),
				ChronicleBeforeHash = S(r, true), ChronicleDeclaredAfterHash = S(r, true),
				ChronicleObservedHash = S(r, true)
			};
			if (wireVersion >= KingdomLifecycleRules.PreviousGrowthFormatVersion)
			{
				result.LegacySingleRegisterChronicle = ReadExactBoolean(r);
				result.OutsiderBeforeCount = r.ReadInt32();
				result.OutsiderDeclaredAfterCount = r.ReadInt32();
				result.OutsiderObservedCount = r.ReadInt32();
				result.OutsiderBeforeHash = S(r, true);
				result.OutsiderDeclaredAfterHash = S(r, true);
				result.OutsiderObservedHash = S(r, true);
				result.ChronicleOfficial = S(r, false, true);
				result.ChronicleOutsider = S(r, false, true);
			}
			else
			{
				result.LegacySingleRegisterChronicle = true;
				result.OutsiderObservedCount = -1;
			}
			result.LedgerBeforeCount = r.ReadInt32();
			result.LedgerDeclaredAfterCount = r.ReadInt32();
			result.LedgerObservedCount = r.ReadInt32();
			result.LedgerBeforeHash = S(r, true);
			result.LedgerDeclaredAfterHash = S(r, true);
			result.LedgerObservedHash = S(r, true);
			result.Outbox = ReadOutbox(r);
			if (result.Outbox == null || result.Outbox.Chronicle == null)
				result.LegacySingleRegisterChronicle = false;
			return result;
		}

	}
}
