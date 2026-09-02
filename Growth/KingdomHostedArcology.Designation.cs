using System;
using ThousandAndFirst.Api;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	internal sealed class KingdomHostedLiveContext
	{
		internal Zone Zone;
		internal GameObject Shell;
		internal r_KingdomArcology Root;
		internal GameObject AnchorObject;
		internal r_KingdomArcologyZoneAnchor Anchor;
		internal KingdomHostedLotReceipt Receipt;
		internal KingdomHostedLotDefinition Definition;
		internal KingdomArcologyFixtureSpec[] Fixtures;
		internal string Revision;
	}

	/// <summary>Active-zone-only designation source for a paid hosted interior. The stable
	/// interior slate, not its remote exterior shell, is the exact live designation root.</summary>
	[KingdomDesignationProvider]
	public sealed class KingdomHostedArcologyDesignationProvider
		: IKingdomDesignationProvider, IKingdomTrustedDesignationSource
	{
		public string ProviderId => "taf.hosted-arcology";
		public string ProviderVersion => "1";

		/// <summary>The Api face of the same observation: the trusted row carrying only Api types.</summary>
		public bool TryObserve(Zone ActiveZone,
			out KingdomApiDesignation[] Designations, out string Failure)
		{
			Designations = null;
			if (!TryObserveTrusted(ActiveZone, out KingdomBenefitDesignation[] rows, out Failure))
				return false;
			Designations = new KingdomApiDesignation[rows.Length];
			for (int i = 0; i < rows.Length; i++)
				Designations[i] = KingdomDesignationRules.ToApi(rows[i]);
			return true;
		}

		public bool TryObserveTrusted(Zone ActiveZone,
			out KingdomBenefitDesignation[] Designations, out string Failure)
		{
			Designations = null; Failure = null;
			if (!ReferenceEquals(The.ZoneManager?.ActiveZone, ActiveZone)
				|| !(ActiveZone is InteriorZone interior)
				|| interior.Schema != KingdomHostedArcologyTopology.Schema
				|| string.IsNullOrEmpty(KingdomHostedArcologyTopology.HostedLotAt(
					interior.X, interior.Y, interior.Z))) return false;
			if (!KingdomHostedArcology.TryLiveContext(ActiveZone, true,
				out KingdomHostedLiveContext context, out Failure)) return false;
			if (!KingdomHostedArcology.TryBuildDesignation(context,
				out KingdomBenefitDesignation row, out Failure)) return false;
			Designations = new KingdomBenefitDesignation[] { row }; return true;
		}
	}

	public static partial class KingdomHostedArcology
	{
		internal static bool TryBuildDesignation(KingdomHostedLiveContext Context,
			out KingdomBenefitDesignation Row, out string Failure)
		{
			Row = null; Failure = null;
			if (Context?.AnchorObject?.CurrentCell == null || Context.Fixtures == null)
				return ContextFail("hosted designation has no exact live context", out Failure);
			KingdomBenefitDesignation row = new KingdomBenefitDesignation {
				ProviderId = "taf.hosted-arcology", ProviderVersion = "1",
				Identity = "lot:" + Context.AnchorObject.IDIfAssigned,
				Revision = Context.Revision, ZoneId = Context.Zone.ZoneID,
				RootId = Context.AnchorObject.IDIfAssigned,
				BuildingKey = Context.Receipt.LotKey, LotId = Context.Receipt.LotKey
			};
			const KingdomBenefitCellUse use = KingdomBenefitCellUse.Plot
				| KingdomBenefitCellUse.Building | KingdomBenefitCellUse.Covered
				| KingdomBenefitCellUse.Interior;
			long area = (long)Context.Zone.Width * Context.Zone.Height;
			if (area < 1L || area > KingdomDesignationRules.MaxCellsPerDesignation)
				return ContextFail("hosted programme exceeds its exact-cell bound", out Failure);
			for (int y = 0; y < Context.Zone.Height; y++)
				for (int x = 0; x < Context.Zone.Width; x++)
				{
					bool ingress = x == HostedIngressX(Context.Receipt.LotKey)
						&& y == KingdomHostedArcologyTopology.StairY;
					row.Cells.Add(new KingdomBenefitCell(x, y,
						ingress ? use | KingdomBenefitCellUse.Ingress : use,
						KingdomBenefitCover.ObservedEnclosure));
				}
			Row = row; return true;
		}

		private static int HostedIngressX(string LotKey)
		{
			return LotKey == KingdomHostedArcologyTopology.TerraceLotKey
				? KingdomHostedArcologyTopology.StairsDownX(
					KingdomHostedArcologyTopology.UpperZ)
				: LotKey == KingdomHostedArcologyTopology.WardLotKey
				? KingdomHostedArcologyTopology.StairsUpX(
					KingdomHostedArcologyTopology.LowerZ) : -1;
		}

		internal static bool TryLiveContext(Zone Z, bool RequireFixtures,
			out KingdomHostedLiveContext Context, out string Failure)
		{
			Context = null; Failure = null;
			InteriorZone interior = Z as InteriorZone;
			string lotKey = interior == null ? ""
				: KingdomHostedArcologyTopology.HostedLotAt(interior.X, interior.Y, interior.Z);
			GameObject shell = null;
			if (interior != null && !TryLoadedInteriorRoot(interior, out shell, out Failure))
				return false;
			r_KingdomArcology root = shell?.GetPart<r_KingdomArcology>();
			if (Z == null || interior == null
				|| string.IsNullOrEmpty(lotKey) || !GameObject.Validate(shell) || root == null
				|| string.IsNullOrEmpty(shell.IDIfAssigned)
				|| !string.IsNullOrEmpty(root.QuarantineReason)
				|| interior.Instance != shell.IDIfAssigned)
				return ContextFail("hosted designation lacks exact active interior authority",
					out Failure);
			KingdomArcologyProgramme programme = KingdomHostedArcologyTopology.ProgrammeAt(
				interior.X, interior.Y, interior.Z);
			if (Z.GetZoneProperty("TAFArcologyProgramme", null) != programme.ToString()
				|| Z.BaseDisplayName != KingdomHostedArcologyTopology.ProgrammeName(programme)
				|| !TryInteriorZoneIdentity(shell, lotKey, Z.ZoneID, out Failure)) return false;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			KingdomHostedArcologyAuthority authority;
			if (system == null || !TryReadAuthority(system, out authority, out Failure)
				|| authority == null || authority.Phase != KingdomHostedAuthorityPhase.Active
				|| authority.CarrierId != shell.IDIfAssigned
				|| shell.CurrentZone == null || authority.ZoneId != shell.CurrentZone.ZoneID
				|| authority.RealmId != system.RealmId)
				return ContextFail(Failure ?? "hosted designation lacks current exterior authority",
					out Failure);
			KingdomHostedLotReceipt receipt;
			KingdomHostedLotDefinition definition;
			KingdomArcologyFixtureSpec[] fixtures;
			if (!TryReceipt(root, lotKey, out receipt, out Failure)) return false;
			if (receipt == null || receipt.Phase == KingdomHostedLotPhase.Working) return false;
			if (receipt.Phase != KingdomHostedLotPhase.Active
				|| receipt.RootId != shell.IDIfAssigned
				|| !KingdomHostedArcologyRules.TryHostedLot(lotKey, out definition)
				|| definition.ReadOnly || definition.InteriorCell != interior.Schema
				|| !KingdomHostedArcologyProgrammeBuilder.TryPaidFixtures(
					lotKey, programme, out fixtures))
				return ContextFail("hosted designation lacks its active exact receipt",
					out Failure);
			string revision = KingdomHostedArcologyRules.ReceiptRevision(receipt);
			GameObject anchorObject;
			r_KingdomArcologyZoneAnchor anchor;
			if (string.IsNullOrEmpty(revision)
				|| !TryExactAnchor(Z, shell.IDIfAssigned, lotKey, out anchorObject,
					out anchor, out Failure)) return false;
			if (RequireFixtures && (!anchor.FixturesRealized
				|| !KingdomHostedArcologyVisual.ProvesExactFixtures(
					Z, shell.IDIfAssigned, anchor, fixtures)))
				return ContextFail("hosted designation fixtures do not match the active receipt",
					out Failure);
			Context = new KingdomHostedLiveContext { Zone = Z, Shell = shell, Root = root,
				AnchorObject = anchorObject, Anchor = anchor, Receipt = receipt,
				Definition = definition, Fixtures = fixtures, Revision = revision };
			return true;
		}

		internal static bool TryInteriorZoneIdentity(GameObject Shell, string LotKey,
			string ZoneId, out string Failure)
		{
			Failure = null;
			if (!KingdomHostedArcologyTopology.TryHostedLotCoordinate(LotKey,
				out KingdomArcologyCoordinate at))
				return ContextFail("hosted observation has no exact native interior declaration",
					out Failure);
			if (!TryNativeInteriorTarget(Shell, at.X, at.Y, at.Z,
				out string target, out Failure)) return false;
			return target == ZoneId || ContextFail(
				"hosted observation interior identity is noncanonical", out Failure);
		}

		internal static bool TryNativeInteriorTarget(GameObject Shell, int X, int Y, int Z,
			out string Target, out string Failure)
		{
			Target = null; Failure = null;
			Interior part = Shell?.GetPart<Interior>();
			if (!GameObject.Validate(Shell) || string.IsNullOrEmpty(Shell.IDIfAssigned)
				|| part == null || part.ParentObject != Shell || !part.Unique
				|| part.Cell != KingdomHostedArcologyTopology.Schema
				|| part.X != KingdomHostedArcologyTopology.EntryX
				|| part.Y != KingdomHostedArcologyTopology.EntryY
				|| part.Z != KingdomHostedArcologyTopology.EntryZ
				|| !KingdomHostedArcologyTopology.InBounds(X, Y, Z))
				return ContextFail("hosted shell has no exact native interior declaration",
					out Failure);
			string world, schema, instance; int wx, wy, x, y, z;
			string entry = part.ZoneID;
			if (!ZoneID.Parse(entry, out world, out schema, out instance,
				out wx, out wy, out x, out y, out z) || world != "Interior"
				|| schema != part.Cell || instance != Shell.IDIfAssigned
				|| wx != part.WX || wy != part.WY || x != part.X || y != part.Y || z != part.Z
				|| entry != ZoneID.Assemble(world + "@" + schema + "@" + instance,
					wx, wy, x, y, z))
				return ContextFail("hosted native interior entry identity is noncanonical",
					out Failure);
			Target = ZoneID.Assemble(world + "@" + schema + "@" + instance,
				wx, wy, X, Y, Z);
			return true;
		}

		private static bool TryExactAnchor(Zone Z, string RootId, string LotKey,
			out GameObject AnchorObject, out r_KingdomArcologyZoneAnchor Anchor,
			out string Failure)
		{
			AnchorObject = null; Anchor = null; Failure = null;
			InteriorZone interior = Z as InteriorZone;
			string id = KingdomHostedArcologyRules.StableChildId(RootId,
				KingdomHostedArcologyTopology.StableRole(interior.X, interior.Y,
					interior.Z, "anchor"));
			int count = 0;
			foreach (GameObject item in Z.GetObjects())
				if (item.IDIfAssigned == id) { AnchorObject = item; count++; }
			Anchor = AnchorObject?.GetPart<r_KingdomArcologyZoneAnchor>();
			return count == 1 && AnchorObject.Blueprint == "r_KingdomArcologyZoneAnchor"
				&& AnchorObject.CurrentCell == Z.GetCell(40, 3) && Anchor != null
				&& Anchor.ZoneX == interior.X && Anchor.ZoneY == interior.Y
				&& Anchor.ZoneZ == interior.Z && (Anchor.LotKey ?? "") == LotKey
				|| ContextFail("hosted observation has no exact stable interior anchor",
					out Failure);
		}

		private static bool ContextFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
