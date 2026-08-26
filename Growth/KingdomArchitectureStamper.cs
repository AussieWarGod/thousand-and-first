using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine boundary that turns one frozen architecture receipt into exact durable scenery.
	/// Preparation may inspect current craft, stock claims, blueprints, and ground. Once an owner is
	/// frozen, every stage and proof reads only its architecture receipt and named per-slot receipts.
	/// </summary>
	public static partial class KingdomArchitectureStamper
	{
		public const int LayoutSchema = 1;
		public const int ComponentSchema = 1;
		public const int MaxFailureChars = 512;
		private const int MaxLotIdChars = 256;

		public const string SchemaProperty = "r_TAF_LayoutSchema";
		public const string LotIdProperty = "r_TAF_LayoutLotId";
		public const string HashProperty = "r_TAF_LayoutHash";
		public const string NextLayerProperty = "r_TAF_LayoutNextLayer";
		public const string FaultProperty = "r_TAF_LayoutFault";
		public const string OutputIdPrefix = "r_TAF_LayoutOutputId_";
		public const string OutputStatePrefix = "r_TAF_LayoutOutputState_";

		public const string ComponentSchemaProperty = "r_TAF_LayoutComponentSchema";
		public const string ComponentSlotProperty = "r_TAF_LayoutSlot";
		public const string ComponentLayerProperty = "r_TAF_LayoutLayer";
		public const string ComponentAnchorProperty = "r_TAF_LayoutAnchor";
		public const string ComponentHashProperty = "r_TAF_LayoutComponentHash";
		public const string ComponentTokenProperty = "r_TAF_LayoutComponentToken";
		public const string ComponentExistingProperty = "r_TAF_LayoutExisting";
		public const string ComponentCarriedProperty = "r_TAF_LayoutCarried";

		public const int UpgradeSchema = 1;
		public const string UpgradeSchemaProperty = "r_TAF_LayoutUpgradeSchema";
		public const string UpgradeTargetProperty = "r_TAF_LayoutUpgradeTarget";
		public const string UpgradeHashProperty = "r_TAF_LayoutUpgradeHash";
		public const string UpgradeLotProperty = "r_TAF_LayoutUpgradeLot";
		public const string UpgradePhaseProperty = "r_TAF_LayoutUpgradePhase";
		public const string UpgradeFaultProperty = "r_TAF_LayoutUpgradeFault";
		public const string UpgradeRemovePrefix = "r_TAF_LayoutUpgradeRemove_";
		public const string UpgradeRetainPrefix = "r_TAF_LayoutUpgradeRetain_";
	}
}
