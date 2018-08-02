﻿using Newtonsoft.Json;
using Quartz;
using RuiJi.Net.Core.Expression;
using RuiJi.Net.Core.Extractor;
using RuiJi.Net.Core.Extractor.Selector;
using RuiJi.Net.Core.Queue;
using RuiJi.Net.Core.Utils;
using RuiJi.Net.Core.Utils.Logging;
using RuiJi.Net.Storage;
using RuiJi.Net.Storage.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuiJi.Net.Node.Feed.LTS
{
    public class FeedExtractJob : IJob
    {
        private static readonly string snapshotPath;
        private static readonly string basePath;
        private static readonly string failPath;
        private static readonly string historyPath;
        private static readonly string delayPath;

        private static string baseUrl;

        private static TaskQueuePool queuePool;

        private static bool IsRunning = false;

        static FeedExtractJob()
        {
            basePath = AppDomain.CurrentDomain.BaseDirectory;
            snapshotPath = Path.Combine(basePath, "snapshot");
            failPath = Path.Combine(basePath, "save_failed");
            historyPath = Path.Combine(basePath + "history");
            delayPath = Path.Combine(basePath + "delay");

            Directory.CreateDirectory(snapshotPath);
            Directory.CreateDirectory(failPath);
            Directory.CreateDirectory(historyPath);
            Directory.CreateDirectory(delayPath);
            Directory.CreateDirectory(Path.Combine(basePath + "pre"));

            if (Environment.ProcessorCount == 1)
            {
                queuePool = FeedJob.queuePool;
            }
            else
            {
                queuePool = new TaskQueuePool(8);
                queuePool.Start();
            }
        }

        private void CreateDirectory(string path)
        {
            path = Path.Combine(basePath, path);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        protected void OnJobStart(IJobExecutionContext context)
        {
            try
            {
                Logger.GetLogger(baseUrl).Info("extract job started ");

                baseUrl = context.JobDetail.JobDataMap.Get("baseUrl").ToString();

                Logger.GetLogger(baseUrl).Info("begin move delay feed ");

                var files = Directory.GetFiles(delayPath);
                foreach (var file in files)
                {
                    var filename = new FileInfo(file).Name;
                    var sp = filename.Split('_');
                    var ticks = sp[1].Substring(0, sp[1].LastIndexOf("."));

                    if (long.Parse(ticks) < DateTime.Now.Ticks)
                    {
                        var desFile = file.Replace("delay", "snapshot");
                        File.Move(file, desFile);

                        Logger.GetLogger(baseUrl).Info("move delay feed " + file);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.GetLogger(baseUrl).Error("extract job error " + ex.Message);
            }
        }

        public List<string> ExtractAddress(FeedSnapshot feed)
        {
            var block = new ExtractBlock();
            block.TileSelector.Selectors.Add(new CssSelector("a", "href"));

            if (!string.IsNullOrEmpty(feed.RuiJiExpression))
            {
                block.TileSelector.Selectors.Clear();

                var parser = new RuiJiParser();

                var s = RuiJiBlockParser.ParserBase(feed.RuiJiExpression).Selectors;
                block.TileSelector.Selectors.AddRange(s);
            }

            var result = RuiJiExtractor.Extract(feed.Content, block);
            var results = new List<string>();

            if (result.Tiles != null)
            {
                foreach (var item in result.Tiles)
                {
                    var href = item.Content.ToString();
                    if (href.Contains("#"))
                    {
                        href = href.Substring(0, href.IndexOf('#'));
                    }
                    if (Uri.IsWellFormedUriString(href, UriKind.Relative))
                        href = new Uri(new Uri(feed.Url), href).AbsoluteUri.ToString();

                    results.Add(href);
                }
            }

            return results.Distinct().ToList();
        }

        protected List<string> GetSnapshot()
        {
            return Directory.GetFiles(snapshotPath).ToList();
        }

        public void DoTask(string path)
        {
            try
            {
                var file = new FileInfo(path);

                var sp = file.Name.Split('_');
                var feedId = Convert.ToInt32(sp[0]);
                var content = File.ReadAllText(path);

                var feed = JsonConvert.DeserializeObject<FeedSnapshot>(content);
                var url = feed.Url;

                Logger.GetLogger(baseUrl).Info(" extract feed " + feed.Url + " address ");

                var urls = ExtractAddress(feed);

                Logger.GetLogger(baseUrl).Info(" extract feed " + feed.Url + " address count :" + urls.Count);

                var hisFile = Path.Combine(historyPath, feedId + ".txt");
                var urlsHistory = new string[0];
                if (File.Exists(hisFile))
                {
                    urlsHistory = File.ReadAllLines(hisFile, Encoding.UTF8);

                    Logger.GetLogger(baseUrl).Info(" read feed history : " + urlsHistory.Length);
                }

                File.WriteAllLines(hisFile, urls, Encoding.UTF8);

                urls.RemoveAll(m => urlsHistory.Contains(m));
                urls.RemoveAll(m => string.IsNullOrEmpty(m));
                urls.RemoveAll(m => !Uri.IsWellFormedUriString(m, UriKind.Absolute));

                Logger.GetLogger(baseUrl).Info("feed " + feed.Url + " new url count : " + urls.Count);

                foreach (var u in urls)
                {
                    Logger.GetLogger(baseUrl).Info(" extract job " + u + " add to feed extract queue");

                    queuePool.QueueAction(() =>
                    {
                        Logger.GetLogger(baseUrl).Info(" extract job " + u + " starting");

                        var result = NodeVisitor.Cooperater.GetResult(u);
                        if (result != null)
                        {
                            Save(feedId, u, result);
                        }
                        else
                        {
                            Logger.GetLogger(baseUrl).Info(" extract job " + u + " result is null");
                        }
                    });
                }

                var destFile = path.Replace("snapshot", "pre").Replace(file.Name, feedId + ".txt");
                if (File.Exists(destFile))
                    File.Delete(destFile);

                File.Move(path, destFile);

                Logger.GetLogger(baseUrl).Info(" move feed snapshot to pre fold " + destFile);
            }
            catch (Exception ex)
            {
                Logger.GetLogger(baseUrl).Error(" feed snapshot extract error " + ex.Message);
            }
        }

        protected void Save(int feedId, string url, ExtractResult result)
        {
            var cm = new ContentModel();
            cm.FeedId = feedId;
            cm.Url = url;
            cm.Metas = result.Metas;
            cm.CDate = DateTime.Now;

            var saveFlag = NodeVisitor.Feeder.SaveContent(cm);
            if (!saveFlag)
                File.AppendAllText(Path.Combine(failPath, EncryptHelper.GetMD5Hash(url) + ".json"), JsonConvert.SerializeObject(cm));

            Logger.GetLogger(baseUrl).Info(" extract job " + url + " save result " + saveFlag);
        }

        public async Task Execute(IJobExecutionContext context)
        {
            Logger.GetLogger(baseUrl).Info(" feed extract job execute ");

            if (!IsRunning)
            {
                IsRunning = true;

                OnJobStart(context);

                var task = Task.Run(() =>
                {
                    var snapshots = GetSnapshot();
                    Logger.GetLogger(baseUrl).Info(" get snapshot feed count:" + snapshots.Count);

                    foreach (var snapshot in snapshots)
                    {
                        DoTask(snapshot);
                    }
                });

                await task;

                IsRunning = false;
            }
        }
    }
}