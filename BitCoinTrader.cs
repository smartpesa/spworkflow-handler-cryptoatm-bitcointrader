using BitCoinTrader;
using log4net;
using Newtonsoft.Json;
using SmartPesa.Common;
using SmartPesa.Objects;
using SmartPesa.WorkflowLibrary;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Threading;
using ZeroMQ;

namespace SmartPesa.Workflow
{
    public class CryptoATMBitCoinTrader : DestinationBase
    {
        private static NameValueCollection _keySettings;
        private static bool _isSending = false;

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
              <add key="expire" value="5" /> <!-- in seconds -->
              <add key="btcFullNodeUrl" value="bitcoin fullnode url" />
              <add key="btcFullNodeAuth" value="bitcoin fullnode basic auth" />
              <add key="btcInsightApi" value="bitcoin insight api url" />
              <add key="btcSenderAddress" value="bitcoin sender address (public address)" />
              <add key="btcMinerFee" value="0.001" />
              <add key="ltcFullNodeUrl" value="litecoin fullnode url" />
              <add key="ltcFullNodeAuth" value="litecoin fullnode basic auth" />
              <add key="ltcInsightApi" value="litecoin insight api url" />
              <add key="ltcSenderAddress" value="litecoin sender address (public address)" />
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
                BitCoinTraderConfig.EXPIRE = int.Parse(_keySettings["expire"]);
                BitCoinTraderConfig.BTC_FULL_NODE_URL = _keySettings["btcFullNodeUrl"];
                BitCoinTraderConfig.BTC_FULL_NODE_AUTH = _keySettings["btcFullNodeAuth"];
                BitCoinTraderConfig.BTC_INSIGHT_API = _keySettings["btcInsightApi"];
                BitCoinTraderConfig.BTC_SENDER_ADDRESS = _keySettings["btcSenderAddress"];
                BitCoinTraderConfig.BTC_MINER_FEE = decimal.Parse(_keySettings["btcMinerFee"]);

                BitCoinTraderConfig.LTC_FULL_NODE_URL = _keySettings["ltcFullNodeUrl"];
                BitCoinTraderConfig.LTC_FULL_NODE_AUTH = _keySettings["ltcFullNodeAuth"];
                BitCoinTraderConfig.LTC_INSIGHT_API = _keySettings["ltcInsightApi"];
                BitCoinTraderConfig.LTC_SENDER_ADDRESS = _keySettings["ltcSenderAddress"];
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
                    DateTime expires_at = DateTime.Now.AddSeconds(BitCoinTraderConfig.EXPIRE);
                    string receiver_address = dp.extra_data["qrcode"].ToString();
                    decimal amount;
                    decimal.TryParse(dp.extra_data["amount"].ToString(), out amount);
                    string crypto_currency_symbol = dp.extra_data["crypto_currency_symbol"].ToString();
                    TransactionResultSet transaction = new TransactionResultSet();
                    if (dp.extra_data.ContainsKey("transaction"))
                        transaction = JsonConvert.DeserializeObject<TransactionResultSet>(dp.extra_data["transaction"].ToString());

                    while (_isSending)
                    {
                        Thread.Sleep(500);
                    }

                    if (expires_at < DateTime.Now)
                    {
                        response.status.code = Objects.Error.SystemTimeout;
                        response.status.value = "Timeout";
                    }
                    else
                    {
                        _isSending = true;
                        BitCoinTrader.Transaction cryptoTransaction = new BitCoinTrader.Transaction();

                        switch (crypto_currency_symbol)
                        {
                            case "BTC":
                                cryptoTransaction.InitVars(
                                    _log,
                                    BitCoinTraderConfig.BTC_FULL_NODE_URL,
                                    BitCoinTraderConfig.BTC_FULL_NODE_AUTH,
                                    BitCoinTraderConfig.BTC_INSIGHT_API,
                                    BitCoinTraderConfig.BTC_SENDER_ADDRESS,
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
                                    BitCoinTraderConfig.LTC_SENDER_ADDRESS,
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

                        List<UTXOResponse> uTXOs = cryptoTransaction.GetListUnspent(cryptoTransaction._sender);

                        if (uTXOs.Count != 0)
                        {
                            DataProvider dataProvider = new DataProvider();
                            dataProvider.extra_data.Add("uTXOs", uTXOs);
                            dataProvider.extra_data.Add("amount", amount);
                            dataProvider.extra_data.Add("minerFee", cryptoTransaction._minerFee);
                            dataProvider.extra_data.Add("receiverAddress", receiver_address);
                            dataProvider.extra_data.Add("currencySymbol", cryptoTransaction._currencySymbol);
                            response = SpMessage.Transact<DataProvider, Response>(dataProvider, GetZMQAddress(crypto_currency_symbol) , new ZContext(), log: _log);
                            if (response.status.code == Objects.Error.NoError)
                            {
                                RPCResponse rpcResponse = cryptoTransaction.SendRawTransaction(response.result.ToString(), transaction.transaction_id);
                                response.result = rpcResponse;
                                if (rpcResponse.error != null)
                                {
                                    response.status.code = (Objects.Error)rpcResponse.error.code;
                                    response.status.value = rpcResponse.error.message;
                                }
                            }
                        }
                        else
                        {
                            response.status.code = Objects.Error.ServerError;
                            response.status.value = "Sender address don't have utxo";
                        }
                        _isSending = false;
                    }
                }
            }
            catch (Exception ex)
            {
                response.status.code = Objects.Error.ServerError;
                response.status.value = ex.Message;
                _isSending = false;
            }

            return response;
        }

        private static string GetZMQAddress(string crypto_currency_symbol)
        {
            if (crypto_currency_symbol == "BTC") return AddressLabel.SignBitcoin;
            if (crypto_currency_symbol == "LTC") return AddressLabel.SignLitecoin;
            return AddressLabel.SignLitecoin;
        }
    }
}
