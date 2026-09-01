using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRuntime
	{
		private static void Reconcile(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			GameObject Owner, r_KingdomLabCivicFriction Part)
		{
			ReconcileSavant(System, Z, Survey, Owner, Part);
			ReconcileDeparture(System, Z, Survey, Owner, Part);
		}

		private static void ReconcileSavant(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, GameObject Owner, r_KingdomLabCivicFriction Part)
		{
			KingdomLabCivicReceipt receipt = Part.SavantPrice;
			if (Empty(receipt)) return;
			if (!KingdomLabCivicRules.Valid(receipt, out string failure))
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt, failure)); return;
			}
			if (receipt.Phase == KingdomLabCivicPhase.Quarantined) return;
			if (receipt.Phase == KingdomLabCivicPhase.Closed)
			{
				RecordClose(System, Part, receipt.Kind); return;
			}
			KingdomResidentRow row;
			if (!TryResidentRow(System, receipt.SubjectResidentId, out row))
			{
				Close(System, Z, Part, receipt, KingdomLabCivicClosure.CauseGone, null); return;
			}
			GameObject savant = Survey.FindCitizen(receipt.SubjectResidentId);
			if (!GameObject.Validate(savant))
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt,
					"The current roll names the savant but this active ground cannot prove one body."));
				return;
			}
			if (!ExactSavant(Z, savant, row, KingdomCreed.SeatCreed(System),
				out _, out string homePlot, out string lodge)
				|| homePlot != receipt.SourcePlotId || lodge != receipt.NotableLodgeReceiptId
				|| savant.IDIfAssigned != receipt.SubjectObjectId
				|| savant.GetStringProperty(KingdomCreed.CreedProperty) != receipt.SubjectCreed
				|| KingdomCreed.SeatCreed(System) != receipt.CityCreed
				|| row.ArrivedTick != receipt.TasteOrdinal)
			{
				Close(System, Z, Part, receipt, KingdomLabCivicClosure.CauseGone, savant); return;
			}
			GameObject target = FindExact(Survey, receipt.TargetObjectId,
				out KingdomLabObjectMatch targetMatch);
			if (targetMatch == KingdomLabObjectMatch.Missing)
			{
				Close(System, Z, Part, receipt, KingdomLabCivicClosure.CauseGone, savant);
				return;
			}
			if (targetMatch == KingdomLabObjectMatch.Duplicate)
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt,
					"The request's exact target identity is duplicated on active ground."));
				return;
			}
			if (receipt.Request == KingdomLabCivicRequest.ShrineUnconsecrated
				&& !string.IsNullOrEmpty(target.GetStringProperty(KingdomFaith.ShrineCreedProperty)))
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt,
					"The exact shrine changed outside the open request receipt."));
		}

		private static void ReconcileDeparture(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, GameObject Owner, r_KingdomLabCivicFriction Part)
		{
			KingdomLabCivicReceipt receipt = Part.RefusalDeparture;
			if (Empty(receipt)) return;
			if (!KingdomLabCivicRules.Valid(receipt, out string failure))
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt, failure)); return;
			}
			if (receipt.Phase == KingdomLabCivicPhase.Quarantined) return;
			if (receipt.Phase == KingdomLabCivicPhase.Closed)
			{
				ReconcileClosedDeparture(System, Z, Survey, Part, receipt); return;
			}
			GameObject resident = Survey.FindCitizen(receipt.SubjectResidentId);
			if (!GameObject.Validate(resident))
			{
				if (!TryResidentRow(System, receipt.SubjectResidentId, out _))
				{
					Close(System, Z, Part, receipt, KingdomLabCivicClosure.Departed, null);
					return;
				}
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt,
					"The active ground cannot prove the warned resident's exact body."));
				return;
			}
			string currentPlot = resident.GetStringProperty(KingdomLodging.HomePlotIdProperty);
			if (!string.IsNullOrEmpty(currentPlot)
				&& LawfullyRehoused(System, Z, Owner, resident, receipt, currentPlot))
			{
				Close(System, Z, Part, receipt, KingdomLabCivicClosure.Rehoused, resident); return;
			}
			if (!string.IsNullOrEmpty(currentPlot) && currentPlot != receipt.SourcePlotId)
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt,
					"The warned resident names an unproved or still-reached replacement roof."));
				return;
			}
			KingdomLabDepartureProjection projection = DepartureProjection(resident, receipt);
			if (projection == KingdomLabDepartureProjection.Diverged)
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(receipt,
					"The resident-side cause marker or exact home diverged.")); return;
			}
			if (!ExactDepartureCause(System, Z, Survey, Owner, resident, receipt,
				out _, out _))
			{
				Close(System, Z, Part, receipt, KingdomLabCivicClosure.CauseGone, resident); return;
			}
			if (!TryCompleteDepartureProjection(System, Z, Survey, Owner, Part,
				resident, receipt, out string projectionFailure))
				KingdomLog.Log("lab civic recovery remains retryable: " + projectionFailure);
		}

		private static bool ExactDepartureCause(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, GameObject Owner, GameObject Resident,
			KingdomLabCivicReceipt Receipt, out GameObject SourceHome, out string Failure)
		{
			SourceHome = null; Failure = null;
			if (!GameObject.Validate(Owner) || Owner.IDIfAssigned != Receipt.OwnerObjectId
				|| !GameObject.Validate(Resident) || Resident.CurrentZone != Z
				|| Resident.IDIfAssigned != Receipt.SubjectObjectId
				|| KingdomResidentsId(Resident) != Receipt.SubjectResidentId
				|| Resident.GetStringProperty("KingdomName") != Receipt.SubjectName)
				return Fail("The exact work or resident identity changed.", out Failure);
			SourceHome = FindPlot(Survey, Receipt.SourcePlotId);
			if (!GameObject.Validate(SourceHome))
				return Fail("The exact reached roof no longer stands.", out Failure);
			string[] authored = KingdomQolRules.ParseTags(Resident.GetPropertyOrTag(
				KingdomQolRules.RefusesTagName, ""));
			if (!TryPhysicalOffer(Survey, Owner, out string[] offer,
				out string benefitFailure))
				return Fail("The exact work benefit cannot be proved: " + benefitFailure,
					out Failure);
			return (KingdomQolRules.Has(authored, Receipt.RefusedTag)
				&& KingdomQolRules.Has(offer, Receipt.RefusedTag)
				&& KingdomReach.Reaches(System, Z, Owner, SourceHome))
				|| Fail("The authored Refuses/work/reach cause no longer stands.", out Failure);
		}

		private static bool TryPhysicalOffer(KingdomSurvey Survey, GameObject Owner,
			out string[] Offer, out string Failure)
		{
			Offer = new string[0]; Failure = null;
			if (!GameObject.Validate(Owner) || string.IsNullOrEmpty(Owner.IDIfAssigned))
				return Fail("the exact work root has no stable identity", out Failure);
			if (Survey == null || !Survey.TryBenefits(out KingdomBenefitIndex benefits,
				out Failure)) return false;
			Offer = benefits.TagsForRoot(Owner.IDIfAssigned);
			return true;
		}

		private static bool TryResidentRow(KingdomSystem System, int ResidentId,
			out KingdomResidentRow Row)
		{
			List<KingdomResidentRow> rows = KingdomResidents.RollRows(System);
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].ResidentId == ResidentId) { Row = rows[i]; return true; }
			Row = default(KingdomResidentRow); return false;
		}

		private static GameObject FindExact(KingdomSurvey Survey, string ObjectId,
			out KingdomLabObjectMatch Match)
		{
			GameObject found = null; int matches = 0;
			for (int i = 0; Survey != null && i < Survey.Objects.Count; i++)
				if (GameObject.Validate(Survey.Objects[i])
					&& Survey.Objects[i].IDIfAssigned == ObjectId)
				{ matches++; if (found == null) found = Survey.Objects[i]; }
			Match = KingdomLabCivicRules.ClassifyObjectMatches(matches);
			return Match == KingdomLabObjectMatch.Unique ? found : null;
		}

		private static GameObject FindPlot(KingdomSurvey Survey, string PlotId)
		{
			GameObject found = null;
			for (int i = 0; Survey != null && i < Survey.Built.Count; i++)
				if (Survey.Built[i].GetStringProperty(KingdomPlots.PlotIdProperty) == PlotId)
				{ if (found != null) return null; found = Survey.Built[i]; }
			return found;
		}
	}
}
