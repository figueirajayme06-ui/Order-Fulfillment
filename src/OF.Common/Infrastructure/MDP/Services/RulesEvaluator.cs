using Microsoft.Extensions.Caching.Memory;
using OF.Common.Infrastructure.MDP.Models;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace OF.Common.Infrastructure.MDP.Services
{
    public class RulesEvaluator : IRulesEvaluator
    {
        public IList<ProductRule>? ProductRules { get; set; }
        public IList<ConfigurationRule>? ConfigurationRules { get; set; }
        public IList<ErrorCondition>? ErrorConditions { get; set; }
        public IList<SummaryVariable>? SummaryVariables { get; set; }
        public IList<ProductAction>? ProductActions { get; set; }
        public ConfigurationRuleCache ConfigurationRuleCache { get; set; }

        private readonly IRulesExtractor _rulesExtractor;
        private readonly IMemoryCache memoryCache;

        public RulesEvaluator(IRulesExtractor rulesExtractor, IMemoryCache memoryCache)
        {
            _rulesExtractor = rulesExtractor;
            this.memoryCache = memoryCache;
            ConfigurationRuleCache = new ConfigurationRuleCache();
        }

        private async Task Initialize()
        {
            bool isCached = memoryCache.Get<bool>("IsRulesCached");

            if (!isCached)
            {
                var productRulesTask = _rulesExtractor.LoadProductRules();
                var productActionsTask = _rulesExtractor.LoadProductActions();
                var configurationRulesTask = _rulesExtractor.LoadConfigurationRules();
                var errorConditionsTask = _rulesExtractor.LoadErrorConditions();
                var summaryVariablesTask = _rulesExtractor.LoadSummaryVariables();

                await Task.WhenAll(
                    productRulesTask,
                    productActionsTask,
                    configurationRulesTask,
                    errorConditionsTask,
                    summaryVariablesTask
                );

                ProductRules = productRulesTask.Result;
                ProductActions = productActionsTask.Result;
                ConfigurationRules = configurationRulesTask.Result;
                ErrorConditions = errorConditionsTask.Result;
                SummaryVariables = summaryVariablesTask.Result;

                BuildConfigurationRuleCache();

                memoryCache.Set("IsRulesCached", true, TimeSpan.FromHours(1));
            }
        }

        public async Task<IList<ConfigurationRule>> GetRulesForProduct(string productId)
        {
            await Initialize();

            if (ConfigurationRuleCache.Cache.ContainsKey(productId))
            {
                var rules = ConfigurationRuleCache.Cache[productId];

                foreach (var r in rules)
                {
                    r.ProductRule = ProductRules?.FirstOrDefault(pr => pr.Id == r.ProductRuleId);
                    if (r.ProductRule != null)
                    {
                        r.ProductRule.ErrorConditions = ErrorConditions.Where(ec => ec.Rule == r.ProductRule.Id).ToList();

                        foreach (var ec in r.ProductRule.ErrorConditions)
                        {
                            if (ec.TestedVariable != null)
                            {
                                ec.SummaryVariable = SummaryVariables?.FirstOrDefault(sv => sv.Id == ec.TestedVariable);
                            }
                        }

                        r.ProductRule.ExecutableFormula = await ExpandProductRule(r.ProductRule);
                        r.ProductRule.ProductActions = ProductActions.Where(pa => pa.Rule == r.ProductRule.Id).ToList();
                    }
                }
                return rules;
            }
            else
            {
                return new List<ConfigurationRule>();
            }
        }

        private void BuildConfigurationRuleCache()
        {
            ConfigurationRuleCache = new ConfigurationRuleCache();
            foreach (var cr in ConfigurationRules)
            {
                List<ConfigurationRule> container;

                if (!ConfigurationRuleCache.Cache.ContainsKey(cr.Product))
                {
                    container = new List<ConfigurationRule>();
                    ConfigurationRuleCache.Cache.Add(cr.Product, container);
                }
                else
                {
                    container = ConfigurationRuleCache.Cache[cr.Product];
                }
                container.Add(cr);
            }
        }

        private readonly Dictionary<string, string> ObjectMap = new Dictionary<string, string>()
        {
            { "Quote", "quote" },
            { "Configuration Attributes", "attributes" },
            { "Quote Line", "quoteLine" },
            { "Product Option", "productOption" }
        };

        private readonly Dictionary<string, string> OperationMap = new Dictionary<string, string>()
        {
            { "greater than", "{0}['{1}'] > '{2}'" },
            { "less than", "{0}['{1}'] < '{2}'" },
            { "equals", "{0}['{1}'] == '{2}'" },
            { "contains", "{0}['{1}'].indexOf('{2}')>=0" },
            { "less or equals", "{0}['{1}'] <= '{2}'" },
            { "greater or equals", "{0}['{1}'] >= '{2}'" },
            { "not equals", "{0}['{1}'] != '{2}'" }
        };

        public async Task<string> ExpandErrorCondition(ErrorCondition condition)
        {
            await Initialize();

            StringBuilder sb = new StringBuilder("("); // Always start a new group
            bool success = false;

            if (OperationMap.ContainsKey(condition.Operator))
            {
                if (!string.IsNullOrEmpty(condition.TestedObject) && ObjectMap.ContainsKey(condition.TestedObject) && !string.IsNullOrEmpty(condition.TestedField))
                {
                    sb.Append(string.Format(OperationMap[condition.Operator], ObjectMap[condition.TestedObject], condition.TestedField, condition.FilterValue));
                    success = true;
                }

                if (!string.IsNullOrEmpty(condition.FilterVariable) && condition.SummaryVariable != null)
                {
                    sb.Append(string.Format(OperationMap[condition.Operator], "variables['" + condition.SummaryVariable.Id + "']", condition.FilterVariable, condition.FilterValue));
                    success = true;
                }

                if (!success)
                {
                    sb.Append("1==1"); // Ignore malformed rules
                }
            }

            sb.Append(")"); // Terminate group

            return sb.ToString();
        }

        public async Task<string?> ExpandProductRule(ProductRule rule)
        {
            if (rule.ErrorConditions != null && rule.ErrorConditions.Count > 0)
            {
                if (rule.ConditionsMet == "Custom" && !string.IsNullOrEmpty(rule.AdvancedCondition))
                {
                    var ruleBuf = Regex.Replace(rule.AdvancedCondition.Replace("OR", "||").Replace("AND", "&&"), @"\d+", m => "@@" + m.Value);

                    // Work backwards through the indexes of the error conditions
                    var errorConditions = rule.ErrorConditions.OrderByDescending(ec => ec.IndexValue);
                    foreach (var ec in errorConditions)
                    {
                        var index = ec.IndexValue.HasValue ? (int)ec.IndexValue : 0;
                        var condition = await ExpandErrorCondition(ec);
                        ruleBuf = ruleBuf.Replace("@@" + index, condition);
                    }

                    return ruleBuf;
                }
                else
                {
                    var joiner = rule.ConditionsMet == "All" ? " && " : " || ";
                    var sb = new StringBuilder();

                    for (var i = 0; i < rule.ErrorConditions.Count; i++)
                    {
                        sb.Append(ExpandErrorCondition(rule.ErrorConditions[i]));
                        if (i < rule.ErrorConditions.Count - 1)
                        {
                            sb.Append(joiner);
                        }
                    }
                    return sb.ToString();
                }
            }

            return null;
        }
    }
}
