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
	/// <summary>The disposition of one reserved legacy after its site-builder ran.</summary>
	internal enum KingdomInheritApplyStatus
	{
		Applied = 0,
		AlreadyApplied = 1,
		Refused = 2,
		Failed = 3
	}

	internal enum KingdomInheritApplyFault
	{
		None = 0,
		NullInput = 1,
		LegacyNotPromoted = 2,
		ReceiptNotReserved = 3,
		ReceiptMismatch = 4,
		TargetGameMismatch = 5,
		TargetZoneMismatch = 6,
		PlanInvalid = 7,
		WrongZoneSize = 8,
		ApplicationConflict = 9,
		PartialApplication = 10,
		BlueprintMissing = 11,
		InvalidCell = 12,
		ConnectionCell = 13,
		Terrain = 14,
		Occupied = 15,
		Stairs = 16,
		EntryToHeartPath = 17,
		ObjectCreation = 18,
		ObjectNotEmpty = 19,
		ObjectPlacement = 20,
		MarkerWrite = 21
	}

	/// <summary>
	/// A coordinator-facing result. Applied and AlreadyApplied may commit the exact reservation;
	/// a site refusal may release it; a failed/partial transaction deliberately does neither.
	/// </summary>
	internal sealed class KingdomInheritApplyResult
	{
		internal readonly KingdomInheritApplyStatus Status;

		internal readonly KingdomInheritApplyFault Fault;

		internal readonly string Detail;

		internal readonly string ApplicationMarker;

		internal readonly int PlacedCount;

		internal readonly bool FreshEmptyVerified;

		internal bool ShouldCommit
		{
			get
			{
				return Status == KingdomInheritApplyStatus.Applied
					|| Status == KingdomInheritApplyStatus.AlreadyApplied;
			}
		}

		internal bool ShouldRelease
		{
			get { return Status == KingdomInheritApplyStatus.Refused; }
		}

		internal KingdomInheritApplyResult(KingdomInheritApplyStatus Status,
			KingdomInheritApplyFault Fault, string Detail, string ApplicationMarker,
			int PlacedCount, bool FreshEmptyVerified)
		{
			this.Status = Status;
			this.Fault = Fault;
			this.Detail = Detail ?? "";
			this.ApplicationMarker = ApplicationMarker ?? "";
			this.PlacedCount = PlacedCount;
			this.FreshEmptyVerified = FreshEmptyVerified;
		}
	}

	/// <summary>Read-only facts copied from a cell during preflight.</summary>
	internal struct KingdomInheritCellFacts
	{
		internal bool Exists;

		internal bool Occupied;

		internal bool Terrain;

		internal bool Stairs;

		internal bool Connection;

		internal bool Walkable;
	}

	/// <summary>One allowlisted object the transaction will construct after preflight.</summary>
	internal sealed class KingdomInheritBuildSpec
	{
		internal readonly int Index;

		internal readonly string Key;

		internal readonly string Blueprint;

		internal readonly int X;

		internal readonly int Y;

		internal readonly int Condition;

		internal readonly KingdomInheritWorkState State;

		internal readonly int FootprintWidth;

		internal readonly int FootprintHeight;

		internal KingdomInheritBuildSpec(int Index, KingdomInheritWork Work, string Blueprint,
			int FootprintWidth, int FootprintHeight)
		{
			this.Index = Index;
			Key = Work.Key;
			this.Blueprint = Blueprint;
			X = Work.X;
			Y = Work.Y;
			Condition = Work.Condition;
			State = Work.State;
			this.FootprintWidth = FootprintWidth;
			this.FootprintHeight = FootprintHeight;
		}
	}

	/// <summary>
	/// Narrow engine seam. All inspection methods are called before TryCreateFresh; tests enforce
	/// that a refused site crosses no mutating method.
	/// </summary>
	internal interface IKingdomInheritEngineHost
	{
		int Width { get; }

		int Height { get; }

		string ZoneId { get; }

		string TargetGameId { get; }

		string ReadApplicationMarker();

		int CountApplicationObjects(string Marker);

		bool HasAnyApplicationObjects();

		bool HasExactApplicationObject(string Marker, KingdomInheritBuildSpec Spec, string CairnText);

		bool HasBlueprint(string Blueprint);

		bool TryReadCell(int X, int Y, out KingdomInheritCellFacts Facts);

		bool TryCreateFresh(KingdomInheritBuildSpec Spec, string Marker, string CairnText,
			out object Handle, out string Failure);

		bool IsFreshEmpty(object Handle);

		bool TryPlace(object Handle, int X, int Y, out string Failure);

		bool Discard(object Handle);

		bool TryWriteApplicationMarker(string Marker, out string Failure);

		bool TryRemoveApplicationMarker(string Marker);
	}

	/// <summary>
	/// Engine-coupled reconstruction for one exact promoted record and one exact reserved receipt.
	/// The state transform and deterministic geometry stay in <see cref="KingdomInheritRules"/>;
	/// this type proves the live site, creates empty objects, and owns the application marker.
	/// </summary>
	internal static class KingdomInheritEngine
	{
		internal const int ReconstructionVersion = 1;

		internal const string ZoneMarkerProperty = "ThousandAndFirst.Inherit.Application";

		internal const string ObjectMarkerProperty = "ThousandAndFirst.Inherit.Application";

		internal const string ObjectKeyProperty = "ThousandAndFirst.Inherit.Key";

		internal const string ObjectFreshEmptyProperty = "ThousandAndFirst.Inherit.FreshEmpty";

		internal const string ObjectIndexProperty = "ThousandAndFirst.Inherit.Index";

		internal const string ObjectStateProperty = "ThousandAndFirst.Inherit.State";

		internal const string ObjectConditionProperty = "ThousandAndFirst.Inherit.Condition";

		private const int MaxCairnChars = 24000;

		private sealed class SiteSnapshot
		{
			internal readonly KingdomInheritCellFacts[,] Cells;

			internal readonly bool[,] Claimed;

			internal SiteSnapshot(int Width, int Height)
			{
				Cells = new KingdomInheritCellFacts[Width, Height];
				Claimed = new bool[Width, Height];
			}
		}

		private sealed class Prepared
		{
			internal KingdomSealRecord Legacy;

			internal KingdomInheritPlacement Placement;

			internal KingdomInheritBuildSpec[] Specs;

			internal string Marker;

			internal string CairnText;
		}

		internal static KingdomInheritApplyResult Apply(KingdomSealRecord Legacy,
			KingdomSealReceipt Receipt, string TargetZoneId, IKingdomInheritEngineHost Host)
		{
			Prepared prepared;
			KingdomInheritApplyResult refusal;
			if (!TryPrepare(Legacy, Receipt, TargetZoneId, Host, out prepared, out refusal))
			{
				return refusal;
			}

			// An exact, complete marker wins before occupancy checks: those cells are now occupied by
			// this very application. A marker without its exact rows is a repair case, not a rebuild.
			string existingMarker;
			try
			{
				existingMarker = Host.ReadApplicationMarker() ?? "";
			}
			catch (Exception ex)
			{
				return Failed(KingdomInheritApplyFault.PartialApplication,
					"the inherited site's application marker could not be inspected: " + ex.Message,
					prepared.Marker);
			}
			if (existingMarker.Length > 0)
			{
				if (existingMarker != prepared.Marker)
				{
					return Refused(KingdomInheritApplyFault.ApplicationConflict,
						"this zone already carries a different inherited-site application", prepared.Marker);
				}
				if (!IsExactExistingApplication(Host, prepared))
				{
					return Failed(KingdomInheritApplyFault.PartialApplication,
						"the inherited site's marker exists without its exact placed rows", prepared.Marker);
				}
				return new KingdomInheritApplyResult(KingdomInheritApplyStatus.AlreadyApplied,
					KingdomInheritApplyFault.None, "the exact inherited site is already applied",
					prepared.Marker, prepared.Specs.Length, true);
			}
			try
			{
				if (Host.HasAnyApplicationObjects())
				{
					return Failed(KingdomInheritApplyFault.PartialApplication,
						"inherited-site objects exist without a zone application marker", prepared.Marker);
				}
			}
			catch (Exception ex)
			{
				return Failed(KingdomInheritApplyFault.PartialApplication,
					"inherited-site objects could not be inspected: " + ex.Message, prepared.Marker);
			}

			SiteSnapshot site;
			if (!TryPreflight(Host, prepared, out site, out refusal))
			{
				return refusal;
			}

			// No object creation happens above this line. Create every object off-zone and prove every
			// one empty before the first live cell changes.
			object[] handles = new object[prepared.Specs.Length];
			for (int i = 0; i < prepared.Specs.Length; i++)
			{
				string failure;
				try
				{
					if (!Host.TryCreateFresh(prepared.Specs[i], prepared.Marker, prepared.CairnText,
						out handles[i], out failure) || handles[i] == null)
					{
						DiscardAll(Host, handles);
						return Failed(KingdomInheritApplyFault.ObjectCreation,
							Nonempty(failure, "an inherited object could not be created"), prepared.Marker);
					}
					if (!Host.IsFreshEmpty(handles[i]))
					{
						DiscardAll(Host, handles);
						return Failed(KingdomInheritApplyFault.ObjectNotEmpty,
							"a fresh inherited object carried contents, liquid, or charge", prepared.Marker);
					}
				}
				catch (Exception ex)
				{
					DiscardAll(Host, handles);
					return Failed(KingdomInheritApplyFault.ObjectCreation,
						"an inherited object could not be prepared: " + ex.Message, prepared.Marker);
				}
			}

			for (int i = 0; i < prepared.Specs.Length; i++)
			{
				string failure;
				try
				{
					if (!Host.TryPlace(handles[i], prepared.Specs[i].X, prepared.Specs[i].Y, out failure))
					{
						bool clean = DiscardAll(Host, handles);
						return Failed(clean ? KingdomInheritApplyFault.ObjectPlacement
							: KingdomInheritApplyFault.PartialApplication,
							Nonempty(failure, "an inherited object could not enter its prepared cell"),
							prepared.Marker);
					}
				}
				catch (Exception ex)
				{
					bool clean = DiscardAll(Host, handles);
					return Failed(clean ? KingdomInheritApplyFault.ObjectPlacement
						: KingdomInheritApplyFault.PartialApplication,
						"an inherited object could not be placed: " + ex.Message, prepared.Marker);
				}
			}

			// Cell.AddObject is an eventful boundary. A blueprint or another mod may react to
			// placement by adding inventory, liquid, or charge after the off-zone proof above.
			// Re-prove the exact handles before publishing the durable marker. This check belongs
			// only to first application: a later exact-marker retry may legitimately find an
			// inherited work the player has since filled or charged.
			for (int i = 0; i < handles.Length; i++)
			{
				try
				{
					if (!Host.IsFreshEmpty(handles[i]))
					{
						bool clean = DiscardAll(Host, handles);
						return Failed(clean ? KingdomInheritApplyFault.ObjectNotEmpty
							: KingdomInheritApplyFault.PartialApplication,
							"placement gave a fresh inherited object contents, liquid, or charge",
							prepared.Marker);
					}
				}
				catch (Exception ex)
				{
					bool clean = DiscardAll(Host, handles);
					return Failed(clean ? KingdomInheritApplyFault.ObjectNotEmpty
						: KingdomInheritApplyFault.PartialApplication,
						"a placed inherited object could not be proved empty: " + ex.Message,
						prepared.Marker);
				}
			}

			try
			{
				string failure;
				if (!Host.TryWriteApplicationMarker(prepared.Marker, out failure)
					|| Host.ReadApplicationMarker() != prepared.Marker
					|| !IsExactExistingApplication(Host, prepared))
				{
					bool markerClean = Host.TryRemoveApplicationMarker(prepared.Marker);
					bool clean = DiscardAll(Host, handles);
					return Failed(clean && markerClean ? KingdomInheritApplyFault.MarkerWrite
						: KingdomInheritApplyFault.PartialApplication,
						Nonempty(failure, "the inherited site's application marker was not durable"),
						prepared.Marker);
				}
			}
			catch (Exception ex)
			{
				bool markerClean = false;
				try { markerClean = Host.TryRemoveApplicationMarker(prepared.Marker); } catch { }
				bool clean = DiscardAll(Host, handles);
				return Failed(clean && markerClean ? KingdomInheritApplyFault.MarkerWrite
					: KingdomInheritApplyFault.PartialApplication,
					"the inherited site's application marker could not be written: " + ex.Message,
					prepared.Marker);
			}

			return new KingdomInheritApplyResult(KingdomInheritApplyStatus.Applied,
				KingdomInheritApplyFault.None, "the inherited site was applied", prepared.Marker,
				prepared.Specs.Length, true);
		}

		private static bool TryPrepare(KingdomSealRecord Legacy, KingdomSealReceipt Receipt,
			string TargetZoneId, IKingdomInheritEngineHost Host, out Prepared Prepared,
			out KingdomInheritApplyResult Failure)
		{
			Prepared = null;
			Failure = null;
			if (Legacy == null || Receipt == null || Host == null || TargetZoneId == null)
			{
				Failure = Failed(KingdomInheritApplyFault.NullInput,
					"the inherited record, reservation, and zone are all required", "");
				return false;
			}

			KingdomSealRecord canonical;
			KingdomSealFault sealFault;
			string detail;
			try
			{
				if (!KingdomSealRecord.TryReadBody(KingdomSealRecord.CurrentSchema, Legacy.WriteBody(),
					out canonical, out sealFault, out detail))
				{
					Failure = Failed(KingdomInheritApplyFault.LegacyNotPromoted,
						Nonempty(detail, "the inherited record is malformed"), "");
					return false;
				}
			}
			catch (Exception ex)
			{
				Failure = Failed(KingdomInheritApplyFault.LegacyNotPromoted,
					"the inherited record is malformed: " + ex.Message, "");
				return false;
			}
			if (canonical.Status != KingdomSealStatus.Promoted || !canonical.IsResolved)
			{
				Failure = Failed(KingdomInheritApplyFault.LegacyNotPromoted,
					"only an exact promoted and resolved legacy may be reconstructed", "");
				return false;
			}
			long expectedSeed = KingdomSealRules.InterregnumSeed(new KingdomSealLineage(
				canonical.LineageId, canonical.LegacyId, canonical.OriginGameId,
				canonical.Generation, canonical.Revision));
			int expectedRoll = KingdomRules.InterregnumRoll(expectedSeed);
			KingdomRules.InheritedState expectedState = KingdomRules.ResolveInheritedState(
				canonical.Vigour, expectedRoll, canonical.Population);
			if (canonical.InterregnumRoll != expectedRoll
				|| canonical.InheritedState != (int)expectedState)
			{
				Failure = Failed(KingdomInheritApplyFault.LegacyNotPromoted,
					"the promoted legacy's fixed interregnum result does not match its immutable facts", "");
				return false;
			}
			if (Receipt.State != KingdomSealReceiptState.Reserved)
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptNotReserved,
					"the legacy receipt is not reserved", "");
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Receipt.LineageId))
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt's lineage id is malformed", "");
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Receipt.LegacyId))
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt's legacy id is malformed: '" + (Receipt.LegacyId ?? "<null>") + "'", "");
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Receipt.TargetGameId))
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt's target game id is malformed", "");
				return false;
			}
			if (Receipt.WrittenTick < 0L)
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt's written tick is malformed", "");
				return false;
			}
			if (Receipt.LineageId != canonical.LineageId || Receipt.LegacyId != canonical.LegacyId)
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt does not name this exact promoted legacy", "");
				return false;
			}
			if (Host.TargetGameId != Receipt.TargetGameId)
			{
				Failure = Failed(KingdomInheritApplyFault.TargetGameMismatch,
					"the reserved receipt names a different target game", "");
				return false;
			}
			if (TargetZoneId.Length == 0 || TargetZoneId.Length > KingdomSealRecord.MaxIdChars
				|| !KingdomSealRules.IsToken(TargetZoneId) || Host.ZoneId != TargetZoneId)
			{
				Failure = Failed(KingdomInheritApplyFault.TargetZoneMismatch,
					"this zone is not the exact selected new-world target", "");
				return false;
			}

			KingdomInheritPlacement placement;
			KingdomInheritFault inheritFault;
			if (!KingdomInheritRules.TryPrepare(canonical.WorkKeys, canonical.WorkX, canonical.WorkY,
				canonical.WorkConditions, (KingdomRules.InheritedState)canonical.InheritedState,
				canonical.InterregnumRoll, out placement, out inheritFault) || placement == null)
			{
				Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
					KingdomInheritRules.FailureLine(inheritFault), "");
				return false;
			}

			KingdomInheritBuildSpec[] specs = new KingdomInheritBuildSpec[placement.Count];
			int cairns = 0;
			for (int i = 0; i < placement.Count; i++)
			{
				KingdomInheritWork work = placement.WorkAt(i);
				string blueprint;
				int width;
				int height;
				if (work == null || !KingdomInheritRules.TryResolveBlueprint(work.Key, out blueprint)
					|| !KingdomInheritRules.TryFootprint(work.Key, out width, out height))
				{
					Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
						"the prepared plan contains a semantic key this build cannot resolve", "");
					return false;
				}
				specs[i] = new KingdomInheritBuildSpec(i, work, blueprint, width, height);
				if (work.Key == KingdomInheritRules.FounderCairnKey
					&& work.X == placement.CairnX && work.Y == placement.CairnY)
				{
					cairns++;
				}
			}
			if (cairns != 1)
			{
				Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
					"the prepared plan does not carry exactly one founder cairn", "");
				return false;
			}

			string marker;
			if (!KingdomInheritanceStateRules.TryComposeApplicationMarker(canonical, Receipt,
				TargetZoneId, ReconstructionVersion, out marker))
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the exact reservation could not form its deterministic application marker", "");
				return false;
			}
			Prepared = new Prepared
			{
				Legacy = canonical,
				Placement = placement,
				Specs = specs,
				Marker = marker,
				CairnText = ComposeCairnText(canonical)
			};
			return true;
		}

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
					if (!Host.HasBlueprint(Prepared.Specs[i].Blueprint))
					{
						Failure = Failed(KingdomInheritApplyFault.BlueprintMissing,
							"an allowlisted inherited object is not installed: "
							+ Prepared.Specs[i].Blueprint, Prepared.Marker);
						return false;
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
				int left = spec.X - (spec.FootprintWidth - 1) / 2;
				int top = spec.Y - (spec.FootprintHeight - 1) / 2;
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
						if (facts.Connection)
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
						site.Claimed[x, y] = true;
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

		private static bool HasEntryToHeartPath(SiteSnapshot Site, Prepared Prepared)
		{
			KingdomInheritBuildSpec heart = null;
			for (int i = 0; i < Prepared.Specs.Length; i++)
			{
				if (Prepared.Specs[i].X == Prepared.Placement.HeartX
					&& Prepared.Specs[i].Y == Prepared.Placement.HeartY)
				{
					heart = Prepared.Specs[i];
					break;
				}
			}
			if (heart == null)
			{
				return false;
			}

			int heartLeft = heart.X - (heart.FootprintWidth - 1) / 2;
			int heartTop = heart.Y - (heart.FootprintHeight - 1) / 2;
			int heartRight = heartLeft + heart.FootprintWidth - 1;
			int heartBottom = heartTop + heart.FootprintHeight - 1;
			int width = Site.Cells.GetLength(0);
			int height = Site.Cells.GetLength(1);
			bool[,] visited = new bool[width, height];
			Queue<int> queue = new Queue<int>();
			int startX = Prepared.Placement.EntryX;
			int startY = Prepared.Placement.EntryY;
			visited[startX, startY] = true;
			queue.Enqueue(startY * width + startX);
			while (queue.Count > 0)
			{
				int packed = queue.Dequeue();
				int x = packed % width;
				int y = packed / width;
				if (x >= heartLeft - 1 && x <= heartRight + 1
					&& y >= heartTop - 1 && y <= heartBottom + 1
					&& (x < heartLeft || x > heartRight || y < heartTop || y > heartBottom))
				{
					return true;
				}
				for (int dy = -1; dy <= 1; dy++)
				{
					for (int dx = -1; dx <= 1; dx++)
					{
						// Four-way reachability is conservative: a diagonal slit between two occupied
						// corners is not accepted as the old settlement's only road to its heart.
						if (Math.Abs(dx) + Math.Abs(dy) != 1)
						{
							continue;
						}
						int nx = x + dx;
						int ny = y + dy;
						if (nx < 0 || ny < 0 || nx >= width || ny >= height || visited[nx, ny]
							|| Site.Claimed[nx, ny])
						{
							continue;
						}
						KingdomInheritCellFacts facts = Site.Cells[nx, ny];
						if (facts.Exists && !facts.Occupied && !facts.Terrain && !facts.Stairs
							&& facts.Walkable)
						{
							visited[nx, ny] = true;
							queue.Enqueue(ny * width + nx);
						}
					}
				}
			}
			return false;
		}

		internal static string ComposeCairnText(KingdomSealRecord Legacy)
		{
			if (Legacy == null)
			{
				return "A founder's cairn. Chronicle: no chronicle lines survived the sealing.";
			}
			StringBuilder sb = new StringBuilder();
			AppendBounded(sb, "A founder's cairn for ", MaxCairnChars);
			AppendBounded(sb, CairnText(Legacy.FounderName, KingdomSealRecord.MaxNameChars), MaxCairnChars);
			AppendBounded(sb, ", founder of ", MaxCairnChars);
			AppendBounded(sb, CairnText(Legacy.SettlementName, KingdomSealRecord.MaxNameChars), MaxCairnChars);
			if (!string.IsNullOrEmpty(Legacy.RealmName))
			{
				AppendBounded(sb, " in ", MaxCairnChars);
				AppendBounded(sb, CairnText(Legacy.RealmName, KingdomSealRecord.MaxNameChars), MaxCairnChars);
			}
			AppendBounded(sb, ".", MaxCairnChars);
			if (!string.IsNullOrEmpty(Legacy.CauseText))
			{
				AppendBounded(sb, " They died: ", MaxCairnChars);
				AppendBounded(sb, CairnText(Legacy.CauseText, KingdomSealRecord.MaxLineChars), MaxCairnChars);
				AppendBounded(sb, ".", MaxCairnChars);
			}
			AppendBounded(sb, "\n\nChronicle of the old kingdom:\n", MaxCairnChars);
			if (Legacy.Chronicle == null || Legacy.Chronicle.Count == 0)
			{
				AppendBounded(sb, "No chronicle lines survived the sealing.", MaxCairnChars);
			}
			else
			{
				for (int i = 0; i < Legacy.Chronicle.Count; i++)
				{
					AppendBounded(sb, "- ", MaxCairnChars);
					AppendBounded(sb, CairnText(Legacy.Chronicle[i], KingdomSealRecord.MaxLineChars),
						MaxCairnChars);
					AppendBounded(sb, "\n", MaxCairnChars);
				}
			}
			string state = (Legacy.InheritedState >= 0
				&& Legacy.InheritedState < KingdomRules.InheritedStateNames.Length)
				? KingdomRules.InheritedStateNames[Legacy.InheritedState] : "unknown";
			AppendBounded(sb, "\nInterregnum draw: ", MaxCairnChars);
			AppendBounded(sb, Legacy.InterregnumRoll.ToString(CultureInfo.InvariantCulture), MaxCairnChars);
			AppendBounded(sb, ". Inherited state: ", MaxCairnChars);
			AppendBounded(sb, state, MaxCairnChars);
			AppendBounded(sb, ".", MaxCairnChars);
			return sb.ToString();
		}

		private static string CairnText(string Value, int MaxChars)
		{
			// Tilde has Description-specific alternate-text meaning. It is prose in the seal but is
			// flattened here along with Qud markup/control syntax.
			return KingdomSealRules.SanitizeText(Value, MaxChars).Replace('~', '-');
		}

		private static void AppendBounded(StringBuilder Builder, string Value, int MaxChars)
		{
			if (Builder.Length >= MaxChars || string.IsNullOrEmpty(Value))
			{
				return;
			}
			int room = MaxChars - Builder.Length;
			Builder.Append(Value, 0, Math.Min(room, Value.Length));
		}

		private static bool DiscardAll(IKingdomInheritEngineHost Host, object[] Handles)
		{
			bool clean = true;
			if (Host == null || Handles == null)
			{
				return false;
			}
			for (int i = Handles.Length - 1; i >= 0; i--)
			{
				if (Handles[i] == null)
				{
					continue;
				}
				try
				{
					if (!Host.Discard(Handles[i]))
					{
						clean = false;
					}
				}
				catch
				{
					clean = false;
				}
				Handles[i] = null;
			}
			return clean;
		}

		private static KingdomInheritApplyResult Refused(KingdomInheritApplyFault Fault,
			string Detail, string Marker)
		{
			return new KingdomInheritApplyResult(KingdomInheritApplyStatus.Refused, Fault,
				Detail, Marker, 0, false);
		}

		private static KingdomInheritApplyResult Failed(KingdomInheritApplyFault Fault,
			string Detail, string Marker)
		{
			return new KingdomInheritApplyResult(KingdomInheritApplyStatus.Failed, Fault,
				Detail, Marker, 0, false);
		}

		private static string Nonempty(string Value, string Fallback)
		{
			return string.IsNullOrEmpty(Value) ? Fallback : Value;
		}

#if !TAF_TESTS
		internal static KingdomInheritApplyResult Apply(KingdomSealRecord Legacy,
			KingdomSealReceipt Receipt, string TargetZoneId, Zone Zone)
		{
			return Apply(Legacy, Receipt, TargetZoneId, Zone == null ? null : new ZoneHost(Zone));
		}

		private sealed class ZoneHost : IKingdomInheritEngineHost
		{
			private readonly Zone Zone;

			private readonly bool[,] Connections;

			internal ZoneHost(Zone Zone)
			{
				this.Zone = Zone;
				Connections = new bool[Zone.Width, Zone.Height];
				foreach (ZoneConnection connection in Zone.EnumerateConnections())
				{
					MarkConnection(connection);
				}
				// EnumerateConnections only includes pending local ("-") cache entries. Every pending
				// boundary entry also reserves a live cell, so inspect the cache whole before mutation.
				if (Zone.ZoneConnectionCache != null)
				{
					for (int i = 0; i < Zone.ZoneConnectionCache.Count; i++)
					{
						MarkConnection(Zone.ZoneConnectionCache[i]);
					}
				}
			}

			public int Width { get { return Zone.Width; } }

			public int Height { get { return Zone.Height; } }

			public string ZoneId { get { return Zone.ZoneID ?? ""; } }

			public string TargetGameId { get { return The.Game == null ? "" : (The.Game.GameID ?? ""); } }

			public string ReadApplicationMarker()
			{
				return Zone.GetZoneProperty(ZoneMarkerProperty, "") ?? "";
			}

			public int CountApplicationObjects(string Marker)
			{
				int count = 0;
				List<GameObject> objects = Zone.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					if (objects[i].GetStringProperty(ObjectMarkerProperty, "") == Marker)
					{
						count++;
					}
				}
				return count;
			}

			public bool HasAnyApplicationObjects()
			{
				List<GameObject> objects = Zone.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					if (!string.IsNullOrEmpty(objects[i].GetStringProperty(ObjectMarkerProperty, "")))
					{
						return true;
					}
				}
				return false;
			}

			public bool HasExactApplicationObject(string Marker, KingdomInheritBuildSpec Spec,
				string CairnText)
			{
				Cell cell = Zone.GetCell(Spec.X, Spec.Y);
				if (cell == null)
				{
					return false;
				}
				for (int i = 0; i < cell.Objects.Count; i++)
				{
					GameObject obj = cell.Objects[i];
					if (obj.Blueprint == Spec.Blueprint
						&& obj.GetStringProperty(ObjectMarkerProperty, "") == Marker
						&& obj.GetStringProperty(ObjectKeyProperty, "") == Spec.Key
						&& obj.GetIntProperty(ObjectIndexProperty, -1) == Spec.Index
						&& obj.GetIntProperty(ObjectStateProperty, -1) == (int)Spec.State
						&& obj.GetIntProperty(ObjectConditionProperty, -1) == Spec.Condition
						&& obj.GetIntProperty(ObjectFreshEmptyProperty, 0) == 1
						&& (Spec.Key != KingdomInheritRules.FounderCairnKey
							|| (obj.GetPart<Description>() != null
								&& obj.GetPart<Description>()._Short == CairnText)))
					{
						return true;
					}
				}
				return false;
			}

			public bool HasBlueprint(string Blueprint)
			{
				return GameObjectFactory.Factory.HasBlueprint(Blueprint);
			}

			public bool TryReadCell(int X, int Y, out KingdomInheritCellFacts Facts)
			{
				Facts = new KingdomInheritCellFacts();
				Cell cell = Zone.GetCell(X, Y);
				if (cell == null)
				{
					return false;
				}
				Facts.Exists = true;
				Facts.Occupied = IsOccupied(cell);
				Facts.Terrain = cell.HasOpenLiquidVolume();
				Facts.Stairs = cell.HasObjectWithPart("StairsUp")
					|| cell.HasObjectWithPart("StairsDown") || cell.HasStairs();
				Facts.Connection = Connections[X, Y];
				Facts.Walkable = cell.IsPassable(null, false);
				return true;
			}

			public bool TryCreateFresh(KingdomInheritBuildSpec Spec, string Marker, string CairnText,
				out object Handle, out string Failure)
			{
				Handle = null;
				Failure = "";
				string resolved;
				if (Spec == null || !KingdomInheritRules.TryResolveBlueprint(Spec.Key, out resolved)
					|| resolved != Spec.Blueprint || !GameObjectFactory.Factory.HasBlueprint(resolved))
				{
					Failure = "the inherited semantic key is not allowlisted by this build";
					return false;
				}
				GameObject obj = GameObject.CreateUnmodified(resolved);
				if (obj == null)
				{
					Failure = "the allowlisted inherited object factory returned nothing";
					return false;
				}
				Handle = obj;
				obj.StripContents(KeepNatural: false, Silent: true);
				LiquidVolume liquid = obj.GetPart<LiquidVolume>();
				if (liquid != null && !liquid.IsEmpty())
				{
					liquid.Empty();
				}
				Capacitor capacitor = obj.GetPart<Capacitor>();
				if (capacitor != null)
				{
					capacitor.Charge = 0;
				}
				Clockwork clockwork = obj.GetPart<Clockwork>();
				if (clockwork != null)
				{
					clockwork.Charge = 0;
				}
				Circuitry circuitry = obj.GetPart<Circuitry>();
				if (circuitry != null)
				{
					circuitry.Charge = 0;
					circuitry.IncomingCharge = 0;
				}

				obj.SetStringProperty(ObjectMarkerProperty, Marker);
				obj.SetStringProperty(ObjectKeyProperty, Spec.Key);
				obj.SetIntProperty(ObjectIndexProperty, Spec.Index);
				obj.SetIntProperty(ObjectStateProperty, (int)Spec.State);
				obj.SetIntProperty(ObjectConditionProperty, Spec.Condition);
				obj.SetIntProperty(ObjectFreshEmptyProperty, 1);
				if (Spec.State == KingdomInheritWorkState.Standing
					|| Spec.State == KingdomInheritWorkState.Derelict)
				{
					int baseHp = obj.baseHitpoints;
					if (baseHp > 0)
					{
						obj.hitpoints = Math.Max(1, baseHp * Spec.Condition / 100);
					}
				}
				Description description = obj.RequirePart<Description>();
				if (Spec.Key == KingdomInheritRules.FounderCairnKey)
				{
					description._Short = CairnText;
				}
				else if (Spec.State == KingdomInheritWorkState.Derelict)
				{
					description._Short = (description._Short ?? "").TrimEnd()
						+ " It stands intact but derelict, with no stores or household left inside.";
				}
				if (!IsEmptyObject(obj))
				{
					Failure = "the fresh inherited object was not empty after stripping";
					return false;
				}
				return true;
			}

			public bool IsFreshEmpty(object Handle)
			{
				return IsEmptyObject(Handle as GameObject);
			}

			public bool TryPlace(object Handle, int X, int Y, out string Failure)
			{
				Failure = "";
				GameObject obj = Handle as GameObject;
				Cell cell = Zone.GetCell(X, Y);
				if (obj == null || cell == null)
				{
					Failure = "the inherited object or its prepared cell is missing";
					return false;
				}
				cell.AddObject(obj, Forced: false, System: true, IgnoreGravity: true, NoStack: true,
					Silent: true, Repaint: false);
				if (obj.CurrentCell != cell)
				{
					Failure = "the inherited object was rejected by its prepared cell";
					return false;
				}
				return true;
			}

			public bool Discard(object Handle)
			{
				GameObject obj = Handle as GameObject;
				if (obj == null)
				{
					return true;
				}
				obj.Obliterate(null, Silent: true);
				return obj.CurrentCell == null;
			}

			public bool TryWriteApplicationMarker(string Marker, out string Failure)
			{
				Failure = "";
				string existing = ReadApplicationMarker();
				if (!string.IsNullOrEmpty(existing) && existing != Marker)
				{
					Failure = "the zone acquired a different inherited-site marker";
					return false;
				}
				Zone.SetZoneProperty(ZoneMarkerProperty, Marker);
				return ReadApplicationMarker() == Marker;
			}

			public bool TryRemoveApplicationMarker(string Marker)
			{
				if (ReadApplicationMarker() == Marker)
				{
					Zone.RemoveZoneProperty(ZoneMarkerProperty);
				}
				return ReadApplicationMarker().Length == 0;
			}

			private void MarkConnection(ZoneConnection Connection)
			{
				if (Connection != null && Connection.X >= 0 && Connection.Y >= 0
					&& Connection.X < Zone.Width && Connection.Y < Zone.Height)
				{
					Connections[Connection.X, Connection.Y] = true;
				}
			}

			private static bool IsOccupied(Cell Cell)
			{
				for (int i = 0; i < Cell.Objects.Count; i++)
				{
					GameObject obj = Cell.Objects[i];
					if ((obj.Render != null && obj.Render.RenderLayer > 5)
						|| obj.IsCombatObject())
					{
						return true;
					}
				}
				return false;
			}

			private static bool IsEmptyObject(GameObject Object)
			{
				if (Object == null || Object.GetContents(new List<GameObject>()).Count != 0)
				{
					return false;
				}
				LiquidVolume liquid = Object.GetPart<LiquidVolume>();
				Capacitor capacitor = Object.GetPart<Capacitor>();
				Clockwork clockwork = Object.GetPart<Clockwork>();
				Circuitry circuitry = Object.GetPart<Circuitry>();
				return (liquid == null || liquid.IsEmpty())
					&& (capacitor == null || capacitor.Charge == 0)
					&& (clockwork == null || clockwork.Charge == 0)
					&& (circuitry == null || (circuitry.Charge == 0 && circuitry.IncomingCharge == 0));
			}
		}
#endif
	}
}
