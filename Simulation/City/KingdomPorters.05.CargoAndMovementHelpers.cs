using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomPorters
	{

		/// <summary>The larder with room that the city dedicated first. A stored fact and not a
		/// ranking recomputed from contents, so a reload picks the same one (&sect;3.9).</summary>
		private static GameObject NearestLarderWithRoom(KingdomSurvey Survey)
		{
			GameObject best = null;
			int bestOrdinal = int.MaxValue;
			for (int i = 0; i < Survey.Larders.Count; i++)
			{
				GameObject container = Survey.Larders[i];
				if (!GameObject.Validate(container) || container.Inventory == null || container.CurrentCell == null)
				{
					continue;
				}
				if (KingdomSurvey.CapacityOf(container) - KingdomSurvey.HeldIn(container) <= 0)
				{
					continue;
				}
				int ordinal = KingdomCityRules.DrainOrdinal(container.GetIntProperty(KingdomCity.DedicationOrderProperty));
				if (ordinal < bestOrdinal)
				{
					bestOrdinal = ordinal;
					best = container;
				}
			}
			return best;
		}

		private static GameObject LarderAt(Cell at)
		{
			if (at == null)
			{
				return null;
			}
			return at.GetFirstObjectWithPart("Inventory", delegate(GameObject candidate)
			{
				return GameObject.Validate(candidate) && candidate.GetIntProperty(KingdomAdopt.LarderProperty) == 1;
			});
		}

		/// <summary>The real crop, minted onto a real back and marked as the simulation's own
		/// (&sect;3.2(a)). Refuses a blueprint that is not food for the same reason
		/// <c>KingdomSurvey.StoreFood</c> does: an unbounded spawn of a thing nothing counts.</summary>
		private static int Load(GameObject Body, string Blueprint, int Amount)
		{
			if (!GameObject.Validate(Body) || Body.Inventory == null || string.IsNullOrEmpty(Blueprint) || Amount <= 0)
			{
				return 0;
			}
			int carried = 0;
			for (int i = 0; i < Amount; i++)
			{
				GameObject food = GameObject.Create(Blueprint);
				if (food == null)
				{
					break;
				}
				if (!food.HasPart("Food") && !food.HasPart("PreparedCookingIngredient"))
				{
					food.Obliterate();
					break;
				}
				food.SetIntProperty(StockProperty, 1);
				Body.Inventory.AddObject(food, Silent: true);
				carried++;
			}
			return carried;
		}

		/// <summary>The anchor discipline &sect;3.2(b) rides: a carrier that wanders is a carrier
		/// vanilla will not walk back to anything.</summary>
		private static void Settle(GameObject Body)
		{
			Brain brain = Body.Brain;
			if (brain == null)
			{
				return;
			}
			brain.Wanders = false;
			brain.WandersRandomly = false;
			brain.Allegiance.Hostile = false;
		}

		private static void Walk(GameObject Body, Zone Z, int x, int y)
		{
			Cell target = Z.GetCell(x, y);
			Brain brain = Body.Brain;
			if (target == null || brain == null)
			{
				return;
			}
			brain.Stay(target);
			brain.PushGoal(new MoveTo(target, careful: true));
		}

		private static bool Near(GameObject Body, int x, int y)
		{
			Cell at = Body.CurrentCell;
			if (at == null)
			{
				return false;
			}
			int dx = at.X - x;
			int dy = at.Y - y;
			if (dx < 0) { dx = -dx; }
			if (dy < 0) { dy = -dy; }
			return dx <= 1 && dy <= 1;
		}

		private static Cell Standing(Zone Z, short x, short y)
		{
			Cell at = Z.GetCell(x, y);
			if (at != null && at.IsPassable() && at.IsEmptyOfSolid())
			{
				return at;
			}
			// Visit each coordinate at most once in deterministic Chebyshev rings. This finds the
			// nearest viable stand without allocating/scanning the engine's whole empty-cell list.
			int farthest = Math.Max(Z.Width, Z.Height);
			for (int radius = 1; radius < farthest; radius++)
			{
				int y1 = Math.Max(0, y - radius);
				int y2 = Math.Min(Z.Height - 1, y + radius);
				int x1 = Math.Max(0, x - radius);
				int x2 = Math.Min(Z.Width - 1, x + radius);
				for (int cy = y1; cy <= y2; cy++)
				{
					for (int cx = x1; cx <= x2; cx++)
					{
						if (Math.Max(Math.Abs(cx - x), Math.Abs(cy - y)) != radius) continue;
						Cell candidate = Z.GetCell(cx, cy);
						if (candidate != null && candidate.IsPassable()
							&& candidate.IsEmptyOfSolid()) return candidate;
					}
				}
			}
			return null;
		}

		private static bool Near(Cell at, int x, int y)
		{
			int dx = at.X - x;
			int dy = at.Y - y;
			if (dx < 0) { dx = -dx; }
			if (dy < 0) { dy = -dy; }
			return dx <= 2 && dy <= 2;
		}

		/// <summary>The carrier for this job, if their ground is where the founder is standing.
		/// A body whose zone is on disk does not resolve, and that is the whole point: the sweep
		/// deals with those and a close does not reach into a frozen zone.</summary>
		private static GameObject Resolve(int jobId)
		{
			Zone zone = (The.Player == null) ? null : The.Player.CurrentZone;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone);
			if (survey != null) return survey.FindTransient(jobId);
			List<GameObject> found = (zone == null) ? null : zone.GetObjects();
			for (int i = 0; found != null && i < found.Count; i++)
			{
				if (GameObject.Validate(found[i]) && found[i].GetIntProperty(KingdomResidents.JobIdProperty) == jobId)
				{
					return found[i];
				}
			}
			return null;
		}

		/// <summary>
		/// The settlement id every draw about this city's deliveries hangs off.
		/// <para>
		/// The seated city's persisted immutable id. Names and seat order are prose only, so a
		/// rename or seat exchange cannot move a delivery draw onto another subject.
		/// </para>
		/// </summary>
		private static string SeedLabel(KingdomSystem System)
		{
			return KingdomChronicle.SettlementId(System);
		}

		private static void Refuse(string step, KingdomCityFault fault)
		{
			KingdomLog.Log("porter: " + step + " refused (" + fault + "); nothing was minted");
		}
	}
}
