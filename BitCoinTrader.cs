using BitCoinTrader;
using log4net;
using NBitcoin;
using Newtonsoft.Json;
using SmartPesa.Objects;
using SmartPesa.WorkflowLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartPesa.Workflow
{
    public class CryptoATMBitCoinTrader : DestinationBase
    {
        private static NameValueCollection _keySettings;
        private static ConcurrentQueue<string> transactionQueue = new ConcurrentQueue<string>();

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
              <add key="btcFullNodeAuth" value="bitcoin fullnode basic auth" />
              <add key="btcInsightApi" value="bitcoin insight api url" />
              <add key="btcPrivateKeyFile" value="bitcoin private key file path" />
              <add key="btcMinerFee" value="0.001" />
              <add key="ltcFullNodeUrl" value="litecoin fullnode url" />
              <add key="ltcFullNodeAuth" value="litecoin fullnode basic auth" />
              <add key="ltcInsightApi" value="litecoin insight api url" />
              <add key="ltcPrivateKeyFile" value="litecoin private key file path" />
              <add key="ltcMinerFee" value="0.001" />
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
                    BitCoinTraderConfig.BTC_PRIVATE_KEY = ReadFile(AppDomain.CurrentDomain.BaseDirectory + btcPrivateKeyFile);
                }
                BitCoinTraderConfig.BTC_MINER_FEE = decimal.Parse(_keySettings["btcMinerFee"]);


                BitCoinTraderConfig.LTC_FULL_NODE_URL = _keySettings["ltcFullNodeUrl"];
                BitCoinTraderConfig.LTC_FULL_NODE_AUTH = _keySettings["ltcFullNodeAuth"];
                BitCoinTraderConfig.LTC_INSIGHT_API = _keySettings["ltcInsightApi"];
                string ltcPrivateKeyFile = _keySettings["ltcPrivateKeyFile"];
                if (!string.IsNullOrEmpty(ltcPrivateKeyFile))
                {
                    BitCoinTraderConfig.LTC_PRIVATE_KEY = ReadFile(AppDomain.CurrentDomain.BaseDirectory + ltcPrivateKeyFile);
                }
                BitCoinTraderConfig.LTC_MINER_FEE = decimal.Parse(_keySettings["ltcMinerFee"]);
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
                    string crypto_currency_symbol = dp.extra_data["crypto_currency_symbol"].ToString();
                    TransactionResultSet transaction = new TransactionResultSet();
                    if (dp.extra_data.ContainsKey("transaction"))
                        transaction = JsonConvert.DeserializeObject<TransactionResultSet>(dp.extra_data["transaction"].ToString());

                    BitCoinTrader.Transaction cryptoTransaction = new BitCoinTrader.Transaction();

                    switch (crypto_currency_symbol)
                    {
                        case "BTC":
                            cryptoTransaction.InitVars(
                                _log,
                                BitCoinTraderConfig.BTC_FULL_NODE_URL,
                                BitCoinTraderConfig.BTC_FULL_NODE_AUTH,
                                BitCoinTraderConfig.BTC_INSIGHT_API,
                                BitCoinTraderConfig.BTC_PRIVATE_KEY,
                                BitCoinTraderConfig.BTC_MINER_FEE,
                                crypto_currency_symbol
                            );
                            break;

                        case "LTC":
                            cryptoTransaction.InitVars(
                                _log,
                                BitCoinTraderConfig.LTC_FULL_NODE_URL,
                                BitCoinTraderConfig.LTC_FULL_NODE_AUTH,
                                BitCoinTraderConfig.LTC_INSIGHT_API,
                                BitCoinTraderConfig.LTC_PRIVATE_KEY,
                                BitCoinTraderConfig.LTC_MINER_FEE,
                                crypto_currency_symbol
                            );
                            break;

                        default:
                            response.status.code = Objects.Error.InvalidParameters;
                            response.status.value = "Crypto not support";
                            break;
                    }

                    if (response.status.code != Objects.Error.NoError) return response;

                    string signedTxn = cryptoTransaction.CreateRawTransaction(receiverAddress, amount);
                    RPCResponse rpcResponse = cryptoTransaction.SendRawTransaction(signedTxn, transaction.transaction_id);
                    response.result = rpcResponse;
                    if (rpcResponse.error != null)
                    {
                        response.status.code = (Objects.Error)rpcResponse.error.code;
                        response.status.value = rpcResponse.error.message;
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

        private static string ReadFile(string filePath)
        {
            if (!File.Exists(filePath)) throw new Exception("File not exists");
            return File.ReadAllText(filePath);
        }
    }
}
