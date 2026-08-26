using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !TAF_TESTS
using XRL;
using XRL.World;
using XRL.World.Parts;
#endif

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritEngine
	{
		private static bool IsExactExistingApplication(IKingdomInheritEngineHost Host, Prepared Prepared)
		{
			try
			{
				if (Host.CountApplicationObjects(Prepared.Marker) != Prepared.Specs.Length)
				{
					return false;
				}
				for (int i = 0; i < Prepared.Specs.Length; i++)
				{
					if (!Host.HasExactApplicationObject(Prepared.Marker, Prepared.Specs[i],
						Prepared.CairnText))
					{
						return false;
					}
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryPreflight(IKingdomInheritEngineHost Host, Prepared Prepared,
			out SiteSnapshot Site, out KingdomInheritApplyResult Failure)
		{
			Site = null;
			Failure = null;
			if (Host.Width != KingdomInheritRules.TargetWidth
				|| Host.Height != KingdomInheritRules.TargetHeight)
			{
				Failure = Refused(KingdomInheritApplyFault.WrongZoneSize,
					"the inherited seat is not an eighty-by-twenty-five zone", Prepared.Marker);
				return false;
			}

			for (int i = 0; i < Prepared.Specs.Length; i++)
			{
				try
				{
					bool missingArchitecture = false;
					if (!Host.HasBlueprint(Prepared.Specs[i].Blueprint))
					{
						if (!Prepared.Specs[i].IsArchitecture)
						{
							Failure = Failed(KingdomInheritApplyFault.BlueprintMissing,
								"an allowlisted inherited object is not installed: "
								+ Prepared.Specs[i].Blueprint, Prepared.Marker);
							return false;
						}
						missingArchitecture = true;
					}
					if (Prepared.Specs[i].IsArchitecture)
					{
						ArchitectureLayoutSnapshot snapshot;
						if (!KingdomArchitectureRules.TryDecodeSnapshot(
							Prepared.Specs[i].ArchitectureSnapshot, out snapshot, out _))
						{
							Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
								"a frozen inherited architecture snapshot no longer decodes",
								Prepared.Marker);
							return false;
						}
						for (int p = 0; p < snapshot.Placements.Count; p++)
						{
							if (!Host.HasBlueprint(snapshot.Placements[p].Blueprint))
							{
								missingArchitecture = true;
							}
						}
						if (missingArchitecture
							&& !Host.HasBlueprint("r_KingdomCairn"))
						{
							Failure = Failed(KingdomInheritApplyFault.BlueprintMissing,
								"frozen architecture is incomplete and its bounded memory marker is absent",
								Prepared.Marker);
							return false;
						}
					}
				}
				catch (Exception ex)
				{
					Failure = Failed(KingdomInheritApplyFault.BlueprintMissing,
						"an allowlisted inherited object could not be inspected: " + ex.Message,
						Prepared.Marker);
					return false;
				}
			}

			SiteSnapshot site = new SiteSnapshot(Host.Width, Host.Height);
			for (int y = 0; y < Host.Height; y++)
			{
				for (int x = 0; x < Host.Width; x++)
				{
					KingdomInheritCellFacts facts;
					try
					{
						if (!Host.TryReadCell(x, y, out facts) || !facts.Exists)
						{
							Failure = Refused(KingdomInheritApplyFault.InvalidCell,
								"the inherited seat has a missing cell", Prepared.Marker);
							return false;
						}
					}
					catch (Exception ex)
					{
						Failure = Refused(KingdomInheritApplyFault.InvalidCell,
							"the inherited seat could not be inspected: " + ex.Message, Prepared.Marker);
						return false;
					}
					site.Cells[x, y] = facts;
				}
			}

			for (int i = 0; i < Prepared.Specs.Length; i++)
			{
				KingdomInheritBuildSpec spec = Prepared.Specs[i];
				int left = spec.FootprintX;
				int top = spec.FootprintY;
				for (int y = top; y < top + spec.FootprintHeight; y++)
				{
					for (int x = left; x < left + spec.FootprintWidth; x++)
					{
						if (x < 0 || y < 0 || x >= Host.Width || y >= Host.Height)
						{
							Failure = Refused(KingdomInheritApplyFault.InvalidCell,
								"an inherited footprint leaves the zone", Prepared.Marker);
							return false;
						}
						KingdomInheritCellFacts facts = site.Cells[x, y];
						if (facts.Connection && !spec.IsStreet)
						{
							Failure = Refused(KingdomInheritApplyFault.ConnectionCell,
								"an inherited footprint crosses a zone connection", Prepared.Marker);
							return false;
						}
						if (facts.Stairs)
						{
							Failure = Refused(KingdomInheritApplyFault.Stairs,
								"an inherited footprint crosses stairs", Prepared.Marker);
							return false;
						}
						if (facts.Occupied)
						{
							Failure = Refused(KingdomInheritApplyFault.Occupied,
								"an inherited footprint crosses an occupied cell", Prepared.Marker);
							return false;
						}
						if (facts.Terrain || !facts.Walkable)
						{
							Failure = Refused(KingdomInheritApplyFault.Terrain,
								"an inherited footprint crosses invalid terrain", Prepared.Marker);
							return false;
						}
						if (!spec.IsStreet) site.Claimed[x, y] = true;
					}
				}
			}

			int entryX = Prepared.Placement.EntryX;
			int entryY = Prepared.Placement.EntryY;
			if (entryX < 0 || entryY < 0 || entryX >= Host.Width || entryY >= Host.Height)
			{
				Failure = Refused(KingdomInheritApplyFault.InvalidCell,
					"the inherited plan's entry is outside the zone", Prepared.Marker);
				return false;
			}
			KingdomInheritCellFacts entry = site.Cells[entryX, entryY];
			if (entry.Stairs || entry.Terrain || entry.Occupied || !entry.Walkable
				|| site.Claimed[entryX, entryY])
			{
				Failure = Refused(KingdomInheritApplyFault.EntryToHeartPath,
					"the inherited plan's entry conflicts with the live site", Prepared.Marker);
				return false;
			}

			if (!HasEntryToHeartPath(site, Prepared))
			{
				Failure = Refused(KingdomInheritApplyFault.EntryToHeartPath,
					"the live site leaves no entry-to-heart path", Prepared.Marker);
				return false;
			}

			Site = site;
			return true;
		}

	}
}
