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
		internal static bool TryHoldProcession(KingdomSystem system, string token, string zoneId,
			string fixtureObjectId, string manifest, out GameObject heir, out string failure)
		{
			heir = null;
			failure = "";
			Zone zone = ExactLoadedZone(zoneId);
			KingdomRiteAttendee[] rows;
			GameObject fixture = FindByAssignedId(zone, fixtureObjectId);
			if (system == null || zone == null || !OwnedGround(system, zoneId)
				|| !KingdomSuccessionRules.TryDecodeRiteManifest(manifest, out rows)
				|| !GameObject.Validate(fixture) || fixture.CurrentCell == null)
			{
				failure = "the frozen procession locus or manifest no longer exists exactly";
				return false;
			}

			List<Walker> walkers = new List<Walker>();
			for (int i = 0; i < rows.Length; i++)
			{
				GameObject body;
				string boundZone;
				if (!KingdomResidents.TryResolveBoundBody(system, rows[i].ResidentId,
					LoadZone: false, out body, out boundZone)
					|| !string.Equals(boundZone, zoneId, StringComparison.Ordinal)
					|| !string.Equals(body.IDIfAssigned, rows[i].ObjectId, StringComparison.Ordinal)
					|| !string.Equals(body.GetStringProperty("KingdomName")
						?? body.BaseDisplayNameStripped, rows[i].Name, StringComparison.Ordinal)
					|| body.Brain == null || !UnchangedPosts(body, rows[i]))
				{
					failure = "a named attendee no longer matches the frozen body, post, or home";
					return false;
				}
				Cell target = zone.GetCell(rows[i].RiteX, rows[i].RiteY);
				Cell original = zone.GetCell(rows[i].OriginalX, rows[i].OriginalY);
				if (target == null || original == null)
				{
					failure = "an attendee cell fell outside the frozen city ground";
					return false;
				}
				walkers.Add(new Walker(body, rows[i], target, original));
			}
			if (string.Equals(fixture.GetStringProperty("KingdomLastMourningRiteToken"),
				token, StringComparison.Ordinal))
			{
				// The fixture token is written only after everybody stood at the rite. A cut
				// while mourners were walking home resumes restoration, never the ceremony.
				if (!Walk(walkers[0], walkers[0].RiteCell))
				{
					failure = "the proved rite no longer has its exact heir at the boundary";
					return false;
				}
				for (int i = walkers.Count - 1; i >= 1; i--)
				{
					if (!Walk(walkers[i], walkers[i].OriginalCell))
					{
						failure = "the proved rite could not finish restoring a mourner";
						return false;
					}
				}
				for (int i = 0; i < walkers.Count; i++)
				{
					Cell wanted = i == 0 ? walkers[i].RiteCell : walkers[i].OriginalCell;
					if (walkers[i].Body.CurrentCell != wanted
						|| !UnchangedPosts(walkers[i].Body, walkers[i].Row))
					{
						failure = "the proved rite's restoration evidence is incomplete";
						return false;
					}
				}
				heir = walkers[0].Body;
				return true;
			}

			for (int i = 0; i < walkers.Count; i++)
			{
				if (!Walk(walkers[i], walkers[i].RiteCell))
				{
					ReturnAll(walkers, includeHeir: true);
					failure = "a named attendee could not walk the frozen route to the fixture";
					return false;
				}
			}
			for (int i = 0; i < walkers.Count; i++)
			{
				if (walkers[i].Body.CurrentCell != walkers[i].RiteCell
					|| !UnchangedPosts(walkers[i].Body, walkers[i].Row))
				{
					ReturnAll(walkers, includeHeir: true);
					failure = "the assembled procession lost exact physical or schedule evidence";
					return false;
				}
			}

			fixture.SetStringProperty("KingdomLastMourningRiteToken", token);
			fixture.SetStringProperty("KingdomLastMourningAttendees", manifest);
			for (int i = walkers.Count - 1; i >= 1; i--)
			{
				if (!Walk(walkers[i], walkers[i].OriginalCell))
				{
					ReturnAll(walkers, includeHeir: true);
					failure = "a mourner could not walk back to their exact prior place";
					return false;
				}
			}
			for (int i = 1; i < walkers.Count; i++)
			{
				if (walkers[i].Body.CurrentCell != walkers[i].OriginalCell
					|| !UnchangedPosts(walkers[i].Body, walkers[i].Row))
				{
					ReturnAll(walkers, includeHeir: true);
					failure = "a mourner's prior place, post, or home was not restored exactly";
					return false;
				}
			}
			heir = walkers[0].Body;
			return true;
		}

		internal static bool ProcessionEvidence(KingdomSystem system, string token, string zoneId,
			string fixtureObjectId, string manifest, out GameObject heir)
		{
			heir = null;
			Zone zone = ExactLoadedZone(zoneId);
			KingdomRiteAttendee[] rows;
			GameObject fixture = FindByAssignedId(zone, fixtureObjectId);
			if (system == null || zone == null || !OwnedGround(system, zoneId)
				|| !KingdomSuccessionRules.TryDecodeRiteManifest(manifest, out rows)
				|| !GameObject.Validate(fixture)
				|| !string.Equals(fixture.GetStringProperty("KingdomLastMourningRiteToken"),
					token, StringComparison.Ordinal)) return false;
			for (int i = 0; i < rows.Length; i++)
			{
				GameObject body;
				string bound;
				if (!KingdomResidents.TryResolveBoundBody(system, rows[i].ResidentId, false,
					out body, out bound) || body.IDIfAssigned != rows[i].ObjectId
					|| !UnchangedPosts(body, rows[i])) return false;
				Cell wanted = zone.GetCell(i == 0 ? rows[i].RiteX : rows[i].OriginalX,
					i == 0 ? rows[i].RiteY : rows[i].OriginalY);
				if (body.CurrentCell != wanted) return false;
				if (i == 0) heir = body;
			}
			return GameObject.Validate(heir);
		}

	}
}
