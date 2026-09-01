using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	[Serializable]
	public class r_KingdomPlotWorks : IPart
	{
		/// <summary>Registry key of the design being raised.</summary>
		public string DesignKey;

		/// <summary>What the founder is told this is, when a stage is announced.</summary>
		public string DisplayName;

		/// <summary>Low corner of the plot, in cells.</summary>
		public int X1;

		/// <summary>Low corner of the plot, in cells.</summary>
		public int Y1;

		/// <summary>High corner of the plot, in cells. Inclusive.</summary>
		public int X2;

		/// <summary>High corner of the plot, in cells. Inclusive.</summary>
		public int Y2;

		/// <summary>Tick the ground was staked.</summary>
		public long StartTick;

		/// <summary>Ticks the whole raising takes, clearing and enclosure included.</summary>
		public long TotalTicks;

		/// <summary>Stage already applied, as <c>KingdomPlotRules.PlotStage</c>. Held as an int so
		/// the field's serialized type never depends on an enum's backing type.</summary>
		public int StageApplied;

		/// <summary>True for a plot that is never roofed.</summary>
		public bool Open;

		/// <summary>True when this plot is being carved rather than built: the rock is the wall.</summary>
		public bool Carved;

		/// <summary>Blueprint the enclosure is raised in. Empty on an open or carved plot.</summary>
		public string WallBlueprint;

		/// <summary>Frozen legacy/third-party furnishing fallback table. May be null; current
		/// authored realizations preserve it as metadata but do not roll it.</summary>
		public string ContentsTable;

		/// <summary>Hands the finished work wants, carried through to the finished object.</summary>
		public int StaffNeeded;

		/// <summary>Whether those hands are a threshold rather than a scale.</summary>
		public bool ThresholdManning;

		/// <summary>Defence the finished work carries, already resolved at staking time.</summary>
		public int DefencePending;

		/// <summary>Whether this plot has a doorway at all. False for an open plot, and for a
		/// rect too small to have a border cell that is not a corner.</summary>
		public bool HasDoor;

		/// <summary>Doorway x, decided when the ground was staked rather than when the walls go
		/// up: which way a building faces is part of the plan, not an afterthought, and the
		/// carving has to know it before it cuts.</summary>
		public int DoorX;

		/// <summary>Doorway y. See <see cref="DoorX"/>.</summary>
		public int DoorY;

		/// <summary>The ground this plot holds.</summary>
		public KingdomPlotRules.PlotRect Rect()
		{
			return new KingdomPlotRules.PlotRect(X1, Y1, X2, Y2);
		}
	}
}
