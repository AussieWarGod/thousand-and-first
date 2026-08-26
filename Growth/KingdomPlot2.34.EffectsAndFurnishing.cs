using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static bool FinishPlotEffects(KingdomSystem System, Zone Z,
			GameObject Building, ref KingdomConstructionJob Job)
		{
			if (System == null || !System.Founded || Z == null || Job == null
				|| !GameObject.Validate(Building)
				|| Building.CurrentZone != Z || Building.CurrentCell != Z.GetCell(Job.X, Job.Y)
				|| Building.ID != Job.OutputId || !KingdomConstruction.HasReceipt(Building, Job)
				|| Building.GetIntProperty("KingdomBuilt") != 1
				|| Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Job.TargetKey
				|| !r_KingdomScaffold.HasRemovalProof(Building, Job.SubjectId)) return false;
			if (Job.Phase != KingdomConstructionPhase.Complete)
			{
				if (Job.PhysicalPhase != KingdomPhysicalPhase.FinalRemoved
					|| !KingdomConstruction.Complete(ref Job)) return false;
			}
			if (Job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled) return true;
			if (Job.PhysicalPhase != KingdomPhysicalPhase.EffectsPending
				&& !KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.EffectsPending, Job.PhysicalIndex, Job.PhysicalAmount,
					Job.PhysicalSpilled, Job.SubjectId, Job.OutputId,
					Job.PhysicalReceipt)) return false;
			string display = Building.GetStringProperty(r_KingdomScaffold.CompletionNameProperty)
				?? Building.ShortDisplayName ?? "structure";
			long tick;
			if (!long.TryParse(Building.GetStringProperty(r_KingdomScaffold.CompletionTickProperty),
				global::System.Globalization.NumberStyles.Integer,
				global::System.Globalization.CultureInfo.InvariantCulture, out tick)) tick = Job.DueTick;
			if (!KingdomCeremony.EnsureBuildingRaised(System, Building.CurrentCell, display, tick,
				Building.GetStringProperty(r_KingdomScaffold.CompletionPlanProperty), ref Job)) return false;
			if (!ExactPlotEffectEndpoint(System, Z, Building, Job)) return false;

			bool heart = Building.GetIntProperty(HeartPlotProperty) == 1;
			int rung = KingdomPlotRules.HeartRungOf(Job.TargetKey);
			if (heart && rung > 0)
			{
				// The functional stamp is inspectable and idempotent. Ceremony callbacks are
				// at-most-once: an interrupted Attempting marker becomes honestly lost.
				Z.SetZoneProperty(HeartRungProperty, rung.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture));
				if (Z.GetZoneProperty(HeartRungProperty, null) != rung.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture)) return false;
				int state = Building.GetIntProperty(HeartEffectProperty);
				if (state < 0 || state > 2) return false;
				if (state == 0)
				{
					Building.SetIntProperty(HeartEffectProperty, 1);
					if (Building.GetIntProperty(HeartEffectProperty) != 1) return false;
					KingdomCeremonyHeart.OnRungRaised(System, Z, Job.TargetKey, true);
					if (!ExactPlotEffectEndpoint(System, Z, Building, Job)) return false;
				}
				if (Building.GetIntProperty(HeartEffectProperty) == 1)
					Building.SetIntProperty(HeartEffectProperty, 2);
				if (Building.GetIntProperty(HeartEffectProperty) != 2) return false;
			}
			if (KingdomDelveRules.IsDelve(Job.TargetKey))
			{
				if (!KingdomDelveLink.TrySettle(Building, Z, out string linkFailure))
				{
					KingdomLog.Log("delve link: effects wait: " + linkFailure);
					return false;
				}
				KingdomDelve.RecordShaft(Z.ZoneID);
				if (!KingdomDelve.ShaftStands(Z.ZoneID)) return false;
				int state = Building.GetIntProperty(DelveEffectProperty);
				if (state < 0 || state > 2) return false;
				if (state == 0)
				{
					Building.SetIntProperty(DelveEffectProperty, 1);
					if (Building.GetIntProperty(DelveEffectProperty) != 1) return false;
					string opened = KingdomDelveRules.ShaftOpens(KingdomPresentation.Rich(System.SeatName));
					System.Ledger.Note("{{G|" + opened + "}}");
					MessageQueue.AddPlayerMessage("{{G|" + opened + "}}");
					if (!ExactPlotEffectEndpoint(System, Z, Building, Job)) return false;
				}
				if (Building.GetIntProperty(DelveEffectProperty) == 1)
					Building.SetIntProperty(DelveEffectProperty, 2);
				if (Building.GetIntProperty(DelveEffectProperty) != 2) return false;
			}
			return KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.EffectsSettled, Job.PhysicalIndex, Job.PhysicalAmount,
				Job.PhysicalSpilled, Job.SubjectId, Job.OutputId, Job.PhysicalReceipt);
		}

		private static bool ExactPlotEffectEndpoint(KingdomSystem System, Zone Z,
			GameObject Building, KingdomConstructionJob Job)
		{
			GameObject exact;
			return KingdomConstruction.Owns(System, Z, Job)
				&& KingdomConstruction.IsCurrent(Job)
				&& KingdomConstruction.FindExactId(Z, Job.OutputId, out exact)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(exact, Building) && GameObject.Validate(Building)
				&& Building.CurrentCell == Z.GetCell(Job.X, Job.Y)
				&& KingdomConstruction.HasReceipt(Building, Job);
		}


		private static bool FurnishDurable(Zone Z, KingdomPlotRules.PlotRect Rect,
			string Table, string PlotId, string Key, ref KingdomConstructionJob Job)
		{
			if (Job.PhysicalPhase == KingdomPhysicalPhase.FurnishingSettled
				|| Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved
				|| Job.PhysicalPhase == KingdomPhysicalPhase.EffectsPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled) return true;
			List<FurnishRow> rows;
			if (Job.PhysicalPhase == KingdomPhysicalPhase.FinalOutputSettled)
			{
				KingdomSystem semanticSystem = The.Game == null
					? null : The.Game.RequireSystem<KingdomSystem>();
				string streamId;
				if (!Simulation.Kernel.KingdomSemanticSelectionRules.TryOwnerStreamId(
					"furnish", Job.Id, out streamId)
					|| !TryFreezeFurnishPlan(semanticSystem, Z, Rect, Table, Key,
						streamId, out rows))
				{
					KingdomConstruction.Quarantine(ref Job,
						"The bounded furnishing plan could not be frozen.");
					return false;
				}
				string frozen = EncodeFurnish(rows);
				if (frozen == null || !KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.FurnishingPending, 0, Job.PhysicalAmount,
					Job.PhysicalSpilled,
					Job.SubjectId, Job.OutputId, frozen)) return false;
			}
			else if (Job.PhysicalPhase != KingdomPhysicalPhase.FurnishingPending)
			{
				KingdomConstruction.Quarantine(ref Job,
					"The plot finalization carries an impossible furnishing phase.");
				return false;
			}
			if (!TryDecodeFurnish(Job.PhysicalReceipt, out rows)
				|| Job.PhysicalIndex < 0 || Job.PhysicalIndex > rows.Count)
			{
				KingdomConstruction.Quarantine(ref Job,
					"The frozen furnishing receipt is malformed.");
				return false;
			}
			for (int i = 0; i < rows.Count; i++)
			{
				FurnishRow row = rows[i];
				GameObject exact;
				KingdomPhysicalLookupState exactState = KingdomConstruction.FindExactId(
					Z, row.Id, out exact);
				if (exactState == KingdomPhysicalLookupState.Ambiguous)
				{
					KingdomConstruction.Quarantine(ref Job,
						"A furnishing ID resolves to more than one loaded physical object.");
					return false;
				}
				if (row.Settled)
				{
					if (exactState != KingdomPhysicalLookupState.Exact
						|| !ExactFurnishing(exact, Z, row, PlotId, Job.Id))
					{
						KingdomConstruction.Quarantine(ref Job,
							"A settled furnishing was removed, moved, merged, or replaced.");
						return false;
					}
					continue;
				}
				if (!string.IsNullOrEmpty(row.Id))
				{
					// The exact ID crossed AddObject intent. Only that exact loaded object may
					// settle; absence never authorizes a replacement.
					if (exactState != KingdomPhysicalLookupState.Exact
						|| !ExactFurnishing(exact, Z, row, PlotId, Job.Id))
					{
						KingdomConstruction.Quarantine(ref Job,
							"Furnishing AddObject was interrupted without exact output proof.");
						return false;
					}
					row.Settled = true;
					if (!KingdomConstruction.UpdatePhysical(ref Job,
						KingdomPhysicalPhase.FurnishingPending, i + 1, Job.PhysicalAmount,
						Job.PhysicalSpilled, Job.SubjectId, Job.OutputId,
						EncodeFurnish(rows))) return false;
					continue;
				}
				Cell cell = Z.GetCell(row.X, row.Y);
				if (cell == null || !cell.IsEmpty() || !cell.IsPassable())
				{
					KingdomConstruction.Quarantine(ref Job,
						"Frozen furnishing ground was occupied before insertion.");
					return false;
				}
				GameObject placed;
				try { placed = GameObject.Create(row.Blueprint); }
				catch (System.Exception ex)
				{
					KingdomConstruction.Quarantine(ref Job,
						"Furnishing creation threw: " + ex.Message);
					return false;
				}
				if (!GameObject.Validate(placed))
				{
					KingdomConstruction.Quarantine(ref Job,
						"Furnishing blueprint created no exact object.");
					return false;
				}
				row.Id = placed.ID;
				placed.SetIntProperty(PlotPartProperty, 1);
				if (!string.IsNullOrEmpty(PlotId)) placed.SetStringProperty(PlotIdProperty, PlotId);
				placed.SetStringProperty(FurnishReceiptProperty, Job.Id);
				// Furnishings belong to their plot but are not extra civic accounts. Root works and
				// explicit player dedication own that authority; multiplying it by up to sixty-four
				// population results per plot makes the catch-up envelope unbounded by the plot rail.
				if (!KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.FurnishingPending, i, Job.PhysicalAmount,
					Job.PhysicalSpilled,
					Job.SubjectId, Job.OutputId, EncodeFurnish(rows)))
				{
					RemoveCreatedWorks(placed, Z);
					return false;
				}
				GameObject accepted = null;
				try
				{
					accepted = cell.AddObject(placed);
					KingdomSurvey.ObserveAddResultInActive(Z, placed, accepted);
				}
				catch (System.Exception ex)
				{
					bool cleaned = RemoveCreatedWorks(placed, Z);
					KingdomConstruction.Quarantine(ref Job, (cleaned
						? "Furnishing AddObject threw after output publication: "
						: "Furnishing AddObject threw and cleanup failed: ") + ex.Message);
					return false;
				}
				if (!ReferenceEquals(accepted, placed)
					|| !ExactFurnishing(placed, Z, row, PlotId, Job.Id))
				{
					KingdomConstruction.Quarantine(ref Job,
						"Furnishing changed during AddObject.");
					return false;
				}
				row.Settled = true;
				if (!KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.FurnishingPending, i + 1, Job.PhysicalAmount,
					Job.PhysicalSpilled,
					Job.SubjectId, Job.OutputId, EncodeFurnish(rows))) return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.Furnishings)
			{
				if (!GameObject.Validate(item)
					|| item.GetStringProperty(FurnishReceiptProperty) != Job.Id) continue;
				bool known = false;
				for (int i = 0; i < rows.Count; i++) if (rows[i].Id == item.ID) known = true;
				if (!known)
				{
					KingdomConstruction.Quarantine(ref Job,
						"A replacement furnishing carries the construction receipt.");
					return false;
				}
			}
			return KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.FurnishingSettled, rows.Count, Job.PhysicalAmount,
				Job.PhysicalSpilled, Job.SubjectId, Job.OutputId, EncodeFurnish(rows));
		}

	}
}
