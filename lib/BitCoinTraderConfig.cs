using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitCoinTrader
{
    public class BitCoinTraderConfig
    {
        public static string BTC_FULL_NODE_URL { get; set; }
        public static string BTC_FULL_NODE_AUTH { get; set; }
        public static string BTC_INSIGHT_API { get; set; }
        public static string BTC_PRIVATE_KEY { get; set; }
        public static decimal BTC_MINER_FEE { get; set; }

        public static string LTC_FULL_NODE_URL { get; set; }
        public static string LTC_FULL_NODE_AUTH { get; set; }
        public static string LTC_INSIGHT_API { get; set; }
        public static string LTC_PRIVATE_KEY { get; set; }
        public static decimal LTC_MINER_FEE { get; set; }
    }
}
