﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RuiJi.Net.Core.Crawler;
using RuiJi.Net.Core.Extractor;
using RuiJi.Net.Core.Extractor.Selector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuiJi.Net.Core.Expression
{
    /// <summary>
    /// RuiJi Expression Parser
    /// </summary>
    public class RuiJiParser
    {
        /// <summary>
        /// parse result list
        /// </summary>
        public List<IParseResult> Results { get; private set; }

        /// <summary>
        /// constructor
        /// </summary>
        public RuiJiParser()
        {
        }

        /// <summary>
        /// parse from file
        /// </summary>
        /// <param name="file">file path</param>
        /// <returns>parse result</returns>
        public bool ParseFile(string file)
        {
            if (!File.Exists(file))
                return false;

            return Parse(File.ReadAllText(file));
        }

        /// <summary>
        /// parse expression
        /// </summary>
        /// <param name="expression">ruiji expression</param>
        /// <returns>parse result</returns>
        public bool Parse(string expression)
        {
            if (string.IsNullOrEmpty(expression))
                return false;

            var sections = new Dictionary<string, List<string>>();
            var key = "";

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(expression)))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line.StartsWith("##"))
                    {
                        sections.Add(line, new List<string>());
                        key = line;
                        continue;
                    }

                    sections[key].Add(line);
                }
            }

            Results = new List<IParseResult>();

            foreach (var sectionKey in sections.Keys)
            {
                var exp = string.Join("\r\n", sections[sectionKey]);

                switch (sectionKey)
                {
                    case "##request":
                        {
                            Results.Add(ParseRequest(exp));
                            break;
                        }
                    case "##extract":
                        {
                            Results.Add(ParseExtract(exp));
                            break;
                        }
                    case "##storage":
                        {
                            //Results.Add(ParseStorage(exp));
                            break;
                        }
                    case "##setting":
                        {
                            Results.Add(ParseSetting(exp));
                            break;
                        }
                    case "##rule":
                        {
                            Results.Add(ParseFeatureRule(exp));
                            break;
                        }
                }
            }

            return Results.Sum(m=>m.Messages.Count) == 0;
        }

        /// <summary>
        /// parse request from ruiji expression
        /// </summary>
        /// <param name="expression">ruiji expression</param>
        /// <returns>parse result with crawl request</returns>
        public ParseResult<Request> ParseRequest(string expression)
        {
            var result = new ParseResult<Request>(expression);            

            var request = new Request();

            var jObj = JObject.FromObject(request);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(expression)))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        var name = line.TrimStart('[').TrimEnd(']');
                        if (name == "address")
                            name = "uri";

                        var property = jObj.Property(name);
                        if (property != null)
                        {
                            line = reader.ReadLine();
                            if (string.IsNullOrEmpty(line))
                                continue;

                            switch (name)
                            {
                                case "uri":
                                    {
                                        if (!Uri.IsWellFormedUriString(line, UriKind.Absolute))
                                        {
                                            result.Messages.Add("address is not wellformated");
                                            continue;
                                        }
                                        property.Value = line;
                                        break;
                                    }
                                case "proxy":
                                    {
                                        var sp = line.Split(' ');
                                        if (sp.Length < 2)
                                        {
                                            result.Messages.Add("proxy must set with ip and port");
                                            continue;
                                        }

                                        var proxy = new RequestProxy(sp[0], Convert.ToInt32(sp[1]));
                                        if (sp.Length > 2)
                                            proxy.Scheme = sp[2];
                                        if (sp.Length > 3)
                                            proxy.Username = sp[3];
                                        if (sp.Length > 4)
                                            proxy.Password = sp[4];

                                        property.Value = JToken.FromObject(proxy);
                                        break;
                                    }
                                case "headers":
                                    {
                                        var headers = new List<WebHeader>();
                                        while (!string.IsNullOrEmpty(line) && !reader.EndOfStream)
                                        {
                                            var sp = line.Split(':');
                                            if (sp.Length < 2)
                                            {
                                                result.Messages.Add("header is not expected");
                                                continue;
                                            }

                                            headers.Add(new WebHeader(line.Substring(0, line.IndexOf(':')), line.Substring(line.IndexOf(':') + 1)));

                                            line = reader.ReadLine();
                                        }

                                        if (headers.Count > 0)
                                            property.Value = JToken.FromObject(headers);

                                        break;
                                    }
                                case "data":
                                    {
                                        var data = "";
                                        while (!string.IsNullOrEmpty(line) && !reader.EndOfStream)
                                        {
                                            data += "\n" + line;

                                            line = reader.ReadLine();
                                        }

                                        data = data.Trim();
                                        if (data.StartsWith("{") && data.EndsWith("}"))
                                            property.Value = JToken.FromObject(JsonConvert.DeserializeObject<object>(data));
                                        else
                                            property.Value = data;

                                        break;
                                    }
                                default:
                                    {
                                        property.Value = line;
                                        break;
                                    }
                            }
                        }
                    }
                }

                result.Result = jObj.ToObject<Request>();
            }

            return result;
        }

        /// <summary>
        /// parse extract block from ruiji expression
        /// </summary>
        /// <param name="expression">ruiji expression</param>
        /// <returns>parse result with extract block</returns>
        public ParseResult<ExtractBlock> ParseExtract(string expression)
        {
            var result = new ParseResult<ExtractBlock>(expression);

            try
            {
                result.Result = RuiJiBlockParser.ParserBlock(expression);
            }
            catch (Exception ex)
            {
                result.Messages.Add(ex.Message);
            }

            return result;
        }

        /// <summary>
        /// parse storage from ruiji expression
        /// </summary>
        /// <param name="expression">ruiji expression</param>
        /// <returns>parse result with storage</returns>
        //public ParseResult<object> ParseStorage(string expression)
        //{
        //    return new ParseResult<object>(expression);
        //}

        /// <summary>
        /// parse extract feature from ruiji expression
        /// </summary>
        /// <param name="expression">ruiji expression</param>
        /// <returns>parse result wieth extract feature</returns>
        public ParseResult<ExtractFeature> ParseFeatureRule(string expression)
        {
            var result = new ParseResult<ExtractFeature>(expression);
            result.Result.Features = new List<ISelector>();

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(expression)))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        var name = line.TrimStart('[').TrimEnd(']');
                        line = reader.ReadLine();

                        switch (name)
                        {
                            case "wildcard":
                                {
                                    if (string.IsNullOrEmpty(line))
                                    {
                                        result.Messages.Add("wildcard expression is empty");
                                        continue;
                                    }
                                    result.Result.Wildcard = line;
                                    break;
                                }
                            case "feature":
                                {
                                    while (!string.IsNullOrEmpty(line) && !reader.EndOfStream)
                                    {
                                        result.Result.Features.Add(RuiJiBlockParser.ParserSelector(line));
                                        line = reader.ReadLine();
                                    }
                                    break;
                                }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// parse setting from ruiji expression
        /// </summary>
        /// <param name="expression">ruiji expression</param>
        /// <returns>parse result with feed setting</returns>
        public ParseResult<FeedSetting> ParseSetting(string expression)
        {
            var result = new ParseResult<FeedSetting>(expression);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(expression)))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        var name = line.TrimStart('[').TrimEnd(']');
                        line = reader.ReadLine();

                        switch (name)
                        {
                            case "corn":
                                {
                                    if (string.IsNullOrEmpty(line))
                                    {
                                        result.Messages.Add("corn expression is empty");
                                        continue;
                                    }
                                    result.Result.CornExpression = line;
                                    break;
                                }
                            case "id":
                                {
                                    if (string.IsNullOrEmpty(line))
                                    {
                                        result.Messages.Add("id is not set");
                                        continue;
                                    }
                                    result.Result.Id = line;
                                    break;
                                }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// get parse result by T
        /// </summary>
        /// <typeparam name="T">T is IRuiJiParseResult</typeparam>
        /// <returns></returns>
        public ParseResult<T> GetResult<T>() where T : IRuiJiParseResult, new()
        {
            var result = Results.SingleOrDefault(m => m.Type == typeof(T));
            if(result != null)
                return result as ParseResult<T>;

            return null;
        }
    }
}