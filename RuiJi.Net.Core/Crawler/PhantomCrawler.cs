﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RuiJi.Net.Core.Cookie;
using RuiJi.Net.Core.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RuiJi.Net.Core.Crawler
{
    /// <summary>
    /// PhantomJs Crawler
    /// </summary>
    public class PhantomCrawler
    {
        /// <summary>
        /// PhantomJs response
        /// </summary>
        class PhantomResponse
        {
            /// <summary>
            /// content type
            /// </summary>
            public string contentType { get; set; }

            /// <summary>
            /// response headers
            /// </summary>
            public List<WebHeader> headers { get; set; }

            /// <summary>
            /// response charset
            /// </summary>
            public string charset { get; set; }

            /// <summary>
            /// response cookie
            /// </summary>
            public string cookie { get; set; }

            /// <summary>
            /// response status
            /// </summary>
            public int? status { get; set; }

            /// <summary>
            /// response url
            /// </summary>
            public string url { get; set; }
        }

        private static string _js;
        private static string _tmp_js_path;

        static PhantomCrawler()
        {
            _js = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crawl.js"));
            _tmp_js_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ph_download");

            if (!Directory.Exists(_tmp_js_path))
                Directory.CreateDirectory(_tmp_js_path);
        }

        /// <summary>
        /// PhantomCrawler constructor
        /// </summary>
        public PhantomCrawler()
        {
        }

        /// <summary>
        /// Request 
        /// </summary>
        /// <param name="request">Crawl Request</param>
        /// <returns>Crawl Response</returns>
        public Response Request(Request request)
        {
            var extension = Path.GetExtension(request.Uri.ToString()).ToLower();
            if (extension.IndexOf("?") != -1)
                extension = extension.Substring(0, extension.IndexOf("?"));
            var guid = ShortGUID();
            var file = @"ph_download\" + guid + extension;

            var args = "";
            if (request.Proxy != null)
            {
                args += "--proxy=" + request.Proxy.Ip + ":" + request.Proxy.Port + " --proxy-type=" + request.Proxy.Scheme;
                if (!string.IsNullOrEmpty(request.Proxy.Username))
                    args += " " + request.Proxy.Username;
                if (!string.IsNullOrEmpty(request.Proxy.Password))
                    args += " " + request.Proxy.Password;
            }

            var cookies = GetCookie(request);

            var cookie = GenerateCookieJs(cookies);

            var js = _js.Replace("phantom.addCookie({});", cookie);
            var ua = request.Headers.SingleOrDefault(m => m.Name == "User-Agent").Value;
            js = js.Replace("page.settings.userAgent = {};", "page.settings.userAgent = \"" + ua + "\";");

            var jsFile = _tmp_js_path + @"\" + guid + ".js";
            File.WriteAllText(jsFile, js);

            args += @" \ph_download\" + guid + ".js " + Uri.EscapeUriString(request.Uri.ToString()) + " " + file;

            //request.WaitDom = "#J_price";
            if (!string.IsNullOrEmpty(request.WaitDom))
                args += " " + Uri.EscapeUriString(request.WaitDom);

            var p = new Process();
            p.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "phantomjs.exe");
            p.StartInfo.Arguments = args.Trim();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = false;
            p.StartInfo.RedirectStandardOutput = false;
            p.StartInfo.RedirectStandardError = false;
            p.StartInfo.CreateNoWindow = false;
            p.Start();

            p.WaitForExit(30000);
            p.Dispose();
            p = null;

            file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);

            var response = new Response();
            if (File.Exists(file))
            {
                response.Data = File.ReadAllText(file);
                File.Delete(file);
            }

            if (File.Exists(file + ".json"))
            {
                var json = File.ReadAllText(file + ".json");
                var res = JsonConvert.DeserializeObject<PhantomResponse>(json);
                response.Headers = res.headers;
                response.Charset = res.charset;
                response.ResponseUri = new Uri(res.url);
                response.StatusCode = (System.Net.HttpStatusCode)res.status.Value;
                if (!string.IsNullOrEmpty(res.contentType))
                    response.IsRaw = MimeDetect.IsRaw(res.contentType);
                else
                    response.IsRaw = MimeDetect.IsRaw(res.contentType);


                File.Delete(file + ".json");
            }

            if (File.Exists(_tmp_js_path + @"\" + guid + ".js"))
            {
                File.Delete(_tmp_js_path + @"\" + guid + ".js");
            }

            return response;
        }

        /// <summary>
        /// guid with 16 length
        /// </summary>
        /// <returns></returns>
        private string ShortGUID()
        {
            long i = 1;
            foreach (byte b in Guid.NewGuid().ToByteArray())
                i *= ((int)b + 1);
            return string.Format("{0:x}", i - DateTime.Now.Ticks);
        }

        /// <summary>
        /// generate cookie use by PhantomJs
        /// </summary>
        /// <param name="cookie">cookie collection</param>
        /// <returns>cookie js string</returns>
        private string GenerateCookieJs(CookieCollection cookie)
        {
            var c = cookie.Cast<System.Net.Cookie>().Select(m => "phantom.addCookie(" + JsonConvert.SerializeObject(new
            {
                name = m.Name,
                value = m.Value,
                path = m.Path,
                domain = m.Domain
            }) + ");").ToList();

            return string.Join("\r\n\r\n", c);
        }

        /// <summary>
        /// get cookie by crawl request
        /// </summary>
        /// <param name="request">crawl request</param>
        /// <returns>cookie collection</returns>
        private CookieCollection GetCookie(Request request)
        {
            if (!string.IsNullOrEmpty(request.Cookie))
            {
                var c = new CookieContainer();
                c.SetCookies(request.Uri, request.Cookie);

                return c.GetCookies(request.Uri);
            }

            var ip = request.Ip;
            if (string.IsNullOrEmpty(request.Ip))
            {
                ip = IPHelper.GetDefaultIPAddress().ToString();
            }

            var ua = request.Headers.SingleOrDefault(m => m.Name == "User-Agent").Value;

            return IpCookieManager.Instance.GetCookie(ip, request.Uri.ToString(), ua);
        }
    }
}