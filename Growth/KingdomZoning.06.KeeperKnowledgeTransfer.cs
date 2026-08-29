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
		// The teaching act (Addendum 22 B4). What crosses between two of the founder's cities is a
		// SEED and never a holding: the founder sets down what one city's keepers worked out, the
		// other city's keepers have the shape of it, and the walking is still theirs. Doors, never
		// rooms - Addendum 18's clause, applied to the road between two of your own cities exactly
		// as it applies to the road out of exile. The other city's immutable id is the source, so
		// opening this menu repeatedly cannot turn one lesson into several seeds.
		private static void SetDownWhatWasLearned(KingdomSystem System,
			KingdomSettlement Source, List<ResearchNode> Carried)
		{
			string away = SourceName(System, Source);
			string source = KingdomZoningRules.ComposeKey("settlement",
				Source?.City?.SettlementId);
			List<string> named = new List<string>();
			for (int i = 0; i < Carried.Count; i++)
			{
				if (source != null && KingdomResearch.SeedFromSource(System, Carried[i].Key, source,
					"the keepers of " + away, "share keeper knowledge"))
				{
					named.Add(Carried[i].Named);
				}
			}
			if (named.Count == 0)
			{
				Popup.Show("There is nothing here they could not already have told you themselves.");
				return;
			}
			Popup.Show("{{G|You set it down for them: " + KingdomZoningRules.JoinAnd(named) + ".}} The keepers of "
				+ KingdomPresentation.Rich(System.SeatName) + " have the shape of it now. The rest of the walking is theirs.");
			KingdomChronicle.Record(System, "what the keepers of " + KingdomPresentation.Rich(away) + " knew was set down at " + KingdomPresentation.Rich(System.SeatName));
		}

		private static string SourceName(KingdomSystem System, KingdomSettlement Source)
		{
			return (Source != null && !string.IsNullOrEmpty(Source.SettlementName))
				? Source.SettlementName
				: "your other city";
		}

		private static string KeepersIntro(KingdomSystem System)
		{
			List<string> roster = Roster(System);
			int points = KingdomZoningRules.TechPoints(roster);
			TechLevel level = KingdomZoningRules.LevelForPoints(points);
			int wanted = KingdomZoningRules.PointsToNext(points);
			StringBuilder text = new StringBuilder();
			text.Append(KingdomPresentation.Rich(System.SeatName)).Append(" builds at the level of {{C|").Append(KingdomZoningRules.TechName(level)).Append("}}.");
			text.Append(wanted <= 0
				? "\n{{K|The keepers have learned everything this settlement can teach itself.}}"
				: ("\n{{K|" + wanted + " more toward " + KingdomZoningRules.TechName((TechLevel)((int)level + 1))
					+ ". A design taught is worth " + KingdomZoningRules.TechPointsPerDisk
					+ "; a machine certified fit for the grid is worth " + KingdomZoningRules.TechPointsPerCertification + ".}}"));
			AppendKind(text, roster, KingdomZoningRules.KindDisk, "\n\nTaught to the keepers: ");
			AppendKind(text, roster, KingdomZoningRules.KindMachine, "\nCertified fit for the grid: ");
			AppendKind(text, roster, KingdomZoningRules.KindOrigin, "\nTrades among the people: ");
			AppendKind(text, roster, KingdomZoningRules.KindNode, "\nWorked out here: ");
			AppendKind(text, roster, KingdomCeremonyRules.PatternKnowledgeKind, "\nHeld from a ceremony here: ");
			AppendKind(text, roster, KingdomZoningRules.KindRite, "\nRites the founder remembers: ");
			return text.ToString();
		}

		private static void AppendKind(StringBuilder Text, List<string> Roster, string Kind, string Label)
		{
			List<string> named = new List<string>();
			foreach (string key in Roster)
			{
				if (KingdomZoningRules.KindOf(key) == Kind)
				{
					string name = KingdomZoningRules.NameOf(key);
					if (name != null && !named.Contains(name))
					{
						named.Add(name);
					}
				}
			}
			if (named.Count > 0)
			{
				Text.Append(Label).Append(KingdomZoningRules.JoinAnd(named));
			}
		}

		// Only what the founder is actually carrying. A disk lying in a chest somewhere is not
		// something the keepers can be taught from, and reaching into containers the founder
		// merely owns would be the protection law's exact prohibition (STANDARDS 7).
		private static List<GameObject> CarriedDisks()
		{
			List<GameObject> disks = new List<GameObject>();
			Inventory inventory = The.Player?.Inventory;
			if (inventory == null)
			{
				return disks;
			}
			foreach (GameObject item in inventory.GetObjects())
			{
				DataDisk disk = item?.GetPart<DataDisk>();
				if (disk != null && disk.Data != null && !string.IsNullOrEmpty(DiskName(disk)))
				{
					disks.Add(item);
				}
			}
			return disks;
		}

		// The roster keys the founder is carrying right now, as a node's TaughtBy and SeededBy
		// lists would spell them. Never stored: this is what is in their hands this moment.
		private static List<string> CarriedKeys(List<GameObject> Disks)
		{
			List<string> keys = new List<string>();
			for (int i = 0; Disks != null && i < Disks.Count; i++)
			{
				string key = KingdomZoningRules.ComposeKey(KingdomZoningRules.KindDisk, DiskName(Disks[i].GetPart<DataDisk>()));
				if (key != null && !keys.Contains(key))
				{
					keys.Add(key);
				}
			}
			return keys;
		}

		/// <summary>
		/// The name a disk teaches under: an item modification's own display name, otherwise the
		/// blueprint the recipe builds. This is the string an author writes in a
		/// <c>Knowledge</c> attribute, so it has to be the one the founder reads on the screen.
		/// </summary>
		private static string DiskName(DataDisk Disk)
		{
			if (Disk == null || Disk.Data == null)
			{
				return null;
			}
			if (Disk.Data.Type == "Mod" && !string.IsNullOrEmpty(Disk.Data.DisplayName))
			{
				return Disk.Data.DisplayName;
			}
			return Disk.Data.Blueprint;
		}

		// The disk is not consumed. Vanilla's own "Learn" action destroys it because it writes
		// into the PLAYER's recipe list, which is a different ledger; here the founder is lending
		// the keepers something to copy, and taking a player's property to do it would be the
		// protection law broken for a convenience.
		private static void TeachFromDisk(KingdomSystem System, List<GameObject> Disks)
		{
			List<string> options = new List<string>();
			for (int i = 0; i < Disks.Count; i++)
			{
				string name = DiskName(Disks[i].GetPart<DataDisk>());
				bool known = KingdomZoningRules.Knows(Roster(System), KingdomZoningRules.ComposeKey(KingdomZoningRules.KindDisk, name));
				options.Add(name + (known ? " {{K|[already known here]}}" : ""));
			}
			int chosen = Popup.PickOption(Title: "Teach the keepers", Intro: "The disk is read and handed back. Nothing you carry is spent.", Options: options, AllowEscape: true);
			if (chosen < 0)
			{
				return;
			}
			string design = DiskName(Disks[chosen].GetPart<DataDisk>());
			if (!Learn(System, KingdomZoningRules.KindDisk, design, "teach keeper design"))
			{
				Popup.Show("The keepers of " + KingdomPresentation.Rich(System.SeatName) + " already have that one written down.");
				return;
			}
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			KingdomChronicle.Record(System, "the keepers of " + realm + " were taught to build " + design);
			System.RecordDeed("taught the keepers of " + realm + " to build " + design);
			Popup.Show("{{G|The keepers copy it out and hand the disk back.}} " + KingdomPresentation.Rich(System.SeatName) + " can raise " + XRL.Language.Grammar.A(design) + " when the ground and the stores allow.");
			// A roll changed, so a node somebody had already answered may now be answered here.
			KingdomResearch.ApplySources(System);
		}

	}
}
