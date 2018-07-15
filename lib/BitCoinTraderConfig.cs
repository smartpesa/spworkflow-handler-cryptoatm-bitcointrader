namespace BitCoinTrader
{
    public class BitCoinTraderConfig
    {
        public static int EXPIRE { get; set; }
        public static string BTC_FULL_NODE_URL { get; set; }
        public static string BTC_FULL_NODE_AUTH { get; set; }
        public static string BTC_INSIGHT_API { get; set; }
        public static string BTC_SENDER_ADDRESS { get; set; }
        public static decimal BTC_MINER_FEE { get; set; }

        public static string LTC_FULL_NODE_URL { get; set; }
        public static string LTC_FULL_NODE_AUTH { get; set; }
        public static string LTC_INSIGHT_API { get; set; }
        public static string LTC_SENDER_ADDRESS { get; set; }
        public static decimal LTC_MINER_FEE { get; set; }
    }
}
