using log4net;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BitCoinTrader
{
    public class Transaction
    {
        private static ILog _log;
        private string _fullnodeUrl;
        private string _fullnodeAuth;
        private string _insightApi;
        private string _currencySymbol;
        private BitcoinSecret _sender;
        private decimal _minerFee;

        public Transaction()
        {

        }

        public void InitVars(ILog log, string fullnodeUrl, string fullnodeAuth, string insightApi, string privateKey, decimal minerFee, string currencySymbol)
        {
            _log = log;
            _fullnodeUrl = fullnodeUrl;
            _fullnodeAuth = fullnodeAuth;
            _insightApi = insightApi;
            _minerFee = minerFee;
            _currencySymbol = currencySymbol;
            _sender = new BitcoinSecret(privateKey);
        }

        public string CreateRawTransaction(string receiverAddress, decimal amount)
        {
            BitcoinAddress receiverAddr = BitcoinAddress.Create(receiverAddress);

            List<UTXOResponse> uTXOs = GetListUnspent(_sender.GetAddress());

            if (uTXOs.Count == 0) throw new Exception("Sender address don't have utxo");

            amount += _minerFee;

            if (uTXOs.Sum(item => item.amount) < amount) throw new Exception(string.Format("Sender's total amount: {0} {1} less than send amount {2} {3}", uTXOs.Sum(item => item.amount), _currencySymbol, amount, _currencySymbol));

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

        public List<UTXOResponse> GetListUnspent(BitcoinPubKeyAddress senderAddress)
        {
            dynamic obj = new
            {
                addrs = senderAddress.ToString()
            };

            string jsonUTXO = WebUtils.RequestApi(_log, _insightApi + "/addrs/utxo", Newtonsoft.Json.JsonConvert.SerializeObject(obj));
            List<UTXOResponse> uTXOResponse = WebUtils.ParseApiResponse<List<UTXOResponse>>(jsonUTXO);

            _log.InfoFormat("Total amount unspent: {0} {1}", uTXOResponse.Sum(x => x.amount), _currencySymbol);
            return uTXOResponse;
        }

        public Coin[] GetTxOuts(List<UTXOResponse> uTXOs, BitcoinPubKeyAddress senderAddr, decimal sendAmount)
        {
            List<Coin> coins = new List<Coin>();
            int idx = 0;
            while (coins.Sum(x => x.TxOut.Value.ToDecimal(MoneyUnit.BTC)) < sendAmount)
            {
                TxOut txOut = new TxOut(new Money(uTXOs[idx].satoshis), senderAddr);
                coins.Add(new Coin(new OutPoint(uint256.Parse(uTXOs[idx].txid), uTXOs[idx].vout), txOut));
                idx++;
            }
            return coins.ToArray();
        }

        public RPCResponse SendRawTransaction(string signedHex, Guid transaction_id)
        {
            Dictionary<string, object> rPCRequest = new Dictionary<string, object>()
            {
                { "jsonrpc", "1.0" },
                { "id", transaction_id.ToString() },
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