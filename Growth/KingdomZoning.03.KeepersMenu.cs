using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomZoning
	{
		/// <summary>
		/// The keepers' own screen: what the settlement's craft stands at, everything it has been
		/// taught, and the acts that teach or carry evidence. Research subjects are deliberately
		/// absent: the technology map is a reading and the one pressable research surface is the
		/// physical inquiry bench. Owns its whole interaction the way
		/// <c>KingdomLarder</c> and <c>KingdomSalvage</c> do, so the Charter needs one line to
		/// reach it.
		/// </summary>
		/// <param name="System">The realm; must be founded.</param>
		public static void ShowKeepers(KingdomSystem System)
		{
			KingdomSystem.Guard("keepers screen", delegate
			{
				if (System == null || !System.Founded)
				{
					Popup.Show("You rule nothing yet.");
					return;
				}
				KingdomResearch.RevealRoots(System);
				KingdomResearch.EnsureBenches(System, The.ZoneManager?.ActiveZone);
				while (true)
				{
					List<GameObject> disks = CarriedDisks();
					// A fragment in hand tells the founder a thing exists before anybody is taught
					// it, which is vanilla's own idiom one step out: a disk you cannot learn from
					// still tells you what it is. Scanned here rather than on pickup, because a
					// per-turn inventory walk is a cost this design refuses to pay.
					KingdomResearch.RevealFromCarried(System, CarriedKeys(disks));
					KingdomResearch.ApplySources(System);
					List<ResearchNode> carried = KingdomResearch.CarriedFromAway(System);
					List<string> options = new List<string>();
					List<char> hotkeys = new List<char>();
					options.Add((disks.Count > 0)
						? "{{W|Teach the keepers a design from a data disk}}"
						: "{{K|You carry no data disk to teach from}}");
					hotkeys.Add('t');
					if (KingdomResearch.Enabled)
					{
						if (carried.Count > 0)
						{
							options.Add("{{W|Set down what the keepers of " + AwayName(System) + " worked out}}");
							hotkeys.Add('s');
						}
					}
					options.Add("Close");
					hotkeys.Add('z');
					int chosen = Popup.PickOption(Title: "What the keepers of " + KingdomPresentation.Rich(System.SeatName) + " know", Intro: KeepersIntro(System), Options: options, Hotkeys: hotkeys, AllowEscape: true);
					if (chosen < 0 || chosen >= hotkeys.Count || hotkeys[chosen] == 'z')
					{
						return;
					}
					switch (hotkeys[chosen])
					{
					case 't':
						if (disks.Count > 0)
						{
							TeachFromDisk(System, disks);
						}
						break;
					case 's':
						SetDownWhatWasLearned(System, carried);
						break;
					}
					if (KingdomGovernanceScope.HasCommitted)
					{
						return;
					}
				}
			});
		}

	}
}
