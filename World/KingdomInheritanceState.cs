using System;
using System.Collections.Generic;
using System.IO;
using Genkit;
using Qud.API;
using XRL;
using XRL.CharacterBuilds.Qud;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;
using XRL.World.WorldBuilders;

namespace ThousandAndFirst
{
	/// <summary>
	/// Target-save owner of one exact promoted legacy, reservation receipt, and selected new-world
	/// site. Cross-save files are copied into bounded canonical text; no old save object graph enters.
	/// </summary>
	[Serializable]
	[GameStateSingleton(StateId)]
	public sealed class KingdomInheritanceState : IGameStateSingleton, IComposite
	{
		internal const string StateId = "r_TAF_Inheritance";

		internal const string BuilderClass = "KingdomInheritedSiteBuilder";

		private const int SerializationMagic = 1413568073;

		private const int CurrentSerializationVersion = 4;

		private const int MaxFailureChars = 1000;

		private int SerializationVersion = CurrentSerializationVersion;

		private int PhaseValue = (int)KingdomInheritancePhase.Empty;

		private string LegacyText = "";

		private string ReceiptText = "";

		// Commit advances WrittenTick, while engine marker identity retains reservation tick.
		// Keep both canonical receipts so later proof never reconstructs or guesses either.
		private string CommittedReceiptText = "";

		private string TargetZoneId = "";

		private string TargetTerrainBlueprint = "";

		private int TargetTerrainRank = -1;

		private string SecretId = "";

		private string SiteName = "";

		private string FailureDetail = "";

		private int ApplyStatusValue = -1;

		private int ApplyFaultValue = -1;

		private string ApplicationMarker = "";

		private bool FailureAnnounced;

		// If exact artifact cleanup or profile release is temporarily unavailable, the next load
		// retries cleanup then only that exact release. No inherited-site build may run meanwhile.
		private bool ReleasePending;

		private bool OwnsSkipTerrainBuilders;

		private bool OwnsNoBiomes;

		// Set before the first of the five zone-name writes and cleared only after all five
		// keys are absent. This makes every torn install/removal prefix recoverable without
		// granting authority over a mismatching foreign value.
		private bool OwnsZoneName;

		// Corrupt or future saved shapes are inert. In particular they retain no identifiers or
		// ownership bits that could authorize deletion of another extension's artifacts.
		private bool RecoveryDisabled;

		// Granted only after the live builder proves exact builders, zero parts, and a pristine
		// fresh zone. A generic Failed status alone never authorizes loaded-zone mutation.
		private bool RetryAuthorized;

		[NonSerialized]
		private bool ProfileReceiptWasCommitted;

		[NonSerialized]
		private KingdomSealReceipt ProfileCommittedReceipt;

		[NonSerialized]
		private KingdomSealReservationLease ReservationLease;

		[NonSerialized]
		private MutabilityMap ReservedMap;

		[NonSerialized]
		private WorldInfo ReservedWorldInfo;

		[NonSerialized]
		private int TargetX = -1;

		[NonSerialized]
		private int TargetY = -1;

		[NonSerialized]
		private string ReservedTerrainTag = "";

		internal KingdomInheritancePhase Phase
		{
			get { return Enum.IsDefined(typeof(KingdomInheritancePhase), PhaseValue)
				? (KingdomInheritancePhase)PhaseValue : KingdomInheritancePhase.RepairRequired; }
		}

		internal string SelectedZoneId
		{
			get { return TargetZoneId ?? ""; }
		}

		public bool WantFieldReflection
		{
			get { return false; }
		}

		internal static KingdomInheritanceState Instance
		{
			get { return The.Game == null ? null
				: The.Game.GetObjectGameState(StateId) as KingdomInheritanceState; }
		}

		public void Initialize()
		{
			KingdomInheritanceLeaseOwner.BeginGame(The.Game == null ? "" : The.Game.GameID);
			ResetNewGame();
			XRLGame game = The.Game;
			if (game == null || !KingdomInheritanceStateRules.ShouldOffer(game.gameMode,
				TutorialManager.currentStep != null))
			{
				return;
			}
			KingdomSeal seal = null;
			KingdomSealRecord legacy = null;
			KingdomSealReceipt receipt = null;
			KingdomSealReservationLease lease = null;
			try
			{
				game.RequireSystem<KingdomInheritanceLifecycle>();
				seal = game.RequireSystem<KingdomSeal>();
				string failure = "";
				if (seal == null || !seal.TryReserveImport(KingdomImportPolicy.LatestEligible,
					out legacy, out receipt, out lease, out failure))
				{
					HoldUnreleased(game.GameID, receipt, lease);
					SetRepair("legacy reservation was refused: " + Nonempty(failure,
						"the profile claim could not be proved"));
					return;
				}
				if (legacy == null && receipt == null)
				{
					if (lease != null)
					{
						HoldUnreleased(game.GameID, receipt, lease);
						SetRepair("the import coordinator returned a live claim without a receipt");
					}
					return;
				}
				if (legacy == null || receipt == null || lease == null)
				{
					HoldUnreleased(game.GameID, receipt, lease);
					SetRepair("the import coordinator returned a torn reservation");
					return;
				}
				LegacyText = legacy.Compose();
				ReceiptText = receipt.Compose();
				if (!CanonicalReservation(legacy, receipt, game.GameID))
				{
					ReleaseExact(seal, receipt, lease,
						"the selected legacy or receipt was not canonical");
					return;
				}
				KingdomInheritanceLeaseOwner.Hold(game.GameID, receipt, lease);
				ReservationLease = lease;
				lease = null;
				Transition(KingdomInheritancePhase.Reserved);
			}
			catch (Exception ex)
			{
				if (receipt != null && lease != null && seal != null)
				{
					ReleaseExact(seal, receipt, lease, "legacy reservation failed: " + ex.Message);
				}
				else
				{
					HoldUnreleased(game == null ? "" : game.GameID, receipt, lease);
					SetRepair("legacy reservation failed: " + ex.Message);
				}
			}
		}

		public void HandleEvent(EmbarkEvent E)
		{
			if (E == null)
			{
				return;
			}
			if (E.EventID == QudGameBootModule.BOOTEVENT_AFTERINITIALIZEWORLDS)
			{
				ValidateAfterWorlds();
			}
			else if (E.EventID == QudGameBootModule.BOOTEVENT_BOOTSTARTINGLOCATION)
			{
				ValidateStartAndInstall(E.Element as GlobalLocation);
			}
			else if (E.EventID == QudGameBootModule.BOOTEVENT_AFTERBOOTPLAYEROBJECT)
			{
				AnnounceFailure();
			}
		}

		public void Write(SerializationWriter Writer)
		{
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.Write(PhaseValue);
			Writer.Write(LegacyText ?? "");
			Writer.Write(ReceiptText ?? "");
			Writer.Write(CommittedReceiptText ?? "");
			Writer.Write(TargetZoneId ?? "");
			Writer.Write(TargetTerrainBlueprint ?? "");
			Writer.Write(TargetTerrainRank);
			Writer.Write(SecretId ?? "");
			Writer.Write(SiteName ?? "");
			Writer.Write(FailureDetail ?? "");
			Writer.Write(ApplyStatusValue);
			Writer.Write(ApplyFaultValue);
			Writer.Write(ApplicationMarker ?? "");
			Writer.Write(FailureAnnounced);
			Writer.Write(ReleasePending);
			Writer.Write(OwnsSkipTerrainBuilders);
			Writer.Write(OwnsNoBiomes);
			Writer.Write(RecoveryDisabled);
			Writer.Write(RetryAuthorized);
			Writer.Write(OwnsZoneName);
		}

		public void Read(SerializationReader Reader)
		{
			try
			{
			int magic = Reader.ReadInt32();
			SerializationVersion = Reader.ReadInt32();
			if (!KingdomInheritanceStateRules.IsSupportedSerializationHeader(magic,
				SerializationVersion, SerializationMagic, CurrentSerializationVersion))
			{
				// DeserializeComposite can realign only when Read throws and its catch executes
				// SkipBlock. Returning after a future/invalid short schema would under-consume
				// this composite and corrupt every following read in the save block.
				DisableRecovery("the inherited target state had unsupported framing");
				throw new InvalidDataException("unsupported inheritance state magic or version");
			}
			PhaseValue = Reader.ReadInt32();
			LegacyText = Reader.ReadString() ?? "";
			ReceiptText = Reader.ReadString() ?? "";
			CommittedReceiptText = Reader.ReadString() ?? "";
			TargetZoneId = Reader.ReadString() ?? "";
			TargetTerrainBlueprint = Reader.ReadString() ?? "";
			TargetTerrainRank = Reader.ReadInt32();
			SecretId = Reader.ReadString() ?? "";
			SiteName = Reader.ReadString() ?? "";
			FailureDetail = Reader.ReadString() ?? "";
			ApplyStatusValue = Reader.ReadInt32();
			ApplyFaultValue = Reader.ReadInt32();
			ApplicationMarker = Reader.ReadString() ?? "";
			FailureAnnounced = Reader.ReadBoolean();
			ReleasePending = Reader.ReadBoolean();
			OwnsSkipTerrainBuilders = Reader.ReadBoolean();
			OwnsNoBiomes = Reader.ReadBoolean();
			RecoveryDisabled = SerializationVersion >= 2 && SerializationVersion <=
				CurrentSerializationVersion && Reader.ReadBoolean();
			RetryAuthorized = SerializationVersion >= 3
				&& SerializationVersion <= CurrentSerializationVersion
				&& Reader.ReadBoolean();
			OwnsZoneName = SerializationVersion >= 4
				&& SerializationVersion <= CurrentSerializationVersion
				&& Reader.ReadBoolean();
			if (SerializationVersion >= 1 && SerializationVersion < 4)
			{
				// Older versions predate explicit name-write provenance. Migrate only a complete exact
				// footprint; partial or mismatching old state remains authority-free.
				try
				{
					OwnsZoneName = HasExactOwnedZoneName();
				}
				catch (Exception)
				{
					OwnsZoneName = false;
				}
			}
			bool invalid = LegacyText.Length > KingdomSealFormat.MaxFileChars
				|| ReceiptText.Length > KingdomSealFormat.MaxFileChars
				|| CommittedReceiptText.Length > KingdomSealFormat.MaxFileChars
				|| TargetZoneId.Length > KingdomSealRecord.MaxIdChars
				|| TargetTerrainBlueprint.Length > KingdomSealRecord.MaxIdChars
				|| (TargetTerrainRank < -1
					|| TargetTerrainRank > KingdomInheritanceSiteRules.MaxTerrainRank)
				|| SecretId.Length > KingdomSealRecord.MaxIdChars + 32
				|| SiteName.Length > KingdomSealRecord.MaxNameChars + 32
				|| FailureDetail.Length > MaxFailureChars
				|| ApplicationMarker.Length > 1000
				|| !Enum.IsDefined(typeof(KingdomInheritancePhase), PhaseValue);
			string shapeFailure = "";
			if (!invalid)
			{
				KingdomInheritanceSavedShape shape = new KingdomInheritanceSavedShape
				{
					PhaseValue = PhaseValue,
					LegacyText = LegacyText,
					ReceiptText = ReceiptText,
					CommittedReceiptText = CommittedReceiptText,
					TargetZoneId = TargetZoneId,
					TargetTerrainBlueprint = TargetTerrainBlueprint,
					TargetTerrainRank = TargetTerrainRank,
					SecretId = SecretId,
					SiteName = SiteName,
					ApplyStatus = ApplyStatusValue,
					ApplyFault = ApplyFaultValue,
					ApplicationMarker = ApplicationMarker,
					ReleasePending = ReleasePending,
					OwnsSkipTerrainBuilders = OwnsSkipTerrainBuilders,
					OwnsNoBiomes = OwnsNoBiomes,
					OwnsZoneName = OwnsZoneName,
					RecoveryDisabled = RecoveryDisabled,
					RetryAuthorized = RetryAuthorized
				};
				invalid = !KingdomInheritanceStateRules.TryValidateSavedShape(shape,
					The.Game == null ? "" : The.Game.GameID,
					KingdomInheritEngine.ReconstructionVersion, out shapeFailure);
			}
			if (invalid)
			{
				DisableRecovery("the inherited target state could not be validated on load"
					+ (string.IsNullOrEmpty(shapeFailure) ? "" : ": " + shapeFailure));
			}
			}
			catch (Exception ex)
			{
				// DeserializeComposite may return this partially-read object after recording the
				// exception. Strip every identifier and ownership bit before it can escape.
				DisableRecovery("the inherited target state was truncated on load: " + ex.Message);
				throw;
			}
		}

