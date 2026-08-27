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
		public const string CargoSchemaProperty = "r_TAF_PurposeCargoSchema";
		public const string CargoKeyProperty = "r_TAF_PurposeCargoKey";
		public const string CargoManifestProperty = "r_TAF_PurposeCargoManifest";
		public const string CargoConsignmentProperty = "r_TAF_PurposeCargoConsignment";
		public const string CargoOriginProperty = "r_TAF_PurposeCargoOrigin";
		public const string CargoDestinationProperty = "r_TAF_PurposeCargoDestination";
		public const string CommitmentProperty = "r_TAF_PurposeCommitment";
		public const string CommitmentLegacyProperty = "r_TAF_PurposeCommitmentLegacy";
		public const int CargoSchema = 1;
		private const string EscrowPrefix = "r_TAF_PurposeCargoEscrow:";

		private static readonly Dictionary<string, KingdomPurposeDefinition> Definitions =
			new Dictionary<string, KingdomPurposeDefinition>(StringComparer.Ordinal);
		private static readonly HashSet<string> InvalidDefinitions =
			new HashSet<string>(StringComparer.Ordinal);

		internal static void ClearDefinitions()
		{
			Definitions.Clear();
			InvalidDefinitions.Clear();
		}

		internal static void RegisterDefinition(string BuildKey, string Purpose, string Site,
			string CargoKey, string CargoName, string CargoMaterial, string CargoWater,
			string CargoCost, string Producers, string Effect)
		{
			Definitions.Remove(BuildKey ?? "");
			InvalidDefinitions.Remove(BuildKey ?? "");
			if (!KingdomPurposeRules.TryCreateDefinition(BuildKey, Purpose, Site, CargoKey,
				CargoName, CargoMaterial, CargoWater, CargoCost, Producers, Effect,
				out KingdomPurposeDefinition definition, out string error))
			{
				if (!string.IsNullOrEmpty(BuildKey)) InvalidDefinitions.Add(BuildKey);
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				return;
			}
			if (definition != null) Definitions[BuildKey] = definition.Copy();
		}

		public static bool TryGetDefinition(string BuildKey,
			out KingdomPurposeDefinition Definition)
		{
			KingdomData.EnsureBuildings();
			if (BuildKey != null && Definitions.TryGetValue(BuildKey, out var found))
			{
				Definition = found.Copy();
				return true;
			}
			Definition = null;
			return false;
		}

		internal static bool RequiresDirectPreview(string BuildKey)
		{
			KingdomData.EnsureBuildings();
			return Definitions.ContainsKey(BuildKey ?? "")
				|| InvalidDefinitions.Contains(BuildKey ?? "");
		}

		internal static string PlanRefusal(string BuildKey)
		{
			return RequiresDirectPreview(BuildKey)
				? "A purposeful megastructure cannot be left as an unattended survey stake. Commission it in person after the exact other-city cargo, live mirror pair, ground, and lodged specialist are all shown in one frozen plan."
				: null;
		}

		/// <summary>Mirror-gate action. Nothing is reserved before the complete prompt is accepted.</summary>
		internal static void Dispatch(r_KingdomMirrorGate Gate, GameObject Actor)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (!KingdomMirrorGate.TryPurposeConnection(Gate, system,
				out KingdomPurposeConnection connection, out string failure))
			{
				Popup.Show(failure ?? "No usable mirror-gate connection answers this arch.");
				return;
			}
			if (!TrySettlementIdentity(system, connection.SourceZone.ZoneID,
				out string originId) || !TrySettlementIdentity(system,
					connection.DestinationZone.ZoneID, out string destinationId))
			{
				Popup.Show("The two cities' immutable settlement identities cannot be reproved. Return each city to its published seat before dispatch.");
				return;
			}
			List<KingdomPurposeDefinition> choices = DefinitionsInOrder();
			if (choices.Count == 0)
			{
				Popup.Show("No purposeful body-work declares a physical consignment.");
				return;
			}
			string[] options = new string[choices.Count];
			for (int i = 0; i < choices.Count; i++)
				options[i] = choices[i].CargoName + " for "
					+ KingdomPurposeRules.PurposeName(choices[i].Kind);
			int picked = Popup.PickOption(Title: "Dispatch through the mirror-gate",
				Intro: "The producing city spends real stock and sends one exact object. The receiving city must keep that object in a dedicated stockpile until its great work is commissioned.",
				Options: options, AllowEscape: true);
			if (picked < 0) return;
			KingdomPurposeDefinition definition = choices[picked];
			HashSet<string> standing = StandingKeys(connection.SourceZone);
			if (!KingdomPurposeRules.ProducersSatisfied(definition.ProducerSpec, standing,
				out string missing))
			{
				Popup.Show("The producing gate's ground lacks {{C|" + missing.Replace('|', '/')
					+ "}}. Raise one work from each named group here, then dispatch again.");
				return;
			}
			GameObject destination = DestinationStockpile(connection.DestinationZone);
			if (!GameObject.Validate(destination))
			{
				Popup.Show("The receiving gate's ground has no dedicated stockpile with an inventory. Visit it, dedicate one, and dispatch again.");
				return;
			}
			if (ExistingCargoOrActiveConsignment(destinationId, definition.BuildKey,
				connection.DestinationZone))
			{
				Popup.Show(KingdomPresentation.Rich(connection.DestinationCity)
					+ " already keeps this purpose's exact cargo, or its consignment remains active. Consume or inspect that receipt before sending another.");
				return;
			}
			KingdomMaterialDebitCost cost = new KingdomMaterialDebitCost(definition.CargoCost);
			KingdomMaterialDebit previewMaterials = KingdomMaterials.ReserveComposite(
				connection.SourceZone, cost);
			if (previewMaterials.Reservation.Outcome != KingdomMaterialDebitOutcome.Reserved)
			{
				Popup.Show(previewMaterials.Reservation.Failure
					?? "The producing city's exact stockpiles cannot cover this consignment.");
				return;
			}
			previewMaterials.Cancel();
			if (KingdomGrowth.CountStoredWater(connection.SourceZone) < definition.CargoWater)
			{
				Popup.Show(KingdomPresentation.Rich(connection.SourceCity) + " needs {{C|"
					+ definition.CargoWater
					+ " drams}} in its stores to seal and dispatch this consignment.");
				return;
			}
			KingdomPurposeManifest manifest = new KingdomPurposeManifest
			{
				BuildKey = definition.BuildKey, Kind = definition.Kind, Site = definition.Site,
				CargoKey = definition.CargoKey, CargoName = definition.CargoName,
				CargoMaterial = definition.CargoMaterial, CargoWater = definition.CargoWater,
				CargoCostClaim = cost.ToClaimString(), OriginSettlementId = originId,
				OriginCity = connection.SourceCity, OriginZoneId = connection.SourceZone.ZoneID,
				SourceGateKey = connection.SourceKey,
				DestinationSettlementId = destinationId,
				DestinationCity = connection.DestinationCity,
				DestinationZoneId = connection.DestinationZone.ZoneID,
				DestinationGateKey = connection.DestinationKey,
				ProducerProof = definition.ProducerSpec, Effect = definition.Effect
			};
			string encoded = KingdomPurposeRules.EncodeManifest(manifest);
			if (encoded == null)
			{
				Popup.Show("The exact consignment receipt could not be frozen. Nothing was spent.");
				return;
			}
			string shownOrigin = KingdomPresentation.Rich(manifest.OriginCity);
			string shownDestination = KingdomPresentation.Rich(manifest.DestinationCity);
			string prompt = "Produce and dispatch exactly {{C|1 " + manifest.CargoName + "}}?\n\n"
				+ "Producer: {{C|" + shownOrigin + "}} at " + manifest.ProducerProof.Replace('|', '/') + ".\n"
				+ "Route: the live mirror-gate from " + shownOrigin + " to "
				+ shownDestination + "; destination stockpile " + destination.ID + ".\n"
				+ "Cost at source: {{C|" + definition.CargoWater + " drams, "
				+ definition.CargoCost.Describe() + "}}.\n"
				+ "Use: required physical input for " + KingdomPurposeRules.PurposeName(definition.Kind)
				+ ". It remains a real stockpile object until that building's atomic material debit consumes it.\n\n"
				+ "If a save or callback interrupts transport, the durable manifest keeps this exact identity. It retries the same object or requires inspection; it never mints or substitutes another.";
			if (Popup.ShowYesNo(prompt) != DialogResult.Yes) return;

			// Re-prove every volatile fact after consent and before publication/debit.
			if (!KingdomMirrorGate.TryPurposeConnection(Gate, system, out connection, out failure)
				|| !TrySettlementIdentity(system, connection.SourceZone.ZoneID, out string liveOrigin)
				|| !TrySettlementIdentity(system, connection.DestinationZone.ZoneID, out string liveDestination)
				|| liveOrigin != originId || liveDestination != destinationId
				|| connection.SourceKey != manifest.SourceGateKey
				|| connection.DestinationKey != manifest.DestinationGateKey
				|| connection.SourceZone.ZoneID != manifest.OriginZoneId
				|| connection.DestinationZone.ZoneID != manifest.DestinationZoneId
				|| !ReferenceEquals(DestinationStockpile(connection.DestinationZone), destination)
				|| !KingdomPurposeRules.ProducersSatisfied(definition.ProducerSpec,
					StandingKeys(connection.SourceZone), out _))
			{
				Popup.Show(failure ?? "The gate, producer, destination, or city identity changed after preview. Nothing was spent; review the dispatch again.");
				return;
			}
			KingdomSurvey survey = KingdomSurvey.Take(connection.SourceZone, system);
			KingdomWaterDebit water = survey.ReserveExactWater(definition.CargoWater);
			KingdomMaterialDebit materials = KingdomMaterials.ReserveComposite(
				connection.SourceZone, cost);
			long now = The.Game.TimeTicks;
			KingdomConstructionJob job = KingdomConstruction.NewJob(system,
				connection.SourceZone, KingdomConstructionRoute.PurposeConsignment,
				Gate.ParentObject.CurrentCell, Gate.ParentObject, definition.BuildKey, encoded,
				definition.CargoWater, cost, now, now);
			job.PhysicalDestinationId = destination.ID;
			job.PhysicalReceipt = encoded;
			KingdomConstructionStartResult funded = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funded == KingdomConstructionStartResult.Refused)
			{
				Popup.Show(fundingFailure ?? "The exact source stores changed. Nothing was spent.");
				return;
			}
			KingdomGovernanceScope.Commit("dispatch purpose consignment");
			if (funded == KingdomConstructionStartResult.Funded)
				RetryConstruction(system, connection.SourceZone, job);
			else MessageQueue.AddPlayerMessage("{{r|The purpose consignment has a measured outstanding receipt. Its exact claim remains queued and will not be charged twice.}}");
		}

	}
}
