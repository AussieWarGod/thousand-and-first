using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		public void ReleaseBuilding(KingdomSystem System)
		{
			Cell cell = ParentObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			Zone zone = ParentObject.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A building is released on the kingdom's own ground, not in other people's houses.");
				return;
			}
			System.Collections.Generic.List<GameObject> adopted = new System.Collections.Generic.List<GameObject>();
			foreach (GameObject item in cell.GetObjects())
			{
				if (item.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1)
				{
					adopted.Add(item);
				}
			}
			foreach (Cell adjacentCell in cell.GetLocalAdjacentCells())
			{
				foreach (GameObject item in adjacentCell.GetObjects())
				{
					if (item.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1 && !adopted.Contains(item))
					{
						adopted.Add(item);
					}
				}
			}
			if (adopted.Count == 0)
			{
				Popup.Show("Nothing here is adopted into the settlement.");
				return;
			}
			string[] options = new string[adopted.Count];
			for (int i = 0; i < adopted.Count; i++)
			{
				options[i] = adopted[i].ShortDisplayName + " {{K|(" + adopted[i].GetStringProperty(KingdomAdopt.AdoptedKeyProperty) + ")}}";
			}
			int index = Popup.PickOption(Title: "Release which adoption?", Options: options, AllowEscape: true);
			if (index < 0)
			{
				return;
			}
			GameObject target = adopted[index];
			if (Popup.ShowYesNo("Release " + target.ShortDisplayName + " from " + KingdomPresentation.Rich(System.SeatName) + "'s standing? It stands exactly where it stands; the settlement will simply stop answering for it.") != DialogResult.Yes)
			{
				return;
			}
			if (!KingdomAdopt.Release(System, zone, target, out var failure))
			{
				Popup.Show(failure);
				return;
			}
		}

		/// <summary>
		/// What the realm's two cities believe, and the founder's levers over it: read the
		/// standing report, pour a rite of shared water, declare (or recant) the realm's own
		/// creed, or ask a seceded city to come back. Every lever below is safe to call at any
		/// temper &mdash; each one checks its own preconditions and declines in the settlement's
		/// own voice rather than trusting this menu to gate anything. See <see cref="KingdomCreed"/>.
		/// </summary>
		public void ManageCreed(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			while (true)
			{
				bool riteAvailable = KingdomCreed.RiteAvailable(System);
				System.Collections.Generic.List<string> declarable = KingdomCreed.DeclarableCreeds(System);
				string[] options = new string[4]
				{
					"{{W|Read the report}}",
					riteAvailable ? "Hold a rite of shared water" : "{{K|Hold a rite of shared water}}",
					(declarable.Count > 0) ? "Declare the realm's creed" : "{{K|Declare the realm's creed}}",
					(System.Seceded != null) ? "Ask them back" : "{{K|Ask them back}}"
				};
				int num = Popup.PickOption(Title: "How your cities hold each other", Options: options, AllowEscape: true);
				if (num < 0)
				{
					return;
				}
				if (num == 0)
				{
					Popup.Show(KingdomCreed.Report(System));
				}
				else if (num == 1)
				{
					if (!KingdomCreed.HoldRite(System, zone, out var riteFailure))
					{
						Popup.Show(riteFailure);
					}
					else
					{
						return;
					}
				}
				else if (num == 2)
				{
					DeclareCreed(System, declarable);
					if (KingdomGovernanceScope.HasCommitted)
					{
						return;
					}
				}
				else if (num == 3)
				{
					if (!KingdomCreed.TryRejoin(System, zone, out var rejoinFailure))
					{
						Popup.Show(rejoinFailure);
					}
					else
					{
						return;
					}
				}
			}
		}

		/// <summary>The declare/recant sub-menu <see cref="ManageCreed"/> opens. A separate method
		/// only to keep that loop's body short; it owns no state of its own.</summary>
		private void DeclareCreed(KingdomSystem System, System.Collections.Generic.List<string> Declarable)
		{
			if (Declarable.Count == 0)
			{
				Popup.Show("Neither city holds a creed strongly enough to declare it the realm's own.");
				return;
			}
			string[] options = new string[Declarable.Count + 1];
			for (int i = 0; i < Declarable.Count; i++)
			{
				options[i] = KingdomCreed.CreedName(Declarable[i]) + ((Declarable[i] == System.DeclaredCreed) ? " {{G|[declared]}}" : "");
			}
			options[Declarable.Count] = "{{K|Unsay it}}";
			int num = Popup.PickOption(Title: "Declare the realm's creed", Options: options, AllowEscape: true);
			if (num < 0)
			{
				return;
			}
			string chosen = (num == Declarable.Count) ? null : Declarable[num];
			// The price is named before it is paid, the same as sending a manifest or calling a
			// meal. This is the heaviest thing in the Charter - it moves a faction's regard for
			// the realm across the whole world and bends every settler who comes afterwards - and
			// it was the one spending action that committed without a word.
			KingdomCivicVoiceReceipt voice = null;
			if (chosen != null)
			{
				int slighted = 0;
				for (int i = 0; i < Declarable.Count; i++)
					if (Declarable[i] != chosen) slighted++;
				string facts = KingdomCreedRules.DeclarationPreview(
					KingdomCreed.CreedName(chosen), slighted, System.Dissent);
				long tick = The.Game == null || The.Game.TimeTicks < 0L ? 0L : The.Game.TimeTicks;
				string settlement = System.City?.SettlementId;
				string source = string.IsNullOrEmpty(settlement) ? null
					: KingdomLifecycleRules.ChildId(settlement, "civic-creed-" + tick, 0);
				KingdomExperienceRuntime.TryPrepareCivicVoice(System,
					KingdomCivicVoiceFixture.CreedDeclaration, 1, source, settlement, facts, tick,
					out voice, out string rendering);
				string precedent = KingdomDecisionTagRules.CreedScene(System.City?.AssentingMoot);
				if (!string.IsNullOrEmpty(precedent)) rendering += "\n\n" + precedent;
				if (Popup.ShowYesNo(rendering) != DialogResult.Yes) return;
			}
			if (!KingdomCreed.Declare(System, chosen, out var failure))
			{
				Popup.Show(failure);
				return;
			}
			if (chosen != null) KingdomExperienceRuntime.TryPublishCivicVoice(System, voice);
		}
	}
}