		internal bool TrySelectionInputs(out string LegacyId, out string OldGroundZoneId,
			out string PreferredTerrainBlueprint)
		{
			LegacyId = "";
			OldGroundZoneId = "";
			PreferredTerrainBlueprint = "";
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			if (Phase != KingdomInheritancePhase.Reserved
				|| !TryGetReservation(out legacy, out receipt))
			{
				return false;
			}
			LegacyId = legacy.LegacyId;
			OldGroundZoneId = legacy.GroundZoneId ?? "";
			PreferredTerrainBlueprint = legacy.TerrainBlueprint ?? "";
			return true;
		}

		internal bool StageSite(KingdomInheritanceSiteCandidate Candidate, int X, int Y,
			MutabilityMap Map, WorldInfo Info, out string Failure)
		{
			Failure = "";
			if (Phase != KingdomInheritancePhase.Reserved || !KingdomInheritanceSiteRules.IsSafe(Candidate)
				|| Map == null || Info == null || Map.GetMutable(X, Y) != 0
				|| Candidate.ZoneId != XRL.World.ZoneID.Assemble(KingdomInheritanceSiteRules.WorldId,
					X / 3, Y / 3, X % 3, Y % 3, KingdomInheritanceSiteRules.SurfaceDepth))
			{
				Failure = "the selected inherited site was not an exact removed mutable surface cell";
				return false;
			}
			TargetZoneId = Candidate.ZoneId;
			TargetTerrainBlueprint = Candidate.TerrainBlueprint;
			TargetTerrainRank = Candidate.TerrainRank;
			ReservedMap = Map;
			ReservedWorldInfo = Info;
			TargetX = X;
			TargetY = Y;
			ReservedTerrainTag = Candidate.TerrainTag ?? "";
			Transition(KingdomInheritancePhase.SiteSelected);
			return true;
		}

		internal void RefuseBootstrap(string Detail)
		{
			if (Phase == KingdomInheritancePhase.Reserved
				|| Phase == KingdomInheritancePhase.SiteSelected
				|| Phase == KingdomInheritancePhase.WorldValidated)
			{
				ReleaseReservation(Detail);
			}
		}

		internal void RecordApplyResult(KingdomInheritApplyResult Result, bool WillRetry = false,
			bool DuringZoneBuild = false)
		{
			if (Result == null)
			{
				SetRepair("the inherited-site builder returned no result");
				if (!WillRetry)
				{
					AnnounceFailure();
				}
				return;
			}
			ApplyStatusValue = (int)Result.Status;
			ApplyFaultValue = (int)Result.Fault;
			ApplicationMarker = Bound(Result.ApplicationMarker, 1000);
			switch (Result.Status)
			{
			case KingdomInheritApplyStatus.Applied:
			case KingdomInheritApplyStatus.AlreadyApplied:
				RetryAuthorized = false;
				FailureDetail = "";
				FailureAnnounced = false;
				ReleasePending = false;
				if (Phase != KingdomInheritancePhase.Committed)
				{
					Transition(KingdomInheritancePhase.AppliedPendingDurability);
				}
				break;
			case KingdomInheritApplyStatus.Refused:
				RetryAuthorized = false;
				if (DuringZoneBuild)
				{
					SetRepair("the inherited site was refused when its zone was built: "
						+ Result.Detail);
				}
				else
				{
					ReleaseReservation("the inherited site was refused: " + Result.Detail);
				}
				break;
			default:
				SetRepair("the inherited site needs repair: " + Result.Detail);
				break;
			}
			if (!WillRetry)
			{
				AnnounceFailure();
			}
		}

		/// <summary>An older same-game rollback can retain an unbuilt exact lazy site after the
		/// profile receipt has already committed. The builder may adopt that external final state
		/// immediately after exact application; no second profile transition or durability spend is
		/// involved, and a crash simply repeats the deterministic reconstruction.</summary>
		internal void AdoptExternalCommittedIfKnown(Zone Zone)
		{
			try
			{
				if (!ProfileReceiptWasCommitted || ProfileCommittedReceipt == null)
				{
					return;
				}
				KingdomSealRecord legacy;
				KingdomSealReceipt reserved;
				string expected;
				string marker = Zone == null ? ""
					: Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
				if (!TryGetReservation(out legacy, out reserved)
					|| Zone == null || Zone.ZoneID != TargetZoneId
					|| !KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy, reserved,
						TargetZoneId, KingdomInheritEngine.ReconstructionVersion, out expected)
					|| !KingdomInheritanceStateRules.RetainsDurableApplicationCandidate(
						ApplyStatusValue, ApplyFaultValue, ApplicationMarker)
					|| ApplicationMarker != expected || marker != expected)
				{
					SetRepair("the externally committed lazy site lost its exact application marker");
					HideDiscoverability(Zone);
					AnnounceFailure();
					return;
				}
				AdoptCommitted(reserved, ProfileCommittedReceipt, Zone);
			}
			catch (Exception ex)
			{
				// Exact Apply already succeeded. Optional profile adoption/discovery must never
				// escape into the builder's application-fallback catch.
				RecordDiscoveryFailure("external committed-site adoption threw: " + ex.Message);
			}
		}

		internal void AuthorizeExactOwnedRepair()
		{
			RetryAuthorized = true;
		}

		internal void RecordBuilderFailure(string Detail)
		{
			ApplyStatusValue = (int)KingdomInheritApplyStatus.Failed;
			ApplyFaultValue = (int)KingdomInheritApplyFault.PartialApplication;
			SetRepair("the inherited-site builder failed closed: " + Detail);
			AnnounceFailure();
		}

		internal bool TryBuilderPayload(string LegacyId, string TargetGameId, string ZoneId,
			int ReconstructionVersion, out KingdomSealRecord Legacy, out KingdomSealReceipt Receipt,
			out string Failure)
		{
			Legacy = null;
			Receipt = null;
			Failure = "";
			if ((Phase != KingdomInheritancePhase.Installed
					&& Phase != KingdomInheritancePhase.AppliedPendingDurability
					&& Phase != KingdomInheritancePhase.Committed
					&& Phase != KingdomInheritancePhase.RepairRequired)
				|| ReconstructionVersion != KingdomInheritEngine.ReconstructionVersion
				|| ZoneId != TargetZoneId || !TryGetReservation(out Legacy, out Receipt)
				|| Legacy.LegacyId != LegacyId || Receipt.TargetGameId != TargetGameId)
			{
				Failure = "the persisted builder does not name this exact inherited target";
				Legacy = null;
				Receipt = null;
				return false;
			}
			return true;
		}

		internal bool TryGroundPaint(string ZoneId, out string Tile, out string Color,
			out string Render, out string Failure)
		{
			Tile = "";
			Color = "";
			Render = ".";
			Failure = "";
			if (ZoneId != TargetZoneId || TargetTerrainRank < 0
				|| TargetTerrainRank > KingdomInheritanceSiteRules.MaxTerrainRank)
			{
				Failure = "the target lost its validated terrain paint class";
				return false;
			}
			switch (TargetTerrainRank)
			{
			case 0:
				Tile = "Terrain/sw_ground_desert_1.bmp";
				Color = "&y";
				break;
			case 1:
				Tile = "Tiles/tile-dirt1.png";
				Color = "&G";
				break;
			case 2:
				Tile = "Tiles/tile-dirt1.png";
				Color = "&y";
				break;
			default:
				Tile = "Tiles/tile-dirt1.png";
				Color = "&w";
				break;
			}
			return true;
		}

