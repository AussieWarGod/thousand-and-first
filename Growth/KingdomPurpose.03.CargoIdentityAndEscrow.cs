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
		private static List<KingdomPurposeDefinition> DefinitionsInOrder()
		{
			List<KingdomPurposeDefinition> values = new List<KingdomPurposeDefinition>();
			foreach (var pair in Definitions)
				if (!pair.Value.PortfolioOnly) values.Add(pair.Value.Copy());
			values.Sort((a, b) => string.CompareOrdinal(a.BuildKey, b.BuildKey));
			return values;
		}

		private static HashSet<string> StandingKeys(Zone Z)
		{
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			foreach (GameObject work in Z?.GetObjects() ?? new List<GameObject>())
			{
				if (!KingdomUpgrade.IsFunctionallyBuilt(work)) continue;
				string key = KingdomUpgrade.DesignKeyOf(work);
				if (!string.IsNullOrEmpty(key)) keys.Add(key);
			}
			return keys;
		}

		private static GameObject DestinationStockpile(Zone Z)
		{
			List<GameObject> choices = new List<GameObject>();
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			for (int i = 0; i < stock.Stockpiles.Count; i++)
				if (GameObject.Validate(stock.Stockpiles[i])
					&& stock.Stockpiles[i].Inventory != null
					&& !string.IsNullOrEmpty(stock.Stockpiles[i].IDIfAssigned))
					choices.Add(stock.Stockpiles[i]);
			choices.Sort((a, b) => string.CompareOrdinal(a.IDIfAssigned, b.IDIfAssigned));
			return choices.Count == 0 ? null : choices[0];
		}

		private static bool ExistingCargoOrActiveConsignment(string DestinationId,
			string BuildKey, Zone DestinationZone)
		{
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(DestinationZone);
			for (int i = 0; i < stock.Stockpiles.Count; i++)
			{
				Inventory inventory = stock.Stockpiles[i].Inventory;
				for (int j = 0; inventory != null && j < inventory.Objects.Count; j++)
				{
					GameObject cargo = inventory.Objects[j];
					if (cargo.GetIntProperty(CargoSchemaProperty) == CargoSchema
						&& KingdomPurposeRules.TryDecodeManifest(
							cargo.GetStringProperty(CargoManifestProperty), out var held)
						&& held.BuildKey == BuildKey
						&& held.DestinationSettlementId == DestinationId) return true;
				}
			}
			if (!KingdomConstruction.TryRead(out List<KingdomConstructionJob> jobs, out _)) return true;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomConstructionJob job = jobs[i];
				if (job.Route != KingdomConstructionRoute.PurposeConsignment
					|| job.TargetKey != BuildKey || !KingdomPurposeRules.TryDecodeManifest(
						job.Payload, out KingdomPurposeManifest manifest)
					|| manifest.DestinationSettlementId != DestinationId) continue;
				if (!KingdomConstructionRules.IsTerminal(job.Phase)) return true;
			}
			return false;
		}

		private static bool ExactEndpoints(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job, KingdomPurposeManifest Manifest, out GameObject Source,
			out GameObject Destination, out KingdomPurposeConnection Connection,
			out bool RequiresInspection, out string Failure)
		{
			Source = null;
			Destination = null;
			Connection = null;
			RequiresInspection = false;
			Failure = null;
			KingdomPhysicalLookupState sourceState = FindExactKnown(Z,
				Job.SubjectId, out Source);
			if (sourceState != KingdomPhysicalLookupState.Exact)
			{
				RequiresInspection = sourceState == KingdomPhysicalLookupState.Ambiguous;
				return Fail(RequiresInspection
					? "More than one loaded object bears the frozen source gate identity; inspect the consignment."
					: "The exact source mirror-gate is absent. Restore that same arch to its frozen cell before retrying.", out Failure);
			}
			if (Source.GetPart<r_KingdomMirrorGate>() == null)
			{
				RequiresInspection = true;
				return Fail("The object bearing the frozen source identity is no longer a mirror-gate; inspect the consignment.", out Failure);
			}
			if (Source.CurrentCell != Z.GetCell(Job.X, Job.Y))
				return Fail("The exact source mirror-gate moved. Return that same arch to its frozen cell before retrying.", out Failure);
			if (!KingdomMirrorGate.TryPurposeConnection(Source.GetPart<r_KingdomMirrorGate>(),
				System, out Connection, out RequiresInspection, out Failure)) return false;
			if (Connection.SourceKey != Manifest.SourceGateKey
				|| Connection.DestinationKey != Manifest.DestinationGateKey
				|| Connection.SourceZone.ZoneID != Manifest.OriginZoneId
				|| Connection.DestinationZone.ZoneID != Manifest.DestinationZoneId
				|| !TrySettlementIdentity(System, Manifest.OriginZoneId, out string origin)
				|| !TrySettlementIdentity(System, Manifest.DestinationZoneId, out string destinationId)
				|| origin != Manifest.OriginSettlementId
				|| destinationId != Manifest.DestinationSettlementId)
			{
				RequiresInspection = true;
				return Fail("The mirror route or immutable city provenance changed after dispatch; inspect rather than rerouting the cargo.", out Failure);
			}
			KingdomPhysicalLookupState destinationState = FindExactKnown(
				Connection.DestinationZone, Job.PhysicalDestinationId, out Destination);
			if (destinationState != KingdomPhysicalLookupState.Exact)
			{
				RequiresInspection = destinationState == KingdomPhysicalLookupState.Ambiguous;
				return Fail(RequiresInspection
					? "More than one loaded object bears the frozen destination stockpile identity; inspect the consignment."
					: "The exact destination stockpile is absent. Restore that same stockpile before retrying.", out Failure);
			}
			if (Destination.Inventory == null
				|| Destination.GetIntProperty(KingdomMaterials.StockpileProperty) != 1)
			{
				RequiresInspection = true;
				return Fail("The object bearing the frozen destination identity is no longer a dedicated stockpile; inspect the consignment.", out Failure);
			}
			return true;
		}

		private static GameObject CreateCargo(KingdomConstructionJob Job,
			KingdomPurposeManifest Manifest)
		{
			string blueprint = KingdomMaterials.BlueprintFor(Manifest.CargoMaterial);
			GameObject cargo;
			try { cargo = GameObject.Create(blueprint); }
			catch { return null; }
			if (!GameObject.Validate(cargo) || cargo.Count != 1) return null;
			try { cargo.RemovePart("Stacker"); } catch { return null; }
			cargo.DisplayName = Manifest.CargoName;
			cargo.SetIntProperty(CargoSchemaProperty, CargoSchema);
			cargo.SetStringProperty(CargoKeyProperty, Manifest.CargoKey);
			cargo.SetStringProperty(CargoManifestProperty, Job.Payload);
			cargo.SetStringProperty(CargoConsignmentProperty, Job.Id);
			cargo.SetStringProperty(CargoOriginProperty, Manifest.OriginSettlementId);
			cargo.SetStringProperty(CargoDestinationProperty, Manifest.DestinationSettlementId);
			KingdomConstruction.Bind(cargo, Job);
			return ExactCargo(cargo, Job, Manifest) ? cargo : null;
		}

		private static bool ExactCargo(GameObject Cargo, KingdomConstructionJob Job,
			KingdomPurposeManifest Manifest)
		{
			string encoded = KingdomPurposeRules.EncodeManifest(Manifest);
			if (!GameObject.Validate(Cargo) || Job == null || Manifest == null
				|| encoded == null
				|| Cargo.Count != 1 || Cargo.HasPart("Stacker")
				|| CargoFieldPresent(Cargo, PortfolioCargoSchemaProperty)
				|| CargoFieldPresent(Cargo, PortfolioCargoReceiptProperty)
				|| CargoFieldPresent(Cargo, PortfolioCargoKeyProperty)
				|| CargoFieldPresent(Cargo, PortfolioCargoFoodProperty)
				|| CargoFieldPresent(Cargo, PortfolioLandedFoodProperty)
				|| CargoFieldPresent(Cargo, PortfolioLandedReceiptProperty)
				|| CargoFieldPresent(Cargo, PortfolioLandedCountProperty)
				|| CargoFieldPresent(Cargo, PortfolioLandedAttemptProperty)
				|| CargoFieldPresent(Cargo, PortfolioLandedFaultProperty)
				|| Cargo.Blueprint != KingdomMaterials.BlueprintFor(Manifest.CargoMaterial)
				|| !KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(CargoSchemaProperty),
					Cargo.HasStringProperty(CargoSchemaProperty), true)
				|| Cargo.GetIntProperty(CargoSchemaProperty) != CargoSchema
				|| !KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(CargoKeyProperty),
					Cargo.HasStringProperty(CargoKeyProperty), false)
				|| Cargo.GetStringProperty(CargoKeyProperty) != Manifest.CargoKey
				|| !KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(CargoManifestProperty),
					Cargo.HasStringProperty(CargoManifestProperty), false)
				|| Cargo.GetStringProperty(CargoManifestProperty) != encoded
				|| (!Job.Compacted && (Job.Payload != encoded
					|| Job.PhysicalReceipt != encoded))
				|| !KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(CargoConsignmentProperty),
					Cargo.HasStringProperty(CargoConsignmentProperty), false)
				|| Cargo.GetStringProperty(CargoConsignmentProperty) != Job.Id
				|| !KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(CargoOriginProperty),
					Cargo.HasStringProperty(CargoOriginProperty), false)
				|| Cargo.GetStringProperty(CargoOriginProperty) != Manifest.OriginSettlementId
				|| !KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(CargoDestinationProperty),
					Cargo.HasStringProperty(CargoDestinationProperty), false)
				|| Cargo.GetStringProperty(CargoDestinationProperty) != Manifest.DestinationSettlementId
				|| !KingdomPurposePortfolioRules.PurposeCargoFieldTypeIsExact(
					Cargo.HasIntProperty(KingdomConstruction.ReceiptProperty),
					Cargo.HasStringProperty(KingdomConstruction.ReceiptProperty), false)
				|| Cargo.GetStringProperty(KingdomConstruction.ReceiptProperty) != Job.Id
				|| !KingdomMaterials.TryMaterialOf(Cargo, out KingdomMaterial material)
				|| material != Manifest.CargoMaterial) return false;
			return string.IsNullOrEmpty(Job.OutputId) || Cargo.IDIfAssigned == Job.OutputId;
		}

		private static string EscrowKey(KingdomConstructionJob Job)
		{
			if (Job == null || string.IsNullOrEmpty(Job.Id)) return null;
			byte[] digest;
			using (SHA256 hash = SHA256.Create())
				digest = hash.ComputeHash(Encoding.UTF8.GetBytes(Job.Id + "\npurpose-cargo"));
			StringBuilder key = new StringBuilder(EscrowPrefix, 96);
			for (int i = 0; i < digest.Length; i++) key.Append(digest[i].ToString("x2",
				CultureInfo.InvariantCulture));
			return key.ToString();
		}

		private static bool RootCargo(KingdomConstructionJob Job, GameObject Cargo)
		{
			string key = EscrowKey(Job);
			if (The.Game == null || key == null || !GameObject.Validate(Cargo)) return false;
			if (The.Game.ObjectGameState.TryGetValue(key, out object collision)
				&& !ReferenceEquals(collision, Cargo)) return false;
			The.Game.SetObjectGameState(key, Cargo);
			return The.Game.ObjectGameState.TryGetValue(key, out object rooted)
				&& ReferenceEquals(rooted, Cargo);
		}

		private static bool TryEscrowCargo(KingdomConstructionJob Job,
			KingdomPurposeManifest Manifest, out GameObject Cargo)
		{
			Cargo = null;
			string key = EscrowKey(Job);
			if (The.Game == null || key == null
				|| !The.Game.ObjectGameState.TryGetValue(key, out object rooted)) return false;
			Cargo = rooted as GameObject;
			return ExactCargo(Cargo, Job, Manifest);
		}

		private static bool RetireCargoRoot(KingdomConstructionJob Job, GameObject Cargo)
		{
			string key = EscrowKey(Job);
			if (The.Game == null || key == null) return false;
			if (!The.Game.ObjectGameState.TryGetValue(key, out object rooted)) return true;
			if (!ReferenceEquals(rooted, Cargo)) return false;
			The.Game.ObjectGameState.Remove(key);
			return !The.Game.ObjectGameState.ContainsKey(key);
		}

		private static bool ExactOwned(GameObject Item, GameObject Owner)
		{
			if (!GameObject.Validate(Item) || !GameObject.Validate(Owner)
				|| Owner.Inventory == null || Item.InInventory != Owner || Item.CurrentCell != null)
				return false;
			int count = 0;
			for (int i = 0; i < Owner.Inventory.Objects.Count; i++)
				if (ReferenceEquals(Owner.Inventory.Objects[i], Item)) count++;
			return count == 1;
		}

		private static bool ExactLoose(GameObject Item, Zone Source, Zone Destination)
		{
			if (!GameObject.Validate(Item) || Item.Physics == null
				|| Item.InInventory != null || Item.CurrentCell != null) return false;
			// Qud IDs are global authority. A rooted loose object may remain in FindByID's
			// one-entry cache, but no different live object may answer its exact ID.
			GameObject found = GameObject.FindByID(Item.IDIfAssigned);
			return !GameObject.Validate(found) || ReferenceEquals(found, Item);
		}

	}
}
