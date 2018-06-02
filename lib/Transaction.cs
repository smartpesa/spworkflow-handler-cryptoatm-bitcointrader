using log4net;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;

namespace BitCoinTrader
{
    public interface ITransaction
    {
        void InitVars(ILog log, string fullnodeUrl, string fullnodeAuth, string insightApi, string privateKey, decimal minerFee);
        string CreateRawTransaction(string receiverAddress, decimal amount);
        RPCResponse SendRawTransaction(string signedHex);
        List<UTXOResponse> GetListUnspent(BitcoinPubKeyAddress senderAddress);
        Coin[] GetTxOuts(List<UTXOResponse> uTXOs, BitcoinPubKeyAddress senderAddress, decimal sendAmount);
        long CrytoCurrency2Satoshi(long amount);
        string CurrencySymbol();
    }

    public class LitecoinTransaction : ITransaction
    {
        private static ILog _log;
        private string _fullnodeUrl;
        private string _fullnodeAuth;
        private string _insightApi;
        private BitcoinSecret _sender;
        private decimal _minerFee;

        public LitecoinTransaction()
        {
        }

        public void InitVars(ILog log, string fullnodeUrl, string fullnodeAuth, string insightApi, string privateKey, decimal minerFee)
        {
            _log = log;
            _fullnodeUrl = fullnodeUrl;
            _fullnodeAuth = fullnodeAuth;
            _insightApi = insightApi;
            _minerFee = minerFee;
            _sender = new BitcoinSecret(privateKey);
        }

        public string CreateRawTransaction(string receiverAddress, decimal amount)
        {
            BitcoinAddress receiverAddr = BitcoinAddress.Create(receiverAddress);

            List<UTXOResponse> uTXOs = GetListUnspent(_sender.GetAddress());

            Coin[] sendCoins = GetTxOuts(uTXOs, _sender.GetAddress(), amount);

            var txBuilder = new TransactionBuilder();
            var tx = txBuilder
                .AddCoins(sendCoins)
                .AddKeys(_sender.PrivateKey)
                .Send(receiverAddr, (amount - _minerFee).ToString())
                .SetChange(_sender.GetAddress())
                .SendFees(_minerFee.ToString())
                .BuildTransaction(true);

            _log.Debug("CreateRawTransaction: ");
            _log.Debug(tx);
            _log.Debug(tx.ToHex());

            return tx.ToHex();
        }

        public long CrytoCurrency2Satoshi(long amount)
        {
            return amount * 100000000;
        }

        public string CurrencySymbol()
        {
            return "LTC";
        }

        public List<UTXOResponse> GetListUnspent(BitcoinPubKeyAddress senderAddress)
        {
            dynamic obj = new
            {
                addrs = senderAddress.ToString()
            };

            string jsonUTXO = WebUtils.RequestApi(_log, _insightApi + "/addrs/utxo", Newtonsoft.Json.JsonConvert.SerializeObject(obj));
            List<UTXOResponse> uTXOResponse = WebUtils.ParseApiResponse<List<UTXOResponse>>(jsonUTXO);

            _log.InfoFormat("Total amount unspent: {0} {1}", uTXOResponse.Sum(x => x.amount), CurrencySymbol());
            return uTXOResponse;
        }

        public Coin[] GetTxOuts(List<UTXOResponse> uTXOs, BitcoinPubKeyAddress senderAddr, decimal sendAmount)
        {
            List<Coin> coins = new List<Coin>();
            int idx = 0;
            while (coins.Sum(x => x.TxOut.Value.Satoshi) < CrytoCurrency2Satoshi((long)sendAmount))
            {
                TxOut txOut = new TxOut(new Money(uTXOs[idx].satoshis), senderAddr);
                coins.Add(new Coin(new OutPoint(uint256.Parse(uTXOs[idx].txid), uTXOs[idx].vout), txOut));
                idx++;
            }
            return coins.ToArray();
        }

