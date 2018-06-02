﻿using System.IO;
using System.Net;
using System;
using log4net;

namespace BitCoinTrader
{
    public class WebUtils
    {
        public static string RequestRPC(ILog log, string jsonParameters = null)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(BitCoinTraderConfig.BTC_FULL_NODE_URL);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Headers["authorization"] = "Basic " + BitCoinTraderConfig.BTC_FULL_NODE_AUTH;
            using (var streamWriter = new StreamWriter(req.GetRequestStream()))
            {
                streamWriter.Write(jsonParameters);
                streamWriter.Flush();
            }
            try
            {
                HttpWebResponse rsp = (HttpWebResponse) req.GetResponse();
                return GetResponse(rsp);
            }
            catch (WebException ex)
            {
                log.Error(ex.Message);
                return GetResponse((HttpWebResponse)ex.Response);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                return "{ \"error\": { \"code\" : \"500\", \"message\": \"" + ex.Message + "\" }}";
            }
        }

        private static string GetResponse(HttpWebResponse rsp)
        {
            using (var streamReader = new StreamReader(rsp.GetResponseStream()))
            {
                return streamReader.ReadToEnd();
            }
        }

        public static T ParseRPCResponse<T>(string json) where T : RPCResponse
        {
            T result = (T)Activator.CreateInstance(typeof(T));
            try
            {
                result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                result.error = new Error
                {
                    message = ex.Message
                };
            }
            return result;
        }

        public static string RequestApi(ILog log, string url, string jsonParameters)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(BitCoinTraderConfig.BTC_INSIGHT_API + url);
            req.Method = "POST";
            req.ContentType = "application/json";
            using (var streamWriter = new StreamWriter(req.GetRequestStream()))
            {
                streamWriter.Write(jsonParameters);
                streamWriter.Flush();
            }
            try
            {
                HttpWebResponse rsp = (HttpWebResponse)req.GetResponse();
                return GetResponse(rsp);
            }
            catch (WebException ex)
            {
                log.Error(ex.Message);
                return GetResponse((HttpWebResponse)ex.Response);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                return "{ \"error\": { \"code\" : \"500\", \"message\": \"" + ex.Message + "\" }}";
            }
        }

        public static T ParseApiResponse<T>(string json)
        {
            T result = (T)Activator.CreateInstance(typeof(T));
            try
            {
                result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                //result.error = ex.Message;
            }
            return result;
        }
    }
}