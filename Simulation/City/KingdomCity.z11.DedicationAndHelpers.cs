using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomCity
	{
		private static bool IndexOf(KingdomCityState state, string zoneId, out int index)
		{
			for (index = 0; index < state.ZoneCount; index++)
			{
				KingdomZoneRow row;
				if (state.TryZone(index, out row) && string.Equals(row.ZoneId, zoneId, StringComparison.Ordinal))
				{
					return true;
				}
			}
			index = -1;
			return false;
		}

		private static void StampDedicationOrder(KingdomSystem System, KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Stores.Count; i++)
			{
				Stamp(System, Survey.Stores[i].ParentObject);
			}
			for (int i = 0; i < Survey.Larders.Count; i++)
			{
				Stamp(System, Survey.Larders[i]);
			}
		}

		private static void Stamp(KingdomSystem System, GameObject container)
		{
			if (!GameObject.Validate(container) || container.GetIntProperty(DedicationOrderProperty) > 0)
			{
				return;
			}
			System.DedicationCounter++;
			container.SetIntProperty(DedicationOrderProperty, System.DedicationCounter);
		}

		private static List<LiquidVolume> Ordered(List<LiquidVolume> stores)
		{
			List<LiquidVolume> ordered = new List<LiquidVolume>(stores);
			ordered.Sort(delegate(LiquidVolume left, LiquidVolume right)
			{
				return OrdinalOf(left.ParentObject).CompareTo(OrdinalOf(right.ParentObject));
			});
			return ordered;
		}

		private static List<GameObject> Ordered(List<GameObject> containers)
		{
			List<GameObject> ordered = new List<GameObject>(containers);
			ordered.Sort(delegate(GameObject left, GameObject right)
			{
				return OrdinalOf(left).CompareTo(OrdinalOf(right));
			});
			return ordered;
		}

		/// <summary>A container the city has never counted sorts LAST, not first: the drain order
		/// is a stored fact, and an unstamped vessel has no claim to being the oldest.</summary>
		private static int OrdinalOf(GameObject container)
		{
			if (!GameObject.Validate(container))
			{
				return int.MaxValue;
			}
			return KingdomCityRules.DrainOrdinal(container.GetIntProperty(DedicationOrderProperty));
		}

		private static KingdomWorkRunState RunStateOf(GameObject work)
		{
			KingdomWorkKind kind = KingdomStations.KindOf(work);
			if (kind == KingdomWorkKind.Growing)
			{
				r_KingdomPlot field = KingdomCrops.FieldOf(work);
				if (field != null)
				{
					return new KingdomWorkRunState(kind, (byte)field.Stage, 0,
						field.NextStageTick);
				}
			}
			// Every other owner still keeps its progress on its own receipt/object. Publish the
			// shared kind, but do not invent progress for a work row that has no authority for it.
			return new KingdomWorkRunState(kind, 0, 0, 0L);
		}

		private static string CropOf(KingdomSystem System)
		{
			return KingdomData.CropForStyle(System.Style);
		}

		/// <summary>
		/// What one turn's spend moved, and what it could not.
		/// <para>
		/// <paramref name="waterLeft"/> and <paramref name="foodLeft"/> are non-zero only for a kind
		/// whose unit was spent and whose containers gave nothing back &mdash; which is the one thing
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 requires be told, and never silently forgiven. A debt
		/// that is simply draining says nothing, because a debt draining is the design working.
		/// </para>
		/// </summary>
		private static void Tell(KingdomSystem System, int waterPaid, int foodPaid, int waterLeft, int foodLeft)
		{
			string note = KingdomCityRules.ShortfallNote(waterLeft, foodLeft);
			if (note != null)
			{
				System.Ledger.Note("{{r|" + note + "}}");
			}
			if (KingdomLog.Enabled && (waterPaid != 0 || foodPaid != 0 || note != null))
			{
				KingdomLog.Log("city: reify paid water=" + waterPaid + " food=" + foodPaid + " unpaid water=" + waterLeft + " food=" + foodLeft);
			}
		}

		private static void Refuse(string step, KingdomCityFault fault)
		{
			KingdomLog.Log("city: " + step + " refused (" + fault + "); the book is unchanged");
		}

		private static int Floor(int value)
		{
			return (value > 0) ? value : 0;
		}

		private static long Clamp(long value)
		{
			if (value <= 0L)
			{
				return 0L;
			}
			return (value > int.MaxValue) ? int.MaxValue : value;
		}

		/// <summary>A sighting tick, quantised to the day the retired game-state slot could hold.
		/// Kept so the staleness clause reads exactly as it did.</summary>
		private static long DayStamp(long TimeTicks)
		{
			return (long)KingdomSubsidence.SeenStamp(TimeTicks) * KingdomRules.TicksPerDay;
		}
	}
}
