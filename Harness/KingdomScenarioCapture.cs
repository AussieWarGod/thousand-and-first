using System;
using System.Collections.Generic;
using System.Globalization;

using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The one shared semantic capture for the architecture-stamper authority.
	/// <para>
	/// Every value is read from <see cref="KingdomArchitectureIntent"/>, the production receipt an
	/// ordinary commission and a gallery staging both produce, so the same measurement is reachable
	/// on both paths. Gallery-only receipt properties are deliberately excluded: a differential
	/// whose keys exist on one path only could never be satisfied by an ordinary-play anchor, which
	/// would make an empty anchor store conceal an impossible oracle rather than a pending one.
	/// </para>
	/// <para>
	/// The heaviest key is <c>architecture.realized.digest</c>, the shared production capture of the
	/// exact realized lot: cells, architecture-owned objects, their relative coordinates and
	/// slot/layer/anchor relations, and their rendering. A matching receipt over different cells
	/// therefore fails, which is the point a receipt-only differential missed.
	/// </para>
	/// <para>
	/// Absolute placement is excluded for the same reason. Two lawful builds of the same design sit
	/// at different coordinates, so extent and main-root offset are recorded relative to the lot.
	/// </para>
	/// </summary>
	internal static class KingdomScenarioCapture
	{
		internal const string ArchitectureAuthority = "architecture-stamper";

		/// <summary>
		/// Measures the declared key set off any stamped architecture owner, however it was built.
		/// </summary>
		internal static bool TryMeasure(GameObject Owner,
			out IDictionary<string, string> Captured, out string Failure)
		{
			Captured = null;
			Failure = null;
			if (!GameObject.Validate(Owner))
				return Refuse("the architecture owner is not a valid object", out Failure);
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureStamper.TryReadOwner(Owner, out intent, out snapshot, out _,
				out Failure)) return false;
			if (intent == null) return Refuse("the owner carries no architecture intent", out Failure);
			string realized;
			int width;
			int height;
			if (!KingdomRealizedArchitectureCapture.TryCapture(Owner, out realized, out width,
				out height, out Failure)) return false;
			Captured = new SortedDictionary<string, string>(StringComparer.Ordinal)
			{
				{ "architecture.realized.digest", realized },
				{ "architecture.binding.key", intent.BindingKey ?? "-" },
				{ "architecture.build.key", intent.BuildKey ?? "-" },
				{ "architecture.extent", Extent(intent) },
				{ "architecture.facing", intent.Facing.ToString() },
				{ "architecture.lot.size", intent.LotSize.ToString() },
				{ "architecture.lot.type", intent.LotType ?? "-" },
				{ "architecture.main.offset", Offset(intent) },
				{ "architecture.palette.key", intent.PaletteKey ?? "-" },
				{ "architecture.plan.key", intent.PlanKey ?? "-" },
				{ "architecture.receipt.schema",
					intent.SchemaVersion.ToString(CultureInfo.InvariantCulture) },
				{ "architecture.snapshot.hash", intent.SnapshotHash ?? "-" },
				{ "architecture.tier.key", intent.TierKey ?? "-" },
				{ "architecture.variant.key", intent.VariantKey ?? "-" }
			};
			return true;
		}

		/// <summary>
		/// Every stamped architecture owner standing in a zone. Used by the read-only ordinary-play
		/// capture report, where the reviewer names which one they commissioned.
		/// </summary>
		internal static IList<GameObject> Owners(Zone Zone)
		{
			List<GameObject> owners = new List<GameObject>();
			List<GameObject> objects = Zone?.GetObjects() ?? new List<GameObject>();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject candidate = objects[i];
				if (!GameObject.Validate(candidate)) continue;
				KingdomArchitectureIntent intent;
				ArchitectureLayoutSnapshot snapshot;
				string ignored;
				if (KingdomArchitectureStamper.TryReadOwner(candidate, out intent, out snapshot,
					out _, out ignored) && intent != null) owners.Add(candidate);
			}
			return owners;
		}

		/// <summary>Lot-relative extent, so placement never enters the differential.</summary>
		private static string Extent(KingdomArchitectureIntent Intent)
		{
			int width = Intent.Rect.X2 - Intent.Rect.X1 + 1;
			int height = Intent.Rect.Y2 - Intent.Rect.Y1 + 1;
			return width.ToString(CultureInfo.InvariantCulture) + "x"
				+ height.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>Main behaviour root relative to the lot origin, never its world coordinate.</summary>
		private static string Offset(KingdomArchitectureIntent Intent)
		{
			int x = Intent.MainWorldX - Intent.Rect.X1;
			int y = Intent.MainWorldY - Intent.Rect.Y1;
			return x.ToString(CultureInfo.InvariantCulture) + ","
				+ y.ToString(CultureInfo.InvariantCulture);
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
