using BitCoinTrader;
using log4net;
using NBitcoin;
using SmartPesa.Objects;
using SmartPesa.WorkflowLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartPesa.Workflow
{
    public class CryptoATMBitCoinTrader : DestinationBase
    {
        private static NameValueCollection _keySettings;
        private static ConcurrentQueue<string> transactionQueue = new ConcurrentQueue<string>();
        public static List<UTXOResponse> _uTXOs { get; set; }

        private static readonly ILog _log = LogManager.GetLogger("LogFile");

        /*
          spWorkflow.spring
          =================
          <objects> 
            <object name="bitcointrader" type="SmartPesa.Workflow.CryptoATMBitCoinTrader,CryptoATMBitCoinTrader"></object>
          </objects> 
          
          spWorkflow.config
          =================
          <Workflow>
            <Destinations>
              <add key="bitcointrader" type="CryptoATMBitCoinTrader" active="true" />
            </Destinations>
          </Workflow>
         
          <CryptoATMBitCoinTrader>
            <keySettings>
              <add key="btcFullNodeUrl" value="bitcoin fullnode url" />
              <add key="btcPrivateKeyFile" value="bitcoin private key file path" />
            </keySettings>
          </CryptoATMBitCoinTrader>
        */

        public override string Name()
        {
            return "CrytoATM BitCoin Trader Handler";
        }

        public override string Version()
        {
            return "1.0";
        }

        public CryptoATMBitCoinTrader()
        {
            _log.Info("Loading key settings");
            _keySettings = ConfigurationManager.GetSection("CryptoATMBitCoinTrader/keySettings") as NameValueCollection;

            if (_keySettings != null)
            {
                BitCoinTraderConfig.BTC_FULL_NODE_URL = _keySettings["btcFullNodeUrl"];
                BitCoinTraderConfig.BTC_FULL_NODE_AUTH = _keySettings["btcFullNodeAuth"];
                BitCoinTraderConfig.BTC_INSIGHT_API = _keySettings["btcInsightApi"];
                string btcPrivateKeyFile = _keySettings["btcPrivateKeyFile"];
                if (!string.IsNullOrEmpty(btcPrivateKeyFile))
                {
                    BitCoinTraderConfig.BTC_PRIVATE_KEY = btcPrivateKeyFile;
                }
            }
            else
            {
                _log.Error("Check configuration CryptoATMBitCoinTrader/keySettings");
            }
        }

        public override object ProcessMessage(object payload)
        {
            //NOTE: cast payload first
            DataProvider dp = (DataProvider)payload;
            
            _log.Info("CrytoATMBitCoinTrader Tracking ID: " + dp.tracking_id);
            return HandleRequest(dp);
        }

        public override string Shutdown()
        {
            return null;
        }

        private static Response HandleRequest(DataProvider dp)
        {
            Response response = new Response();
            try
            {

                if (dp.extra_data != null)
                {
                    string receiverAddress = dp.extra_data["qrcode"].ToString();
                    decimal amount;
                    decimal.TryParse(dp.extra_data["amount"].ToString(), out amount);
                    string crpto_currency_symbol = dp.extra_data["crpto_currency_symbol"].ToString();

                    switch (crpto_currency_symbol)
                    {
                        case "BTC":
                            break;

                        case "LTC":
                            string signedTxn = CreateRawTransaction(receiverAddress, amount);
                            RPCResponse rpcResponse = SendRawTransaction(signedTxn);
                            response.result = rpcResponse;
                            if (rpcResponse.error != null)
                            {
                                response.status.code = (Objects.Error)rpcResponse.error.code;
                                response.status.value = rpcResponse.error.message;
                            }
                            break;

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                response.status.code = Objects.Error.ServerError;
                response.status.value = ex.Message;
            }

            return response;
        }

        public static string CreateRawTransaction(string receiverAddress, decimal amount)
        {
            BitcoinSecret sender = new BitcoinSecret(BitCoinTraderConfig.BTC_PRIVATE_KEY, Network.TestNet);
            BitcoinAddress receiverAddr = BitcoinAddress.Create(receiverAddress, Network.TestNet);

            _uTXOs = GetListUnspent(sender.GetAddress());

            decimal minerFee = 0.001m;

            Coin[] sendCoins = GetTxOuts(sender.GetAddress(), amount);

            var txBuilder = new TransactionBuilder();
            var tx = txBuilder
                .AddCoins(sendCoins)
                .AddKeys(sender.PrivateKey)
                .Send(receiverAddr, amount.ToString())
                .SetChange(sender.GetAddress())
                .SendFees(minerFee.ToString())
                .BuildTransaction(true);

            _log.Debug("CreateRawTransaction: ");
            _log.Debug(tx);
            _log.Debug(tx.ToHex());

            return tx.ToHex();
        }

        private static List<UTXOResponse> GetListUnspent(BitcoinPubKeyAddress address)
        {
            dynamic obj = new
            {
                addrs = address.ToString()
            };

            string jsonUTXO = WebUtils.RequestApi(_log, "/addrs/utxo", Newtonsoft.Json.JsonConvert.SerializeObject(obj));
            List<UTXOResponse> uTXOResponse = WebUtils.ParseApiResponse<List<UTXOResponse>>(jsonUTXO);

            _log.InfoFormat("Total amount unspent: {0}", uTXOResponse.Sum(x => x.amount));
            return uTXOResponse;
        }

        private static Coin[] GetTxOuts(BitcoinPubKeyAddress senderAddr, decimal sendAmount)
        {
            List<Coin> coins = new List<Coin>();
            int idx = 0;
            while (coins.Sum(x => x.TxOut.Value.Satoshi) < CrytoCurrency2Satoshi((long)sendAmount))
            {
                TxOut txOut = new TxOut(new Money(_uTXOs[idx].satoshis), senderAddr);
                coins.Add(new Coin(new OutPoint(uint256.Parse(_uTXOs[idx].txid), _uTXOs[idx].vout), txOut));
                idx++;
            }

            return coins.ToArray();
        }

        private static long CrytoCurrency2Satoshi(long crytoCurrency)
        {
            return crytoCurrency * 100000000;
        }

        public static RPCResponse SendRawTransaction(string signedHex)
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
            string response = WebUtils.RequestRPC(_log, jsonRequest);
            _log.Info("Response: " + response);
            return WebUtils.ParseRPCResponse<RPCResponse>(response);
        }
    }
}
