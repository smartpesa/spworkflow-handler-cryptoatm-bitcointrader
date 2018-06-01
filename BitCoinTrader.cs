using log4net;
using SmartPesa.Objects;
using SmartPesa.WorkflowLibrary;
using System;
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
        private static string _btcFullnodeUrl;
        private static string _btcAddress;
        private static string _btcPrivateKeyFile;

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
              <add key="btcAddress" value="bitcoin address" />
              <add key="btcPrivateKeyFile" value="bitcoin private key file path" />
            </keySettings>
          </CryptoATMBitCoinTrader>
        */

        public override string Name()
        {
            return "CrytoATM BitCoin Trader Adaptor";
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
                _btcFullnodeUrl = _keySettings["fullnodeUrl"];
                _btcAddress = _keySettings["btcAddress"];
                _btcPrivateKeyFile = _keySettings["btcPrivateKeyFile"];
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
            
            Response rc = new Response();
            return rc;
        }

        public override string Shutdown()
        {
            return null;
        }
    }
}