		internal bool TryInstallLocationFinder(Zone Zone, string LegacyId, string TargetGameId,
			string ZoneId, int ReconstructionVersion, out string Failure)
		{
			Failure = "";
			if (Zone == null || Zone.ZoneID != ZoneId || ZoneId != TargetZoneId
				|| ReconstructionVersion != KingdomInheritEngine.ReconstructionVersion
				|| (Phase != KingdomInheritancePhase.AppliedPendingDurability
					&& Phase != KingdomInheritancePhase.Committed))
			{
				return false;
			}
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			string expected;
			if (!TryGetReservation(out legacy, out receipt) || legacy.LegacyId != LegacyId
				|| receipt.TargetGameId != TargetGameId
				|| !KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy, receipt,
					ZoneId, ReconstructionVersion, out expected)
				|| ApplicationMarker != expected
				|| (Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "")
					!= expected)
				{
					Failure = "the success-aware finder lacked an exact application marker";
					RecordDiscoveryFailure(Failure);
					return false;
				}
			try
			{
				EnsureOwnedMapNote(legacy);
				new XRL.World.ZoneBuilders.AddLocationFinder
				{
					SecretID = SecretId,
					Value = 1
				}.BuildZone(Zone);
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the success-aware finder could not create its widget: " + ex.Message;
				BestEffortHideBrokenDiscovery(Zone);
				RecordDiscoveryFailure(Failure);
				return false;
			}
		}

		internal void RecordDiscoveryFailure(string Detail)
		{
			string message = "the inherited site needs discovery repair: " + Detail;
			if (!KingdomInheritanceStateRules.PreservesApplicationProofDuringDiscoveryRepair(
				Phase, ApplyStatusValue, ApplyFaultValue, ApplicationMarker))
			{
				SetRepair(message);
				AnnounceFailure();
				return;
			}
			// Discovery is optional state layered over an already-proved application. Never
			// poison phase/status/marker merely because a note, name, or finder could not form.
			FailureDetail = Bound(message, MaxFailureChars);
			try
			{
				LogFailure(FailureDetail);
			}
			catch (Exception)
			{
			}
			if (!FailureAnnounced)
			{
				FailureAnnounced = true;
				try
				{
					MessageQueue.AddPlayerMessage("&yThe inherited kingdom entered this world, "
						+ "but its map discovery needs repair: &Y" + Detail);
				}
				catch (Exception)
				{
				}
			}
		}

		internal bool HasOnlyOwnedBuilders(string ZoneId, string LegacyId, string TargetGameId,
			int ReconstructionVersion, out string Failure)
		{
			Failure = "";
			if (The.ZoneManager == null || ZoneId != TargetZoneId)
			{
				Failure = "the exact target builder collection is unavailable";
				return false;
			}
			if (!OwnsSkipTerrainBuilders || !OwnsNoBiomes
				|| !(The.ZoneManager.GetZoneProperty(ZoneId, "SkipTerrainBuilders") is bool)
				|| !(bool)The.ZoneManager.GetZoneProperty(ZoneId, "SkipTerrainBuilders")
				|| (The.ZoneManager.GetZoneProperty(ZoneId, "NoBiomes") as string) != "Yes")
			{
				Failure = "the target's reserved generation properties changed ownership or value";
				return false;
			}
			ZoneBuilderCollection collection = The.ZoneManager.GetBuilderCollection(ZoneId);
			if (collection == null || collection.Members == null || collection.Members.Count != 2)
			{
				Failure = "the target acquired a foreign or missing persistent builder";
				return false;
			}
			bool foundSite = false;
			bool foundFinder = false;
			for (int i = 0; i < collection.Members.Count; i++)
			{
				OrderedBuilderBlueprint ordered = collection.Members[i];
				ZoneBuilderBlueprint builder = ordered.Blueprint;
				if (builder != null && ordered.Priority == 6000 && builder.Class == BuilderClass
					&& builder.GetParameter<string>("LegacyId", "") == LegacyId
					&& builder.GetParameter<string>("TargetGameId", "") == TargetGameId
					&& builder.GetParameter<string>("TargetZoneId", "") == ZoneId
					&& builder.GetParameter<int>("ReconstructionVersion", -1)
						== ReconstructionVersion)
				{
					foundSite = true;
				}
				else if (builder != null && ordered.Priority == 6100
					&& KingdomInheritanceStateRules.IsExactLocationFinderBuilder(builder.Class,
						builder.GetParameter<string>("LegacyId", ""),
						builder.GetParameter<string>("TargetGameId", ""),
						builder.GetParameter<string>("TargetZoneId", ""),
						builder.GetParameter<int>("ReconstructionVersion", -1),
						LegacyId, TargetGameId, ZoneId, ReconstructionVersion))
				{
					foundFinder = true;
				}
				else
				{
					Failure = "the target's persistent builder set is not exclusively inheritance-owned";
					return false;
				}
			}
			if (!foundSite || !foundFinder)
			{
				Failure = "the exact inherited builder or location finder is missing";
				return false;
			}
			return true;
		}

		internal bool PrepareVanillaFallback(Zone Zone, string Detail, bool ExactOwnedZone = false)
		{
			string cleanupFailure = "";
			bool zoneClean = false;
			try
			{
				HideDiscoverability(Zone);
			}
			catch (Exception ex)
			{
				cleanupFailure = AppendFailure(cleanupFailure,
					"failed to hide inherited discovery during fallback: " + ex.Message);
			}
			try
			{
				string quarantineFailure;
				zoneClean = TryQuarantineExact(Zone, out quarantineFailure);
				if (!zoneClean)
				{
					cleanupFailure = AppendFailure(cleanupFailure, quarantineFailure);
				}
			}
			catch (Exception ex)
			{
				cleanupFailure = AppendFailure(cleanupFailure,
					"exact inherited-zone quarantine threw: " + ex.Message);
			}
			KingdomSealReceipt committedReceipt;
			bool profileCommitted = Phase == KingdomInheritancePhase.Committed
				|| ProfileReceiptWasCommitted
				|| TryGetCommittedReceipt(out committedReceipt);
			bool artifactsClean = false;
			if (KingdomInheritanceStateRules.ShouldAttemptFallbackArtifactCleanup(zoneClean,
				profileCommitted))
			{
				ApplicationMarker = "";
				RetryAuthorized = false;
				string artifactFailure;
				artifactsClean = TryRemoveInstalledArtifacts(out artifactFailure);
				if (!artifactsClean)
				{
					cleanupFailure = AppendFailure(cleanupFailure, artifactFailure);
				}
				if (artifactsClean)
				{
					ReleaseReservation(Detail, RestoreMutable: false);
					AnnounceFailure();
					// Persistent builders/properties are absent. ApplyTo stops on false, and the next
					// attempt therefore runs ordinary vanilla terrain even if profile release is pending.
					return false;
				}
			}
			if (KingdomInheritanceStateRules.MustPersistFallbackReleaseIntent(zoneClean,
				profileCommitted, artifactsClean))
			{
				ReleasePending = true;
			}

			if (!zoneClean && (!OwnsSkipTerrainBuilders || !OwnsNoBiomes))
			{
				RetryAuthorized = false;
			}
			SetRepair(Detail + "; exact cleanup could not be proved: "
				+ Nonempty(cleanupFailure, "the target retained unresolved inheritance artifacts"));
			string terminalFailure;
			if (!TryPrepareSafeTerminalZone(Zone, ExactOwnedZone, out terminalFailure))
			{
				SetRepair(FailureDetail + "; safe hidden terrain validation failed: " + terminalFailure);
				AnnounceFailure();
				return false;
			}
			AnnounceFailure();
			// Never repeat false until Qud force-ignores the custom builder. The success-aware
			// finder that follows no-ops in RepairRequired, leaving this passable zone hidden.
			return true;
		}

		private bool TryPrepareSafeTerminalZone(Zone Zone, bool ExactOwnedZone,
			out string Failure)
		{
			Failure = "";
			string tile;
			string color;
			string render;
			if (Zone == null || Zone.ZoneID != TargetZoneId
				|| !TryGroundPaint(TargetZoneId, out tile, out color, out render, out Failure))
			{
				Failure = Nonempty(Failure, "the exact fallback zone or terrain paint was unavailable");
				return false;
			}
			if (ExactOwnedZone)
			{
				List<GameObject> objects = Zone.GetObjects();
				for (int i = objects.Count - 1; i >= 0; i--)
				{
					objects[i].Obliterate(null, Silent: true);
				}
				Zone.RemoveZoneProperty(KingdomInheritEngine.ZoneMarkerProperty);
			}
			for (int y = 0; y < Zone.Height; y++)
			{
				for (int x = 0; x < Zone.Width; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null)
					{
						Failure = "the fallback terrain contained a missing cell";
						return false;
					}
					// ExactOwnedZone follows a pristine proof and may clear its own partial placement.
					// Foreign-conflict fallback preserves nonblank mod paint and fills only suppressed
					// blank terrain.
					if (ExactOwnedZone || string.IsNullOrEmpty(cell.PaintTile)) cell.PaintTile = tile;
					if (ExactOwnedZone || string.IsNullOrEmpty(cell.PaintTileColor))
						cell.PaintTileColor = color;
					if (ExactOwnedZone || string.IsNullOrEmpty(cell.PaintColorString))
						cell.PaintColorString = color;
					if (ExactOwnedZone || string.IsNullOrEmpty(cell.PaintRenderString))
						cell.PaintRenderString = render;
				}
			}
			Zone.ClearReachableMap();
			int reachable = Zone.BuildReachableMap(0, 0);
			if (!KingdomInheritanceStateRules.CanTerminalizeHiddenFallback(reachable, 0))
			{
				Failure = "the hidden fallback had only " + reachable.ToString()
					+ " cells reachable from its entry";
				return false;
			}
			return true;
		}

		internal bool TryCleanControlledRetry(Zone Zone, out string Failure)
		{
			Failure = "";
			try
			{
				RemoveLocationFinders(Zone);
				return TryQuarantineExact(Zone, out Failure);
			}
			catch (Exception ex)
			{
				Failure = "exact retry cleanup threw: " + ex.Message;
				return false;
			}
		}

		internal bool TryValidateAppliedZone(Zone Zone, out string Failure)
		{
			Failure = "";
			try
			{
				if (Zone == null || Zone.ZoneID != TargetZoneId)
				{
					Failure = "the exact applied target zone was unavailable";
					return false;
				}
				Zone.ClearReachableMap();
				int reachable = Zone.BuildReachableMap(0, 0);
				if (!KingdomInheritanceStateRules.MeetsReachability(reachable))
				{
					Failure = "the reconstructed site left only " + reachable.ToString()
						+ " cells reachable from its entry";
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "post-application reachability validation threw: " + ex.Message;
				return false;
			}
		}

		private bool TryRecoverUnvalidatedApplication(Zone Zone, KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved, out string Failure)
		{
			Failure = "";
			string expected;
			string marker = Zone == null ? ""
				: Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			if (!KingdomInheritanceStateRules.TryComposeApplicationMarker(Legacy, Reserved,
					TargetZoneId, KingdomInheritEngine.ReconstructionVersion, out expected)
				|| !KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
					ApplyStatusValue, ApplyFaultValue, RetryAuthorized, ApplicationMarker,
					marker, expected))
			{
				Failure = "the marker was not an exact retry-authorized unvalidated application";
				return false;
			}
			if (!TryCleanControlledRetry(Zone, out Failure))
			{
				return false;
			}
			ApplicationMarker = "";
			return true;
		}

		internal void ResumeAfterLoad(KingdomInheritanceLoadKind LoadKind,
			string LoadSourceFailure)
		{
			XRLGame game = The.Game;
			if (game == null)
			{
				return;
			}
			KingdomInheritanceLeaseOwner.BeginGame(game.GameID);
			if (RecoveryDisabled)
			{
				AnnounceFailure();
				return;
			}
			if (Phase == KingdomInheritancePhase.Empty || Phase == KingdomInheritancePhase.Refused)
			{
				return;
			}
			try
			{
				bool exactPrimaryLoad = LoadKind == KingdomInheritanceLoadKind.Primary;
				KingdomSealRecord legacy;
				KingdomSealReceipt reserved;
				if (!TryGetReservation(out legacy, out reserved))
				{
					SetRepair("the loaded inheritance state lost its canonical reservation");
					AnnounceFailure();
					return;
				}
				KingdomSeal seal = game.RequireSystem<KingdomSeal>();
				KingdomSealReceipt expected = reserved;
				KingdomSealReceipt savedCommitted;
				if (TryGetCommittedReceipt(out savedCommitted))
				{
					expected = savedCommitted;
				}
				KingdomSealReceipt current;
				string failure = "";
				if (seal == null || !seal.TryInspectImport(expected, out current, out failure)
					|| current == null)
				{
					SetRepair("the loaded inheritance receipt could not be inspected: "
						+ Nonempty(failure, "the exact profile receipt was unavailable"));
					AnnounceFailure();
					return;
				}
				if (KingdomInheritanceStateRules.ProfileReceiptBlocksRelease(current.State))
				{
					ProfileReceiptWasCommitted = true;
					if (LoadKind == KingdomInheritanceLoadKind.Unknown)
					{
						// Coda, case collisions, and unproved paths cannot mutate target state.
						// Still retain the no-release guard: inspection already proved a final receipt.
						ProfileCommittedReceipt = null;
						return;
					}
					ProfileCommittedReceipt = current;
					Zone provenZone;
					if (!TryDurableProof(legacy, reserved, Phase == KingdomInheritancePhase.Installed,
						out provenZone, out failure))
					{
						ReconcileCommittedRewind(legacy, reserved, current, LoadKind, failure);
						return;
					}
					AdoptCommitted(reserved, current, provenZone);
					return;
				}
				if (current.State != KingdomSealReceiptState.Reserved)
				{
					SetRepair("the loaded inheritance receipt entered an unsupported final state");
					AnnounceFailure();
					return;
				}
				if (current.Compose() != ReceiptText)
				{
					if (Phase == KingdomInheritancePhase.AppliedPendingDurability
						|| !string.IsNullOrEmpty(ApplicationMarker))
					{
						SetRepair("the reservation tick changed after an application marker was formed");
						AnnounceFailure();
						return;
					}
					ReceiptText = current.Compose();
					reserved = current;
				}
				if (!EnsureReservationLease(seal, reserved, out failure))
				{
					SetRepair("the loaded inheritance reservation could not resume: " + failure);
					AnnounceFailure();
					return;
				}

				if (ReleasePending)
				{
					string cleanupFailure;
					if (TryRemoveInstalledArtifacts(out cleanupFailure))
					{
						ReleaseReservation("the loaded target is retrying its exact refused import release",
							RestoreMutable: false);
					}
					else
					{
						SetRepair("the loaded target could not prove artifact cleanup before release: "
							+ cleanupFailure);
					}
					AnnounceFailure();
				}
				else if (Phase == KingdomInheritancePhase.AppliedPendingDurability
					&& exactPrimaryLoad)
				{
					CommitDurableProof(seal, legacy, reserved);
				}
				else if (Phase == KingdomInheritancePhase.Installed && exactPrimaryLoad
					&& The.ZoneManager != null && The.ZoneManager.IsZoneBuilt(TargetZoneId))
				{
					Zone recovered;
					if (TryDurableProof(legacy, reserved, AllowInstalledRecovery: true,
						out recovered, out failure))
					{
						Transition(KingdomInheritancePhase.AppliedPendingDurability);
						CommitKnownProof(seal, reserved, recovered);
					}
					else
					{
						SetRepair("a built target with installed state failed marker-ownership recovery: "
							+ failure);
						HideDiscoverability(recovered);
						AnnounceFailure();
					}
				}
				else if (Phase == KingdomInheritancePhase.RepairRequired)
				{
					RepairLoadedTarget(seal, legacy, reserved, exactPrimaryLoad);
				}
				else if (Phase == KingdomInheritancePhase.Committed)
				{
					SetRepair("the primary says committed while the exact profile receipt is reserved");
					AnnounceFailure();
				}
			}
			catch (Exception ex)
			{
				SetRepair("loaded inheritance recovery failed closed: " + ex.Message);
				AnnounceFailure();
			}
		}

		internal void HandleTargetZoneBuilt(Zone Zone)
		{
			if (RecoveryDisabled || Zone == null || Zone.ZoneID != TargetZoneId)
			{
				return;
			}
			try
			{
				if (Phase == KingdomInheritancePhase.Refused)
				{
					HideDiscoverability(Zone);
					string cleanupFailure;
					if (!TryRemoveInstalledArtifacts(out cleanupFailure))
					{
						SetRepair("the refused target retained unresolved artifacts: " + cleanupFailure);
					}
				}
				else if (Phase == KingdomInheritancePhase.RepairRequired)
				{
					HideDiscoverability(Zone);
					if (!KingdomInheritanceStateRules.RetainsDurableApplicationCandidate(
						ApplyStatusValue, ApplyFaultValue, ApplicationMarker))
					{
						string failure;
						if (!TryQuarantineExact(Zone, out failure))
						{
							SetRepair("the failed inherited zone could not be quarantined: " + failure);
						}
					}
				}
			}
			catch (Exception ex)
			{
				SetRepair("the failed inherited zone could not be hidden: " + ex.Message);
			}
			AnnounceFailure();
		}

		private void CommitDurableProof(KingdomSeal Seal, KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved)
		{
			Zone zone;
			string failure = "";
			if (!TryDurableProof(Legacy, Reserved, AllowInstalledRecovery: false,
				out zone, out failure))
			{
				SetRepair("the inherited application was not durable in the loaded primary: " + failure);
				HideDiscoverability(zone);
				AnnounceFailure();
				return;
			}
			CommitKnownProof(Seal, Reserved, zone);
		}

		private void CommitKnownProof(KingdomSeal Seal, KingdomSealReceipt Reserved, Zone Zone)
		{
			KingdomSealReservationLease lease = GetReservationLease(Reserved);
			KingdomSealReceipt committed;
			string failure = "";
			if (lease == null || Seal == null
				|| !Seal.TryCommitImport(Reserved, lease, out committed, out failure)
				|| committed == null)
			{
				SetRepair("the durable inherited application could not commit its receipt: "
					+ Nonempty(failure, "the exact live reservation was unavailable"));
				HideDiscoverability(Zone);
				AnnounceFailure();
				return;
			}
			KingdomInheritanceLeaseOwner.Forget(lease);
			ReservationLease = null;
			// The profile transition is already durable at this point. Guard against any
			// subsequent target-state/adoption fault attempting to release the spent receipt.
			ProfileReceiptWasCommitted = true;
			ProfileCommittedReceipt = committed;
			AdoptCommitted(Reserved, committed, Zone);
		}

		private void AdoptCommitted(KingdomSealReceipt Reserved,
			KingdomSealReceipt Committed, Zone Zone)
		{
			if (Reserved == null || Committed == null
				|| Committed.State != KingdomSealReceiptState.Committed
				|| Committed.LineageId != Reserved.LineageId
				|| Committed.LegacyId != Reserved.LegacyId
				|| Committed.TargetGameId != Reserved.TargetGameId
				|| Committed.WrittenTick < Reserved.WrittenTick)
			{
				SetRepair("the committed receipt was not a monotone state of the exact reservation");
				return;
			}
			string committedText;
			try
			{
				committedText = Committed.Compose();
			}
			catch (Exception ex)
			{
				SetRepair("the committed receipt could not be persisted canonically: " + ex.Message);
				AnnounceFailure();
				return;
			}
			string discoveryFailure = "";
			string marker = ApplicationMarker;
			bool zoneMarkerValid = Zone == null;
			if (Zone != null)
			{
				try
				{
					string observedMarker = Bound(Zone.GetZoneProperty(
						KingdomInheritEngine.ZoneMarkerProperty, ""), 1000);
					if (string.IsNullOrEmpty(observedMarker)
						|| (!string.IsNullOrEmpty(ApplicationMarker)
							&& observedMarker != ApplicationMarker))
					{
						discoveryFailure = "the committed zone marker changed after exact proof";
					}
					else
					{
						marker = observedMarker;
						zoneMarkerValid = true;
					}
				}
				catch (Exception ex)
				{
					discoveryFailure = "the committed zone marker could not be reread: "
						+ ex.Message;
				}
			}
			CommittedReceiptText = committedText;
			ProfileReceiptWasCommitted = true;
			ProfileCommittedReceipt = Committed;
			ApplicationMarker = marker;
			FailureDetail = "";
			FailureAnnounced = false;
			Transition(KingdomInheritancePhase.Committed);
			try
			{
				KingdomInheritanceLeaseOwner.Finish(Committed.TargetGameId, Reserved);
			}
			catch (Exception ex)
			{
				discoveryFailure = AppendFailure(discoveryFailure,
					"the completed process lease could not close: " + ex.Message);
			}
			ReservationLease = null;
			if (!string.IsNullOrEmpty(discoveryFailure))
			{
				RecordDiscoveryFailure(discoveryFailure);
			}
			if (!zoneMarkerValid)
			{
				BestEffortHideBrokenDiscovery(Zone);
				return;
			}
			TryRestoreDiscoverability(Zone);
		}

		private bool TryDurableProof(KingdomSealRecord Legacy, KingdomSealReceipt Reserved,
			bool AllowInstalledRecovery, out Zone Zone, out string Failure)
		{
			Zone = null;
			Failure = "";
			if (ReleasePending)
			{
				Failure = "the target is pending release rather than durable commit";
				return false;
			}
			string expected;
			if (!KingdomInheritanceStateRules.TryComposeApplicationMarker(Legacy, Reserved,
				TargetZoneId, KingdomInheritEngine.ReconstructionVersion, out expected))
			{
				Failure = "the canonical reservation could not recompute its application marker";
				return false;
			}
			bool built = The.ZoneManager != null
				&& KingdomInheritanceSiteRules.IsCanonicalSurfaceZoneId(TargetZoneId)
				&& The.ZoneManager.IsZoneBuilt(TargetZoneId);
			if (!built)
			{
				Failure = "the exact target zone was not persisted as built";
				return false;
			}
			Zone = The.ZoneManager.GetZone(TargetZoneId);
			if (Zone == null || Zone.ZoneID != TargetZoneId)
			{
				Failure = "the persisted target zone could not be loaded exactly";
				return false;
			}
			string marker = Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			if (!KingdomInheritanceStateRules.IsDurableMarkerProof(Phase, ApplyStatusValue,
				built, ApplicationMarker, expected, marker, AllowInstalledRecovery))
			{
				Failure = "the persisted phase and exact zone marker do not prove one durable application";
				return false;
			}
			// Engine.Apply already proved the exact objects before publishing this marker. On a later
			// Primary load, state phase + recomputed marker + loaded zone marker are the durability
			// proof. Rechecking objects here would punish lawful moving, filling, or destruction.
			ApplicationMarker = expected;
			ApplyStatusValue = (int)KingdomInheritApplyStatus.AlreadyApplied;
			ApplyFaultValue = (int)KingdomInheritApplyFault.None;
			return true;
		}

		private void RepairLoadedTarget(KingdomSeal Seal, KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved, bool ExactPrimaryLoad)
		{
			if (ReleasePending)
			{
				string cleanupFailure;
				if (TryRemoveInstalledArtifacts(out cleanupFailure))
				{
					ReleaseReservation("the repaired target is retrying its exact profile release",
						RestoreMutable: false);
				}
				else
				{
					SetRepair("the repaired target could not prove cleanup before release: "
						+ cleanupFailure);
				}
				AnnounceFailure();
				return;
			}
			if (The.ZoneManager == null || !The.ZoneManager.IsZoneBuilt(TargetZoneId))
			{
				return;
			}
			Zone zone = The.ZoneManager.GetZone(TargetZoneId);
			if (zone == null || zone.ZoneID != TargetZoneId)
			{
				return;
			}
			HideDiscoverability(zone);
			string marker = zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			KingdomInheritApplyResult result;
			if (!string.IsNullOrEmpty(marker))
			{
				string retryFailure;
				if (TryRecoverUnvalidatedApplication(zone, Legacy, Reserved, out retryFailure))
				{
					marker = "";
				}
				else if (!ExactPrimaryLoad)
				{
					return;
				}
				else
				{
					Zone proven;
					string markerFailure;
					if (TryDurableProof(Legacy, Reserved, AllowInstalledRecovery: false,
						out proven, out markerFailure))
					{
						CommitKnownProof(Seal, Reserved, proven);
						return;
					}
					SetRepair("the loaded repair marker failed exact ownership proof: "
						+ markerFailure + "; " + retryFailure);
					HideDiscoverability(zone);
					AnnounceFailure();
					return;
				}
			}
			if (ApplyStatusValue != (int)KingdomInheritApplyStatus.Failed
				|| !RetryAuthorized)
			{
				return;
			}
			string failure;
			if (!TryQuarantineExact(zone, out failure))
			{
				SetRepair("the inherited target was not clean enough to retry: " + failure);
				AnnounceFailure();
				return;
			}
			if (!TryProveDirectRepairPrecondition(zone, Legacy, Reserved, out failure))
			{
				RetryAuthorized = false;
				SetRepair("the inherited target lost exact direct-repair provenance: " + failure);
				AnnounceFailure();
				return;
			}
			result = KingdomInheritEngine.Apply(Legacy, Reserved, TargetZoneId, zone);
			if (result == null)
			{
				RecordApplyResult(new KingdomInheritApplyResult(
					KingdomInheritApplyStatus.Failed,
					KingdomInheritApplyFault.PartialApplication,
					"the loaded repair Apply returned no result", "", 0, false));
				if (!TryCleanControlledRetry(zone, out failure))
				{
					SetRepair("null loaded repair result could not quarantine: " + failure);
				}
				HideDiscoverability(zone);
				AnnounceFailure();
				return;
			}
			if (result.Status == KingdomInheritApplyStatus.Applied
				|| result.Status == KingdomInheritApplyStatus.AlreadyApplied)
			{
				string reachFailure;
				if (!TryValidateAppliedZone(zone, out reachFailure))
				{
					KingdomInheritApplyResult failed = new KingdomInheritApplyResult(
						KingdomInheritApplyStatus.Failed,
						KingdomInheritApplyFault.PartialApplication, reachFailure,
						result.ApplicationMarker, result.PlacedCount, result.FreshEmptyVerified);
					RecordApplyResult(failed);
					string cleanupFailure;
					if (!TryCleanControlledRetry(zone, out cleanupFailure))
					{
						SetRepair("loaded repair failed reachability and exact quarantine: "
							+ cleanupFailure);
					}
					else
					{
						ApplicationMarker = "";
					}
					HideDiscoverability(zone);
					AnnounceFailure();
					return;
				}
				RecordApplyResult(result);
				TryRestoreDiscoverability(zone);
				return;
			}
			if (result.Status == KingdomInheritApplyStatus.Refused)
			{
				RetryAuthorized = false;
				SetRepair("the clean loaded inherited target was refused: " + result.Detail);
			}
			else
			{
				RecordApplyResult(result);
				if (result.Fault == KingdomInheritApplyFault.PartialApplication)
				{
					if (TryCleanControlledRetry(zone, out failure))
					{
						ApplicationMarker = "";
					}
					else
					{
						SetRepair("partial loaded repair could not quarantine: " + failure);
					}
				}
				else
				{
					RetryAuthorized = false;
				}
			}
			HideDiscoverability(zone);
			AnnounceFailure();
		}

		private void ReconcileCommittedRewind(KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved, KingdomSealReceipt Committed,
			KingdomInheritanceLoadKind LoadKind, string PriorFailure)
		{
			if (The.ZoneManager == null || Legacy == null || Reserved == null || Committed == null)
			{
				SetRepair("the externally committed inheritance could not inspect its rewound target: "
					+ PriorFailure);
				AnnounceFailure();
				return;
			}
			if (!The.ZoneManager.IsZoneBuilt(TargetZoneId))
			{
				string builderFailure;
				bool exactLazy = HasOnlyOwnedBuilders(TargetZoneId, Legacy.LegacyId,
					Reserved.TargetGameId,
					KingdomInheritEngine.ReconstructionVersion, out builderFailure)
					&& The.ZoneManager.CountPartsFor(TargetZoneId) == 0;
				if (KingdomInheritanceStateRules.DecideCommittedRewind(LoadKind,
					ReceiptAlreadyCommitted: true, DurableProof: false, TargetBuilt: false,
					MarkerEmpty: true, ExactLazyBuilders: exactLazy,
					CleanReapplyPrecondition: false)
					!= KingdomCommittedRewindAction.AwaitLazyBuilder)
				{
					SetRepair("the rewound unbuilt target lost its exact lazy builder: "
						+ Nonempty(builderFailure, "a foreign persistent part was present"));
					AnnounceFailure();
				}
				// Keep Installed/Repair state and the nonserialized committed guard. The exact lazy
				// builder adopts immediately after exact application; a crash safely repeats it.
				return;
			}

			Zone zone = The.ZoneManager.GetZone(TargetZoneId);
			if (zone == null || zone.ZoneID != TargetZoneId)
			{
				SetRepair("the rewound committed target could not load its exact built zone");
				AnnounceFailure();
				return;
			}
			string marker = zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			if (!string.IsNullOrEmpty(marker))
			{
				string retryFailure;
				if (TryRecoverUnvalidatedApplication(zone, Legacy, Reserved, out retryFailure))
				{
					marker = "";
				}
				else
				{
					SetRepair("the rewound committed target carried a marker without valid "
						+ "saved provenance: " + retryFailure);
					HideDiscoverability(zone);
					AnnounceFailure();
					return;
				}
			}
			string failure;
			bool cleanReapply = TryQuarantineExact(zone, out failure)
				&& TryProveDirectRepairPrecondition(zone, Legacy, Reserved, out failure,
					RequireRetryAuthorization: false);
			if (KingdomInheritanceStateRules.DecideCommittedRewind(LoadKind,
				ReceiptAlreadyCommitted: true, DurableProof: false, TargetBuilt: true,
				MarkerEmpty: true, ExactLazyBuilders: true,
				CleanReapplyPrecondition: cleanReapply)
				!= KingdomCommittedRewindAction.ReapplyCleanBuiltTarget)
			{
				RetryAuthorized = false;
				SetRepair("the rewound committed target was not clean enough to reconstruct: "
					+ failure);
				HideDiscoverability(zone);
				AnnounceFailure();
				return;
			}
			AuthorizeExactOwnedRepair();
			KingdomInheritApplyResult result = KingdomInheritEngine.Apply(Legacy, Reserved,
				TargetZoneId, zone);
			if (result != null && (result.Status == KingdomInheritApplyStatus.Applied
				|| result.Status == KingdomInheritApplyStatus.AlreadyApplied)
				&& TryValidateAppliedZone(zone, out failure))
			{
				RecordApplyResult(result);
				AdoptCommitted(Reserved, Committed, zone);
				return;
			}
			RecordApplyResult(new KingdomInheritApplyResult(KingdomInheritApplyStatus.Failed,
				KingdomInheritApplyFault.PartialApplication,
				result == null ? "the committed rewind Apply returned no result"
					: Nonempty(failure, result.Detail),
				result == null ? "" : result.ApplicationMarker,
				result == null ? 0 : result.PlacedCount,
				result != null && result.FreshEmptyVerified));
			if (!TryCleanControlledRetry(zone, out failure))
			{
				SetRepair("rewound committed reconstruction failed and could not quarantine: "
					+ failure);
			}
			else
			{
				ApplicationMarker = "";
			}
			HideDiscoverability(zone);
			AnnounceFailure();
		}

		private bool TryProveDirectRepairPrecondition(Zone Zone, KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved, out string Failure,
			bool RequireRetryAuthorization = true)
		{
			Failure = "";
			if ((RequireRetryAuthorization && !RetryAuthorized)
				|| Zone == null || Zone.ZoneID != TargetZoneId
				|| Legacy == null || Reserved == null
				|| !HasOnlyOwnedBuilders(TargetZoneId, Legacy.LegacyId, Reserved.TargetGameId,
					KingdomInheritEngine.ReconstructionVersion, out Failure)
				|| The.ZoneManager.CountPartsFor(TargetZoneId) != 0
				|| Zone.GetObjects().Count != 0)
			{
				Failure = Nonempty(Failure,
					"the loaded target was not still exact-owned, part-free, and object-free");
				return false;
			}
			string tile;
			string color;
			string render;
			if (!TryGroundPaint(TargetZoneId, out tile, out color, out render, out Failure))
			{
				return false;
			}
			for (int y = 0; y < Zone.Height; y++)
			{
				for (int x = 0; x < Zone.Width; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || cell.PaintTile != tile || cell.PaintTileColor != color
						|| cell.PaintColorString != color || cell.PaintRenderString != render)
					{
						Failure = "the loaded direct-repair ground was changed or not inheritance-painted";
						return false;
					}
				}
			}
			return true;
		}

		private void ValidateAfterWorlds()
		{
			try
			{
				if (Phase == KingdomInheritancePhase.Reserved)
				{
					ReleaseReservation("the Joppa world extension did not reserve a compatible site");
					return;
				}
				if (Phase != KingdomInheritancePhase.SiteSelected)
				{
					return;
				}
				string failure;
				if (!KingdomInheritanceWorldRuntime.ValidateSelected(TargetZoneId,
					TargetTerrainBlueprint, TargetX, TargetY, ReservedMap, ReservedWorldInfo,
					requireRemovedMap: true, out failure))
				{
					ReleaseReservation("post-world validation refused the inherited site: " + failure);
					return;
				}
				Transition(KingdomInheritancePhase.WorldValidated);
			}
			catch (Exception ex)
			{
				ReleaseReservation("post-world validation failed: " + ex.Message);
			}
		}

		private void ValidateStartAndInstall(GlobalLocation Start)
		{
			if (Phase != KingdomInheritancePhase.WorldValidated)
			{
				return;
			}
			try
			{
				KingdomInheritanceStartFault startFault = KingdomInheritanceStateRules.ValidateStart(
					TargetZoneId, Start == null || Start.IsClear() ? "" : Start.World,
					Start == null || Start.IsClear() ? "" : Start.ZoneID);
				if (startFault != KingdomInheritanceStartFault.None)
				{
					ReleaseReservation("the inherited site is incompatible with this start: "
						+ startFault.ToString());
					return;
				}
				string failure;
				if (!KingdomInheritanceWorldRuntime.ValidateSelected(TargetZoneId,
					TargetTerrainBlueprint, TargetX, TargetY, ReservedMap, ReservedWorldInfo,
					requireRemovedMap: true, out failure))
				{
					ReleaseReservation("final world validation refused the inherited site: " + failure);
					return;
				}
				InstallArtifacts();
			}
			catch (Exception ex)
			{
				string cleanupFailure;
				if (TryRemoveInstalledArtifacts(out cleanupFailure))
				{
					ReleaseReservation("the inherited site's discoverability could not be installed: "
						+ ex.Message);
				}
				else
				{
					ReleasePending = true;
					SetRepair("artifact installation failed and exact cleanup was unresolved: "
						+ ex.Message + "; " + cleanupFailure);
				}
			}
		}

		private void InstallArtifacts()
		{
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			if (!TryGetReservation(out legacy, out receipt))
			{
				throw new InvalidDataException("the target no longer carries its exact reservation");
			}
			SecretId = "taf.inherit." + legacy.LegacyId;
			if (JournalAPI.GetMapNote(SecretId) != null)
			{
				throw new InvalidDataException("the inherited site's secret id is already in use");
			}
			SiteName = KingdomInheritanceStateRules.ComposeSiteName(legacy);
			if (HasAnyZoneNameFootprint())
			{
				throw new InvalidDataException("the target already has an explicit zone-name footprint");
			}
			if (The.ZoneManager.HasZoneProperty(TargetZoneId, "SkipTerrainBuilders")
				|| The.ZoneManager.HasZoneProperty(TargetZoneId, "NoBiomes"))
			{
				throw new InvalidDataException(
					"the target's reserved generation property is already owned");
			}
			The.ZoneManager.SetZoneProperty(TargetZoneId, "SkipTerrainBuilders", true);
			OwnsSkipTerrainBuilders = true;
			The.ZoneManager.SetZoneProperty(TargetZoneId, "NoBiomes", "Yes");
			OwnsNoBiomes = true;
			The.ZoneManager.AddZoneBuilder(TargetZoneId, 6000, BuilderClass,
				"LegacyId", legacy.LegacyId,
				"TargetGameId", receipt.TargetGameId,
				"TargetZoneId", TargetZoneId,
				"ReconstructionVersion", KingdomInheritEngine.ReconstructionVersion);
			// ZoneBuilderCollection copies its member count before running. A custom finder must
			// therefore no-op unless the preceding builder published exact success; removing a generic
			// AddLocationFinder from persistence cannot suppress that same-attempt local copy.
			The.ZoneManager.AddZoneBuilder(TargetZoneId, 6100,
				"KingdomInheritanceLocationFinderBuilder",
				"LegacyId", legacy.LegacyId,
				"TargetGameId", receipt.TargetGameId,
				"TargetZoneId", TargetZoneId,
				"ReconstructionVersion", KingdomInheritEngine.ReconstructionVersion);
			JournalAPI.AddMapNote(TargetZoneId, ComposeMapNote(legacy), Category(legacy),
				new string[4] { "settlement", "historic", "taf", "inheritance" },
				SecretId, revealed: true, sold: false, 0L, silent: true);
			OwnsZoneName = true;
			SetOwnedZoneName();
			Transition(KingdomInheritancePhase.Installed);
		}

		private void ReleaseReservation(string Detail, bool RestoreMutable = true)
		{
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			KingdomSealReservationLease lease;
			if (!TryGetReservation(out legacy, out receipt)
				|| (lease = GetReservationLease(receipt)) == null)
			{
				ReleasePending = true;
				SetRepair(Detail + "; the exact live reservation was unavailable for release");
				return;
			}
			string restoreFailure;
			if (RestoreMutable && !RestoreMutableReservation(out restoreFailure))
			{
				ReleasePending = true;
				SetRepair(Detail + "; the removed mutable site could not be restored: "
					+ restoreFailure);
				return;
			}
			KingdomSeal seal = The.Game == null ? null : The.Game.GetSystem<KingdomSeal>();
			string failure = "";
			if (seal != null && seal.TryReleaseImport(receipt, lease, out failure))
			{
				KingdomInheritanceLeaseOwner.Forget(lease);
				ReservationLease = null;
				ReleasePending = false;
				FailureDetail = Bound(Detail, MaxFailureChars);
				Transition(KingdomInheritancePhase.Refused);
				LogFailure(FailureDetail);
				return;
			}
			ReleasePending = true;
			SetRepair(Detail + "; the reservation could not be released: "
				+ Nonempty(failure, "the seal coordinator was unavailable"));
		}

		private void ReleaseExact(KingdomSeal Seal, KingdomSealReceipt Receipt,
			KingdomSealReservationLease Lease, string Detail)
		{
			string failure = "";
			HoldUnreleased(The.Game == null ? "" : The.Game.GameID, Receipt, Lease);
			if (Seal != null && Receipt != null && Lease != null
				&& Seal.TryReleaseImport(Receipt, Lease, out failure))
			{
				KingdomInheritanceLeaseOwner.Forget(Lease);
				ReservationLease = null;
				ReleasePending = false;
				FailureDetail = Bound(Detail, MaxFailureChars);
				PhaseValue = (int)KingdomInheritancePhase.Refused;
				LogFailure(FailureDetail);
				return;
			}
			ReleasePending = true;
			SetRepair(Detail + "; the exact reservation could not be released: "
				+ Nonempty(failure, "unknown release failure"));
		}

		private bool TryRemoveInstalledArtifacts(out string Failure)
		{
			Failure = "";
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			if (RecoveryDisabled || The.Game == null || The.ZoneManager == null
				|| string.IsNullOrEmpty(TargetZoneId)
				|| !TryGetReservation(out legacy, out receipt))
			{
				Failure = "cleanup lacked a trusted exact target reservation";
				return false;
			}
			try
			{
				The.ZoneManager.RemoveZoneBuilders(TargetZoneId, delegate(ZoneBuilderBlueprint builder)
				{
					if (builder == null)
					{
						return false;
					}
					return KingdomInheritanceStateRules.IsExactSiteBuilder(builder.Class,
						builder.GetParameter<string>("LegacyId", ""),
						builder.GetParameter<string>("TargetGameId", ""),
						builder.GetParameter<string>("TargetZoneId", ""),
						builder.GetParameter<int>("ReconstructionVersion", -1),
						legacy.LegacyId, receipt.TargetGameId, TargetZoneId,
						KingdomInheritEngine.ReconstructionVersion)
						|| KingdomInheritanceStateRules.IsExactLocationFinderBuilder(builder.Class,
							builder.GetParameter<string>("LegacyId", ""),
							builder.GetParameter<string>("TargetGameId", ""),
							builder.GetParameter<string>("TargetZoneId", ""),
							builder.GetParameter<int>("ReconstructionVersion", -1),
							legacy.LegacyId, receipt.TargetGameId, TargetZoneId,
							KingdomInheritEngine.ReconstructionVersion);
				});
			}
			catch (Exception ex)
			{
				Failure = AppendFailure(Failure, "builder removal threw: " + ex.Message);
			}
			try
			{
				JournalMapNote note = string.IsNullOrEmpty(SecretId)
					? null : JournalAPI.GetMapNote(SecretId);
				if (note != null && note.ZoneID == TargetZoneId)
				{
					JournalAPI.DeleteMapNote(note);
				}
			}
			catch (Exception ex)
			{
				Failure = AppendFailure(Failure, "map-note removal threw: " + ex.Message);
			}
			string nameFailure;
			if (!TryRemoveOwnedZoneName(out nameFailure))
			{
				Failure = AppendFailure(Failure, nameFailure);
			}
			try
			{
				object skip = The.ZoneManager.GetZoneProperty(TargetZoneId, "SkipTerrainBuilders");
				if (OwnsSkipTerrainBuilders && skip is bool && (bool)skip)
				{
					The.ZoneManager.RemoveZoneProperty(TargetZoneId, "SkipTerrainBuilders");
				}
				if (!The.ZoneManager.HasZoneProperty(TargetZoneId, "SkipTerrainBuilders"))
				{
					OwnsSkipTerrainBuilders = false;
				}
				if (OwnsNoBiomes
					&& (The.ZoneManager.GetZoneProperty(TargetZoneId, "NoBiomes") as string) == "Yes")
				{
					The.ZoneManager.RemoveZoneProperty(TargetZoneId, "NoBiomes");
				}
				if (!The.ZoneManager.HasZoneProperty(TargetZoneId, "NoBiomes"))
				{
					OwnsNoBiomes = false;
				}
			}
			catch (Exception ex)
			{
				Failure = AppendFailure(Failure, "zone-property removal threw: " + ex.Message);
			}
			string proofFailure;
			if (!TryProveInstalledArtifactsAbsent(legacy, receipt, out proofFailure))
			{
				Failure = AppendFailure(Failure, proofFailure);
				return false;
			}
			return string.IsNullOrEmpty(Failure);
		}

		private bool TryProveInstalledArtifactsAbsent(KingdomSealRecord Legacy,
			KingdomSealReceipt Receipt, out string Failure)
		{
			Failure = "";
			try
			{
				ZoneBuilderCollection collection = The.ZoneManager.GetBuilderCollection(TargetZoneId);
				if (collection != null && collection.Members != null)
				{
					for (int i = 0; i < collection.Members.Count; i++)
					{
						ZoneBuilderBlueprint builder = collection.Members[i].Blueprint;
						if (builder != null && (KingdomInheritanceStateRules.IsExactSiteBuilder(
							builder.Class, builder.GetParameter<string>("LegacyId", ""),
							builder.GetParameter<string>("TargetGameId", ""),
							builder.GetParameter<string>("TargetZoneId", ""),
							builder.GetParameter<int>("ReconstructionVersion", -1),
							Legacy.LegacyId, Receipt.TargetGameId, TargetZoneId,
							KingdomInheritEngine.ReconstructionVersion)
							|| KingdomInheritanceStateRules.IsExactLocationFinderBuilder(
								builder.Class, builder.GetParameter<string>("LegacyId", ""),
								builder.GetParameter<string>("TargetGameId", ""),
								builder.GetParameter<string>("TargetZoneId", ""),
								builder.GetParameter<int>("ReconstructionVersion", -1),
								Legacy.LegacyId, Receipt.TargetGameId, TargetZoneId,
								KingdomInheritEngine.ReconstructionVersion)))
						{
							Failure = "an exact inherited persistent builder survived cleanup";
							return false;
						}
					}
				}
				JournalMapNote note = string.IsNullOrEmpty(SecretId)
					? null : JournalAPI.GetMapNote(SecretId);
				if (note != null)
				{
					Failure = note.ZoneID == TargetZoneId
						? "the exact inherited map note survived cleanup"
						: "the inherited secret id now belongs to a foreign map note";
					return false;
				}
				if (OwnsZoneName)
				{
					Failure = "owned zone-name cleanup authority survived artifact cleanup";
					return false;
				}
				if (The.ZoneManager.HasZoneProperty(TargetZoneId, "SkipTerrainBuilders")
					|| The.ZoneManager.HasZoneProperty(TargetZoneId, "NoBiomes")
					|| OwnsSkipTerrainBuilders || OwnsNoBiomes)
				{
					Failure = "a reserved generation property or ownership bit survived cleanup";
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "artifact-absence reproof threw: " + ex.Message;
				return false;
			}
		}

		private bool EnsureReservationLease(KingdomSeal Seal, KingdomSealReceipt Reserved,
			out string Failure)
		{
			Failure = "";
			KingdomSealReservationLease held = GetReservationLease(Reserved);
			if (held != null)
			{
				ReservationLease = held;
				return true;
			}
			KingdomSealReservationLease resumed = null;
			if (Seal == null || Reserved == null
				|| !Seal.TryResumeImport(Reserved, out resumed, out Failure) || resumed == null)
			{
				Failure = Nonempty(Failure, "the exact reservation lease was unavailable");
				return false;
			}
			try
			{
				ReservationLease = KingdomInheritanceLeaseOwner.Hold(
					The.Game == null ? "" : The.Game.GameID, Reserved, resumed);
				return ReservationLease != null;
			}
			catch (Exception ex)
			{
				resumed.Dispose();
				Failure = "the resumed reservation could not become the process owner: " + ex.Message;
				return false;
			}
		}

		private KingdomSealReservationLease GetReservationLease(KingdomSealReceipt Reserved)
		{
			if (ReservationLease != null && ReservationLease.IsHeld
				&& ReservationLease.Matches(Reserved))
			{
				return ReservationLease;
			}
			return KingdomInheritanceLeaseOwner.Get(The.Game == null ? "" : The.Game.GameID,
				Reserved);
		}

		private void HoldUnreleased(string GameId, KingdomSealReceipt Receipt,
			KingdomSealReservationLease Lease)
		{
			if (Lease == null)
			{
				return;
			}
			ReservationLease = Lease;
			try
			{
				ReservationLease = Receipt == null
					? KingdomInheritanceLeaseOwner.HoldUnknown(GameId, Lease)
					: KingdomInheritanceLeaseOwner.Hold(GameId, Receipt, Lease);
			}
			catch (Exception ex)
			{
				LogFailure("a live inheritance lease could not enter the process owner: " + ex.Message);
			}
		}

		private bool TryGetCommittedReceipt(out KingdomSealReceipt Receipt)
		{
			Receipt = null;
			KingdomSealRecord legacy;
			KingdomSealReceipt reserved;
			return !string.IsNullOrEmpty(CommittedReceiptText)
				&& CommittedReceiptText.Length <= KingdomSealFormat.MaxFileChars
				&& TryGetReservation(out legacy, out reserved)
				&& KingdomSealReceipt.TryParse(CommittedReceiptText, out Receipt)
				&& Receipt != null && Receipt.Compose() == CommittedReceiptText
				&& Receipt.State == KingdomSealReceiptState.Committed
				&& Receipt.LineageId == reserved.LineageId
				&& Receipt.LegacyId == reserved.LegacyId
				&& Receipt.TargetGameId == reserved.TargetGameId
				&& Receipt.WrittenTick >= reserved.WrittenTick;
		}

		private bool RestoreMutableReservation(out string Failure)
		{
			Failure = "";
			if (ReservedMap == null)
			{
				// No site was removed yet, or a later runtime build deliberately kept it consumed.
				return Phase == KingdomInheritancePhase.Reserved || TargetX < 0 || TargetY < 0;
			}
			if (TargetX < 0 || TargetY < 0 || string.IsNullOrEmpty(ReservedTerrainTag))
			{
				Failure = "the exact mutable coordinate or terrain tag was lost";
				return false;
			}
			if (ReservedMap.GetMutable(TargetX, TargetY) == 0)
			{
				ReservedMap.AddMutableLocation(Location2D.Get(TargetX, TargetY), ReservedTerrainTag, 1);
			}
			if (ReservedMap.GetMutable(TargetX, TargetY) != 1)
			{
				Failure = "the exact mutable cell did not return to value one";
				return false;
			}
			ReservedMap = null;
			ReservedWorldInfo = null;
			TargetX = -1;
			TargetY = -1;
			ReservedTerrainTag = "";
			return true;
		}

		private void HideDiscoverability(Zone Zone)
		{
			RemoveLocationFinders(Zone);
			JournalMapNote note = string.IsNullOrEmpty(SecretId) ? null : JournalAPI.GetMapNote(SecretId);
			if (note != null && note.ZoneID == TargetZoneId)
			{
				JournalAPI.DeleteMapNote(note);
			}
			string failure;
			if (!TryRemoveOwnedZoneName(out failure))
			{
				throw new InvalidDataException(failure);
			}
		}

		private void RestoreDiscoverability(Zone Zone)
		{
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			if (Zone == null || Zone.ZoneID != TargetZoneId
				|| !TryGetReservation(out legacy, out receipt))
			{
				throw new InvalidDataException("the exact committed target is unavailable for discovery");
			}
			EnsureOwnedMapNote(legacy);
			if (!OwnsZoneName)
			{
				if (HasAnyZoneNameFootprint())
				{
					throw new InvalidDataException(
						"the committed inherited zone has an unowned zone-name footprint");
				}
				OwnsZoneName = true;
			}
			SetOwnedZoneName();
			List<GameObject> objects = Zone.GetObjects();
			GameObject keeper = null;
			for (int i = objects.Count - 1; i >= 0; i--)
			{
				LocationFinder finder = objects[i].GetPart<LocationFinder>();
				if (finder == null || finder.ID != SecretId)
				{
					continue;
				}
				if (keeper == null)
				{
					keeper = objects[i];
					finder.Value = 1;
				}
				else
				{
					objects[i].Obliterate(null, Silent: true);
				}
			}
			if (keeper == null)
			{
				new XRL.World.ZoneBuilders.AddLocationFinder
				{
					SecretID = SecretId,
					Value = 1
				}.BuildZone(Zone);
			}
		}

		private void TryRestoreDiscoverability(Zone Zone)
		{
			try
			{
				RestoreDiscoverability(Zone);
			}
			catch (Exception ex)
			{
				BestEffortHideBrokenDiscovery(Zone);
				RecordDiscoveryFailure(ex.Message);
			}
		}

		private void EnsureOwnedMapNote(KingdomSealRecord Legacy)
		{
			if (Legacy == null)
			{
				throw new InvalidDataException("the inherited map note lost its legacy payload");
			}
			string expectedCategory = Category(Legacy);
			string expectedText = ComposeMapNote(Legacy);
			JournalMapNote note = JournalAPI.GetMapNote(SecretId);
			if (note != null && note.ZoneID != TargetZoneId)
			{
				throw new InvalidDataException("the inherited map-note id belongs to another zone");
			}
			if (note != null && !KingdomInheritanceStateRules.IsUsableOwnedMapNote(
				true, true, note.Attributes != null, note.Category, note.Text,
				expectedCategory, expectedText))
			{
				JournalAPI.DeleteMapNote(note);
				note = null;
			}
			if (note == null)
			{
				JournalAPI.AddMapNote(TargetZoneId, expectedText, expectedCategory,
					new string[4] { "settlement", "historic", "taf", "inheritance" },
					SecretId, revealed: true, sold: false, 0L, silent: true);
				note = JournalAPI.GetMapNote(SecretId);
			}
			if (!KingdomInheritanceStateRules.IsUsableOwnedMapNote(note != null,
				note != null && note.ZoneID == TargetZoneId,
				note != null && note.Attributes != null, note == null ? null : note.Category,
				note == null ? null : note.Text, expectedCategory, expectedText))
			{
				throw new InvalidDataException("the inherited map note was not recreated canonically");
			}
		}

		private void BestEffortHideBrokenDiscovery(Zone Zone)
		{
			try
			{
				RemoveLocationFinders(Zone);
			}
			catch (Exception)
			{
			}
			try
			{
				JournalMapNote note = string.IsNullOrEmpty(SecretId)
					? null : JournalAPI.GetMapNote(SecretId);
				if (note != null && note.ZoneID == TargetZoneId)
				{
					JournalAPI.DeleteMapNote(note);
				}
			}
			catch (Exception)
			{
			}
		}

		private bool TryQuarantineExact(Zone Zone, out string Failure)
		{
			Failure = "";
			if (Zone == null || Zone.ZoneID != TargetZoneId)
			{
				Failure = "the exact target zone is unavailable";
				return false;
			}
			string expected = ApplicationMarker ?? "";
			if (string.IsNullOrEmpty(expected))
			{
				KingdomSealRecord legacy;
				KingdomSealReceipt receipt;
				if (!TryGetReservation(out legacy, out receipt)
					|| !KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy, receipt,
						TargetZoneId, KingdomInheritEngine.ReconstructionVersion, out expected))
				{
					Failure = "the exact application marker could not be recomputed";
					return false;
				}
			}
			string zoneMarker = Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			if (!string.IsNullOrEmpty(zoneMarker) && zoneMarker != expected)
			{
				Failure = "the target carries a different inheritance marker";
				return false;
			}
			List<GameObject> objects = Zone.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				string marker = objects[i].GetStringProperty(
					KingdomInheritEngine.ObjectMarkerProperty, "") ?? "";
				if (!string.IsNullOrEmpty(marker) && marker != expected)
				{
					Failure = "the target carries a foreign marked object";
					return false;
				}
			}
			for (int i = objects.Count - 1; i >= 0; i--)
			{
				if (objects[i].GetStringProperty(KingdomInheritEngine.ObjectMarkerProperty, "")
					== expected)
				{
					objects[i].Obliterate(null, Silent: true);
				}
			}
			if (zoneMarker == expected)
			{
				Zone.RemoveZoneProperty(KingdomInheritEngine.ZoneMarkerProperty);
			}
			RemoveLocationFinders(Zone);
			objects = Zone.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				if (!string.IsNullOrEmpty(objects[i].GetStringProperty(
					KingdomInheritEngine.ObjectMarkerProperty, "")))
				{
					Failure = "a marked inherited object survived quarantine";
					return false;
				}
			}
			if (!string.IsNullOrEmpty(Zone.GetZoneProperty(
				KingdomInheritEngine.ZoneMarkerProperty, "") ?? ""))
			{
				Failure = "the inherited zone marker survived quarantine";
				return false;
			}
			return true;
		}

		private void RemoveLocationFinders(Zone Zone)
		{
			if (Zone == null || Zone.ZoneID != TargetZoneId || string.IsNullOrEmpty(SecretId))
			{
				return;
			}
			List<GameObject> objects = Zone.GetObjects();
			for (int i = objects.Count - 1; i >= 0; i--)
			{
				LocationFinder finder = objects[i].GetPart<LocationFinder>();
				if (finder != null && finder.ID == SecretId)
				{
					objects[i].Obliterate(null, Silent: true);
				}
			}
		}

		private void SetOwnedZoneName()
		{
			if (!OwnsZoneName || !HasCompatibleOwnedZoneNameSubset())
			{
				throw new InvalidDataException("the target zone-name subset is not inheritance-owned");
			}
			if (!The.Game.HasStringGameState("ZoneName_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneDisplayName(TargetZoneId, SiteName, Sync: false);
			}
			RequireCompatibleOwnedZoneNameSubset();
			if (!The.Game.HasStringGameState("ZoneNameContext_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneNameContext(TargetZoneId, "", Sync: false);
			}
			RequireCompatibleOwnedZoneNameSubset();
			if (!The.Game.HasBooleanGameState("ZoneProperName_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneHasProperName(TargetZoneId, true);
			}
			RequireCompatibleOwnedZoneNameSubset();
			if (!The.Game.HasStringGameState("ZoneIndefiniteArticle_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneIndefiniteArticle(TargetZoneId, "");
			}
			RequireCompatibleOwnedZoneNameSubset();
			if (!The.Game.HasStringGameState("ZoneDefiniteArticle_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneDefiniteArticle(TargetZoneId, "");
			}
			RequireCompatibleOwnedZoneNameSubset();
			The.ZoneManager.SynchronizeZoneName(TargetZoneId);
			if (!HasExactOwnedZoneName())
			{
				throw new InvalidDataException("the inherited zone-name install did not complete exactly");
			}
		}

		private void RequireCompatibleOwnedZoneNameSubset()
		{
			if (!HasCompatibleOwnedZoneNameSubset())
			{
				throw new InvalidDataException(
					"the target zone-name subset changed during inheritance installation");
			}
		}

		private bool HasCompatibleOwnedZoneNameSubset()
		{
			if (The.Game == null || string.IsNullOrEmpty(TargetZoneId)
				|| string.IsNullOrEmpty(SiteName))
			{
				return false;
			}
			string nameKey = "ZoneName_" + TargetZoneId;
			string contextKey = "ZoneNameContext_" + TargetZoneId;
			string indefiniteKey = "ZoneIndefiniteArticle_" + TargetZoneId;
			string definiteKey = "ZoneDefiniteArticle_" + TargetZoneId;
			string properKey = "ZoneProperName_" + TargetZoneId;
			return KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
				The.Game.HasStringGameState(nameKey), The.Game.GetStringGameState(nameKey, null),
				The.Game.HasStringGameState(contextKey),
				The.Game.GetStringGameState(contextKey, null),
				The.Game.HasStringGameState(indefiniteKey),
				The.Game.GetStringGameState(indefiniteKey, null),
				The.Game.HasStringGameState(definiteKey),
				The.Game.GetStringGameState(definiteKey, null),
				The.Game.HasBooleanGameState(properKey),
				The.Game.GetBooleanGameState(properKey), SiteName);
		}

		private bool HasExactOwnedZoneName()
		{
			if (The.Game == null || string.IsNullOrEmpty(TargetZoneId))
			{
				return false;
			}
			string contextKey = "ZoneNameContext_" + TargetZoneId;
			string indefiniteKey = "ZoneIndefiniteArticle_" + TargetZoneId;
			string definiteKey = "ZoneDefiniteArticle_" + TargetZoneId;
			string properKey = "ZoneProperName_" + TargetZoneId;
			return KingdomInheritanceStateRules.IsExactZoneNameFootprint(
				The.Game.GetStringGameState("ZoneName_" + TargetZoneId, null),
				The.Game.HasStringGameState(contextKey),
				The.Game.GetStringGameState(contextKey, null),
				The.Game.HasStringGameState(indefiniteKey),
				The.Game.GetStringGameState(indefiniteKey, null),
				The.Game.HasStringGameState(definiteKey),
				The.Game.GetStringGameState(definiteKey, null),
				The.Game.HasBooleanGameState(properKey),
				The.Game.GetBooleanGameState(properKey), SiteName);
		}

		private bool HasAnyZoneNameFootprint()
		{
			return The.Game != null && !string.IsNullOrEmpty(TargetZoneId)
				&& (The.Game.HasStringGameState("ZoneName_" + TargetZoneId)
					|| The.Game.HasStringGameState("ZoneNameContext_" + TargetZoneId)
					|| The.Game.HasStringGameState("ZoneIndefiniteArticle_" + TargetZoneId)
					|| The.Game.HasStringGameState("ZoneDefiniteArticle_" + TargetZoneId)
					|| The.Game.HasBooleanGameState("ZoneProperName_" + TargetZoneId));
		}

		private bool TryRemoveOwnedZoneName(out string Failure)
		{
			Failure = "";
			if (!HasAnyZoneNameFootprint())
			{
				if (OwnsZoneName)
				{
					try
					{
						The.ZoneManager.SynchronizeZoneName(TargetZoneId);
					}
					catch (Exception ex)
					{
						// Set/remove callbacks can throw after their base-state write. With all five
						// keys still absent, exact reproof—not callback completion—is authoritative.
						try
						{
							LogFailure("the cleared inherited zone-name synchronization threw after "
								+ "exact absence proof: " + ex.Message);
						}
						catch (Exception)
						{
						}
					}
				}
				OwnsZoneName = false;
				return true;
			}
			if (!OwnsZoneName)
			{
				// A name that appeared before our provenance bit was set is foreign. Preserve it;
				// it does not prevent proving absence of inheritance-owned artifacts.
				return true;
			}
			if (!HasCompatibleOwnedZoneNameSubset())
			{
				Failure = "the target zone-name footprint changed after inheritance installed it";
				return false;
			}
			try
			{
				The.Game.RemoveStringGameState("ZoneName_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.Game.RemoveStringGameState("ZoneNameContext_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.Game.RemoveStringGameState("ZoneIndefiniteArticle_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.Game.RemoveStringGameState("ZoneDefiniteArticle_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.Game.RemoveBooleanGameState("ZoneProperName_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.ZoneManager.SynchronizeZoneName(TargetZoneId);
			}
			catch (Exception ex)
			{
				if (KingdomInheritanceStateRules.CanClearZoneNameOwnership(
					HasAnyZoneNameFootprint()))
				{
					OwnsZoneName = false;
					return true;
				}
				Failure = "the exact inherited zone-name cleanup tore: " + ex.Message;
				return false;
			}
			if (HasAnyZoneNameFootprint())
			{
				Failure = "the exact inherited zone-name footprint survived cleanup";
				return false;
			}
			OwnsZoneName = false;
			return true;
		}

		private bool TryGetReservation(out KingdomSealRecord Legacy, out KingdomSealReceipt Receipt)
		{
			Legacy = null;
			Receipt = null;
			KingdomSealFault fault;
			string detail;
			return LegacyText.Length > 0 && ReceiptText.Length > 0
				&& LegacyText.Length <= KingdomSealFormat.MaxFileChars
				&& ReceiptText.Length <= KingdomSealFormat.MaxFileChars
				&& KingdomSealRecord.TryParse(LegacyText, out Legacy, out fault, out detail)
				&& KingdomSealReceipt.TryParse(ReceiptText, out Receipt)
				&& CanonicalReservation(Legacy, Receipt, The.Game == null ? "" : The.Game.GameID)
				&& Legacy.Compose() == LegacyText && Receipt.Compose() == ReceiptText;
		}

		private static bool CanonicalReservation(KingdomSealRecord Legacy,
			KingdomSealReceipt Receipt, string TargetGameId)
		{
			return Legacy != null && Receipt != null
				&& Legacy.Status == KingdomSealStatus.Promoted && Legacy.IsResolved
				&& Receipt.State == KingdomSealReceiptState.Reserved
				&& Receipt.LineageId == Legacy.LineageId && Receipt.LegacyId == Legacy.LegacyId
				&& Receipt.TargetGameId == TargetGameId;
		}

		private void SetRepair(string Detail)
		{
			FailureDetail = Bound(Detail, MaxFailureChars);
			if (Phase != KingdomInheritancePhase.RepairRequired)
			{
				if (KingdomInheritanceStateRules.CanTransition(Phase,
					KingdomInheritancePhase.RepairRequired))
				{
					PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
				}
				else
				{
					PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
				}
			}
			try
			{
				LogFailure(FailureDetail);
			}
			catch (Exception)
			{
				// Neutralization must not be undone by a diagnostic sink failure.
			}
		}

		private void Transition(KingdomInheritancePhase Next)
		{
			if (!KingdomInheritanceStateRules.CanTransition(Phase, Next))
			{
				SetRepair("the inherited target attempted an invalid phase transition from "
					+ Phase.ToString() + " to " + Next.ToString());
				return;
			}
			PhaseValue = (int)Next;
		}

		private void AnnounceFailure()
		{
			if (FailureAnnounced || string.IsNullOrEmpty(FailureDetail)
				|| (Phase != KingdomInheritancePhase.Refused
					&& Phase != KingdomInheritancePhase.RepairRequired))
			{
				return;
			}
			FailureAnnounced = true;
			MessageQueue.AddPlayerMessage("&yAn inherited kingdom could not enter this world: &Y"
				+ FailureDetail);
		}

		private void ResetNewGame()
		{
			SerializationVersion = CurrentSerializationVersion;
			PhaseValue = (int)KingdomInheritancePhase.Empty;
			LegacyText = "";
			ReceiptText = "";
			CommittedReceiptText = "";
			TargetZoneId = "";
			TargetTerrainBlueprint = "";
			TargetTerrainRank = -1;
			SecretId = "";
			SiteName = "";
			FailureDetail = "";
			ApplyStatusValue = -1;
			ApplyFaultValue = -1;
			ApplicationMarker = "";
			FailureAnnounced = false;
			ReleasePending = false;
			OwnsSkipTerrainBuilders = false;
			OwnsNoBiomes = false;
			OwnsZoneName = false;
			RecoveryDisabled = false;
			RetryAuthorized = false;
			ProfileReceiptWasCommitted = false;
			ProfileCommittedReceipt = null;
			ReservationLease = null;
			ReservedMap = null;
			ReservedWorldInfo = null;
			TargetX = -1;
			TargetY = -1;
			ReservedTerrainTag = "";
		}

		private void DisableRecovery(string Detail)
		{
			SerializationVersion = CurrentSerializationVersion;
			PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
			LegacyText = "";
			ReceiptText = "";
			CommittedReceiptText = "";
			TargetZoneId = "";
			TargetTerrainBlueprint = "";
			TargetTerrainRank = -1;
			SecretId = "";
			SiteName = "";
			FailureDetail = Bound(Detail, MaxFailureChars);
			ApplyStatusValue = -1;
			ApplyFaultValue = -1;
			ApplicationMarker = "";
			FailureAnnounced = false;
			ReleasePending = false;
			OwnsSkipTerrainBuilders = false;
			OwnsNoBiomes = false;
			OwnsZoneName = false;
			RecoveryDisabled = true;
			RetryAuthorized = false;
			ProfileReceiptWasCommitted = false;
			ProfileCommittedReceipt = null;
			ReservationLease = null;
			ReservedMap = null;
			ReservedWorldInfo = null;
			TargetX = -1;
			TargetY = -1;
			ReservedTerrainTag = "";
			try
			{
				LogFailure(FailureDetail);
			}
			catch (Exception)
			{
				// Neutralization must not be undone by a diagnostic sink failure.
			}
		}

		private static string ComposeMapNote(KingdomSealRecord Legacy)
		{
			string name = KingdomSealRules.SanitizeText(Legacy.SettlementName,
				KingdomSealRecord.MaxNameChars);
			if (string.IsNullOrEmpty(name))
			{
				name = "an inherited settlement";
			}
			return "the inherited seat of " + name;
		}

		private static string Category(KingdomSealRecord Legacy)
		{
			return Legacy.InheritedState <= (int)KingdomRules.InheritedState.Faded
				? "Settlements" : "Historic Sites";
		}

		private static string Bound(string Value, int MaxChars)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return "";
			}
			return Value.Length <= MaxChars ? Value : Value.Substring(0, MaxChars);
		}

		private static string Nonempty(string Value, string Fallback)
		{
			return string.IsNullOrEmpty(Value) ? Fallback : Value;
		}

		private static string AppendFailure(string Existing, string Addition)
		{
			if (string.IsNullOrEmpty(Addition))
			{
				return Existing ?? "";
			}
			return Bound(string.IsNullOrEmpty(Existing) ? Addition : Existing + "; " + Addition,
				MaxFailureChars);
		}

		private static void LogFailure(string Detail)
		{
			MetricsManager.LogWarning("ThousandAndFirst inheritance: " + Detail);
		}
	}
}
