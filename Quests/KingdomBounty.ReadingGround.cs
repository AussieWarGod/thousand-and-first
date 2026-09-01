using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;
	public static partial class KingdomBounty
	{
		/// <summary>Every notice standing on this ground, in zone order.</summary>
		public static List<GameObject> Notices(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null)
			{
				return found;
			}
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.HasPart(typeof(r_KingdomNotice)))
				{
					found.Add(item);
				}
			}
			return found;
		}

		/// <summary>
		/// Containers within reach of the founder that a fetch notice could be posted over: things
		/// that hold material, are not already dedicated stockpiles, and are not already marked for
		/// another notice.
		/// </summary>
		private static List<GameObject> MarkablePiles(Zone Z, GameObject Founder)
		{
			List<GameObject> found = new List<GameObject>();
			Cell here = (Founder != null) ? Founder.CurrentCell : null;
			if (Z == null || here == null)
			{
				return found;
			}
			List<Cell> reach = new List<Cell>();
			reach.Add(here);
			here.GetAdjacentCells(1, reach);
			for (int i = 0; i < reach.Count; i++)
			{
				Cell cell = reach[i];
				if (cell == null || cell.ParentZone != Z)
				{
					continue;
				}
				foreach (GameObject item in cell.GetObjects())
				{
					if (item.Inventory == null || item.IsCreature || KingdomMaterials.IsStockpile(item))
					{
						continue;
					}
					if (!string.IsNullOrEmpty(item.GetStringProperty(FetchMarkProperty)) || found.Contains(item))
					{
						continue;
					}
					if (MaterialUnits(item) > 0)
					{
						found.Add(item);
					}
				}
			}
			return found;
		}

		private static int MaterialUnits(GameObject Container)
		{
			if (Container == null || Container.Inventory == null)
			{
				return 0;
			}
			int units = 0;
			foreach (GameObject held in Container.Inventory.Objects)
			{
				if (KingdomMaterials.TryOrdinaryMaterialOf(held, out _))
				{
					units += held.Count;
				}
			}
			return units;
		}

		private static GameObject FindPile(Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			if (Z == null || Data == null || string.IsNullOrEmpty(Data.PileId))
			{
				return null;
			}
			GameObject pile = Z.FindObjectByID(Data.PileId);
			if (pile == null || !GameObject.Validate(pile))
			{
				return null;
			}
			// The mark is the designation, so the mark is what is checked - not the id we happen
			// to have stored. A founder who cleared it has taken their permission back.
			string marked = pile.GetStringProperty(FetchMarkProperty);
			if (string.IsNullOrEmpty(marked))
			{
				return null;
			}
			if (Notice != null && marked != Notice.IDIfAssigned)
			{
				return null;
			}
			return pile;
		}

		private static void ClearFetchMark(Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			GameObject pile = FindPile(Z, Notice, Data);
			pile?.RemoveStringProperty(FetchMarkProperty);
			Data.PileId = null;
		}

		/// <summary>
		/// Every zone touching the realm's claim that the realm does not hold: the ground a scout
		/// can be sent to look at. Sorted ordinally, so the kernel's pick lands on the same ground
		/// on any reload.
		/// </summary>
		public static List<string> Frontier(KingdomSystem System)
		{
			List<string> found = new List<string>();
			if (System == null || !System.Founded)
			{
				return found;
			}
			for (int i = 0; i < System.ClaimedZones.Count; i++)
			{
				string world;
				int px;
				int py;
				int zx;
				int zy;
				int z;
				if (!ZoneID.Parse(System.ClaimedZones[i], out world, out px, out py, out zx, out zy, out z))
				{
					continue;
				}
				int globalX = px * KingdomBountyRules.ZonesPerParasang + zx;
				int globalY = py * KingdomBountyRules.ZonesPerParasang + zy;
				for (int step = 0; step < KingdomBountyRules.NeighbourCount; step++)
				{
					int nx;
					int ny;
					if (!KingdomBountyRules.TryNeighbour(globalX, globalY, step, out nx, out ny))
					{
						continue;
					}
					int npx;
					int nzx;
					int npy;
					int nzy;
					if (!KingdomBountyRules.TrySplitGlobal(nx, out npx, out nzx) || !KingdomBountyRules.TrySplitGlobal(ny, out npy, out nzy))
					{
						continue;
					}
					string id = ZoneID.Assemble(world, npx, npy, nzx, nzy, z);
					if (System.ClaimedZones.Contains(id) || found.Contains(id))
					{
						continue;
					}
					if (System.NonSeatClaimsZone(id))
					{
						continue;
					}
					found.Add(id);
				}
			}
			found.Sort(StringComparer.Ordinal);
			return found;
		}

		// ==================================================================================
		// Prose on the object itself
		// ==================================================================================

		private static void Describe(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			BountyTask task = (BountyTask)Data.TaskCode;
			string text = KingdomBountyRules.NoticeText(task, Data.Price, DetailOf(System, Z, Data)) + " " + Progress(System, Data);
			Notice.DisplayName = string.IsNullOrEmpty(Data.WorkerName) ? "a posted notice" : "a claimed notice";
			Notice.RequirePart<Description>().Short = text;
		}

		private static string DetailOf(KingdomSystem System, Zone Z, r_KingdomNotice Data)
		{
			switch ((BountyTask)Data.TaskCode)
			{
			case BountyTask.Clearance:
				return "The cord runs round " + Data.Magnitude + " paces of it.";
			case BountyTask.Fetch:
			{
				GameObject pile = FindPile(Z, null, Data);
				return (pile == null) ? null : ("The mark is cut into " + pile.ShortDisplayName + ".");
			}
			case BountyTask.Manning:
				return ManningDetail(Data);
			default:
				return null;
			}
		}

		private static string Progress(KingdomSystem System, r_KingdomNotice Data)
		{
			if (Data.Done)
			{
				int owed = Data.Price - Data.Paid;
				return (owed > 0)
					? ("{{r|The work is done and " + owed + ((owed == 1) ? " dram is" : " drams are") + " still owed on it.}}")
					: "{{G|The work is done and the price is paid.}}";
			}
			if (string.IsNullOrEmpty(Data.WorkerName))
			{
				string reason = KingdomBountyRules.BlockReason((BountyBlock)Data.AnnouncedBlock,
					(BountyTask)Data.TaskCode, KingdomPresentation.Rich(System.SeatName));
				return (reason == null) ? "{{K|Nobody has taken it yet.}}" : ("{{r|" + reason + "}}");
			}
			if ((BountyTask)Data.TaskCode == BountyTask.Manning) return ManningProgress(Data);
			if (Data.DueTick <= 0L)
			{
				return "{{W|" + KingdomPresentation.Rich(Data.WorkerName) + " has it, and is at it now.}}";
			}
			long left = Data.DueTick - The.Game.TimeTicks;
			int days = (int)((left + KingdomRules.TicksPerDay - 1L) / KingdomRules.TicksPerDay);
			if (days <= 0)
			{
				return "{{W|" + KingdomPresentation.Rich(Data.WorkerName) + " has it, and is due back.}}";
			}
			return "{{W|" + KingdomPresentation.Rich(Data.WorkerName) + " has it. " + days + ((days == 1) ? " day" : " days") + " left of it.}}";
		}

		private static string StatusLine(KingdomSystem System, GameObject Notice)
		{
			r_KingdomNotice data = Notice.GetPart<r_KingdomNotice>();
			if (data == null)
			{
				return "{{K|an unreadable notice}}";
			}
			int price = KingdomBountyRules.ClampPrice(data.Price);
			return KingdomBountyRules.TaskName((BountyTask)data.TaskCode).Capitalize()
				+ " {{K|(" + price + ((price == 1) ? " dram" : " drams") + ")}} -- " + Progress(System, data);
		}

		private static Cell HeartCell(Zone Z, GameObject Founder)
		{
			int heartX = -1;
			int heartY = -1;
			KingdomSystem.Guard("bounty: heart", delegate
			{
				List<KingdomLayoutRules.LayoutMark> marks = KingdomLayout.ReadMarks(Z);
				int centreX;
				int centreY;
				if (KingdomLayoutRules.TryHeart(marks, out centreX, out centreY))
				{
					heartX = centreX;
					heartY = centreY;
				}
			});
			Cell here = (Founder != null) ? Founder.CurrentCell : null;
			if (heartX < 0 && here != null)
			{
				heartX = here.X;
				heartY = here.Y;
			}
			if (heartX < 0)
			{
				return null;
			}
			// Empty cells only, per the protection law: a notice never lands on top of anything,
			// and least of all on top of something the founder put there.
			for (int radius = 0; radius <= 6; radius++)
			{
				for (int y = heartY - radius; y <= heartY + radius; y++)
				{
					for (int x = heartX - radius; x <= heartX + radius; x++)
					{
						if (radius > 0 && x != heartX - radius && x != heartX + radius && y != heartY - radius && y != heartY + radius)
						{
							continue;
						}
						Cell candidate = Z.GetCell(x, y);
						if (candidate != null && candidate.IsEmpty() && candidate.IsPassable())
						{
							return candidate;
						}
					}
				}
			}
			return null;
		}
	}
}
