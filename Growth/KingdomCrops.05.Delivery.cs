using System;
using System.Collections.Generic;

using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	public static partial class KingdomCrops
	{
		// ==================================================================================
		// Delivery, including across zones
		// ==================================================================================

		/// <summary>
		/// Writes down what this zone's dedicated pantries hold and can hold, on the pass that
		/// stood in it. Rewritten from the ground every time, including down to zero &mdash; a
		/// larder that was struck stops being somewhere a harvest can be sent on the pass the
		/// founder sees the empty plot, and never before.
		/// <para>
		/// The <c>r_TAF_Larders_&lt;zoneID&gt;_*</c> game-state pair this replaced held ROOM, one
		/// int; the zone row holds the level and the capacity it is the difference of, which is the
		/// same answer and one the drain can also read (LIVING-CITY-ARCHITECTURE &sect;1.2(b)).
		/// </para>
		/// </summary>
		public static void RecordLarders(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			if (Survey == null)
			{
				return;
			}
			Simulation.City.KingdomCity.RecordLarder(System, Z, Survey.FoodStored, Survey.FoodCapacity, TimeTicks);
		}

		/// <summary>
		/// Room the city's OTHER claimed zones were last seen holding for a harvest. The exclusion
		/// is the whole point: this zone has just been offered the load from the ground.
		/// <para>
		/// Knowledge, not truth, exactly as <c>KingdomSubsidence.OtherZones</c> is: a zone nobody
		/// has ever stood in contributes nothing, and a sighting stays exactly as old as it is.
		/// When the belief turns out wrong the load arrives at a full larder and is lost there,
		/// which is a story rather than a bug &mdash; the same contract the manifest keeps.
		/// </para>
		/// </summary>
		public static int LarderRoomElsewhere(KingdomSystem System, Zone Z)
		{
			return Simulation.City.KingdomCity.LarderRoomElsewhere(System, Z);
		}

		/// <summary>
		/// Materialises whatever of the city's harvest is still on the road into this zone's
		/// pantries. Called at the top of every settlement pass, before the day's rations are
		/// drawn, so a load that arrived is a load the settlement can eat.
		/// <para>
		/// This is the crystallise-at-awareness idiom the rest of the mod runs on: the CITY's
		/// stores were credited the moment the harvest came due, wherever that was; the physical
		/// crop appears when somebody is standing where it was sent. Nothing is touched in an
		/// unloaded zone, because nothing in an unloaded zone can be touched.
		/// </para>
		/// </summary>
		public static void DeliverPending(KingdomSystem System, KingdomSurvey Survey)
		{
			DeliverPending(System, null, Survey);
		}

		/// <summary>
		/// As above, and with the ground in hand it can arrive <b>embodied</b>.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.7, Addendum 12(c)'s canonical image: <i>"walking around
		/// in my house in 1 zone, a farm finishes harvesting in another zone, a porter should come
		/// and put the harvested goods in the storage that is in the zone i am walking around."</i>
		/// The load was already the city's the moment the harvest came due; what the porter changes
		/// is the RENDERING and never the effect, which is invariant I2. A load that walks in on a
		/// back is not delivered twice by the plain path below, because it left
		/// <see cref="KingdomSystem.PendingCrop"/> when it went onto that back.
		/// </para>
		/// </summary>
		public static void DeliverPending(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || Survey == null || System.PendingCrop <= 0 || Survey.Larders.Count == 0)
			{
				return;
			}
			string blueprint = System.PendingCropBlueprint;
			if (string.IsNullOrEmpty(blueprint))
			{
				blueprint = KingdomData.CropForStyle(System.Style);
			}
			if (Z != null)
			{
				string from = System.PendingCropZoneId;
				if (!string.IsNullOrEmpty(from) && !System.ClaimedZones.Contains(from))
				{
					// Ground this city does not hold cannot be walked out of. The carrier still
					// comes in by a wall, it is simply no longer a wall that faces anything.
					from = null;
				}
				int carried = Simulation.City.KingdomPorters.Embody(System, Z, Survey, from, blueprint,
					System.PendingCrop, (The.Game != null) ? The.Game.TimeTicks : 0L);
				if (carried > 0)
				{
					System.PendingCrop -= carried;
					if (System.PendingCrop <= 0)
					{
						System.PendingCrop = 0;
						System.PendingCropBlueprint = null;
						System.PendingCropZoneId = null;
					}
					if (KingdomLog.Enabled) KingdomLog.Log("crop: " + carried + " went onto a porter's back, pending=" + System.PendingCrop);
					return;
				}
			}
			int delivered = Survey.StoreFood(System.PendingCrop, blueprint);
			if (delivered <= 0)
			{
				return;
			}
			System.PendingCrop -= delivered;
			if (System.PendingCrop <= 0)
			{
				System.PendingCrop = 0;
				System.PendingCropBlueprint = null;
				System.PendingCropZoneId = null;
			}
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			System.Ledger.Note("{{G|" + KingdomCropRules.DeliveryNote(delivered, realm) + "}}");
			MessageQueue.AddPlayerMessage("{{G|" + KingdomCropRules.DeliveryNote(delivered, realm) + "}}");
			if (KingdomLog.Enabled) KingdomLog.Log("crop: delivered " + delivered + " pending=" + System.PendingCrop);
		}

		/// <summary>
		/// Puts a gathering where it can go: this zone's pantries first, the city's other pantries
		/// second (as a load in flight), and the ground last.
		/// </summary>
		/// <returns>What was lost for want of room anywhere.</returns>
		public static int Deposit(KingdomSystem System, Zone Z, KingdomSurvey Survey, string CropBlueprint, int Amount, out int Delivered, out int Pending)
		{
			Delivered = 0;
			Pending = 0;
			if (System == null || Survey == null || Amount <= 0 || string.IsNullOrEmpty(CropBlueprint))
			{
				return 0;
			}
			Delivered = Survey.StoreFood(Amount, CropBlueprint);
			int left = Amount - Delivered;
			if (left <= 0)
			{
				return 0;
			}
			int elsewhere = LarderRoomElsewhere(System, Z) - System.PendingCrop;
			if (elsewhere > 0)
			{
				Pending = (left < elsewhere) ? left : elsewhere;
				// One crop at a time on the road. A second harvest of a different crop arriving
				// while the first is still in flight travels as the first: the load is servings,
				// and what it physically is was decided when it left.
				if (System.PendingCrop <= 0 || string.IsNullOrEmpty(System.PendingCropBlueprint))
				{
					System.PendingCropBlueprint = CropBlueprint;
					// Where it left from, so the carrier who renders it walks in by the edge that
					// faces the field rather than by whichever wall is nearest the code (§3.7).
					System.PendingCropZoneId = (Z != null) ? Z.ZoneID : null;
				}
				System.PendingCrop += Pending;
				left -= Pending;
			}
			return left;
		}

	}
}
