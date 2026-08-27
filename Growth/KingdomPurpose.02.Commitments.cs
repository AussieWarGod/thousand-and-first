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
		/// <summary>Purpose gate for a direct plot quote. Ordinary designs are unchanged.</summary>
		internal static bool TryQuoteCommit(KingdomSystem System, Zone Z, string BuildKey,
			out string Receipt, out GameObject Cargo, out string Failure)
		{
			Receipt = null;
			Cargo = null;
			Failure = null;
			if (InvalidDefinitions.Contains(BuildKey ?? ""))
				return Fail("This purpose declaration is malformed; fix its catalogue metadata before commissioning it.", out Failure);
			if (!Definitions.TryGetValue(BuildKey ?? "", out KingdomPurposeDefinition definition))
				return true;
			if (System == null || Z == null || !TrySettlementIdentity(System, Z.ZoneID,
				out string settlementId))
				return Fail("The city's immutable settlement identity cannot be reproved; reseat and revisit it before committing its purpose.", out Failure);
			if (!FindLocalConnection(System, Z, out KingdomPurposeConnection connection,
				out Failure)) return false;
			if (!FindDeliveredCargo(Z, definition, settlementId, connection.SourceKey,
				connection.DestinationKey, out Cargo, out KingdomConstructionJob consignment,
				out KingdomPurposeManifest manifest, out Failure)) return false;
			if (!TrySiteProof(System, Z, definition, out string siteProof,
				out GameObject specialist, out Failure))
			{
				Cargo = null;
				return false;
			}
			KingdomPurposeCommitment commitment = new KingdomPurposeCommitment
			{
				Manifest = KingdomPurposeRules.EncodeManifest(manifest),
				ConsignmentId = consignment.Id, CargoItemId = Cargo.ID,
				SiteProof = siteProof, SpecialistId = specialist.ID,
				SpecialistName = !string.IsNullOrEmpty(
					specialist.GetStringProperty("KingdomName"))
					? specialist.GetStringProperty("KingdomName")
					: (specialist.BaseDisplayNameStripped ?? "the specialist")
			};
			Receipt = KingdomPurposeRules.EncodeCommitment(commitment);
			if (Receipt == null)
			{
				Cargo = null;
				return Fail("The exact purpose commitment could not be frozen. Nothing was spent.", out Failure);
			}
			return true;
		}

		internal static bool ResolveCommitCargo(Zone Z, string BuildKey, string Receipt,
			out GameObject Cargo, out string Failure)
		{
			Cargo = null;
			Failure = null;
			if (!Definitions.ContainsKey(BuildKey ?? "")) return string.IsNullOrEmpty(Receipt);
			if (!KingdomPurposeRules.TryDecodeCommitment(Receipt,
				out KingdomPurposeCommitment commitment)
				|| !KingdomPurposeRules.TryDecodeManifest(commitment.Manifest,
					out KingdomPurposeManifest manifest) || manifest.BuildKey != BuildKey)
				return Fail("The frozen purpose commitment is absent or malformed.", out Failure);
			if (!KingdomConstruction.TryFind(commitment.ConsignmentId,
				out KingdomConstructionJob job) || job.Route != KingdomConstructionRoute.PurposeConsignment
				|| job.OutputId != commitment.CargoItemId
				|| !SettledConsignment(job, commitment.Manifest, commitment.CargoItemId))
				return Fail("The frozen consignment is not a settled exact delivery.", out Failure);
			if (FindExactKnown(Z, commitment.CargoItemId, out Cargo)
				!= KingdomPhysicalLookupState.Exact || !ExactCargo(Cargo, job, manifest)
				|| Cargo.GetIntProperty(KingdomMaterials.StockpileProperty) == 1
				|| Cargo.InInventory == null
				|| Cargo.InInventory.GetIntProperty(KingdomMaterials.StockpileProperty) != 1)
			{
				Cargo = null;
				return Fail("The exact delivered cargo left its destination stockpile, changed, or was consumed. Return that object; no same-kind material substitutes for it.", out Failure);
			}
			return true;
		}

		/// <summary>Whether an unfinished plot-funding row carries our frozen exact commitment.</summary>
		internal static bool HasFrozenCommitment(KingdomConstructionJob Job)
		{
			return Job != null && Job.Route == KingdomConstructionRoute.PlotCommission
				&& KingdomPurposeRules.TryDecodeCommitment(Job.PhysicalReceipt,
					out KingdomPurposeCommitment commitment)
				&& KingdomPurposeRules.TryDecodeManifest(commitment.Manifest,
					out KingdomPurposeManifest manifest) && manifest.BuildKey == Job.TargetKey;
		}

		internal static bool RequiresExactFunding(KingdomConstructionJob Job)
		{
			if (Job == null || Job.Route != KingdomConstructionRoute.PlotCommission) return false;
			KingdomData.EnsureBuildings();
			return Definitions.ContainsKey(Job.TargetKey ?? "")
				|| InvalidDefinitions.Contains(Job.TargetKey ?? "")
				|| !string.IsNullOrEmpty(Job.PhysicalReceipt);
		}

		/// <summary>
		/// Rebinds an outstanding commission to the same delivered cargo. The outstanding claim must
		/// still contain its typed unit; absence or prior consumption is ambiguous because material
		/// tallies cannot prove which object a callback destroyed, so it quarantines rather than
		/// accepting a fungible replacement.
		/// </summary>
		internal static bool TryRequiredFundingItem(Zone Z, KingdomConstructionJob Job,
			out GameObject RequiredItem, out string Failure)
		{
			RequiredItem = null;
			Failure = null;
			if (!HasFrozenCommitment(Job)
				|| !KingdomPurposeRules.TryDecodeCommitment(Job.PhysicalReceipt,
					out KingdomPurposeCommitment commitment)
				|| !KingdomPurposeRules.TryDecodeManifest(commitment.Manifest,
					out KingdomPurposeManifest manifest)
				|| Job.Claims == null || !KingdomMaterialDebitCost.TryParseClaim(
					Job.Claims.MaterialOutstanding, out KingdomMaterialDebitCost outstanding))
				return Fail("The frozen city-purpose funding receipt cannot be decoded for retry.",
					out Failure);
			if (outstanding.Materials.Get(manifest.CargoMaterial) < 1)
				return Fail("The purpose cargo identity and outstanding typed claim disagree. Inspect the receipt; no same-kind object may stand in for it.", out Failure);
			KingdomData.EnsureBuildings();
			if (!ResolveCommitCargo(Z, Job.TargetKey, Job.PhysicalReceipt,
				out RequiredItem, out Failure)) return false;
			return GameObject.Validate(RequiredItem) && RequiredItem.ID == commitment.CargoItemId;
		}

		internal static string AppendPreview(string Existing, string PurposeReceipt)
		{
			if (string.IsNullOrEmpty(PurposeReceipt)) return Existing;
			if (!KingdomPurposeRules.TryDecodeCommitment(PurposeReceipt,
				out KingdomPurposeCommitment commitment)
				|| !KingdomPurposeRules.TryDecodeManifest(commitment.Manifest,
					out KingdomPurposeManifest manifest))
				return (Existing ?? "") + "\nPURPOSE RECEIPT INVALID: nothing may be spent.";
			return (Existing ?? "") + "\nPurpose commitment: "
				+ KingdomPurposeRules.PurposeName(manifest.Kind) + ".\n"
				+ "Exact cross-city input: 1 " + manifest.CargoName + " (object "
				+ commitment.CargoItemId + "), produced by "
				+ KingdomPresentation.Rich(manifest.OriginCity) + " at "
				+ manifest.ProducerProof.Replace('|', '/') + " and delivered through the live mirror-gate to "
				+ KingdomPresentation.Rich(manifest.DestinationCity) + ".\n"
				+ "Site: " + commitment.SiteProof + "; lodged specialist: "
				+ KingdomPresentation.Rich(commitment.SpecialistName) + ".\n"
				+ "Output: " + manifest.Effect + ".\n"
				+ "Commit: the ordinary material debit consumes this exact object as its declared "
				+ KingdomMaterialRules.MaterialName(manifest.CargoMaterial) + " unit. If an engine callback is interrupted, the durable receipt retries only the same identity; ambiguity requires inspection and never substitutes or charges it twice.\n";
		}

		internal static bool FreezeOnWork(GameObject Work, string BuildKey, string Receipt)
		{
			if (!GameObject.Validate(Work) || !Definitions.ContainsKey(BuildKey ?? "")) return true;
			if (string.IsNullOrEmpty(Receipt))
			{
				// Paid, in-progress saves from before this schema are preserved but explicitly legacy.
				Work.SetIntProperty(CommitmentLegacyProperty, 1);
				return Work.GetIntProperty(CommitmentLegacyProperty) == 1;
			}
			if (!KingdomPurposeRules.TryDecodeCommitment(Receipt, out var commitment)
				|| !KingdomPurposeRules.TryDecodeManifest(commitment.Manifest, out var manifest)
				|| manifest.BuildKey != BuildKey) return false;
			Work.SetStringProperty(CommitmentProperty, Receipt);
			Work.SetIntProperty(CommitmentLegacyProperty, 0);
			return Work.GetStringProperty(CommitmentProperty) == Receipt
				&& Work.GetIntProperty(CommitmentLegacyProperty) == 0;
		}

		internal static bool CopyCommit(GameObject Source, GameObject Destination)
		{
			if (!GameObject.Validate(Source) || !GameObject.Validate(Destination)) return false;
			string receipt = Source.GetStringProperty(CommitmentProperty);
			int legacy = Source.GetIntProperty(CommitmentLegacyProperty);
			if (string.IsNullOrEmpty(receipt) && legacy == 0) return true;
			if (!string.IsNullOrEmpty(receipt)) Destination.SetStringProperty(CommitmentProperty, receipt);
			Destination.SetIntProperty(CommitmentLegacyProperty, legacy == 1 ? 1 : 0);
			return Destination.GetStringProperty(CommitmentProperty) == receipt
				&& Destination.GetIntProperty(CommitmentLegacyProperty) == (legacy == 1 ? 1 : 0);
		}

	}
}
