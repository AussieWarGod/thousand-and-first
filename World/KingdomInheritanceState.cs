using System;
using System.Collections.Generic;
using System.IO;
using Genkit;
using Qud.API;
using XRL;
using XRL.CharacterBuilds.Qud;
using XRL.Messages;
using XRL.UI;
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
	public sealed partial class KingdomInheritanceState : IGameStateSingleton, IComposite
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
				TutorialManager.currentStep != null) || !LegacyImportEnabled())
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

		private static bool LegacyImportEnabled()
		{
			// Fail closed when the option is absent or unreadable. This is read before the
			// profile coordinator is required, so Off cannot reserve, decline, or consume a seal.
			return Options.GetOption("r_TAF_OptionLegacyImport", "No") == "Yes";
		}

	}
}
