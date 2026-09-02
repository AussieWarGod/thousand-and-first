using System;
using XRL;
using XRL.Core;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Save/load and completed-zone hooks absent from IGameStateSingleton. Persisted with the target
	/// save; on load it hands the game identity to KingdomInheritanceLeaseOwner, records the
	/// KingdomInheritancePrimaryLoad classification in KingdomSystem's one pending-resume slot,
	/// retries that resume each turn until the gate opens, and forwards built target zones.
	/// </summary>
	[Serializable]
	public sealed class KingdomInheritanceLifecycle : IPlayerSystem
	{
		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterGameLoadedEvent.ID);
			Registrar.Register(EndTurnEvent.ID);
			Registrar.Register(ZoneBuiltEvent.ID);
		}

		public override void AfterLoad(XRLGame Game)
		{
			base.AfterLoad(Game);
			KingdomInheritanceLeaseOwner.BeginGame(Game == null ? "" : Game.GameID);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			string sourceFailure;
			KingdomInheritanceLoadKind loadKind = KingdomInheritancePrimaryLoad.TryConsume(
				The.Game, out sourceFailure);
			// Capture before the gate: LoadGame's AsyncLocal evidence exists only for this event.
			// This is classification, not inheritance work or external/profile mutation.
			KingdomSystem kingdom = The.Game?.GetSystem<KingdomSystem>();
			if (kingdom != null)
			{
				// KingdomSystem's additive named fields are one serialized, overwrite-only slot.
				// Reload replaces the slot with the newer load's exact classification; no queue forms.
				kingdom.InheritanceResumePending = true;
				kingdom.InheritancePendingLoadKindValue = (int)loadKind;
				kingdom.InheritancePendingLoadSourceFailure = sourceFailure ?? "";
			}
			TryResumePendingLoad(kingdom);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EndTurnEvent E)
		{
			TryResumePendingLoad(The.Game?.GetSystem<KingdomSystem>());
			return base.HandleEvent(E);
		}

		private static void TryResumePendingLoad(KingdomSystem kingdom)
		{
			KingdomInheritanceLoadKind loadKind;
			string sourceFailure;
			if (kingdom == null || !KingdomInheritanceResumeRules.TryConsume(
				kingdom.InheritanceResumePending,
				kingdom.InheritancePendingLoadKindValue,
				kingdom.InheritancePendingLoadSourceFailure,
				KingdomMaster.AutomaticWorkAllowed(kingdom), out loadKind,
				out sourceFailure)) return;

			// Retire runtime authority before entering the idempotent inheritance transaction. A save
			// before this wake retains the slot; a save after it cannot replay the same load recovery.
			kingdom.InheritanceResumePending = false;
			kingdom.InheritancePendingLoadKindValue = (int)KingdomInheritanceLoadKind.Unknown;
			kingdom.InheritancePendingLoadSourceFailure = "";
			KingdomInheritanceState.Instance?.ResumeAfterLoad(loadKind, sourceFailure);
		}

		public override bool HandleEvent(ZoneBuiltEvent E)
		{
			KingdomInheritanceState.Instance?.HandleTargetZoneBuilt(E?.Zone);
			return base.HandleEvent(E);
		}
	}
}
