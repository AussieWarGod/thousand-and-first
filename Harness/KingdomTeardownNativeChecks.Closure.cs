using System.Text;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomTeardownNativeChecks
	{
		private sealed partial class Case
		{
			private bool ClosureRefusalObserved;

			private void CheckClosureRefusal(GameObject Built, KingdomConstructionJob Job,
				StringBuilder Evidence)
			{
				if (ClosureRefusalObserved) return;
				Require(Job != null && Job.Phase == KingdomConstructionPhase.Complete,
					Name + ": closure probe requires the real completed construction row");
				string registry = Game.GetStringGameState(KingdomConstruction.RegistryStateKey, null);
				string receipt = Built.GetStringProperty(KingdomConstruction.ReceiptProperty);
				Cell cell = Built.CurrentCell;
				int effort = Built.GetIntProperty(KingdomMaterials.StrikeEffortProperty);
				for (int attempt = 0; attempt < 2; attempt++)
				{
					Require(!KingdomMaterials.OrderStrike(System, Zone, Built, out string failure),
						Name + ": strike crossed unfinished terminal closure");
					Require(failure == KingdomConstructionRules.PendingStrikeClosureMessage,
						Name + ": wrong closure refusal: " + failure);
					Require(Game.GetStringGameState(KingdomConstruction.RegistryStateKey, null) == registry
						&& Built.GetStringProperty(KingdomConstruction.ReceiptProperty) == receipt
						&& Built.CurrentCell == cell && GameObject.Validate(Built)
						&& Built.GetIntProperty(KingdomMaterials.StrikeEffortProperty) == effort,
						Name + ": refused strike changed the registry or building");
				}
				ClosureRefusalObserved = true;
				if (Name == "larder") CheckForeignReceiptRefusal(Built, Job, Evidence);
				Evidence.Append("; closure-refusal case=").Append(Name)
					.Append(" attempts=2 correct-message=true registry-unchanged=true building-unchanged=true");
			}

			private void CheckForeignReceiptRefusal(GameObject Built, KingdomConstructionJob Job,
				StringBuilder Evidence)
			{
				Require(KingdomConstruction.TryRead(out var rows, out _), Name + ": registry unreadable");
				KingdomConstructionJob other = null;
				foreach (KingdomConstructionJob row in rows)
					if (row.Id != Job.Id && row.OwnerKey == Job.OwnerKey && row.ZoneId == Job.ZoneId
						&& row.OutputId != Built.IDIfAssigned && row.SourceId != Built.IDIfAssigned)
					{ other = row; break; }
				Require(other != null, Name + ": missing other work for foreign-receipt counterexample");
				string registry = Game.GetStringGameState(KingdomConstruction.RegistryStateKey, null);
				string receipt = Built.GetStringProperty(KingdomConstruction.ReceiptProperty);
				int effort = Built.GetIntProperty(KingdomMaterials.StrikeEffortProperty);
				try
				{
					Built.SetStringProperty(KingdomConstruction.ReceiptProperty, other.Id);
					Require(!KingdomMaterials.OrderStrike(System, Zone, Built, out string failure)
						&& failure == "That building carries another construction receipt.",
						Name + ": foreign receipt was mistaken for pending own closure");
					Require(Built.GetStringProperty(KingdomConstruction.ReceiptProperty) == other.Id,
						Name + ": refusal replaced the borrowed receipt");
				}
				finally { Built.SetStringProperty(KingdomConstruction.ReceiptProperty, receipt); }
				Require(Game.GetStringGameState(KingdomConstruction.RegistryStateKey, null) == registry
					&& Built.GetIntProperty(KingdomMaterials.StrikeEffortProperty) == effort
					&& Built.GetStringProperty(KingdomConstruction.ReceiptProperty) == receipt,
					Name + ": foreign-receipt probe changed durable work or failed restoration");
				Evidence.Append("; foreign-receipt-refusal case=").Append(Name)
					.Append(" correct-message=true registry-unchanged=true restored=true synthetic-receipt-probe=true");
			}
		}
	}
}
