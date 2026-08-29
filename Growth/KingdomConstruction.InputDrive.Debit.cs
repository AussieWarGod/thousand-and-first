using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool DriveInputDebit(KingdomSystem system, Zone target,
			ref KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			out string failure)
		{
			failure = null;
			for (int i = 0; i < receipt.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine cargo = receipt.CargoAt(i);
				KingdomConstructionInputSourceLine source = receipt.SourceAt(
					cargo.SourceLineOrdinal);
				if (cargo.Phase == KingdomConstructionInputCargoPhase.Landed)
				{
					if (!KingdomCentralLogistics.TryResolveConstructionInputTargetCarrier(system,
						job.Id, cargo.ChildJobId, cargo.ChildTripId, receipt.Schema,
						receipt.PlanDigest, receipt.Revision, target,
						out GameObject standingCarrier, out KingdomCityFault _)
						|| !ExactInputCargo(target, standingCarrier, job, receipt, cargo,
							out GameObject _)
						|| !ExactDebitChildManifest(system, target, standingCarrier, job,
							receipt, cargo))
					{
						failure = "Landed cargo is not in its exact active carrier; debit waits.";
						return false;
					}
					return InputCargoPhase(ref job, receipt, i,
						KingdomConstructionInputCargoPhase.DebitIntent, out failure);
				}
				if (cargo.Phase == KingdomConstructionInputCargoPhase.DebitIntent)
					return ConsumeInputCargo(system, target, ref job, receipt,
						cargo, out failure);
				if (cargo.Phase != KingdomConstructionInputCargoPhase.Spent)
				{
					failure = "A routed construction cargo row left debit custody.";
					return false;
				}
				if (source.Phase == KingdomConstructionInputSourcePhase.Debited)
					return InputSourcePhase(ref job, receipt, source.Ordinal,
						KingdomConstructionInputSourcePhase.Spent, out failure);
				if (source.Phase != KingdomConstructionInputSourcePhase.Spent)
				{
					failure = "A routed construction source cannot close its exact debit.";
					return false;
				}
			}
			return TransitionInputTx(ref job, receipt,
				KingdomConstructionInputTxPhase.Closing, out failure);
		}

		private static bool ConsumeInputCargo(KingdomSystem system, Zone target,
			ref KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo, out string failure)
		{
			failure = null;
			GameObject carrier;
			KingdomCityFault central;
			if (!KingdomCentralLogistics.TryResolveConstructionInputTargetCarrier(system,
				job.Id, cargo.ChildJobId, cargo.ChildTripId, receipt.Schema,
				receipt.PlanDigest, receipt.Revision, target, out carrier, out central))
			{
				failure = "The exact landed construction carrier is unavailable ("
					+ central + ").";
				return false;
			}
			if (!ExactDebitChildManifest(system, target, carrier, job, receipt, cargo))
			{
				failure = "The complete landed child manifest changed before exact debit.";
				return false;
			}
			string before = InputWitness("consume", cargo.CargoKey, cargo.ObjectId,
				(int)cargo.Kind, cargo.Amount, "present");
			string after = InputWitness("consume", cargo.CargoKey, cargo.ObjectId,
				(int)cargo.Kind, cargo.Amount, "absent");
			if (string.IsNullOrEmpty(cargo.BeforeWitnessHash))
				return InputCargoEvidence(ref job, receipt, cargo.Ordinal, cargo.ObjectId,
					cargo.CustodyTopology, cargo.CustodyOwnerId, cargo.CustodyZoneId,
					cargo.CustodyX, cargo.CustodyY, before, cargo.AfterWitnessHash,
					cargo.Spent, cargo.Lost, out failure);
			if (string.IsNullOrEmpty(cargo.AfterWitnessHash))
				return InputCargoEvidence(ref job, receipt, cargo.Ordinal, cargo.ObjectId,
					cargo.CustodyTopology, cargo.CustodyOwnerId, cargo.CustodyZoneId,
					cargo.CustodyX, cargo.CustodyY, cargo.BeforeWitnessHash, after,
					cargo.Spent, cargo.Lost, out failure);

			GameObject exact;
			bool graveyard;
			KingdomPhysicalLookupState state = FindGlobalInputId(receipt, cargo.ObjectId,
				out exact, out graveyard);
			if (state == KingdomPhysicalLookupState.Exact
				&& !graveyard && !(cargo.Kind == KingdomConstructionInputKind.Water
					&& exact.GetPart<LiquidVolume>()?.Volume == 0
						? ExactInputCargo(target, carrier, job, receipt, cargo, 0, out exact)
						: ExactInputCargo(target, carrier, job, receipt, cargo, out exact)))
				state = KingdomPhysicalLookupState.Ambiguous;
			string observed = ObserveConsume(job, receipt, cargo, state, exact, graveyard);
			if (state == KingdomPhysicalLookupState.Exact && !graveyard
				&& cargo.Kind == KingdomConstructionInputKind.Water
				&& exact.GetPart<LiquidVolume>()?.Volume == 0)
			{
				if (receipt.Paused || !KingdomMaster.NewWorkAllowed(system)) return false;
				if (!ExactDebitChildManifest(system, target, carrier, job, receipt, cargo)
					|| !ExactInputCargo(target, carrier, job, receipt, cargo, 0, out exact))
				{
					failure = "The empty water cask changed immediately before cleanup.";
					return false;
				}
				bool removed = false;
				try { removed = exact.Obliterate(null, Silent: true); } catch { removed = false; }
				finally { KingdomSurvey.ObserveChangedInActive(target, carrier); }
				state = FindGlobalInputId(receipt, cargo.ObjectId, out exact, out graveyard);
				observed = ObserveConsume(job, receipt, cargo, state, exact, graveyard);
				if (!removed && observed != after)
				{
					failure = "The empty routed water cask refused exact cleanup; debit remains pending.";
					return false;
				}
			}
			KingdomConstructionInputDecision decision =
				KingdomConstructionInputRules.DecidePhysicalMutation(before, after,
					observed, receipt.Paused);
			if (decision == KingdomConstructionInputDecision.WaitPaused) return false;
			if (decision == KingdomConstructionInputDecision.Apply)
			{
				if (!KingdomMaster.NewWorkAllowed(system)) return false;
				if (!ExactDebitChildManifest(system, target, carrier, job, receipt, cargo)
					|| !ExactInputCargo(target, carrier, job, receipt, cargo, out exact))
				{
					failure = "Routed cargo changed immediately before its exact debit callback.";
					return false;
				}
				bool removed = false;
				if (cargo.Kind == KingdomConstructionInputKind.Water)
				{
					LiquidVolume liquid = exact.GetPart<LiquidVolume>();
					int drained = 0;
					try { drained = KingdomLiquids.Drain(liquid, cargo.Amount); }
					catch { }
					finally { KingdomSurvey.ObserveChangedInActive(target, carrier); }
					if (drained == cargo.Amount && liquid.Volume == 0)
					{
						if (!ExactDebitChildManifest(system, target, carrier, job, receipt, cargo)
							|| !ExactInputCargo(target, carrier, job, receipt, cargo, 0, out exact))
						{
							failure = "Water debit changed protected evidence before cask cleanup.";
							return false;
						}
						try { removed = exact.Obliterate(null, Silent: true); }
						catch { }
						finally { KingdomSurvey.ObserveChangedInActive(target, carrier); }
					}
				}
				else
				{
					try { removed = exact.Obliterate(null, Silent: true); }
					catch { }
					finally { KingdomSurvey.ObserveChangedInActive(target, carrier); }
				}
				state = FindGlobalInputId(receipt, cargo.ObjectId, out exact, out graveyard);
				observed = ObserveConsume(job, receipt, cargo, state, exact, graveyard);
				if (!removed && observed != after)
				{
					failure = "The exact routed cargo refused destruction; debit remains pending.";
					return false;
				}
			}
			if (cargo.Kind == KingdomConstructionInputKind.Water
				&& state == KingdomPhysicalLookupState.Exact && !graveyard
				&& exact.GetPart<LiquidVolume>()?.Volume == 0)
			{
				if (!ExactDebitChildManifest(system, target, carrier, job, receipt, cargo)
					|| !ExactInputCargo(target, carrier, job, receipt, cargo, 0, out exact))
				{
					failure = "The drained water cask changed before terminal cleanup.";
					return false;
				}
				bool removed = false;
				try { removed = exact.Obliterate(null, Silent: true); } catch { removed = false; }
				KingdomSurvey.ObserveChangedInActive(target, carrier);
				state = FindGlobalInputId(receipt, cargo.ObjectId, out exact, out graveyard);
				observed = ObserveConsume(job, receipt, cargo, state, exact, graveyard);
				if (!removed && observed != after)
				{
					failure = "The empty routed water cask refused exact cleanup; debit remains pending.";
					return false;
				}
			}
			if (observed != after)
			{
				failure = "The exact construction cargo debit has an ambiguous aftermath.";
				return false;
			}
			return InputCargoPhaseEvidence(ref job, receipt, cargo.Ordinal,
				KingdomConstructionInputCargoPhase.Spent, cargo.ObjectId,
				KingdomConstructionInputTopology.Consumed, "spent:" + job.Id,
				receipt.TargetZoneId, receipt.TargetX, receipt.TargetY,
				cargo.BeforeWitnessHash, cargo.AfterWitnessHash, cargo.Amount, 0,
				out failure);
		}

		private static string ObserveConsume(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputCargoLine cargo,
			KingdomPhysicalLookupState state, GameObject exact, bool graveyard)
		{
			if (state == KingdomPhysicalLookupState.Exact && graveyard
				&& ExactConsumedCargoEvidence(job, receipt, cargo, exact))
				return InputWitness("consume", cargo.CargoKey, cargo.ObjectId,
					(int)cargo.Kind, cargo.Amount, "absent");
			if (state != KingdomPhysicalLookupState.Exact || !GameObject.Validate(exact)
				|| exact.Blueprint != cargo.Blueprint) return InputWitness("invalid");
			if (cargo.Kind == KingdomConstructionInputKind.Water)
			{
				LiquidVolume liquid = exact.GetPart<LiquidVolume>();
				if (liquid == null || liquid.Volume != cargo.Amount
					|| !KingdomLiquids.HasFreshWater(liquid)) return InputWitness("invalid");
			}
			else if (exact.Count != cargo.Amount) return InputWitness("invalid");
			return InputWitness("consume", cargo.CargoKey, cargo.ObjectId,
				(int)cargo.Kind, cargo.Amount, "present");
		}

		private static bool ExactConsumedCargoEvidence(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputCargoLine cargo,
			GameObject exact)
		{
			string marker = cargo.Kind == KingdomConstructionInputKind.Water
				? cargo.CreationMarker : cargo.CargoKey;
			if (exact == null || !exact.IsInGraveyard() || exact.IDIfAssigned != cargo.ObjectId
				|| exact.Blueprint != cargo.Blueprint || exact.IsImportant()
				|| exact.Equipped != null || !exact.IsTakeable() || exact.HasTag("AlwaysStack")
				|| !exact.HasStringProperty(InputMarkerProperty)
				|| exact.HasIntProperty(InputMarkerProperty)
				|| exact.GetStringProperty(InputMarkerProperty) != marker
				|| !KingdomOrdinaryCustody.TryProveRetiredEmpty(exact, out string _)
				|| !RetiredRoutedInputItemAuthorized(job, receipt, exact)
				|| !KingdomPurpose.HasProtectedCargoEvidence(exact)
					&& exact.GetIntProperty("NeverStack") != 1) return false;
			if (cargo.Kind != KingdomConstructionInputKind.Water)
				return exact.Count == cargo.Amount
					&& TryInputClassification(exact, out KingdomConstructionInputKind kind,
						out string classification) && kind == cargo.Kind
					&& classification == cargo.Classification;
			LiquidVolume liquid = exact.GetPart<LiquidVolume>();
			return liquid != null && !liquid.Sealed && liquid.MaxVolume == cargo.Capacity
				&& liquid.Volume == 0
				&& exact.GetIntProperty(KingdomPorters.StockProperty) == 1;
		}

		/// <summary>Searches every cached object graph and the engine graveyard. Absence is
		/// never inferred from the target zone alone; duplicate identities are ambiguous.</summary>
		private static KingdomPhysicalLookupState FindGlobalInputId(
			KingdomConstructionInputReceipt receipt, string objectId,
			out GameObject exact, out bool graveyard)
		{
			exact = null;
			graveyard = false;
			if (string.IsNullOrEmpty(objectId) || The.ZoneManager == null)
				return KingdomPhysicalLookupState.Ambiguous;
			HashSet<GameObject> found = new HashSet<GameObject>();
			Zone zone = The.ZoneManager.ActiveZone;
			if (zone != null && KingdomSurvey.ActiveFor(zone) != null)
			{
				GameObject candidate;
				KingdomPhysicalLookupState state = FindExactId(zone, objectId, out candidate);
				if (state == KingdomPhysicalLookupState.Ambiguous)
					return KingdomPhysicalLookupState.Ambiguous;
				if (state == KingdomPhysicalLookupState.Exact) found.Add(candidate);
			}
			GameObject transit;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			KingdomPhysicalLookupState transitState = KingdomCentralLogistics
				.FindConstructionInputTransitObject(system, receipt, objectId, out transit);
			if (transitState == KingdomPhysicalLookupState.Ambiguous)
				return KingdomPhysicalLookupState.Ambiguous;
			if (transitState == KingdomPhysicalLookupState.Exact) found.Add(transit);
			if (The.ZoneManager.Graveyard?.Objects != null)
				for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
				{
					GameObject candidate = The.ZoneManager.Graveyard.Objects[i];
					if (candidate != null && candidate.IDIfAssigned == objectId)
						found.Add(candidate);
				}
			if (found.Count == 0) return KingdomPhysicalLookupState.Absent;
			if (found.Count != 1) return KingdomPhysicalLookupState.Ambiguous;
			foreach (GameObject candidate in found) exact = candidate;
			graveyard = exact != null && exact.IsInGraveyard();
			return KingdomPhysicalLookupState.Exact;
		}
	}
}
