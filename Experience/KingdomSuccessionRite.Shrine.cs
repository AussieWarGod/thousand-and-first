using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSuccessionRite
	{
		internal static bool TryEnsureFounderShrine(string token, string founderName,
			long deathTick, string cause, string history, string cityName, string zoneId,
			string fixtureObjectId, int shrineX, int shrineY, string receiptObjectId,
			out GameObject shrine, out string failure)
		{
			shrine = null;
			failure = "";
			Zone zone = ExactLoadedZone(zoneId);
			Cell cell = zone?.GetCell(shrineX, shrineY);
			if (zone == null || cell == null)
			{
				failure = "the frozen founder-shrine cell is unavailable";
				return false;
			}
			List<GameObject> matches = new List<GameObject>();
			foreach (GameObject candidate in zone.GetObjects())
			{
				r_KingdomFounderShrine part = candidate?.GetPart<r_KingdomFounderShrine>();
				if (part != null && part.Matches(token)) matches.Add(candidate);
			}
			if (matches.Count > 1)
			{
				failure = "more than one in-run shrine claims the same founder death";
				return false;
			}
			GameObject evidence = matches.Count == 1 ? matches[0] : null;
			bool hasReceipt = !string.IsNullOrEmpty(receiptObjectId);
			bool exact = GameObject.Validate(evidence) && evidence.CurrentCell == cell
				&& (!hasReceipt || evidence.IDIfAssigned == receiptObjectId);
			FounderShrinePlacementVerdict verdict = KingdomSuccessionRules.JudgeFounderShrinePlacement(
				hasReceipt || evidence != null, exact, cell.IsPassable(null, false), cell.Objects.Count);
			if (verdict == FounderShrinePlacementVerdict.AdoptExact)
			{
				shrine = evidence;
				return true;
			}
			if (verdict != FounderShrinePlacementVerdict.Create)
			{
				failure = "the founder-shrine receipt conflicts with its exact ground";
				return false;
			}

			GameObject created = null;
			try
			{
				created = GameObject.Create(ShrineBlueprint);
				r_KingdomFounderShrine part = created?.GetPart<r_KingdomFounderShrine>();
				if (part == null) throw new InvalidOperationException("founder shrine blueprint has no history part");
				part.Stamp(token, founderName, deathTick, cause, history, cityName, fixtureObjectId);
				created.SetIntProperty("KingdomFounderShrine", 1);
				created.SetStringProperty("KingdomFounderDeathToken", token);
				cell.AddObject(created);
				if (created.CurrentCell != cell || !cell.Objects.Contains(created))
				{
					throw new InvalidOperationException("founder shrine did not remain on its frozen cell");
				}
				string id = created.ID;
				if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("founder shrine has no exact object id");
				shrine = created;
				return true;
			}
			catch (Exception ex)
			{
				r_KingdomFounderShrine retained = created?.GetPart<r_KingdomFounderShrine>();
				if (GameObject.Validate(created) && created.CurrentCell == cell
					&& retained != null && retained.Matches(token)
					&& cell.Objects.Contains(created) && !string.IsNullOrEmpty(created.IDIfAssigned))
				{
					// Cell.AddObject and ID assignment can publish before a later callback throws.
					// Exact ground is authority; adopt the proved object instead of minting again.
					shrine = created;
					return true;
				}
				if (GameObject.Validate(created) && created.CurrentCell == null)
				{
					try { created.Obliterate(); } catch { }
				}
				failure = "founder shrine placement failed (" + ex.GetType().Name + ")";
				return false;
			}
		}

	}
}
