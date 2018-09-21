﻿using RuiJi.Net.Core.Extractor;
using RuiJi.Net.Core.Extractor.Selector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RuiJi.Net.Core.Extractor.Processor
{
    /// <summary>
    /// regex processor
    /// </summary>
    public class RegexProcessor : ProcessorBase<RegexSelector>
    {
        /// <summary>
        /// process need
        /// </summary>
        /// <param name="selector">regex selector</param>
        /// <param name="result">pre process result</param>
        /// <returns>new process result</returns>
        public override ProcessResult ProcessNeed(RegexSelector selector, ProcessResult result)
        {
            var regex = new Regex(selector.Pattern);
            var m = regex.Match(result.Content);
            if (!m.Success)
                return result;

            var results = new List<string>();
            var pr = new ProcessResult();

            if (selector.Index.Length > 0)
            {
                foreach (var index in selector.Index)
                {
                    if (index < m.Groups.Count)
                        results.Add(m.Groups[index].Value);
                }
                pr.Matches.Add(string.Join(" ", results.ToArray()));
            }
            else
            {
                pr.Matches.Add(m.Value);
            }

            return pr;
        }

        /// <summary>
        /// process remove
        /// </summary>
        /// <param name="selector">regex selector</param>
        /// <param name="result">pre process result</param>
        /// <returns>new process result</returns>
        public override ProcessResult ProcessRemove(RegexSelector selector, ProcessResult result)
        {
            var pr = new ProcessResult();
            pr.Matches.Add(Regex.Replace(result.Content, selector.Pattern, ""));

            return pr;
        }
    }
}