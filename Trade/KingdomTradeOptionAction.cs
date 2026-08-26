using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public enum KingdomTradeOptionAction : byte
	{
		None = 0,
		StayDisabled = 1,
		Disable = 2,
		EnableAndRestamp = 3
	}
}
