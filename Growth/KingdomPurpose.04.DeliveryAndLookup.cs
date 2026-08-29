using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static KingdomPhysicalLookupState FindExactKnown(Zone Zone, string Id,
			out GameObject Exact)
		{
			Exact = null;
			if (Zone == null || string.IsNullOrEmpty(Id)) return KingdomPhysicalLookupState.Absent;
			if (KingdomSurvey.ActiveFor(Zone) != null)
				return KingdomConstruction.FindExactId(Zone, Id, out Exact);
			GameObject candidate = GameObject.FindByID(Id);
			if (!GameObject.Validate(candidate)) return KingdomPhysicalLookupState.Absent;
			if (candidate.IDIfAssigned != Id || candidate.CurrentZone != Zone)
				return KingdomPhysicalLookupState.Ambiguous;
			Exact = candidate;
			return KingdomPhysicalLookupState.Exact;
		}

		private static void ObserveCargoOwners(Zone SourceZone, GameObject Source,
			Zone DestinationZone, GameObject Destination)
		{
			if (GameObject.Validate(Source) && Source.CurrentZone == SourceZone)
				KingdomSurvey.ObserveChangedInActive(SourceZone, Source);
			if (!ReferenceEquals(Destination, Source) && GameObject.Validate(Destination)
				&& Destination.CurrentZone == DestinationZone)
				KingdomSurvey.ObserveChangedInActive(DestinationZone, Destination);
		}

		private static void RestoreSourceOrQuarantine(ref KingdomConstructionJob Job,
			GameObject Cargo, GameObject Source, GameObject Destination, string Failure)
		{
			if (ExactOwned(Cargo, Destination)) return;
			if (ExactOwned(Cargo, Source))
			{
				KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0, Cargo.IDIfAssigned,
					Destination.IDIfAssigned, Job.Payload, Failure);
				return;
			}
			if (Cargo.InInventory == null && Cargo.CurrentCell == null && Source.Inventory != null)
			{
				try
				{
					GameObject restored = Source.Inventory.AddObject(Cargo, null,
						Silent: true, NoStack: true);
					ObserveCargoOwners(Source.CurrentZone, Source,
						Destination.CurrentZone, Destination);
					if (ReferenceEquals(restored, Cargo) && ExactOwned(Cargo, Source))
					{
						KingdomConstruction.UpdatePhysical(ref Job,
							KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0, Cargo.IDIfAssigned,
							Destination.IDIfAssigned, Job.Payload, Failure);
						return;
					}
				}
				catch
				{
					ObserveCargoOwners(Source.CurrentZone, Source,
						Destination.CurrentZone, Destination);
				}
			}
			KingdomConstruction.Quarantine(ref Job, Failure
				+ " Exact source rollback could not be proved.");
		}

		private static void SettleDelivery(KingdomSystem System,
			ref KingdomConstructionJob Job, KingdomPurposeManifest Manifest,
			GameObject Cargo, GameObject Destination)
		{
			if (!ExactOwned(Cargo, Destination) || !ExactCargo(Cargo, Job, Manifest))
			{
				KingdomConstruction.Quarantine(ref Job,
					"Purpose delivery does not retain its exact object and destination provenance.");
				return;
			}
			if (Job.PhysicalPhase != KingdomPhysicalPhase.CargoDelivered
				&& !KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.CargoDelivered, 0, 1, 0, Cargo.IDIfAssigned,
					Destination.IDIfAssigned, Job.Payload)) return;
			if (!RetireCargoRoot(Job, Cargo))
			{
				KingdomConstruction.Quarantine(ref Job,
					"The delivered cargo's exact escrow root could not be retired.");
				return;
			}
			if (Job.Phase != KingdomConstructionPhase.Complete
				&& !KingdomConstruction.Complete(ref Job)) return;
			EnsureDeliveryOutbox(System, ref Job, Manifest);
		}

		private static bool EnsureDeliveryOutbox(KingdomSystem System,
			ref KingdomConstructionJob Job, KingdomPurposeManifest Manifest)
		{
			if (System == null || Job == null || Job.Phase != KingdomConstructionPhase.Complete
				|| Job.PhysicalPhase != KingdomPhysicalPhase.CargoDelivered) return false;
			string eventId = "construction:" + Job.Id + ":delivered";
			if (Job.Outbox == null)
			{
				string line = KingdomPresentation.Rich(Manifest.OriginCity) + " sent one "
					+ Manifest.CargoName + " through the mirror-gate to "
					+ KingdomPresentation.Rich(Manifest.DestinationCity);
				KingdomConstructionOutbox box = new KingdomConstructionOutbox
				{
					EventId = eventId, Mode = 1, Chronicle = line,
					ChronicleState = KingdomConstructionSinkDisposition.Pending,
					Ledger = "{{G|" + line + ". It waits in the exact destination stockpile for "
						+ KingdomPurposeRules.PurposeName(Manifest.Kind) + ".}}",
					LedgerState = KingdomConstructionSinkDisposition.Pending,
					Message = "{{G|The " + Manifest.CargoName + " reaches "
						+ KingdomPresentation.Rich(Manifest.DestinationCity) + ".}}",
					MessageState = KingdomConstructionSinkDisposition.Pending,
					Deed = line, DeedState = KingdomConstructionSinkDisposition.Pending
				};
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return false;
			}
			else if (Job.Outbox.EventId != eventId)
			{
				KingdomConstruction.Quarantine(ref Job,
					"The purpose delivery carries another terminal event identity.");
				return false;
			}
			return KingdomCeremony.DispatchPending(System, ref Job);
		}

		private static bool ExactDeliveredCargo(KingdomConstructionJob Job,
			KingdomPurposeManifest Manifest, out GameObject Cargo, out GameObject Destination)
		{
			Cargo = null;
			Destination = null;
			if (The.ZoneManager == null || !The.ZoneManager.IsZoneBuilt(Manifest.DestinationZoneId))
				return false;
			Zone zone;
			try { zone = The.ZoneManager.GetZone(Manifest.DestinationZoneId); }
			catch { return false; }
			return FindExactKnown(zone, Job.PhysicalDestinationId,
				out Destination) == KingdomPhysicalLookupState.Exact
				&& Destination.Inventory != null
				&& Destination.GetIntProperty(KingdomMaterials.StockpileProperty) == 1
				&& FindExactKnown(zone, Job.OutputId, out Cargo)
					== KingdomPhysicalLookupState.Exact
				&& ExactCargo(Cargo, Job, Manifest) && ExactOwned(Cargo, Destination);
		}

		private static bool FindLocalConnection(KingdomSystem System, Zone Z,
			out KingdomPurposeConnection Connection, out string Failure)
		{
			Connection = null;
			Failure = "Raise and key a mirror-gate on this ground, then visit both ends so their current power can be proved.";
			foreach (GameObject item in Z.GetObjects())
			{
				r_KingdomMirrorGate gate = item?.GetPart<r_KingdomMirrorGate>();
				if (gate == null) continue;
				if (KingdomMirrorGate.TryPurposeConnection(gate, System, out Connection,
					out string gateFailure)) return true;
				Failure = gateFailure ?? Failure;
			}
			return false;
		}

		private static bool FindDeliveredCargo(Zone Z, KingdomPurposeDefinition Definition,
			string DestinationSettlementId, string LocalGateKey, string PartnerGateKey,
			out GameObject Cargo, out KingdomConstructionJob Job,
			out KingdomPurposeManifest Manifest, out string Failure)
		{
			Cargo = null;
			Job = null;
			Manifest = null;
			Failure = null;
			List<GameObject> candidates = new List<GameObject>();
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			for (int i = 0; i < stock.Stockpiles.Count; i++)
			{
				Inventory inventory = stock.Stockpiles[i].Inventory;
				for (int j = 0; inventory != null && j < inventory.Objects.Count; j++)
				{
					GameObject item = inventory.Objects[j];
					if (item.GetIntProperty(CargoSchemaProperty) == CargoSchema
						&& item.GetStringProperty(CargoKeyProperty) == Definition.CargoKey)
						candidates.Add(item);
				}
			}
			candidates.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject item = candidates[i];
				string receipt = item.GetStringProperty(CargoConsignmentProperty);
				string encoded = item.GetStringProperty(CargoManifestProperty);
				if (!KingdomConstruction.TryFind(receipt, out KingdomConstructionJob job)
					|| !KingdomPurposeRules.TryDecodeManifest(encoded,
						out KingdomPurposeManifest manifest)
					|| job.Route != KingdomConstructionRoute.PurposeConsignment
					|| !SettledConsignment(job, encoded, item.IDIfAssigned)
					|| job.OutputId != item.IDIfAssigned || manifest.BuildKey != Definition.BuildKey
					|| !KingdomPurposeRules.ManifestMatchesDefinition(manifest, Definition)
					|| manifest.DestinationSettlementId != DestinationSettlementId
					|| manifest.DestinationGateKey != LocalGateKey
					|| manifest.SourceGateKey != PartnerGateKey
					|| manifest.OriginSettlementId == DestinationSettlementId
					|| !ExactCargo(item, job, manifest)) continue;
				Cargo = item;
				Job = job;
				Manifest = manifest;
				return true;
			}
			return Fail("This city lacks the exact other-city " + Definition.CargoName
				+ ". Produce it at the far mirror-gate and keep it in a dedicated stockpile here.", out Failure);
		}

		private static bool SettledConsignment(KingdomConstructionJob Job, string Manifest,
			string CargoId)
		{
			if (!KingdomPurposeRules.TryDecodeManifest(Manifest,
				out KingdomPurposeManifest decoded) || decoded.BuildKey != Job?.TargetKey)
				return false;
			if (Job == null || Job.Route != KingdomConstructionRoute.PurposeConsignment
				|| Job.Phase != KingdomConstructionPhase.Complete
				|| Job.PhysicalPhase != KingdomPhysicalPhase.CargoDelivered
				|| Job.OutputId != CargoId || !KingdomConstructionRules.ValidJob(Job)) return false;
			return Job.Compacted
				? string.IsNullOrEmpty(Job.Payload) && string.IsNullOrEmpty(Job.PhysicalReceipt)
				: KingdomConstructionRules.TerminalClosureSettled(Job)
					&& Job.Payload == Manifest && Job.PhysicalReceipt == Manifest;
		}

	}
}
