using XRL.World;

namespace ThousandAndFirst
{
	internal readonly struct KingdomMaterialDebitLeg
	{
		internal readonly GameObject Container;
		internal readonly GameObject Item;
		internal readonly string Blueprint;
		internal readonly int Before;
		internal readonly int After;
		internal readonly KingdomMaterialDebitSourceKind Kind;
		internal readonly int KindIndex;

		internal KingdomMaterialDebitLeg(GameObject Container, GameObject Item,
			string Blueprint, int Before, int After,
			KingdomMaterialDebitSourceKind Kind, int KindIndex)
		{
			this.Container = Container;
			this.Item = Item;
			this.Blueprint = Blueprint;
			this.Before = Before;
			this.After = After;
			this.Kind = Kind;
			this.KindIndex = KindIndex;
		}
	}

	public sealed partial class KingdomMaterialDebit
	{
		/// <summary>Exports the exact read-only allocation before a durable owner publishes it.</summary>
		internal bool TryDescribe(out KingdomMaterialDebitLeg[] Legs)
		{
			Legs = null;
			if (Reservation?.Outcome != KingdomMaterialDebitOutcome.Reserved || Plan == null
				|| Plan.Steps.Count < 1 || Plan.Steps.Count > 64 || !AllStillReserved()) return false;
			KingdomMaterialDebitLeg[] copy = new KingdomMaterialDebitLeg[Plan.Steps.Count];
			for (int i = 0; i < Plan.Steps.Count; i++)
			{
				KingdomMaterialDebitStep step = Plan.Steps[i];
				Entry entry = EntryFor(step);
				if (entry == null || step.Taken < 1 || step.Remaining < 0
					|| step.Original != entry.OriginalCount
					|| step.Remaining != step.Original - step.Taken) return false;
				copy[i] = new KingdomMaterialDebitLeg(entry.Container, entry.Item,
					entry.Blueprint, step.Original, step.Remaining, entry.Kind, entry.KindIndex);
			}
			Legs = copy;
			return true;
		}
	}
}
