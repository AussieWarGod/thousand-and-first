using System.Collections.Generic;

namespace ThousandAndFirst
{
	// --- Materialised records -------------------------------------------------------------

	public sealed class ArchitectureCellState
	{
		public int X;
		public int Y;
		public bool Claim;
		public ArchitecturePassability Passability;
		public ArchitectureCover Cover;
	}

	public sealed class ArchitecturePlacement
	{
		public ArchitectureLayer Layer;
		public int X;
		public int Y;
		public string Blueprint;
		public string Slot;
		/// <summary>Canonical material key frozen from the palette slot that authored this piece.</summary>
		public string Material;
		/// <summary>Canonical minimum craft-rung key frozen from the palette slot.</summary>
		public string MinTech;
		/// <summary>Optional roster gate required to author this exact placement.</summary>
		public string Knowledge;
		/// <summary>Optional power authority; nonempty values need frozen runtime proof.</summary>
		public string Power;
		/// <summary>Natural scenery is authored truth but does not consume the paid build claim.</summary>
		public bool Natural;
		/// <summary>Bind an immutable pre-existing world relic; never create or clear it.</summary>
		public bool ExistingAuthority;
		/// <summary>Stable anchor whose state must survive a tier delta; null for stateless pieces.</summary>
		public string StatefulAnchor;
	}

	public sealed class ArchitectureAnchor
	{
		public string Key;
		public int X;
		public int Y;
		public ArchitectureAnchorAccess Access;
	}

	public sealed class ArchitectureLayoutSnapshot
	{
		public string PlanKey;
		public string BindingKey;
		public string BuildKey;
		public string TierKey;
		public string VariantKey;
		public string PaletteKey;
		public string LotType;
		public ArchitectureLotSize LotSize;
		public ArchitectureFacing Facing;
		public int Width;
		public int Height;
		public int MainX;
		public int MainY;
		public List<ArchitectureCellState> Cells = new List<ArchitectureCellState>();
		/// <summary>Authored scenery only. Runtime-owned main behavior root is never included.</summary>
		public List<ArchitecturePlacement> Placements = new List<ArchitecturePlacement>();
		public List<ArchitectureAnchor> Anchors = new List<ArchitectureAnchor>();
	}

	public sealed class ArchitectureCellDelta
	{
		public int X;
		public int Y;
		public ArchitectureCellState Before;
		public ArchitectureCellState After;
	}

	public sealed class ArchitectureLayoutDelta
	{
		/// <summary>
		/// Scenery-only exact delta. Caller must preserve the runtime-owned behavior root at main.
		/// </summary>
		public ArchitectureLayoutSnapshot Before;
		public ArchitectureLayoutSnapshot After;
		public List<ArchitecturePlacement> Retained = new List<ArchitecturePlacement>();
		/// <summary>Successor-side partner for each retained predecessor placement.</summary>
		public List<ArchitecturePlacement> RetainedAfter = new List<ArchitecturePlacement>();
		public List<ArchitecturePlacement> Removed = new List<ArchitecturePlacement>();
		public List<ArchitecturePlacement> Added = new List<ArchitecturePlacement>();
		public List<ArchitectureCellDelta> Cells = new List<ArchitectureCellDelta>();
	}

	/// <summary>One exact time × labour × infrastructure accrual result.</summary>
	public sealed class ArchitectureLabourProgress
	{
		public long PreviousTick;
		public long NextTick;
		public long RemainingTicks;
		public long WorkedTicks;
		public long CompletionTick;
		public bool Complete;
	}
}
