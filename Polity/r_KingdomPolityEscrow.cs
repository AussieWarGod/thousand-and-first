using System;
using XRL.World;

namespace XRL.World.Parts
{
	/// <summary>Short-lived visible lease on one consented realm-owned ground object.</summary>
	[Serializable]
	public sealed class r_KingdomPolityEscrow : IPart
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public string ProjectionId = "";
		public string IncidentPlanId = "";
		public string StakeRef = "";
		public string ActorId = "";
		public string ObjectId = "";
		public string Blueprint = "";
		public string DisplayName = "";
		public int Count;
		public string Owner = "";
		public string ZoneId = "";
		public int X;
		public int Y;
		public string SnapshotDigest = "";
		public string AppliedDigest = "";

		public override void Register(GameObject Object, IEventRegistrar Registrar)
		{
			Registrar.Register("CanBeTaken");
			base.Register(Object, Registrar);
		}

		public override bool FireEvent(Event E)
		{
			if (E != null && E.ID == "CanBeTaken") return false;
			return base.FireEvent(E);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeApplyDamageEvent.ID ||
				ID == BeforeDestroyObjectEvent.ID || ID == CanBeReplicatedEvent.ID ||
				ID == CanBeInvoluntarilyMovedEvent.ID || ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(BeforeApplyDamageEvent E) { return false; }
		public override bool HandleEvent(BeforeDestroyObjectEvent E) { return false; }
		public override bool HandleEvent(CanBeReplicatedEvent E) { return false; }
		public override bool HandleEvent(CanBeInvoluntarilyMovedEvent E) { return false; }

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append("\n{{rules|Held briefly by one consented polity escrow. " +
				"It remains here, cannot be harmed or taken, and will be released unchanged.}}");
			return base.HandleEvent(E);
		}

		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			ParentObject?.RemovePart(this);
		}

		public override void Write(GameObject Basis, SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(r_KingdomPolityEscrow));
		}

		public override void Read(GameObject Basis, SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(r_KingdomPolityEscrow));
			ProjectionId = ProjectionId ?? ""; IncidentPlanId = IncidentPlanId ?? "";
			StakeRef = StakeRef ?? ""; ActorId = ActorId ?? ""; ObjectId = ObjectId ?? "";
			Blueprint = Blueprint ?? ""; DisplayName = DisplayName ?? "";
			Owner = Owner ?? ""; ZoneId = ZoneId ?? "";
			SnapshotDigest = SnapshotDigest ?? ""; AppliedDigest = AppliedDigest ?? "";
		}
	}
}
