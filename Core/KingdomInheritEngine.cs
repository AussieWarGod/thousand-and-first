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
	/// <summary>
	/// Engine-coupled reconstruction for one exact promoted record and one exact reserved receipt.
	/// The state transform and deterministic geometry stay in <see cref="KingdomInheritRules"/>;
	/// this type proves the live site, creates empty objects, and owns the application marker.
	/// </summary>
	internal static partial class KingdomInheritEngine
	{
		internal const int LegacyReconstructionVersion = 1;

		internal const int ReconstructionVersion = 3;

		internal const string ZoneMarkerProperty = "ThousandAndFirst.Inherit.Application";

		internal const string ObjectMarkerProperty = "ThousandAndFirst.Inherit.Application";

		internal const string ObjectKeyProperty = "ThousandAndFirst.Inherit.Key";

		internal const string ObjectFreshEmptyProperty = "ThousandAndFirst.Inherit.FreshEmpty";

		internal const string ObjectIndexProperty = "ThousandAndFirst.Inherit.Index";

		internal const string ObjectStateProperty = "ThousandAndFirst.Inherit.State";

		internal const string ObjectConditionProperty = "ThousandAndFirst.Inherit.Condition";

		internal const string ObjectDegradedHashProperty =
			"ThousandAndFirst.Inherit.DegradedArchitectureHash";

		internal const string ObjectAuthorityMemoryProperty =
			"ThousandAndFirst.Inherit.FoundingAuthorityMemory";

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

	}
}
