using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomMirrorGate
	{
		/// <summary>Reads one exact canonical register without repairing or rewriting it. Exact is
		/// byte-for-byte what a build wrote: this build's versioned text, or a pre-version save's
		/// unversioned text, which the one write below then carries forward in the current shape.
		/// A newer build's register is refused whole, and <paramref name="Refusal"/> says so.</summary>
		private static bool TryReadDestinationRegister(out string Raw,
			out KingdomGateRow[] Rows, out string Refusal)
		{
			Refusal = null;
			Raw = The.Game?.GetStringGameState(
				KingdomMirrorGateRules.RegisterStateKey, "") ?? "";
			bool read = KingdomMirrorGateRules.TryParseRegister(Raw, out Rows, out int dropped,
				out bool future);
			if (future)
			{
				Refusal = KingdomMirrorGateRules.FutureVersionLine;
				return false;
			}
			if (read && dropped == 0
				&& (string.Equals(Raw, KingdomMirrorGateRules.FormatRegister(Rows),
						StringComparison.Ordinal)
					|| string.Equals(Raw, KingdomMirrorGateRules.LegacyRegisterText(Rows),
						StringComparison.Ordinal))) return true;
			Refusal = "The realm's arch register is not an exact readable record. Repair or re-key its damaged arches before choosing a capital destination.";
			return false;
		}

		internal static bool CanChooseDestination(r_KingdomMirrorGate Gate)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			GameObject gateObject = Gate?.ParentObject;
			Cell cell = GameObject.Validate(gateObject) ? gateObject.CurrentCell : null;
			Zone zone = cell?.ParentZone;
			if (system == null || !system.Founded || zone == null
				|| !ReferenceEquals(gateObject.GetPart<r_KingdomMirrorGate>(), Gate)
				|| (!KingdomUpgrade.IsFunctionallyBuilt(gateObject)
					&& (gateObject.GetIntProperty("KingdomGrid") != 1
						|| r_KingdomScaffold.HasPendingImprovementSuccessorAuthority(gateObject))))
				return false;
			string city = CityOf(system, zone.ZoneID);
			if (string.IsNullOrEmpty(city) || !KingdomCrown.CrownedHere(system, city)) return false;
			string key = KingdomMirrorGateRules.ComposeLocationKey(zone.ZoneID, cell.X, cell.Y);
			return TryReadDestinationRegister(out string _, out KingdomGateRow[] rows,
					out string _)
				&& KingdomMirrorGateRules.HubSpokeIndices(rows, key).Length > 1;
		}

		/// <summary>Single-threaded compare/write/readback for the one user-authored re-key.</summary>
		private static bool TryWriteDestination(string Expected, KingdomGateRow[] Next)
		{
			if (The.Game == null || !string.Equals(The.Game.GetStringGameState(
				KingdomMirrorGateRules.RegisterStateKey, ""), Expected,
				StringComparison.Ordinal)) return false;
			string written = KingdomMirrorGateRules.FormatRegister(Next);
			The.Game.SetStringGameState(KingdomMirrorGateRules.RegisterStateKey, written);
			return string.Equals(The.Game.GetStringGameState(
				KingdomMirrorGateRules.RegisterStateKey, ""), written,
				StringComparison.Ordinal);
		}

		/// <summary>Explicitly changes the capital arch's one outward spoke.</summary>
		internal static bool ChooseDestination(r_KingdomMirrorGate Gate, GameObject Actor)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			GameObject gateObject = Gate?.ParentObject;
			Cell frozenCell = gateObject?.CurrentCell;
			Zone zone = frozenCell?.ParentZone;
			if (system == null || !system.Founded || zone == null
				|| !GameObject.Validate(gateObject)
				|| !ReferenceEquals(gateObject.GetPart<r_KingdomMirrorGate>(), Gate)
				|| (!KingdomUpgrade.IsFunctionallyBuilt(gateObject)
					&& (gateObject.GetIntProperty("KingdomGrid") != 1
						|| r_KingdomScaffold.HasPendingImprovementSuccessorAuthority(gateObject)))
				|| !GameObject.Validate(Actor) || !Actor.IsPlayer()
				|| !ReferenceEquals(Actor, The.Player)) return false;
			string hubCity = CityOf(system, zone.ZoneID);
			if (string.IsNullOrEmpty(hubCity) || !KingdomCrown.CrownedHere(system, hubCity))
			{
				Popup.Show("Only the arch in the present capital chooses among the realm's spokes.");
				return false;
			}
			string locationKey = KingdomMirrorGateRules.ComposeLocationKey(
				zone.ZoneID, frozenCell.X, frozenCell.Y);
			if (!TryReadDestinationRegister(out string frozen, out KingdomGateRow[] rows,
				out string refusal))
			{
				Popup.Show(refusal);
				return false;
			}
			int hub = KingdomMirrorGateRules.IndexOfKey(rows, locationKey);
			int[] spokes = KingdomMirrorGateRules.HubSpokeIndices(rows, locationKey);
			if (hub < 0 || spokes.Length < 2)
			{
				Popup.Show("This capital arch has fewer than two lawful destinations. Key another city's arch first.");
				return false;
			}
			List<string> options = new List<string>(spokes.Length);
			for (int i = 0; i < spokes.Length; i++)
			{
				KingdomGateRow spoke = rows[spokes[i]];
				string suffix = string.Equals(rows[hub].Partner, spoke.Key,
					StringComparison.Ordinal) ? " {{K|[current]}}" : "";
				options.Add("Answer " + KingdomPresentation.Rich(spoke.City) + suffix);
			}
			int picked = Popup.PickOption(Title: "Choose the capital arch's destination",
				Intro: "Every keyed spoke continues to answer " + KingdomPresentation.Rich(hubCity)
					+ ". The capital arch can answer one spoke at a time. Choose its outward crossing; "
					+ "this loads no distant city and spends nothing.",
				Options: options.ToArray(), AllowEscape: true);
			if (picked < 0 || picked >= spokes.Length) return false;
			KingdomGateRow chosen = rows[spokes[picked]];
			string previousCity = CityNamed(rows, rows[hub].Partner);
			if (string.Equals(rows[hub].Partner, chosen.Key, StringComparison.Ordinal))
			{
				Popup.Show("The capital arch already answers " + KingdomPresentation.Rich(chosen.City) + ".");
				return false;
			}
			if (Popup.ShowYesNo(KingdomMirrorGateRules.DestinationPrompt(
				KingdomPresentation.Rich(hubCity), KingdomPresentation.Rich(previousCity),
				KingdomPresentation.Rich(chosen.City))) != DialogResult.Yes) return false;
			Cell currentCell = GameObject.Validate(gateObject) ? gateObject.CurrentCell : null;
			if (The.Game == null || !system.Founded
				|| !GameObject.Validate(Actor) || !ReferenceEquals(Actor, The.Player)
				|| !GameObject.Validate(gateObject)
				|| !ReferenceEquals(Gate.ParentObject, gateObject)
				|| !ReferenceEquals(gateObject.GetPart<r_KingdomMirrorGate>(), Gate)
				|| (!KingdomUpgrade.IsFunctionallyBuilt(gateObject)
					&& (gateObject.GetIntProperty("KingdomGrid") != 1
						|| r_KingdomScaffold.HasPendingImprovementSuccessorAuthority(gateObject)))
				|| !ReferenceEquals(currentCell, frozenCell)
				|| !string.Equals(CityOf(system, currentCell?.ParentZone?.ZoneID), hubCity,
					StringComparison.OrdinalIgnoreCase)
				|| !KingdomCrown.CrownedHere(system, hubCity)
				|| !string.Equals(KingdomMirrorGateRules.ComposeLocationKey(
					currentCell?.ParentZone?.ZoneID, currentCell?.X ?? -1, currentCell?.Y ?? -1),
					locationKey, StringComparison.Ordinal)
				|| !string.Equals(The.Game.GetStringGameState(
				KingdomMirrorGateRules.RegisterStateKey, ""), frozen, StringComparison.Ordinal))
			{
				Popup.Show("The realm's arch register changed while you chose. Read it again before re-keying.");
				return false;
			}
			KingdomGateVerdict verdict = KingdomMirrorGateRules.TrySelectHubDestination(rows,
				locationKey, chosen.Key, out KingdomGateRow[] next, out string previous);
			if (verdict != KingdomGateVerdict.Joined || !string.Equals(previous,
				rows[hub].Partner, StringComparison.Ordinal))
			{
				Popup.Show(KingdomMirrorGateRules.RefusalLine(verdict, chosen.City));
				return false;
			}
			if (!TryWriteDestination(frozen, next))
			{
				Popup.Show("The arch register did not accept the exact re-key. Nothing is announced as changed; inspect the realm's arch record before trying again.");
				return false;
			}
			ReAnchorHere();
			string line = KingdomMirrorGateRules.DestinationChangedLine(
				KingdomPresentation.Rich(hubCity), KingdomPresentation.Rich(chosen.City));
			system.Ledger.Note("{{C|" + line + "}}");
			MessageQueue.AddPlayerMessage("{{C|" + line + "}}");
			return true;
		}
	}
}
