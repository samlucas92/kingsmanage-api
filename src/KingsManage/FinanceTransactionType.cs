using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KingsManage;

[JsonConverter(typeof(StringEnumConverter))]
public enum FinanceTransactionType
{
	Charge,
	Payment,
	Adjustment
}
