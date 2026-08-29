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
			KingdomConstructionJob consignment = null;
			KingdomPurposeManifest manifest = null;
			if (!definition.PortfolioOnly && !FindDeliveredCargo(Z, definition, settlementId,
				connection.SourceKey, connection.DestinationKey, out Cargo, out consignment,
				out manifest, out Failure)) return false;
			if (!TryQuotePortfolioCargo(System, Z, definition, settlementId,
				out KingdomPurposePairReceipt pair, out GameObject reciprocal,
				out Failure)) return false;
			if (!TrySiteProof(System, Z, definition, out string siteProof,
				out GameObject specialist, out Failure))
			{
				Cargo = null;
				return false;
			}
			KingdomPurposeCommitment commitment = new KingdomPurposeCommitment
			{
				Manifest = manifest == null ? null : KingdomPurposeRules.EncodeManifest(manifest),
				ConsignmentId = consignment?.Id, CargoItemId = Cargo?.IDIfAssigned,
				SiteProof = siteProof, SpecialistId = specialist.IDIfAssigned,
				SpecialistName = !string.IsNullOrEmpty(
					specialist.GetStringProperty("KingdomName"))
					? specialist.GetStringProperty("KingdomName")
					: (specialist.BaseDisplayNameStripped ?? "the specialist"),
				PortfolioPairId = pair?.PairId, PortfolioEpoch = pair?.Epoch ?? 0L,
				PortfolioOperationId = pair?.Operation?.OperationId,
				ReciprocalCargoItemId = reciprocal?.IDIfAssigned,
				ReciprocalCargoReceipt = pair?.Operation?.OutputCargoReceipt,
				InitialBuildKey = pair == null && definition.PortfolioOnly
					? definition.BuildKey : null
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
				out KingdomPurposeCommitment commitment) || !CommitmentMatchesBuild(commitment, BuildKey))
				return Fail("The frozen purpose commitment is absent or malformed.", out Failure);
			if (string.IsNullOrEmpty(commitment.CargoItemId)) return true;
			if (!KingdomPurposeRules.TryDecodeManifest(commitment.Manifest,
				out KingdomPurposeManifest manifest) || manifest.BuildKey != BuildKey)
				return Fail("The frozen legacy purpose commitment is malformed.", out Failure);
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
				&& CommitmentMatchesBuild(commitment, Job.TargetKey);
		}

		internal static bool RequiresExactFunding(KingdomConstructionJob Job)
		{
			if (Job == null || Job.Route != KingdomConstructionRoute.PlotCommission) return false;
			KingdomData.EnsureBuildings();
			return Definitions.ContainsKey(Job.TargetKey ?? "")
				|| InvalidDefinitions.Contains(Job.TargetKey ?? "")
				|| !string.IsNullOrEmpty(Job.PhysicalReceipt);
		}

		internal static string AppendPreview(string Existing, string PurposeReceipt)
		{
			if (string.IsNullOrEmpty(PurposeReceipt)) return Existing;
			if (!KingdomPurposeRules.TryDecodeCommitment(PurposeReceipt,
				out KingdomPurposeCommitment commitment))
				return (Existing ?? "") + "\nPURPOSE RECEIPT INVALID: nothing may be spent.";
			KingdomPurposeManifest manifest = null;
			KingdomPurposeRules.TryDecodeManifest(commitment.Manifest, out manifest);
			KingdomPurposeCargoReceipt reciprocal = null;
			KingdomPurposePortfolioRules.TryDecodeCargo(
				commitment.ReciprocalCargoReceipt, out reciprocal);
			KingdomPurposeKind kind = manifest?.Kind ?? reciprocal?.DestinationKind
				?? KingdomPurposeKind.None;
			string exact = "";
			if (manifest != null)
				exact += "Exact cross-city input: 1 " + manifest.CargoName + " (object "
					+ commitment.CargoItemId + "), produced by "
					+ KingdomPresentation.Rich(manifest.OriginCity) + " at "
					+ manifest.ProducerProof.Replace('|', '/')
					+ " and delivered through the live mirror-gate to "
					+ KingdomPresentation.Rich(manifest.DestinationCity) + ".\n";
			if (reciprocal != null)
				exact += "Exact reciprocal input: 1 " + reciprocal.CargoKey + " (object "
					+ commitment.ReciprocalCargoItemId + "), pair epoch "
					+ commitment.PortfolioEpoch + ", delivered into its frozen purpose store.\n";
			return (Existing ?? "") + "\nPurpose commitment: "
				+ KingdomPurposeRules.PurposeName(kind) + ".\n" + exact
				+ "Site: " + commitment.SiteProof + "; lodged specialist: "
				+ KingdomPresentation.Rich(commitment.SpecialistName) + ".\n"
				+ (manifest == null ? "" : "Output: " + manifest.Effect + ".\n")
				+ "Commit: the ordinary material debit consumes every named exact object as its declared typed unit. If an engine callback is interrupted, the durable receipt retries only those identities; ambiguity requires inspection and never substitutes or charges twice.\n";
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
				|| !CommitmentMatchesBuild(commitment, BuildKey)) return false;
			Work.SetStringProperty(CommitmentProperty, Receipt);
			Work.SetIntProperty(CommitmentLegacyProperty, 0);
			return Work.GetStringProperty(CommitmentProperty) == Receipt
				&& Work.GetIntProperty(CommitmentLegacyProperty) == 0;
		}

		internal static bool FoundingHeartPurposeIsLegacy(string BuildKey)
		{
			return Definitions.ContainsKey(BuildKey ?? "");
		}

		internal static bool FreezeFoundingHeartOnWork(GameObject Work, bool Legacy)
		{
			if (!GameObject.Validate(Work)
				|| Work.HasStringProperty(CommitmentProperty)
				|| Work.HasIntProperty(CommitmentProperty)
				|| Work.HasStringProperty(CommitmentLegacyProperty)
				|| Work.HasIntProperty(CommitmentLegacyProperty)) return false;
			if (!Legacy) return true;
			Work.SetIntProperty(CommitmentLegacyProperty, 1);
			return Work.HasIntProperty(CommitmentLegacyProperty)
				&& !Work.HasStringProperty(CommitmentLegacyProperty)
				&& Work.GetIntProperty(CommitmentLegacyProperty) == 1;
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
