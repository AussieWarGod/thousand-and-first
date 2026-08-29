using System;
using XRL;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>Owned endpoint-body interaction/death bridge; copied bodies are always inert.</summary>
	[Serializable]
	public sealed class r_KingdomPolityCohortBody : IPart
	{
		private const string AnswerCommand = "r_AnswerPolityVisit";
		public string RealmId;
		public string CohortId;
		public ThousandAndFirst.KingdomPolityCohortPurpose Purpose;
		public bool Representative;
		public bool Inert;
		[NonSerialized] private ThousandAndFirst.KingdomPolityEndpointRuntime.DeathWitness PendingDeath;
		[NonSerialized] private bool DeathCallbackInFlight;
		[NonSerialized] private string CleanupProjectionId;
		[NonSerialized] private string CleanupObjectId;
		[NonSerialized] private string CleanupRealmId;
		[NonSerialized] private string CleanupCohortId;
		[NonSerialized] private int CleanupOrdinal = -1;
		[NonSerialized] private Cell CleanupCell;
		[NonSerialized] private byte CleanupCohortPhase;
		[NonSerialized] private byte CleanupProjectionPhase;
		[NonSerialized] private string CleanupIntentKey;
		[NonSerialized] private string CleanupIntentValue;

		internal bool IsDeathCallbackInFlight { get { return DeathCallbackInFlight; } }
		internal void RecoverDeathCallbackGuard() { DeathCallbackInFlight = false; PendingDeath = null; }

		internal void ArmCleanup(string ProjectionId, string ObjectId, int Ordinal, Cell Cell,
			byte CohortPhase, byte ProjectionPhase, string IntentKey, string IntentValue)
		{
			CleanupRealmId = RealmId; CleanupCohortId = CohortId;
			CleanupProjectionId = ProjectionId; CleanupObjectId = ObjectId;
			CleanupOrdinal = Ordinal; CleanupCell = Cell;
			CleanupCohortPhase = CohortPhase; CleanupProjectionPhase = ProjectionPhase;
			CleanupIntentKey = IntentKey; CleanupIntentValue = IntentValue;
		}

		internal void ClearCleanup()
		{
			CleanupRealmId = CleanupCohortId = CleanupProjectionId = CleanupObjectId =
				CleanupIntentKey = CleanupIntentValue = null;
			CleanupOrdinal = -1; CleanupCell = null;
			CleanupCohortPhase = CleanupProjectionPhase = 0;
		}

		public r_KingdomPolityCohortBody() { }

		public r_KingdomPolityCohortBody(string RealmId, string CohortId,
			ThousandAndFirst.KingdomPolityCohortPurpose Purpose, bool Representative)
		{
			this.RealmId = RealmId; this.CohortId = CohortId; this.Purpose = Purpose;
			this.Representative = Representative;
		}

		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == CanBeReplicatedEvent.ID ||
				ID == GetInventoryActionsEvent.ID || ID == InventoryActionEvent.ID ||
				ID == EarlyBeforeDeathRemovalEvent.ID || ID == BeforeDestroyObjectEvent.ID ||
				ID == OnDestroyObjectEvent.ID;
		}

		public override bool HandleEvent(CanBeReplicatedEvent E) { return false; }

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			if (!Inert && Representative && ThousandAndFirst.KingdomPolityVisitInteraction.CanAnswer(
				ParentObject, E.Actor, CohortId))
			{
				string label = ThousandAndFirst.KingdomPolityVisitInteraction.ActionLabel(Purpose);
				string verb = ThousandAndFirst.KingdomPolityVisitInteraction.ActionVerb(Purpose);
				E.AddAction(label, verb, AnswerCommand, null, 'h', FireOnActor: false, 5);
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == AnswerCommand && !Inert && E.Actor != null && E.Actor.IsPlayer())
			{
				ThousandAndFirst.KingdomPolityVisitInteraction.Answer(
					ParentObject, E.Actor, CohortId); E.RequestInterfaceExit();
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(EarlyBeforeDeathRemovalEvent E)
		{
			PendingDeath = null;
			DeathCallbackInFlight = false;
			if (Inert || !ReferenceEquals(E.Dying, ParentObject)) return base.HandleEvent(E);
			if (!ThousandAndFirst.KingdomPolityEndpointRuntime.TryPrepareVisibleDeath(
				ParentObject, RealmId, CohortId, E.Killer, out PendingDeath, out string failure))
			{
				ThousandAndFirst.KingdomLog.Log("polity: visible death remained contested (" +
					(failure ?? "physical proof refused") + ")");
			}
			else DeathCallbackInFlight = true;
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(BeforeDestroyObjectEvent E)
		{
			if (!ReferenceEquals(E.Object, ParentObject) || Inert ||
				string.IsNullOrEmpty(RealmId) || string.IsNullOrEmpty(CohortId))
				return base.HandleEvent(E);
			if (CleanupRealmId == RealmId && CleanupCohortId == CohortId &&
				ReferenceEquals(CleanupCell, ParentObject.CurrentCell) && CleanupObjectId ==
				ParentObject.IDIfAssigned && ThousandAndFirst.KingdomPolityEndpointRuntime.
				TryAuthorizePreparedCleanup(ParentObject, RealmId, CohortId, CleanupProjectionId,
					CleanupObjectId, CleanupOrdinal, CleanupCell, CleanupCohortPhase,
					CleanupProjectionPhase, CleanupIntentKey, CleanupIntentValue))
			{
				ClearCleanup(); return base.HandleEvent(E);
			}
			ClearCleanup();
			if (PendingDeath == null)
			{
				ThousandAndFirst.KingdomLog.Log(
					"polity: owned cohort death removal blocked without exact prepared intent");
				return false;
			}
			if (ThousandAndFirst.KingdomPolityEndpointRuntime.TryReproveVisibleDeath(
				PendingDeath, ParentObject, out string failure)) return base.HandleEvent(E);
			ThousandAndFirst.KingdomLog.Log("polity: death removal blocked by changed custody (" +
				(failure ?? "physical proof refused") + "; intent retained)");
			return false;
		}

		public override bool HandleEvent(OnDestroyObjectEvent E)
		{
			if (PendingDeath == null || !ReferenceEquals(E.Object, ParentObject))
				return base.HandleEvent(E);
			ThousandAndFirst.KingdomPolityEndpointRuntime.DeathWitness witness = PendingDeath;
			PendingDeath = null;
			if (!ThousandAndFirst.KingdomPolityEndpointRuntime.TryCommitVisibleDeathWitness(
				witness, ParentObject, out string failure))
			{
				ThousandAndFirst.KingdomLog.Log("polity: death removal emitted no witness (" +
					(failure ?? "physical proof refused") + ")");
				return base.HandleEvent(E);
			}
			return base.HandleEvent(E);
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			RealmId = null; CohortId = null;
			Purpose = ThousandAndFirst.KingdomPolityCohortPurpose.None;
			Representative = false; Inert = true; PendingDeath = null;
			DeathCallbackInFlight = false; ClearCleanup();
			ParentObject?.RemoveStringProperty(
				ThousandAndFirst.KingdomPolityEndpointRuntime.CohortOwnerProperty);
			ParentObject?.RemoveStringProperty(
				ThousandAndFirst.KingdomPolityEndpointRuntime.CohortProperty);
			ParentObject?.RemoveStringProperty(
				ThousandAndFirst.KingdomPolityEndpointRuntime.ProjectionProperty);
			ParentObject?.RemoveIntProperty(
				ThousandAndFirst.KingdomPolityEndpointRuntime.MemberOrdinalProperty);
			ParentObject?.RemoveIntProperty(
				ThousandAndFirst.KingdomPolityEndpointRuntime.CohortXProperty);
			ParentObject?.RemoveIntProperty(
				ThousandAndFirst.KingdomPolityEndpointRuntime.CohortYProperty);
			ParentObject?.RemoveStringProperty(
				ThousandAndFirst.KingdomPolityNpcRuntime.PolityProperty);
			ParentObject?.RemoveStringProperty(
				ThousandAndFirst.KingdomPolityNpcRuntime.ProfileProperty);
			ParentObject?.RemoveStringProperty(
				ThousandAndFirst.KingdomPolityNpcRuntime.ResolverProperty);
			ParentObject?.RemoveStringProperty(
				ThousandAndFirst.KingdomPolityNpcRuntime.RoleProperty);
			ParentObject?.RemoveStringProperty(
				ThousandAndFirst.KingdomPolityNpcRuntime.FigureProperty);
			ParentObject?.RemoveIntProperty(
				ThousandAndFirst.KingdomPolityNpcRuntime.ContestedProperty);
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
		}
	}
}
