using System;
using XRL;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>Main building verb and immediate damage/destruction recovery for the moot.</summary>
	[Serializable]
	public sealed class r_KingdomAssentingMoot : IPart
	{
		internal const int RuntimeOwnerVersion = 1;

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded) return;
			if (!KingdomMaster.AutomaticWorkAllowed(system)) return;
			KingdomSystem.Guard("assenting moot tick", delegate
			{
				if (KingdomAssentingMoot.TryContext(system, ParentObject,
					out KingdomAssentingMootContext context, out string _)
					&& context.Book.AssentingMoot.Phase != KingdomAssentingMootPhase.None)
				{
					string failure;
					KingdomAssentingMoot.Reconcile(system, context.Book, ParentObject,
						false, out failure);
				}
			});
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetInventoryActionsEvent.ID
				|| ID == InventoryActionEvent.ID || ID == GetShortDescriptionEvent.ID
				|| ID == TookDamageEvent.ID || ID == BeforeDestroyObjectEvent.ID;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Assent", "convene the assenting moot", "r_ConveneAssentingMoot",
				null, 'a', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_ConveneAssentingMoot" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("assenting moot", delegate
				{
					using (KingdomGovernanceScope.Begin(E.Actor))
						KingdomAssentingMoot.Open(ParentObject, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(KingdomAssentingMoot.DescriptionLine(ParentObject));
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(TookDamageEvent E)
		{
			KingdomAssentingMoot.SuspendForBuilding(ParentObject, "The moot was struck.");
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(BeforeDestroyObjectEvent E)
		{
			KingdomAssentingMoot.SuspendForBuilding(ParentObject,
				"The exact moot building is being struck down.");
			return base.HandleEvent(E);
		}
	}
}
