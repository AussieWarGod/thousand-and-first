using System;
using System.Collections.Generic;
using System.Globalization;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		private const int MaximumFrozenCustodyObjects = 128;

		private enum FrozenCustodyKind : byte
		{
			Natural = 1,
			ExactGear = 2,
			Foreign = 3
		}

		private sealed class FrozenCustodyNode
		{
			internal GameObject Object;
			internal GameObject Parent;
			internal FrozenCustodyKind Kind;
			internal int GearOrdinal = -1;
			internal List<FrozenCustodyNode> Children = new List<FrozenCustodyNode>();
		}

		private sealed class FrozenCustodyPlan
		{
			internal GameObject Body;
			internal Cell Cell;
			internal KingdomPolityNpcSpec Spec;
			internal int MemberOrdinal;
			internal List<FrozenCustodyNode> Roots = new List<FrozenCustodyNode>();
		}

		private static bool TryResolveFrozenSpec(KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Cohort, int Ordinal, out KingdomPolityNpcSpec Spec,
			out string FigureId, out string Failure)
		{
			Spec = null; FigureId = null; Failure = null;
			KingdomPolityProfileRevision profile = KingdomPolityAuthority.Profile(Ledger,
				Cohort?.ProfileId, Cohort?.ProfileRevision ?? 0);
			KingdomPolityCohortMember member = Cohort != null && Ordinal >= 0 && Ordinal <
				Cohort.ResolvedMembers.Count ? Cohort.ResolvedMembers[Ordinal] : null;
			if (profile == null || member == null || member.Ordinal != Ordinal ||
				!KingdomPolityCohortRules.TryParseSignature(member.SignatureKey,
					out string role, out string resolver, out FigureId) ||
				!KingdomPolityNpcRules.TryResolvePinned(profile, role, Ordinal,
					Cohort.RulesVersion, Cohort.MinimumLevel, Cohort.MaximumLevel,
					out Spec, out Failure) ||
				Spec.ResolverDigest != resolver || Spec.ResolverDigest != member.LoadoutKey ||
				Spec.BodyBlueprint != member.BlueprintKey)
			{
				Failure = Failure ?? "cohort body no longer matches its frozen NPC resolver";
				Spec = null; return false;
			}
			return true;
		}

		private static bool ExactPreparedBody(GameObject Body, Zone Zone, string RealmId,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			KingdomPolityNpcSpec Spec, string FigureId, int Ordinal, out string Failure)
		{
			Failure = null;
			XRL.World.Parts.r_KingdomPolityCohortBody part = Body == null ? null :
				Body.GetPart<XRL.World.Parts.r_KingdomPolityCohortBody>();
			string profile = Spec == null ? null : Spec.ProfileId + ":" +
				Spec.ProfileRevision.ToString(CultureInfo.InvariantCulture);
			if (Body == null || !TryFindResidentObject(Body.IDIfAssigned,
				out GameObject resident, out Failure)) return false;
			return GameObject.Validate(Body) && Zone != null && Cohort != null && Receipt != null &&
				Spec != null && Body.Count == 1 && Body.Blueprint == Spec.BodyBlueprint &&
				Body.Brain != null && Body.CurrentCell != null && ReferenceEquals(Body.CurrentZone, Zone) &&
				Body.InInventory == null && Body.Equipped == null &&
				!Body.Brain.Wanders && !Body.Brain.WandersRandomly && Body.Brain.Staying &&
				Body.IDIfAssigned == KingdomPolityCohortRules.PreparedObjectId(Cohort, Ordinal) &&
				ReferenceEquals(resident, Body) &&
				Body.GetStringProperty(CohortOwnerProperty) == Cohort.PolityId &&
				Body.GetStringProperty(CohortProperty) == Cohort.CohortId &&
				Body.GetStringProperty(ProjectionProperty) == Receipt.ProjectionId &&
				Body.GetIntProperty(MemberOrdinalProperty, -1) == Ordinal &&
				Body.GetIntProperty(CohortXProperty, -1) == Body.CurrentCell.X &&
				Body.GetIntProperty(CohortYProperty, -1) == Body.CurrentCell.Y &&
				Body.GetIntProperty(KingdomPolityNpcRuntime.ContestedProperty, 0) == 0 &&
				Body.GetStringProperty(KingdomPolityNpcRuntime.PolityProperty) == Cohort.PolityId &&
				Body.GetStringProperty(KingdomPolityNpcRuntime.ProfileProperty) == profile &&
				Body.GetStringProperty(KingdomPolityNpcRuntime.ResolverProperty) == Spec.ResolverDigest &&
				Body.GetStringProperty(KingdomPolityNpcRuntime.RoleProperty) == Spec.RoleKey &&
				(Body.GetStringProperty(KingdomPolityNpcRuntime.SignatureCueProperty) ?? "") ==
					string.Join("|", Spec.SignatureCues.ToArray()) &&
				(Body.GetStringProperty(KingdomPolityNpcRuntime.DialogueCueProperty) ?? "") ==
					string.Join("|", Spec.DialogueCues.ToArray()) &&
				(Body.GetStringProperty(KingdomPolityNpcRuntime.ExpressionReasonProperty) ?? "") ==
					string.Join("|", Spec.ReasonFactIds.ToArray()) &&
				(Body.GetStringProperty(KingdomPolityNpcRuntime.FigureProperty) ?? "") ==
					(FigureId ?? "") && Body.GetIntProperty("SuppressCorpseDrops") == 1 &&
				Body.GetIntProperty("NoXP") == 1 && Body.HasPart<NoXPGain>() && part != null &&
				!part.Inert && part.RealmId == RealmId && part.CohortId == Cohort.CohortId &&
				part.Purpose == Cohort.Purpose && part.Representative == (Ordinal == 0);
		}

		private static bool TryBuildCustodyPlan(KingdomPolityLedger Ledger, Zone Zone,
			string RealmId, KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			GameObject Body, int Ordinal, bool AllowRemovedGear, out FrozenCustodyPlan Plan,
			out string Failure)
		{
			Plan = null; Failure = null;
			string bodyId = Cohort == null || Ordinal < 0 || Ordinal >=
				Cohort.ResolvedMembers.Count ? null : KingdomPolityCohortRules.PreparedObjectId(
					Cohort, Ordinal);
			if (HasContestedPreparedBody(Zone, Receipt, bodyId))
				return FailPhysical("cohort body is durably contested after callback divergence", out Failure);
			if (!TryResolveFrozenSpec(Ledger, Cohort, Ordinal, out KingdomPolityNpcSpec spec,
				out string figureId, out Failure) || !ExactPreparedBody(Body, Zone, RealmId,
					Cohort, Receipt, spec, figureId, Ordinal, out Failure))
				return FailPhysical(Failure ?? "cohort body identity or location is not exact", out Failure);
			FrozenCustodyPlan plan = new FrozenCustodyPlan
			{
				Body = Body, Cell = Body.CurrentCell, Spec = spec, MemberOrdinal = Ordinal
			};
			bool[] seenGear = new bool[spec.GearBlueprints.Count];
			HashSet<GameObject> seenObjects = new HashSet<GameObject>(); int count = 0;
			List<GameObject> roots = Body.GetInventoryDirectAndEquipment();
			for (int i = 0; roots != null && i < roots.Count; i++)
			{
				if (!TryBuildCustodyNode(roots[i], Body, spec, RealmId, Cohort, Receipt,
					seenGear, seenObjects, ref count, out FrozenCustodyNode node, out Failure)) return false;
				if (node != null) plan.Roots.Add(node);
			}
			for (int i = 0; i < seenGear.Length; i++)
			{
				string gearId = GearObjectId(RealmId, Cohort, Receipt, spec, i);
				if (seenGear[i])
				{
					if (HasRemovalWitness(Zone, KingdomPolityPhysicalCustodyRules.GearRemovalKind,
						RealmId, Cohort.CohortId, Receipt.ProjectionId, gearId, i))
						return FailPhysical("removed polity gear is still physically present", out Failure);
					continue;
				}
				if (!TryFindResidentObject(gearId, out GameObject residentGear, out Failure)) return false;
				if (GameObject.Validate(residentGear)) return FailPhysical(
					"frozen polity gear moved outside its exact body custody", out Failure);
				if (!AllowRemovedGear || !HasRemovalWitness(Zone,
					KingdomPolityPhysicalCustodyRules.GearRemovalKind, RealmId, Cohort.CohortId,
					Receipt.ProjectionId, gearId, i)) return FailPhysical(
					"frozen polity gear is missing without an exact removal witness", out Failure);
			}
			Plan = plan; return true;
		}

		private static bool TryProveFrozenCohort(KingdomPolityLedger Ledger, Zone Zone,
			string RealmId, KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Receipt,
			GameObject[] Observed, bool AllowRemovedGear, out FrozenCustodyPlan[] Plans,
			out string Failure)
		{
			Plans = Observed == null ? null : new FrozenCustodyPlan[Observed.Length]; Failure = null;
			if (Observed == null || Observed.Length != Cohort.ResolvedMembers.Count)
				return FailPhysical("cohort custody proof has wrong cardinality", out Failure);
			for (int i = 0; i < Observed.Length; i++)
			{
				if (!GameObject.Validate(Observed[i]))
				{
					if (AllowRemovedGear && HasBodyRemovalWitness(Zone, RealmId, Cohort.CohortId,
						Receipt.ProjectionId, KingdomPolityCohortRules.PreparedObjectId(Cohort, i), i))
					{
						if (!TryResolveFrozenSpec(Ledger, Cohort, i, out KingdomPolityNpcSpec absent,
							out string _, out Failure)) return false;
						for (int gear = 0; gear < absent.GearBlueprints.Count; gear++)
						{
							if (!TryFindResidentObject(GearObjectId(RealmId, Cohort, Receipt,
								absent, gear), out GameObject residentGear, out Failure)) return false;
							if (GameObject.Validate(residentGear)) return FailPhysical(
								"removed cohort body left exact minted gear in world custody", out Failure);
						}
						continue;
					}
					return FailPhysical("cohort custody proof is physically incomplete", out Failure);
				}
				if (!TryBuildCustodyPlan(Ledger, Zone, RealmId, Cohort, Receipt, Observed[i], i,
					AllowRemovedGear, out Plans[i], out Failure)) return false;
			}
			return true;
		}

		private static bool TryBuildCustodyNode(GameObject Item, GameObject Parent,
			KingdomPolityNpcSpec Spec, string RealmId, KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, bool[] SeenGear,
			HashSet<GameObject> SeenObjects, ref int Count, out FrozenCustodyNode Node,
			out string Failure)
		{
			Node = null; Failure = null;
			if (!GameObject.Validate(Item) || (!ReferenceEquals(Item.InInventory, Parent) &&
				!ReferenceEquals(Item.Equipped, Parent)) || !SeenObjects.Add(Item) ||
				++Count > MaximumFrozenCustodyObjects)
				return FailPhysical("cohort custody tree is invalid, cyclic, or unbounded", out Failure);
			FrozenCustodyNode node = new FrozenCustodyNode { Object = Item, Parent = Parent };
			bool marked = KingdomPolityNpcRuntime.HasAnyGearMark(Item);
			int gear = Item.GetIntProperty(KingdomPolityNpcRuntime.GearOrdinalProperty, -1);
			bool inRange = gear >= 0 && gear < SeenGear.Length;
			bool duplicate = inRange && SeenGear[gear];
			bool exactGear = marked && inRange && KingdomPolityNpcRuntime.ExactGear(Item, Spec,
				RealmId, Cohort.CohortId, Receipt.ProjectionId,
				KingdomPolityCohortRules.PreparedObjectId(Cohort, Spec.Ordinal), gear,
				ExactCustody: true);
			bool idCollision = false;
			for (int i = 0; !marked && i < Spec.GearBlueprints.Count; i++)
				if (Item.IDIfAssigned == GearObjectId(RealmId, Cohort, Receipt, Spec, i))
					idCollision = true;
			KingdomPolityCustodyDecision decision =
				KingdomPolityPhysicalCustodyRules.ClassifyCustody(Item.IsNatural(),
					Item.GetBlueprint(UseDefault: false)?.IsNatural() == true, marked,
					exactGear, duplicate, idCollision);
			if (decision == KingdomPolityCustodyDecision.Quarantine)
				return FailPhysical("polity gear mark is copied, partial, natural, duplicated, or colliding",
					out Failure);
			if (decision == KingdomPolityCustodyDecision.DeleteExactGear)
			{
				SeenGear[gear] = true; node.Kind = FrozenCustodyKind.ExactGear;
				node.GearOrdinal = gear;
			}
			else node.Kind = decision == KingdomPolityCustodyDecision.PreserveNatural ?
				FrozenCustodyKind.Natural : FrozenCustodyKind.Foreign;
			List<GameObject> children = Item.GetInventoryDirectAndEquipment();
			for (int i = 0; children != null && i < children.Count; i++)
			{
				if (!TryBuildCustodyNode(children[i], Item, Spec, RealmId, Cohort, Receipt,
					SeenGear, SeenObjects, ref Count, out FrozenCustodyNode child, out Failure)) return false;
				if (child != null) node.Children.Add(child);
			}
			Node = node; return true;
		}

		private static string GearObjectId(string RealmId, KingdomPolityCohortPlan Cohort,
			KingdomPolityProjectionReceipt Receipt, KingdomPolityNpcSpec Spec, int GearOrdinal)
		{
			string profile = Spec.ProfileId + ":" + Spec.ProfileRevision.ToString(
				CultureInfo.InvariantCulture);
			return KingdomPolityPhysicalCustodyRules.GearObjectId(RealmId, Cohort.CohortId,
				Receipt.ProjectionId, KingdomPolityCohortRules.PreparedObjectId(Cohort, Spec.Ordinal),
				Spec.Ordinal, GearOrdinal, profile, Spec.ResolverDigest,
				Spec.GearBlueprints[GearOrdinal]);
		}
	}
}
