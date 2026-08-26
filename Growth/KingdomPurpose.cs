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
	/// <summary>
	/// Physical purpose authority: declarative catalogue definitions, exact other-city production,
	/// mirror-gate transport, distinct site proof, and frozen building commitment.
	/// </summary>
	public static class KingdomPurpose
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

		internal static void RetryConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.PurposeConsignment) return;
			KingdomConstructionJob live = Job;
			if (!KingdomPurposeRules.TryDecodeManifest(live.Payload,
				out KingdomPurposeManifest manifest) || live.PhysicalReceipt != live.Payload
				|| manifest.OriginZoneId != Z.ZoneID || live.ZoneId != Z.ZoneID)
			{
				KingdomConstruction.Quarantine(ref live,
					"The purpose consignment manifest is absent, changed, or bound to another source zone.");
				return;
			}
			if (!ExactEndpoints(System, Z, live, manifest,
				out GameObject source, out GameObject destination,
				out KingdomPurposeConnection connection, out bool requiresInspection,
				out string endpointFailure))
			{
				if (requiresInspection)
				{
					KingdomConstruction.Quarantine(ref live, endpointFailure
						?? "The frozen purpose route is physically ambiguous.");
					return;
				}
				// Missing, moved, stale, or dark ground is an actionable stall: restoring the same
				// endpoint proves the frozen route again without choosing a substitute.
				KingdomLog.Log("purpose consignment waits: " + endpointFailure);
				return;
			}
			if (live.Phase == KingdomConstructionPhase.Funded
				|| live.Phase == KingdomConstructionPhase.Outstanding)
			{
				if (!KingdomConstruction.BeginProjection(ref live, out _)) return;
			}
			if (live.Phase != KingdomConstructionPhase.ProjectionPending) return;
			if (!TryEscrowCargo(live, manifest, out GameObject cargo))
			{
				if (!string.IsNullOrEmpty(live.OutputId))
				{
					KingdomConstruction.Quarantine(ref live,
						"The published purpose cargo identity lost its exact escrow root.");
					return;
				}
				cargo = CreateCargo(live, manifest);
				if (!GameObject.Validate(cargo) || !RootCargo(live, cargo))
				{
					KingdomConstruction.FinishProjection(ref live, false, false,
						"The exact purpose cargo could not be created and rooted for retry.");
					return;
				}
			}
			if (string.IsNullOrEmpty(live.OutputId))
			{
				if (!KingdomConstruction.UpdateOutput(ref live, cargo.ID)) return;
			}
			if (!ExactCargo(cargo, live, manifest))
			{
				KingdomConstruction.Quarantine(ref live,
					"The rooted purpose cargo changed before physical transport.");
				return;
			}
			Inventory sourceInventory = source.RequirePart<Inventory>();
			if (live.PhysicalPhase == KingdomPhysicalPhase.None)
			{
				if (!KingdomConstruction.UpdatePhysical(ref live,
					KingdomPhysicalPhase.CargoOutputPending, 0, 1, 0, cargo.ID,
					destination.ID, live.Payload)) return;
			}
			if (live.PhysicalPhase == KingdomPhysicalPhase.CargoOutputPending)
			{
				if (!ExactOwned(cargo, source))
				{
					if (!ExactLoose(cargo, Z, connection.DestinationZone))
					{
						KingdomConstruction.Quarantine(ref live,
							"Purpose cargo output has an ambiguous owner before source settlement.");
						return;
					}
					GameObject accepted;
					try { accepted = sourceInventory.AddObject(cargo, null,
						Silent: true, NoStack: true); }
					catch (Exception ex)
					{
						ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
						KingdomConstruction.FinishProjection(ref live, false, false,
							"Source-gate cargo placement threw before exact settlement: " + ex.Message);
						return;
					}
					ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
					KingdomSurvey.ObserveAddResultInActive(Z, cargo, accepted);
					if (!ReferenceEquals(accepted, cargo) || !ExactOwned(cargo, source))
					{
						KingdomConstruction.Quarantine(ref live,
							"Source-gate cargo placement replaced or moved the exact object.");
						return;
					}
				}
				if (!KingdomConstruction.UpdatePhysical(ref live,
					KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0, cargo.ID,
					destination.ID, live.Payload)) return;
			}
			if (live.PhysicalPhase == KingdomPhysicalPhase.CargoOutputSettled)
			{
				if (!ExactOwned(cargo, source))
				{
					KingdomConstruction.Quarantine(ref live,
						"Settled purpose cargo is no longer in its exact source gate.");
					return;
				}
				if (!KingdomConstruction.UpdatePhysical(ref live,
					KingdomPhysicalPhase.CargoTransferPending, 0, 1, 0, cargo.ID,
					destination.ID, live.Payload)) return;
			}
			if (live.PhysicalPhase == KingdomPhysicalPhase.CargoTransferPending)
			{
				if (ExactOwned(cargo, destination))
				{
					SettleDelivery(System, ref live, manifest, cargo, destination);
					return;
				}
				if (ExactOwned(cargo, source))
				{
					bool removed;
					try { removed = sourceInventory.RemoveObjectFromInventory(cargo, null,
						Silent: true, NoStack: true); }
					catch (Exception ex)
					{
						ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
						if (ExactOwned(cargo, source))
						{
							KingdomConstruction.UpdatePhysical(ref live,
								KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0,
								cargo.ID, destination.ID, live.Payload, ex.Message);
							return;
						}
						if (!ExactLoose(cargo, Z, connection.DestinationZone))
						{
							KingdomConstruction.Quarantine(ref live,
								"Source removal threw after cargo ownership became ambiguous.");
							return;
						}
						removed = true;
					}
					ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
					if (!removed && ExactOwned(cargo, source))
					{
						KingdomConstruction.UpdatePhysical(ref live,
							KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0, cargo.ID,
							destination.ID, live.Payload,
							"Source gate refused exact cargo removal before changing ownership.");
						return;
					}
				}
				if (!ExactLoose(cargo, Z, connection.DestinationZone))
				{
					if (ExactOwned(cargo, destination))
					{
						SettleDelivery(System, ref live, manifest, cargo, destination);
						return;
					}
					KingdomConstruction.Quarantine(ref live,
						"Purpose cargo left its source without one exact rooted loose object.");
					return;
				}
				GameObject placed = null;
				try { placed = destination.Inventory.AddObject(cargo, null,
					Silent: true, NoStack: true); }
				catch (Exception ex)
				{
					ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
					if (ExactOwned(cargo, destination))
					{
						SettleDelivery(System, ref live, manifest, cargo, destination);
						return;
					}
					RestoreSourceOrQuarantine(ref live, cargo, source, destination,
						"Destination stockpile placement threw: " + ex.Message);
					return;
				}
				ObserveCargoOwners(Z, source, connection.DestinationZone, destination);
				KingdomSurvey.ObserveAddResultInActive(connection.DestinationZone, cargo, placed);
				if (!ReferenceEquals(placed, cargo) || !ExactOwned(cargo, destination))
				{
					RestoreSourceOrQuarantine(ref live, cargo, source, destination,
						"Destination stockpile replaced or rejected the exact cargo object.");
					return;
				}
				SettleDelivery(System, ref live, manifest, cargo, destination);
				return;
			}
			if (live.PhysicalPhase == KingdomPhysicalPhase.CargoDelivered)
				SettleDelivery(System, ref live, manifest, cargo, destination);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (Job == null || Job.Route != KingdomConstructionRoute.PurposeConsignment) return;
			if (Job.Phase == KingdomConstructionPhase.Complete)
			{
				KingdomConstructionJob live = Job;
				if (!KingdomPurposeRules.TryDecodeManifest(live.Payload,
					out KingdomPurposeManifest manifest)
					|| !ExactDeliveredCargo(live, manifest, out _, out _))
				{
					KingdomConstruction.Quarantine(ref live,
						"The terminal purpose consignment no longer has one exact delivered cargo object.");
					return;
				}
				EnsureDeliveryOutbox(System, ref live, manifest);
				return;
			}
			RetryConstruction(System, Z, Job);
		}

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

		private static List<KingdomPurposeDefinition> DefinitionsInOrder()
		{
			List<KingdomPurposeDefinition> values = new List<KingdomPurposeDefinition>();
			foreach (var pair in Definitions) values.Add(pair.Value.Copy());
			values.Sort((a, b) => string.CompareOrdinal(a.BuildKey, b.BuildKey));
			return values;
		}

		private static HashSet<string> StandingKeys(Zone Z)
		{
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			foreach (GameObject work in Z?.GetObjects() ?? new List<GameObject>())
			{
				if (!GameObject.Validate(work) || work.GetIntProperty("KingdomBuilt") != 1) continue;
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
					&& stock.Stockpiles[i].Inventory != null) choices.Add(stock.Stockpiles[i]);
			choices.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));
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
				|| Cargo.Blueprint != KingdomMaterials.BlueprintFor(Manifest.CargoMaterial)
				|| Cargo.GetIntProperty(CargoSchemaProperty) != CargoSchema
				|| Cargo.GetStringProperty(CargoKeyProperty) != Manifest.CargoKey
				|| Cargo.GetStringProperty(CargoManifestProperty) != encoded
				|| (!Job.Compacted && (Job.Payload != encoded
					|| Job.PhysicalReceipt != encoded))
				|| Cargo.GetStringProperty(CargoConsignmentProperty) != Job.Id
				|| Cargo.GetStringProperty(CargoOriginProperty) != Manifest.OriginSettlementId
				|| Cargo.GetStringProperty(CargoDestinationProperty) != Manifest.DestinationSettlementId
				|| Cargo.GetStringProperty(KingdomConstruction.ReceiptProperty) != Job.Id
				|| !KingdomMaterials.TryMaterialOf(Cargo, out KingdomMaterial material)
				|| material != Manifest.CargoMaterial) return false;
			return string.IsNullOrEmpty(Job.OutputId) || Cargo.ID == Job.OutputId;
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
			GameObject found = GameObject.FindByID(Item.ID);
			return !GameObject.Validate(found) || ReferenceEquals(found, Item);
		}

		private static KingdomPhysicalLookupState FindExactKnown(Zone Zone, string Id,
			out GameObject Exact)
		{
			Exact = null;
			if (Zone == null || string.IsNullOrEmpty(Id)) return KingdomPhysicalLookupState.Absent;
			if (KingdomSurvey.ActiveFor(Zone) != null)
				return KingdomConstruction.FindExactId(Zone, Id, out Exact);
			GameObject candidate = GameObject.FindByID(Id);
			if (!GameObject.Validate(candidate)) return KingdomPhysicalLookupState.Absent;
			if (candidate.ID != Id || candidate.CurrentZone != Zone)
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
					KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0, Cargo.ID,
					Destination.ID, Job.Payload, Failure);
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
							KingdomPhysicalPhase.CargoOutputSettled, 0, 1, 0, Cargo.ID,
							Destination.ID, Job.Payload, Failure);
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
					KingdomPhysicalPhase.CargoDelivered, 0, 1, 0, Cargo.ID,
					Destination.ID, Job.Payload)) return;
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
			candidates.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject item = candidates[i];
				string receipt = item.GetStringProperty(CargoConsignmentProperty);
				string encoded = item.GetStringProperty(CargoManifestProperty);
				if (!KingdomConstruction.TryFind(receipt, out KingdomConstructionJob job)
					|| !KingdomPurposeRules.TryDecodeManifest(encoded,
						out KingdomPurposeManifest manifest)
					|| job.Route != KingdomConstructionRoute.PurposeConsignment
					|| !SettledConsignment(job, encoded, item.ID)
					|| job.OutputId != item.ID || manifest.BuildKey != Definition.BuildKey
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

		private static bool TrySiteProof(KingdomSystem System, Zone Z,
			KingdomPurposeDefinition Definition, out string Proof, out GameObject Specialist,
			out string Failure)
		{
			Proof = null;
			Specialist = null;
			Failure = null;
			HashSet<string> standing = StandingKeys(Z);
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			if (Definition.Site == KingdomPurposeSite.LivingSurgery)
			{
				if (!LivingGround(System.FoundingTerrainBlueprint,
					System.FoundingRegionName, System.Style))
					return Fail("The chimeric theatre wants butcherable living-biome ground: a watervine, marsh, flower, banana, jungle, or fungal founding site. This city's founding ground cannot supply it.", out Failure);
				string provider = standing.Contains("graftinghall") ? "graftinghall"
					: standing.Contains("vathouse") ? "vathouse" : null;
				if (provider == null)
					return Fail("The chimeric theatre wants real damp and offal on this ground. Raise a vat-house or grafting hall here and ask again.", out Failure);
				Specialist = LodgedSpecialist(Z, survey.Settlers, false);
				if (!GameObject.Validate(Specialist))
					return Fail("The chimeric theatre wants a lodged savant with Intelligence 18 or better. House one on this ground before committing it.", out Failure);
				Proof = "living-biome=" + Safe(System.FoundingRegionName,
					System.FoundingTerrainBlueprint) + ";damp-offal=" + provider
					+ ";savant=" + Specialist.ID;
				return true;
			}
			if (Definition.Site == KingdomPurposeSite.RuinEnrollment)
			{
				if (!KingdomRules.IsRuinSite(System.FoundingTerrainBlueprint)
					&& (System.FoundingRegionName ?? "").IndexOf("Ruin",
						StringComparison.OrdinalIgnoreCase) < 0)
					return Fail("The becoming annexe wants ruin-ground or ruin-adjacent founding evidence. Found or seat this purpose on a city whose founding terrain is a ruin.", out Failure);
				if (!standing.Contains("smelter") || !standing.Contains("chargingpost"))
					return Fail("The becoming annexe wants a real smelter and charging post on this ground. Raise both so metal and arclight are physical facts here.", out Failure);
				if (!CreedReach(System, "Mechanimists") && !CreedReach(System, "Templar"))
					return Fail("The becoming annexe wants Mechanimist or Templar reach: people here must presently or historically hold one of those creeds.", out Failure);
				Specialist = LodgedSpecialist(Z, survey.Settlers, true);
				if (!GameObject.Validate(Specialist))
					return Fail("The becoming annexe wants a lodged psyberneticist: an Intelligence-18 tinker, technician, or Mechanimist resident housed on this ground.", out Failure);
				Proof = "ruin=" + Safe(System.FoundingRegionName,
					System.FoundingTerrainBlueprint) + ";arclight=smelter+chargingpost;creed="
					+ (CreedReach(System, "Mechanimists") ? "Mechanimists" : "Templar")
					+ ";psyberneticist=" + Specialist.ID;
				return true;
			}
			return Fail("The purpose names no implemented physical site predicate.", out Failure);
		}

		private static GameObject LodgedSpecialist(Zone Z, IList<GameObject> Settlers,
			bool Psyberneticist)
		{
			List<GameObject> candidates = new List<GameObject>();
			for (int i = 0; Settlers != null && i < Settlers.Count; i++)
			{
				GameObject resident = Settlers[i];
				if (!IsLodgedSpecialist(Z, resident, Psyberneticist)) continue;
				candidates.Add(resident);
			}
			candidates.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));
			return candidates.Count == 0 ? null : candidates[0];
		}

		/// <summary>The live, revocable labour fact shared by purpose siting and the annexe's
		/// register. It is deliberately about one concrete resident on this ground: a name retained
		/// in the old roster is not a lodged specialist, and moving out or losing the required craft
		/// closes the register without erasing its rolls.</summary>
		internal static bool IsLodgedSpecialist(Zone Z, GameObject Resident,
			bool Psyberneticist)
		{
			return Z != null && GameObject.Validate(Resident)
				&& Resident.CurrentZone == Z
				&& Resident.GetIntProperty("KingdomCitizen") == 1
				&& KingdomCrews.CapabilityOf(Resident).Intelligence >= 18
				&& !string.IsNullOrEmpty(KingdomLodging.HomeDesignKeyOf(Z, Resident))
				&& (!Psyberneticist || PsyberneticistTruth(Resident));
		}

		private static bool PsyberneticistTruth(GameObject Resident)
		{
			string words = (Resident.Blueprint ?? "") + " "
				+ (Resident.ShortDisplayName ?? "") + " " + (Resident.GetCulture() ?? "");
			return Resident.HasSkill("Tinkering") || Resident.HasSkill("Tinkering_Tinker1")
				|| Resident.HasSkill("Tinkering_Tinker2")
				|| words.IndexOf("tinker", StringComparison.OrdinalIgnoreCase) >= 0
				|| words.IndexOf("technician", StringComparison.OrdinalIgnoreCase) >= 0
				|| words.IndexOf("mechanimist", StringComparison.OrdinalIgnoreCase) >= 0
				|| words.IndexOf("psyber", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool LivingGround(string Terrain, string Region, string Style)
		{
			if (Style == "verdant" || Style == "fungal") return true;
			string ground = (Terrain ?? "") + " " + (Region ?? "");
			string[] living = new string[7]
				{ "Watervine", "Saltmarsh", "Flowerfield", "BananaGrove", "Jungle", "Fungal", "Marsh" };
			for (int i = 0; i < living.Length; i++)
				if (ground.IndexOf(living[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
			return false;
		}

		private static bool CreedReach(KingdomSystem System, string Creed)
		{
			return System != null && ((System.CreedCounts != null
				&& System.CreedCounts.TryGetValue(Creed, out int present) && present > 0)
				|| (System.CreedPastCounts != null
					&& System.CreedPastCounts.TryGetValue(Creed, out int past) && past > 0));
		}

		private static bool TrySettlementIdentity(KingdomSystem System, string ZoneId,
			out string SettlementId)
		{
			SettlementId = null;
			if (System == null || string.IsNullOrEmpty(ZoneId)
				|| !System.TryExactSettlementIds(true, out List<string> ids, out _)) return false;
			if (System.ClaimedZones != null && System.ClaimedZones.Contains(ZoneId))
				SettlementId = System.City?.SettlementId;
			else if (System.Away?.ClaimedZones != null && System.Away.ClaimedZones.Contains(ZoneId))
				SettlementId = System.Away.City?.SettlementId;
			return !string.IsNullOrEmpty(SettlementId) && ids.Contains(SettlementId);
		}

		private static string Safe(string First, string Second)
		{
			string value = !string.IsNullOrEmpty(First) ? First : Second;
			return string.IsNullOrEmpty(value) ? "unrecorded" : value.Replace(';', ',');
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
