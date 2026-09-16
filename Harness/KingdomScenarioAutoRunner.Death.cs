using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst
{
	/// <summary>
	/// Death is terminal, never a hang.
	/// <para>
	/// NATIVE RUN 36 (13122f0). The founder died mid-<c>advance</c> ("bitten to death"); succession
	/// refused (HeirUnreachable); Player.log went silent and the journal stopped at the
	/// <c>advance OK</c> row for ~50 minutes. The runner's only wake-up is the player's own
	/// <c>BeginTakeActionEvent</c>, and after <c>GameObject.Die</c> sets
	/// <c>XRLCore.Core.Game.Running = false</c> (decompile 2.0.211.51 XRL/World/GameObject.cs:15079)
	/// <c>ActionManager.RunSegment</c> never raises that event again
	/// (XRL/Core/ActionManager.cs:731 <c>if (!game.Running) return;</c>). Polling inside the pump
	/// could therefore never see the death; the death has to come to the runner.
	/// </para>
	/// <para>
	/// SEAM. <c>AfterDieEvent</c>, registered through the same player registrar the runner already
	/// uses for its action seam (RegisterPlayer). <c>GameObject.Die</c> sends it at
	/// XRL/World/GameObject.cs:15025, BEFORE the blocking death popup (:15066) and before
	/// <c>Running</c> is lowered (:15079), so the terminal row is on disk while the process is
	/// still writing. It is the seam the production seal already observes (Core/KingdomSeal.cs
	/// RegisterPlayer), so the runner sees exactly the death production sees. The registered body
	/// set is remembered - EVERY body this run registered, never removed on re-register - because
	/// KingdomSuccession re-bodies the player inside this same event (SetPlayerBodyAndRebindAll ->
	/// TrySetBodyAndRebindPlayerSystems re-registers every player system on the heir), after which
	/// the founder is neither IsPlayer() nor the latest registered body. The decision itself is
	/// the engine-free KingdomScenarioDeathRules.ShouldRecordDeath, value-tested for that shape.
	/// </para>
	/// <para>
	/// ROW. <c>SCRIPT-STOPPED</c> whose message opens with <see cref="DiedPrefix" /> followed by the
	/// engine's own death category (<c>Physics.LastDeathCategory</c>, the same reading
	/// Core/KingdomSeal.Utilities.cs takes) and the reason; Tools/check-quickstart-lifecycle.py
	/// classifies that prefix as the distinct FAIL class <c>founder-died</c>. The row also names
	/// what the engine exposes on the event (decompile 2.0.211.51 XRL/World/IDeathEvent.cs:6-18:
	/// <c>Killer</c>, <c>Weapon</c>, <c>Projectile</c>, <c>KillerText</c>, <c>Reason</c>,
	/// <c>ThirdPersonReason</c>, <c>Accidental</c>) - blueprint and id of the killer and weapon,
	/// the reason text, and the accidental flag - since run 39's investigation could not name a
	/// biter from Player.log and the category alone. Any pending advance is
	/// discarded first, which also releases the founder guard; the row records the guard state AT
	/// death, so a death under an armed guard is visible as such rather than explained away.
	/// </para>
	/// </summary>
	public sealed partial class KingdomScenarioAutoRunner
	{
		/// <summary>Opens the message of the terminal row a player death lands.</summary>
		internal const string DiedPrefix = "DIED ";

		/// <summary>The body RegisterPlayer last bound; session state only.</summary>
		[NonSerialized]
		private GameObject RegisteredPlayer;

		/// <summary>Every body RegisterPlayer ever bound this run (founder, then each heir);
		/// never removed on re-register or unregister. Session state only.</summary>
		[NonSerialized]
		private readonly HashSet<GameObject> RegisteredBodies = new HashSet<GameObject>();

		public override bool HandleEvent(AfterDieEvent E)
		{
			GameObject dying = E?.Dying;
			if (dying != null && KingdomScenarioDeathRules.ShouldRecordDeath(dying.IsPlayer(),
				ReferenceEquals(dying, RegisteredPlayer) || RegisteredBodies.Contains(dying),
				Verbs != null))
				KingdomSystem.Guard("scenario auto-runner death", delegate { Died(E, dying); });
			return base.HandleEvent(E);
		}

		/// <summary>One terminal row, then the run is closed exactly as a refusal closes it.</summary>
		private void Died(AfterDieEvent E, GameObject Dying)
		{
			string guard = KingdomScenarioFounderGuard.Describe(Dying,
				KingdomScenarioFounderGuard.Armed ? "armed-at-death" : "unarmed-at-death");
			string category = Dying.Physics?.LastDeathCategory;
			if (string.IsNullOrEmpty(category)) category = "unknown";
			string reason = !string.IsNullOrEmpty(E.ThirdPersonReason) ? E.ThirdPersonReason
				: (!string.IsNullOrEmpty(E.Reason) ? E.Reason : "unstated");
			string killer = "killer=" + Name(E.Killer) + "; killerText="
				+ (string.IsNullOrEmpty(E.KillerText) ? "unstated" : E.KillerText)
				+ "; weapon=" + Name(E.Weapon) + "; projectile=" + Name(E.Projectile)
				+ "; accidental=" + E.Accidental;
			// Explicit and load-bearing even though Finish cancels again: Finish stops the travel
			// driver FIRST, and if that throws the guard must already be down (Release is idempotent).
			KingdomScenarioAdvance.Cancel();
			Finish(StoppedRow, false, KingdomScenarioRules.Bounded(DiedPrefix + category
				+ "; reason=" + reason + "; " + killer + "; " + guard));
		}

		/// <summary>Blueprint and id of an event participant, or "none" when the engine set none.</summary>
		private static string Name(GameObject Object)
		{
			if (Object == null) return "none";
			return Object.Blueprint + "#" + (string.IsNullOrEmpty(Object.IDIfAssigned)
				? "unassigned" : Object.IDIfAssigned);
		}
	}
}