        public RPCResponse SendRawTransaction(string signedHex)
        {
            Dictionary<string, object> rPCRequest = new Dictionary<string, object>()
            {
                { "jsonrpc", "1.0" },
                { "id", "testid" },
                { "method", "sendrawtransaction" },
                { "params", new List<string> {
                    signedHex
                }
            }};

            string jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(rPCRequest);
            _log.Info("Request: " + jsonRequest);
            string response = WebUtils.RequestRPC(_log, _fullnodeUrl, _fullnodeAuth, jsonRequest);
            _log.Info("Response: " + response);
            return WebUtils.ParseRPCResponse<RPCResponse>(response);
        }
    }

    public class BitcoinTransaction : ITransaction
    {
        private static ILog _log;
        private string _fullnodeUrl;
        private string _fullnodeAuth;
        private string _insightApi;
        private BitcoinSecret _sender;
        private decimal _minerFee;

        public BitcoinTransaction()
        {
        }

        public void InitVars(ILog log, string fullnodeUrl, string fullnodeAuth, string insightApi, string privateKey, decimal minerFee)
        {
            _log = log;
            _fullnodeUrl = fullnodeUrl;
            _fullnodeAuth = fullnodeAuth;
            _insightApi = insightApi;
            _minerFee = minerFee;
            _sender = new BitcoinSecret(privateKey);
        }

        public string CreateRawTransaction(string receiverAddress, decimal amount)
        {
            BitcoinAddress receiverAddr = BitcoinAddress.Create(receiverAddress);

            List<UTXOResponse> uTXOs = GetListUnspent(_sender.GetAddress());

            Coin[] sendCoins = GetTxOuts(uTXOs, _sender.GetAddress(), amount);

            var txBuilder = new TransactionBuilder();
            var tx = txBuilder
                .AddCoins(sendCoins)
                .AddKeys(_sender.PrivateKey)
                .Send(receiverAddr, (amount - _minerFee).ToString())
                .SetChange(_sender.GetAddress())
                .SendFees(_minerFee.ToString())
                .BuildTransaction(true);

            _log.Debug("CreateRawTransaction: ");
            _log.Debug(tx);
            _log.Debug(tx.ToHex());

            return tx.ToHex();
        }

        public long CrytoCurrency2Satoshi(long amount)
        {
            return amount * 100000000;
        }

        public string CurrencySymbol()
        {
            return "BTC";
        }

        public List<UTXOResponse> GetListUnspent(BitcoinPubKeyAddress senderAddress)
        {
            dynamic obj = new
            {
                addrs = senderAddress.ToString()
            };

            string jsonUTXO = WebUtils.RequestApi(_log, _insightApi + "/addrs/utxo", Newtonsoft.Json.JsonConvert.SerializeObject(obj));
            List<UTXOResponse> uTXOResponse = WebUtils.ParseApiResponse<List<UTXOResponse>>(jsonUTXO);

            _log.InfoFormat("Total amount unspent: {0} {1}", uTXOResponse.Sum(x => x.amount), CurrencySymbol());
            return uTXOResponse;
        }

        public Coin[] GetTxOuts(List<UTXOResponse> uTXOs, BitcoinPubKeyAddress senderAddr, decimal sendAmount)
        {
            List<Coin> coins = new List<Coin>();
            int idx = 0;
            while (coins.Sum(x => x.TxOut.Value.Satoshi) < CrytoCurrency2Satoshi((long)sendAmount))
            {
                TxOut txOut = new TxOut(new Money(uTXOs[idx].satoshis), senderAddr);
                coins.Add(new Coin(new OutPoint(uint256.Parse(uTXOs[idx].txid), uTXOs[idx].vout), txOut));
                idx++;
            }
            return coins.ToArray();
        }

        public RPCResponse SendRawTransaction(string signedHex)
        {
            Dictionary<string, object> rPCRequest = new Dictionary<string, object>()
            {
                { "jsonrpc", "1.0" },
                { "id", "testid" },
                { "method", "sendrawtransaction" },
                { "params", new List<string> {
                    signedHex
                }
            }};

            string jsonRequest = Newtonsoft.Json.JsonConvert.SerializeObject(rPCRequest);
            _log.Info("Request: " + jsonRequest);
            string response = WebUtils.RequestRPC(_log, _fullnodeUrl, _fullnodeAuth, jsonRequest);
            _log.Info("Response: " + response);
            return WebUtils.ParseRPCResponse<RPCResponse>(response);
        }
    }
}