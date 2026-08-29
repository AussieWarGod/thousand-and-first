using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomCity
	{
		/// <summary>
		/// The settlers whose bodies do not stand where the hour puts them. One heavy unit each
		/// (&sect;0.0(b)), and never more than four a turn, because the heavy tier's cap is a
		/// frame-cost ceiling rather than an ordering preference.
		/// </summary>
		private static List<GameObject> Posted(Zone Z, KingdomSurvey Survey, Dictionary<int, GameObject> stations)
		{
			List<GameObject> wanting = new List<GameObject>();
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (GameObject.Validate(settler) && KingdomStations.Misplaced(settler, Z, now, stations))
				{
					wanting.Add(settler);
				}
			}
			return wanting;
		}

		/// <summary>Moves as many anchors as the budget bought, the visible ones first. Vanilla
		/// walks them the rest of the way (&sect;3.2(b)).</summary>
		private static int Anchor(Zone Z, List<GameObject> posted, Dictionary<int, GameObject> stations, int visible, int rest, long TimeTicks)
		{
			int spentVisible = 0;
			int spentRest = 0;
			for (int i = 0; i < posted.Count; i++)
			{
				bool seen = Visible(posted[i].CurrentCell);
				if (seen && spentVisible >= visible)
				{
					continue;
				}
				if (!seen && spentRest >= rest)
				{
					continue;
				}
				if (!KingdomStations.Place(Z, posted[i], TimeTicks, stations))
				{
					continue;
				}
				if (seen) { spentVisible++; } else { spentRest++; }
			}
			return spentVisible + spentRest;
		}

		private static int VisibleCount(List<GameObject> bodies)
		{
			int seen = 0;
			for (int i = 0; i < bodies.Count; i++)
			{
				if (Visible(bodies[i].CurrentCell))
				{
					seen++;
				}
			}
			return seen;
		}

		/// <summary>The player's own field of view, asked of the engine rather than approximated.
		/// A cell in a zone that is not active is never visible, which is exactly right for a
		/// prefetched zone: nothing in it is what the founder is looking at.</summary>
		private static bool Visible(Cell at)
		{
			return at != null && at.IsVisible();
		}

		private sealed class ContainerGround
		{
			internal KingdomContainerCatchUpRow[] Rows;
			internal LiquidVolume[] Water;
			internal GameObject[] Food;

			internal static ContainerGround Take(KingdomSurvey survey)
			{
				int waterCount = (survey == null) ? 0 : survey.Stores.Count;
				int foodCount = (survey == null) ? 0 : survey.Larders.Count;
				KingdomConstructionInputLeaseSnapshot leases;
				string authorityFailure;
				if (!KingdomOrdinaryFoodAuthority.TryCapture(out leases, out authorityFailure))
					leases = null;
				ContainerGround ground = new ContainerGround();
				ground.Rows = new KingdomContainerCatchUpRow[waterCount + foodCount];
				ground.Water = new LiquidVolume[waterCount + foodCount];
				ground.Food = new GameObject[waterCount + foodCount];
				for (int i = 0; i < waterCount; i++)
				{
					LiquidVolume store = survey.Stores[i];
					GameObject owner = (store == null) ? null : store.ParentObject;
					int room = (store != null && store.MaxVolume >= 0
						&& store.Volume < store.MaxVolume && KingdomLiquids.CanReceiveFreshWater(store))
						? store.MaxVolume - store.Volume : 0;
					int contents = KingdomLiquids.HasFreshWater(store) ? store.Volume : 0;
					ground.Rows[i] = new KingdomContainerCatchUpRow(
						KingdomCityRules.StableId(GameObject.Validate(owner)
							? owner.IDIfAssigned : ""),
						OrdinalOf(owner), KingdomStockKind.Water,
						GameObject.Validate(owner) && Visible(owner.CurrentCell), room, contents);
					ground.Water[i] = store;
				}
				for (int i = 0; i < foodCount; i++)
				{
					int index = waterCount + i;
					GameObject larder = survey.Larders[i];
					int physical = GameObject.Validate(larder) ? KingdomSurvey.HeldIn(larder) : 0;
					int contents = KingdomOrdinaryFoodAuthority.AvailableIn(larder, leases);
					int room = GameObject.Validate(larder)
						? KingdomSurvey.CapacityOf(larder) - physical : 0;
					if (room < 0) room = 0;
					ground.Rows[index] = new KingdomContainerCatchUpRow(
						KingdomCityRules.StableId(GameObject.Validate(larder)
							? larder.IDIfAssigned : ""),
						OrdinalOf(larder), KingdomStockKind.Food,
						GameObject.Validate(larder) && Visible(larder.CurrentCell), room, contents);
					ground.Food[index] = larder;
				}
				return ground;
			}
		}

		private static bool SettleContainer(KingdomSystem System, KingdomSurvey Survey,
			ContainerGround ground, int source, KingdomStockKind kind,
			KingdomUnitDirection direction, int offered, out int applied)
		{
			applied = 0;
			if (ground == null || source < 0 || source >= ground.Rows.Length || offered <= 0
				|| ground.Rows[source].Kind != kind) return false;
			if (kind == KingdomStockKind.Water)
			{
				LiquidVolume store = ground.Water[source];
				if (direction == KingdomUnitDirection.Land)
				{
					applied = Survey.StoreIn(store, offered);
					return applied == offered;
				}
				return Survey.TryLeakFromExact(store, offered, out applied);
			}
			if (kind == KingdomStockKind.Food)
			{
				GameObject larder = ground.Food[source];
				if (direction == KingdomUnitDirection.Land)
				{
					applied = Survey.StoreFoodIn(larder, offered, CropOf(System));
					if (applied > 0) System.Ledger.Harvested += applied;
					return applied == offered;
				}
				return Survey.TrySpoilFromExact(larder, offered, out applied);
			}
			// Materials do not yet have a civic-container ground adapter. Their signed debt remains
			// honest and measured as blocked rather than being silently cleared by a proxy.
			return false;
		}

	}
}
