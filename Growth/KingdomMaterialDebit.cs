using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// A live, non-serializable receipt bound to the exact objects in dedicated stockpiles.
	/// Reservation only reads. A caller must persist its own durable job and
	/// <see cref="KingdomMaterialDebitResult.Requested"/> claim before calling <see cref="Commit"/>.
	/// <para>
	/// Qud's destroy path is not transactional: Stacker turns an ordinary destroy of a nonterminal
	/// stack into a measured one-unit decrement and veto, while a permitted whole-object obliteration
	/// runs teardown and graveyards the identity. This receipt tells those outcomes apart. It never
	/// recreates a graveyard object and never calls a partial loss an all-or-nothing refusal.
	/// </para>
	/// </summary>
	public sealed partial class KingdomMaterialDebit
	{
		private sealed class HeldWitness
		{
			internal GameObject Item;
			internal string Blueprint;
			internal int Count;
		}

		private sealed class ContainerWitness
		{
			internal GameObject Container;
			internal Inventory Inventory;
			internal List<GameObject> ObjectList;
			internal Zone Zone;
			internal readonly List<HeldWitness> Held = new List<HeldWitness>();
		}

		private sealed class Entry
		{
			internal GameObject Container;
			internal GameObject Item;
			internal ContainerWitness Witness;
			internal string Blueprint;
			internal int OriginalCount;
			internal KingdomMaterialDebitSourceKind Kind;
			internal int KindIndex;
			internal KingdomBitTally UnitBits;
		}

		private readonly KingdomMaterials.MaterialStock Stock;
		private readonly List<Entry> Entries = new List<Entry>();
		private readonly List<ContainerWitness> Containers = new List<ContainerWitness>();
		private readonly List<int> Removed = new List<int>();
		private readonly List<bool> ExactObservations = new List<bool>();
		private KingdomMaterialDebitPlan Plan;
		private Zone ReservedZone;
		private readonly GameObject RequiredItem;
		private readonly string RequiredItemId;
		/// <summary>Identity frozen by a read-only local attempt, retained for routed fallback.</summary>
		internal string FrozenRequiredItemId { get { return RequiredItemId; } }
		private int RequiredSource = -1;
		private bool TopologyUncertain;
		private bool Operating;
		private bool StockAdjusted;
		private bool MutationStarted;
		private KingdomMaterialDebitCost AdjustedLoss;

		public KingdomMaterialDebitResult Reservation { get; private set; }

		public KingdomMaterialDebitResult Result { get; private set; }

		public bool CanCompensate
		{
			get
			{
				if (TopologyUncertain || !AllObservationsExact()) return false;
				List<int> current;
				List<bool> same;
				ReadCurrent(out current, out same);
				return KingdomMaterialDebitRules.CanCompensate(Plan, Removed, current, same);
			}
		}

		private KingdomMaterialDebit(KingdomMaterials.MaterialStock Stock,
			KingdomMaterialDebitCost Cost, GameObject RequiredItem = null)
		{
			this.Stock = Stock;
			this.RequiredItem = RequiredItem;
			this.RequiredItemId = GameObject.Validate(RequiredItem) ? RequiredItem.ID : null;
			KingdomMaterialDebitCost requested = (Cost == null)
				? new KingdomMaterialDebitCost()
				: Cost.Copy();
			Reservation = KingdomMaterialDebitRules.EmptyResult(
				KingdomMaterialDebitOutcome.InvalidReservation,
				KingdomMaterialDebitFault.InvalidCost, requested, "The material claim is absent.");
			Result = Reservation;
		}

		internal static KingdomMaterialDebit Reserve(KingdomMaterials.MaterialStock Stock,
			KingdomMaterialDebitCost Cost)
		{
			return Reserve(Stock, Cost, null);
		}

		/// <summary>
		/// Reserves the same composite claim while requiring one exact, identity-stable stockpile
		/// object to answer it. The exact reference is planned first and must be fully consumed; an
		/// equivalent object of the same material can never substitute for it.
		/// </summary>
		internal static KingdomMaterialDebit Reserve(KingdomMaterials.MaterialStock Stock,
			KingdomMaterialDebitCost Cost, GameObject RequiredItem)
		{
			KingdomMaterialDebit debit = new KingdomMaterialDebit(Stock, Cost, RequiredItem);
			if (Cost == null)
			{
				return debit;
			}
			if (Stock == null)
			{
				debit.FailReservation(KingdomMaterialDebitFault.InvalidStock,
					"The stockpile reading is absent.");
				return debit;
			}
			if (!Stock.InputLeaseAuthorityExact || Stock.InputLeases == null)
			{
				debit.FailReservation(KingdomMaterialDebitFault.InvalidStock,
					Stock.InputLeaseFailure
						?? "The durable routed-input leases cannot be read.");
				return debit;
			}
			try
			{
				debit.ReservedZone = Stock.Zone;
				List<KingdomMaterialDebitSource> sources = debit.SnapshotSources();
				KingdomMaterialDebitFault fault;
				if (!KingdomMaterialDebitRules.TryPlan(Cost, sources, out debit.Plan, out fault))
				{
					debit.FailReservation(fault, ReservationFailure(fault));
					return debit;
				}
				if (RequiredItem != null && !debit.RequiredSourceWasConsumed())
				{
					debit.FailReservation(KingdomMaterialDebitFault.InvalidSources,
						"The exact required stockpile item does not answer this material claim.");
					return debit;
				}
				for (int i = 0; i < debit.Plan.Steps.Count; i++)
				{
					debit.Removed.Add(0);
					debit.ExactObservations.Add(true);
				}
				debit.Reservation = KingdomMaterialDebitRules.EmptyResult(
					KingdomMaterialDebitOutcome.Reserved, KingdomMaterialDebitFault.None,
					Cost, null);
				debit.Result = debit.Reservation;
			}
			catch (Exception ex)
			{
				debit.FailReservation(KingdomMaterialDebitFault.Exception, Describe(ex));
			}
			return debit;
		}
	}
}
