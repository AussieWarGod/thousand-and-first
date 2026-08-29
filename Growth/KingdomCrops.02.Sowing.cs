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
		// Sowing, and taking it back
		// ==================================================================================

		/// <summary>
		/// Commits one seed to the field the founder is standing in. Everything the seed item's
		/// own part does is call this, the same split <c>r_FounderBasin</c> and
		/// <c>r_KingdomCarrySign</c> keep.
		/// </summary>
		/// <param name="Actor">The founder. Non-players never reach here.</param>
		/// <param name="Seed">The seed item, which is decremented by exactly one on success.</param>
		public static void AttemptSow(GameObject Actor, GameObject Seed)
		{
			if (Actor == null || Seed == null)
			{
				return;
			}
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			Cell cell = Actor.CurrentCell;
			Zone zone = Actor.CurrentZone;
			string crop = KingdomData.CropForSeed(Seed.Blueprint);
			string row = KingdomData.RowForCrop(crop);
			GameObject work = FieldUnder(zone, cell);
			r_KingdomPlot field = FieldOf(work);
			KingdomSurvey survey = (zone == null) ? null : KingdomSurvey.Take(zone, system);
			KingdomCropRules.SowVerdict verdict = KingdomCropRules.AssessSow(
				HasField: field != null,
				Claimed: system.Founded && zone != null && system.ClaimedZones.Contains(zone.ZoneID),
				AlreadySown: field != null && field.Stage != KingdomCropRules.PlotStage.Dormant,
				Condemned: IsCondemned(work),
				HasRow: !string.IsNullOrEmpty(row),
				StoredWater: (survey == null) ? 0 : survey.StoredWater,
				Population: system.Population);
			if (verdict != KingdomCropRules.SowVerdict.Sown)
			{
				Popup.Show(KingdomCropRules.SowRefusal(verdict));
				return;
			}
			string declaredCrop = DeclaredCrop(work);
			if (!KingdomCropRules.DeclaredCropAllows(declaredCrop, crop))
			{
				Popup.Show(KingdomCropRules.DeclaredCropRefusal(
					CropName(declaredCrop), work.ShortDisplayName));
				return;
			}
			int rows = DeclaredRows(work);
			if (rows <= 0)
			{
				Popup.Show(KingdomCropRules.SowRefusal(KingdomCropRules.SowVerdict.NoCrop));
				return;
			}
			string fieldName = work.ShortDisplayName;
			// Consent before cost, exactly as the carry-sign asks it: the founder sees the crop,
			// the rows, the wait and the water before one dram or one seed is spent.
			if (Popup.ShowYesNo(KingdomCropRules.SowConfirm(CropName(crop), fieldName, rows, KingdomCropRules.PlantWaterCostDrams)) != DialogResult.Yes)
			{
				return;
			}
			KingdomWaterDebit debit;
			if (!survey.TryReserveExactWater(KingdomCropRules.PlantWaterCostDrams, out debit))
			{
				Popup.Show(KingdomCropRules.SowRefusal(KingdomCropRules.SowVerdict.NoWater));
				return;
			}

			// Snapshot field and its pre-existing rows before touching either. Water can therefore
			// return to the exact casks, and a refused seed leaves no half-sown field behind.
			KingdomCropRules.PlotStage oldStage = field.Stage;
			string oldCrop = field.CropBlueprint;
			long oldNext = field.NextStageTick;
			bool oldNoLarder = field.NoLarderAnnounced;
			string oldSeed = work.GetStringProperty(SeedProperty);
			bool hadRows = work.HasIntProperty(RowsProperty);
			int oldRows = work.GetIntProperty(RowsProperty);
			bool hadSownTick = work.HasIntProperty(SownTickProperty);
			int oldSownTick = work.GetIntProperty(SownTickProperty);
			bool hadCycles = work.HasIntProperty(CyclesProperty);
			int oldCycles = work.GetIntProperty(CyclesProperty);
			bool hadSaid = work.HasIntProperty(SaidProperty);
			int oldSaid = work.GetIntProperty(SaidProperty);
			List<GameObject> rowsBefore = RowsOf(zone, work);
			GameObject seedInventory = Seed.InInventory;
			Cell seedCell = Seed.CurrentCell;
			string seedId = Seed.IDIfAssigned;
			string seedBlueprint = Seed.Blueprint;
			int seedCount = Seed.Count;
			// Snapshot first, then cross the exact physical debit boundary. Nothing below may
			// discover that it did not know how to compensate only after water has moved.
			string seedFailure;
			if (!SeedAtSnapshot(Seed, seedInventory, seedCell, seedId, seedBlueprint, seedCount)
				|| !KingdomOrdinaryFoodAuthority.TryObjectNow(Seed, out seedFailure)
				|| !debit.Commit())
			{
				Popup.Show(KingdomCropRules.SowRefusal(KingdomCropRules.SowVerdict.NoWater));
				return;
			}
			if (!SeedAtSnapshot(Seed, seedInventory, seedCell, seedId, seedBlueprint, seedCount)
				|| !KingdomOrdinaryFoodAuthority.TryObjectNow(Seed, out seedFailure))
			{
				debit.Rollback();
				Popup.Show("The seed's custody changed while the water was reserved. Nothing was sown.");
				return;
			}
			long now = The.Game.TimeTicks;
			int laid = 0;
			try
			{
				field.CropBlueprint = crop;
				work.SetStringProperty(SeedProperty, Seed.Blueprint);
				work.SetIntProperty(RowsProperty, rows);
				work.SetIntProperty(SownTickProperty, StampOf(now));
				work.SetIntProperty(CyclesProperty, 0);
				work.SetIntProperty(SaidProperty, 0);
				field.NoLarderAnnounced = false;
				field.NextStageTick = KingdomCropRules.RipenTick(now);
				field.ApplyStage(KingdomCropRules.PlotStage.Growing);
				laid = LayRows(zone, work, row, rows);
				if (laid <= 0)
				{
					throw new InvalidOperationException("No crop row could be laid in the field footprint.");
				}
				if (!SeedAtSnapshot(Seed, seedInventory, seedCell, seedId, seedBlueprint, seedCount)
					|| !KingdomOrdinaryFoodAuthority.TryObjectNow(Seed, out seedFailure))
					throw new InvalidOperationException("The seed's exact custody is no longer ordinary.");
				bool destroyed = Seed.Destroy(null, Silent: true);
				bool seedSpent = (seedCount > 1 && GameObject.Validate(Seed) && Seed.Count == seedCount - 1)
					|| (seedCount == 1 && destroyed && !GameObject.Validate(Seed));
				if (seedCount > 1) seedSpent = seedSpent
					&& SeedAtSnapshot(Seed, seedInventory, seedCell, seedId, seedBlueprint, seedCount - 1)
					&& KingdomOrdinaryFoodAuthority.TryObjectNow(Seed, out seedFailure);
				if (!seedSpent)
				{
					throw new InvalidOperationException("The seed refused to leave its stack.");
				}
			}
			catch (Exception ex)
			{
				// Restore the receipt first. Even a hostile callback in field or row compensation
				// cannot strand the physical debit behind it.
				bool waterRestored = debit.Rollback();
				bool fieldRestored = true;
				try
				{
					field.CropBlueprint = oldCrop;
					field.NextStageTick = oldNext;
					field.NoLarderAnnounced = oldNoLarder;
					field.ApplyStage(oldStage);
				}
				catch (Exception restoreError)
				{
					fieldRestored = false;
					KingdomLog.Log("crop: field snapshot restore failed (" + restoreError.Message + ")");
				}
				try
				{
					work.SetStringProperty(SeedProperty, oldSeed);
					RestoreInt(work, RowsProperty, hadRows, oldRows);
					RestoreInt(work, SownTickProperty, hadSownTick, oldSownTick);
					RestoreInt(work, CyclesProperty, hadCycles, oldCycles);
					RestoreInt(work, SaidProperty, hadSaid, oldSaid);
				}
				catch (Exception restoreError)
				{
					fieldRestored = false;
					KingdomLog.Log("crop: work snapshot restore failed (" + restoreError.Message + ")");
				}
				try
				{
					List<GameObject> rowsAfter = RowsOf(zone, work);
					for (int i = 0; i < rowsAfter.Count; i++)
					{
						if (rowsBefore.Contains(rowsAfter[i])) continue;
						string rowFailure;
						if (!KingdomOrdinaryFoodAuthority.TryObjectNow(rowsAfter[i], out rowFailure))
						{
							fieldRestored = false;
							continue;
						}
						bool removed = false;
						try { removed = rowsAfter[i].Obliterate(null, Silent: true); }
						finally
						{
							KingdomSurvey.ObserveCurrentTopologyInActive(zone, rowsAfter[i]);
						}
						if (!removed)
						{
							fieldRestored = false;
						}
					}
				}
				catch (Exception restoreError)
				{
					fieldRestored = false;
					KingdomLog.Log("crop: row cleanup failed (" + restoreError.Message + ")");
				}
				try
				{
					if (!GameObject.Validate(Seed)) fieldRestored = false;
					else if (Seed.Count != seedCount)
					{
						if (SeedAtSnapshot(Seed, seedInventory, seedCell, seedId, seedBlueprint, Seed.Count)
							&& KingdomOrdinaryFoodAuthority.TryObjectNow(Seed, out seedFailure))
							Seed.Count = seedCount;
						else fieldRestored = false;
					}
				}
				catch (Exception restoreError)
				{
					fieldRestored = false;
					KingdomLog.Log("crop: seed stack restore failed (" + restoreError.Message + ")");
				}
				KingdomLog.Log("crop: sow transaction refused (" + ex.Message + "; water restored="
					+ waterRestored + "; field restored=" + fieldRestored + ")");
				Popup.Show(waterRestored && fieldRestored
					? "The sowing would not hold. The field is unchanged and the water was returned to its casks."
					: "The sowing would not hold, and one rollback could not be proved exact. Inspect the field, seed, and stores.");
				return;
			}
			// Crewed straight away, off a survey retaken now that the field asks for hands at all:
			// an unsown field is deliberately not in KingdomSurvey.Works, so until this runs the
			// new field carries no crew stamp and would read as "sown and nobody working it" to a
			// founder who sowed and then stood still for a week without a zone activation.
			KingdomGrowth.AssignWork(system, KingdomSurvey.Take(zone, system));
			string realm = KingdomPresentation.Rich(system.KingdomDisplayName);
			system.RecordDeed("the " + fieldName + " you sowed at " + realm);
			KingdomChronicle.Record(system, KingdomCropRules.SownChronicle(CropName(crop), fieldName, realm));
			system.Ledger.Note("{{G|The " + fieldName + " is sown with " + CropName(crop) + ": " + laid
				+ ((laid == 1) ? " row" : " rows") + " in the ground, ripe in " + KingdomCropRules.CropDays + " days.}}");
			if (laid < rows)
			{
				// Said plainly rather than swallowed: the ground took fewer rows than the design
				// promises, and the harvest is what STANDS, so the founder is owed the difference.
				MessageQueue.AddPlayerMessage("{{r|The ground took only " + laid + " of the " + rows
					+ " rows the " + fieldName + " wants. Clear what is standing in it, and sow again for the rest.}}");
			}
			if (KingdomLog.Enabled) KingdomLog.Log("crop: sown " + fieldName + " crop=" + crop + " rows=" + laid + "/" + rows);
		}

		private static bool SeedAtSnapshot(GameObject seed, GameObject inventory, Cell cell,
			string id, string blueprint, int count)
		{
			return GameObject.Validate(seed) && seed.InInventory == inventory
				&& seed.CurrentCell == cell && seed.IDIfAssigned == id
				&& seed.Blueprint == blueprint && seed.Count == count;
		}

		private static void RestoreInt(GameObject Object, string Property, bool Had, int Value)
		{
			if (Had)
			{
				Object.SetIntProperty(Property, Value);
			}
			else
			{
				Object.RemoveIntProperty(Property);
			}
		}

	}
}
