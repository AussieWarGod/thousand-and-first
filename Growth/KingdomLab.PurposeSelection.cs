using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomLab
	{
		internal const string PurposeIntentProperty = "r_TAF_PurposeLabIntent";

		internal static bool TrySelectPurposeProcedure(GameObject Building, GameObject Actor,
			KingdomSystem System, string PairId, long PairEpoch, string OperationId,
			out string ProcedureKey, out string Receipt, out string Quote, out string Failure)
		{
			ProcedureKey = null;
			Receipt = null;
			Quote = null;
			Failure = null;
			if (!GameObject.Validate(Building) || !GameObject.Validate(Actor) || System == null
				|| !Building.HasPart("r_KingdomChimericTheatre"))
				return Refuse("The exact chimeric theatre and patient are unavailable.", out Failure);
			if (Building.GetPart<r_KingdomLabJob>() != null || ActiveRemovalJob(Actor) != null)
				return Refuse("The theatre or patient already carries an unsettled body receipt.",
					out Failure);
			List<GameObject> kept = KeptParts(Actor);
			List<string> names = new List<string>();
			List<LabSlot> anatomy = KingdomProcedures.Census(Actor, names);
			r_KingdomLabRecord record = Actor.GetPart<r_KingdomLabRecord>()
				?? new r_KingdomLabRecord();
			record.Normalize();
			List<string> roster = KingdomZoning.Roster(System);
			List<LabProcedure> procedures = new List<LabProcedure>();
			List<int> places = new List<int>();
			List<string> rows = new List<string>();
			for (int at = 0; at < anatomy.Count; at++)
			{
				List<LabProcedure> offers = Candidates(anatomy, at, RungAt(Building), kept,
					record, roster);
				for (int i = 0; i < offers.Count; i++)
				{
					procedures.Add(offers[i]);
					places.Add(at);
					rows.Add("{{W|" + offers[i].Named + "}} at " + names[at] + "\n"
						+ KingdomLabRules.PriceLine(offers[i]));
				}
			}
			if (rows.Count == 0)
				return Refuse("No discovered, eligible procedure has its exact kept source and body slot.",
					out Failure);
			int picked = Popup.PickOption(Title: "Choose the operation's body procedure",
				Intro: "This existing theatre procedure is part of the reciprocal operation. Its own price is additional and is frozen before any cargo or local debit.",
				Options: rows, AllowEscape: true, RespectOptionNewlines: true);
			if (picked < 0) return false;
			LabProcedure procedure = procedures[picked];
			XRL.World.Anatomy.BodyPart part = SelectedPart(Actor, places[picked]);
			GameObject bearer = procedure.Attach == LabAttach.Weapon
				? part?.DefaultBehavior : Actor;
			string bitFailure = null;
			KingdomBitTally bits = null;
			if (part == null || !GameObject.Validate(bearer)
				|| !KingdomMaterialRules.TryParseBitCost(procedure.Bits,
					out bits, out bitFailure))
				return Refuse(part == null || !GameObject.Validate(bearer)
					? "The selected exact body slot changed."
					: "The selected procedure has an invalid bit price (" + bitFailure + ").",
					out Failure);
			string bitClaim = new KingdomMaterialDebitCost(new KingdomMaterialTally(), bits,
				new KingdomExoticTally()).ToClaimString();
			string actorId = Actor.IDIfAssigned;
			int partId = part.ID;
			string bearerId = bearer.IDIfAssigned;
			if (string.IsNullOrEmpty(actorId) || partId <= 0
				|| string.IsNullOrEmpty(bearerId))
				return Refuse("The selected procedure lacks assigned physical identity.", out Failure);
			KingdomPurposeBodyAuthority authority = new KingdomPurposeBodyAuthority
			{
				Kind = KingdomPurposeKind.Flesh, PairId = PairId, PairEpoch = PairEpoch,
				OperationId = OperationId, AuthorityId = Guid.NewGuid().ToString("N"),
				SubjectObjectId = actorId, SubjectGeneId = "", ProcedureKey = procedure.Key,
				BodyPartId = partId, BearerId = bearerId, WaterCost = procedure.Cost,
				BitCost = bitClaim, PreservedCost = procedure.Preserved
			};
			Receipt = KingdomPurposeBodyAuthorityRules.Encode(authority);
			if (Receipt == null)
				return Refuse("The exact procedure selection could not be encoded.", out Failure);
			ProcedureKey = procedure.Key;
			Quote = KingdomLabRules.PriceLine(procedure);
			return true;
		}

		internal static bool TryPreflightPurposeProcedure(GameObject Building, GameObject Actor,
			KingdomSystem System, KingdomPurposeBodyAuthority Authority, int PortfolioWater,
			out string Failure)
		{
			Failure = null;
			if (!PurposeSelectionStands(Building, Actor, System, Authority,
				out LabProcedure procedure, out List<GameObject> kept, out _))
				return Refuse("The frozen theatre procedure, patient, slot, or kept source changed.",
					out Failure);
			KingdomSurvey survey = KingdomSurvey.Take(Building.CurrentZone, System);
			if (survey == null || survey.StoredWater < Authority.WaterCost + PortfolioWater)
				return Refuse("The stores cannot cover both the quoted procedure and purpose water.",
					out Failure);
			if (CountFor(kept, procedure) < Authority.PreservedCost)
				return Refuse("The exact selected procedure no longer has enough kept source.",
					out Failure);
			if (!KingdomMaterialRules.TryParseBitCost(procedure.Bits,
				out KingdomBitTally bits, out _)) return false;
			KingdomMaterialDebit debit = bits.IsEmpty()
				? null : KingdomMaterials.ReserveBits(Building.CurrentZone, bits);
			bool covered = bits.IsEmpty() || (debit != null
				&& debit.Reservation.Outcome == KingdomMaterialDebitOutcome.Reserved);
			debit?.Cancel();
			return covered || Refuse("Dedicated stockpiles cannot cover the procedure's exact bits.",
				out Failure);
		}

		private static bool PurposeSelectionStands(GameObject Building, GameObject Actor,
			KingdomSystem System, KingdomPurposeBodyAuthority Authority,
			out LabProcedure Procedure, out List<GameObject> Kept, out int At)
		{
			Procedure = null;
			Kept = new List<GameObject>();
			At = -1;
			if (Authority == null || Authority.Kind != KingdomPurposeKind.Flesh
				|| !GameObject.Validate(Building) || !Building.HasPart("r_KingdomChimericTheatre")
				|| !GameObject.Validate(Actor) || Actor.IDIfAssigned != Authority.SubjectObjectId
				|| System == null || Building.GetPart<r_KingdomLabJob>() != null
				|| ActiveRemovalJob(Actor) != null
				|| !KingdomProcedures.TryGet(Authority.ProcedureKey, out Procedure)
				|| Procedure.Cost != Authority.WaterCost
				|| Procedure.Preserved != Authority.PreservedCost
				|| !KingdomMaterialRules.TryParseBitCost(Procedure.Bits,
					out KingdomBitTally bits, out _)
				|| new KingdomMaterialDebitCost(new KingdomMaterialTally(), bits,
					new KingdomExoticTally()).ToClaimString() != Authority.BitCost) return false;
			List<string> names = new List<string>();
			List<LabSlot> anatomy = KingdomProcedures.Census(Actor, names);
			for (int i = 0; i < anatomy.Count; i++)
				if (SelectedPart(Actor, i)?.ID == Authority.BodyPartId) { At = i; break; }
			if (At < 0) return false;
			GameObject bearer = Procedure.Attach == LabAttach.Weapon
				? SelectedPart(Actor, At)?.DefaultBehavior : Actor;
			if (!GameObject.Validate(bearer) || bearer.IDIfAssigned != Authority.BearerId) return false;
			Kept = KeptParts(Actor);
			r_KingdomLabRecord record = Actor.GetPart<r_KingdomLabRecord>()
				?? new r_KingdomLabRecord();
			record.Normalize();
			List<LabProcedure> offers = Candidates(anatomy, At, RungAt(Building), Kept,
				record, KingdomZoning.Roster(System));
			for (int i = 0; i < offers.Count; i++)
				if (offers[i].Key == Authority.ProcedureKey) return true;
			return false;
		}

		private static bool Refuse(string Text, out string Failure)
		{
			Failure = Text;
			return false;
		}
	}
}
