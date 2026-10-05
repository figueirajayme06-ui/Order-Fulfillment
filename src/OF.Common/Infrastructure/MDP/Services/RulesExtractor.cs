using Microsoft.EntityFrameworkCore;
using OF.Common.Infrastructure.MDP;
using OF.Common.Infrastructure.MDP.Models;
using OF.Data;

namespace OF.Common.Infrastructure.MDP.Services
{
    public class RulesExtractor : IRulesExtractor
    {
        private readonly IDbContextFactory<FDPDbContext> factory;

        public RulesExtractor(IDbContextFactory<FDPDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task<IList<ProductRule>> LoadProductRules()
        {
            using var context = factory.CreateDbContext();

            var soql = @"
SELECT
Id,
SBQQ__Active__c as Active,
SBQQ__AdvancedCondition__c as AdvancedCondition,
SBQQ__ConditionsMet__c as ConditionsMet,
SBQQ__ErrorMessage__c as ErrorMessage,
SBQQ__EvaluationEvent__c as EvaluationEvent,
SBQQ__EvaluationOrder__c as EvaluationOrder
FROM salesforce_raw_bronze_db.SBQQ__ProductRule__c;";
            return await context.Set<ProductRule>().FromSqlRaw(soql, new object[] { }).AsNoTracking().ToListAsync();
        }

        public async Task<IList<ConfigurationRule>> LoadConfigurationRules()
        {
            using var context = factory.CreateDbContext();

            var soql = @"
SELECT 
    Id,
    SBQQ__Product__c AS Product,
    Name,
    SBQQ__RuleType__c AS RuleType,
    SBQQ__RuleEvaluationEvent__c AS RuleEvaluationEvent,
    SBQQ__ProductRule__c AS ProductRuleId
FROM salesforce_raw_bronze_db.SBQQ__ConfigurationRule__c;";
            return await context.Set<ConfigurationRule>().FromSqlRaw(soql, new object[] { }).AsNoTracking().ToListAsync();
        }

        public async Task<IList<ErrorCondition>> LoadErrorConditions()
        {
            using var context = factory.CreateDbContext();

            var soql = @"
SELECT 
    Id,
    SBQQ__Rule__c AS Rule,
    SBQQ__FilterType__c AS FilterType,
    SBQQ__FilterValue__c AS FilterValue,
    SBQQ__FilterVariable__c AS FilterVariable,
    SBQQ__Index__c AS Index,
    SBQQ__Operator__c AS Operator,
    SBQQ__TestedAttribute__c AS TestedAttribute,
    SBQQ__TestedField__c AS TestedField,
    SBQQ__TestedObject__c AS TestedObject,
    SBQQ__TestedVariable__c AS TestedVariable
FROM salesforce_raw_bronze_db.SBQQ__ErrorCondition__c;";
            return await context.Set<ErrorCondition>().FromSqlRaw(soql, new object[] { }).AsNoTracking().ToListAsync();
        }

        public async Task<IList<SummaryVariable>> LoadSummaryVariables()
        {
            using var context = factory.CreateDbContext();

            var soql = @"
SELECT 
    Id,
    Name,
    SBQQ__AggregateField__c AS AggregateField,
    SBQQ__AggregateFunction__c AS AggregateFunction,
    SBQQ__CombineWith__c AS CombineWith,
    SBQQ__CompositeOperator__c AS CompositeOperator,
    SBQQ__ConstraintField__c AS ConstraintField,
    SBQQ__FilterField__c AS FilterField,
    SBQQ__FilterValue__c AS FilterValue,
    SBQQ__Operator__c AS Operator,
    SBQQ__Scope__c AS Scope,
    SBQQ__TargetObject__c AS TargetObject,
    SBQQ__ValueElement__c AS ValueElement
FROM salesforce_raw_bronze_db.SBQQ__SummaryVariable__c;";
            return await context.Set<SummaryVariable>().FromSqlRaw(soql, new object[] { }).AsNoTracking().ToListAsync();
        }

        public async Task<IList<ProductAction>> LoadProductActions()
        {
            using var context = factory.CreateDbContext();

            var soql = @"
SELECT 
    Id,
    Name,
    SBQQ__Product__c AS Product,
    SBQQ__Required__c AS Required,
    SBQQ__Rule__c AS Rule,
    SBQQ__Type__c AS Type
FROM salesforce_raw_bronze_db.SBQQ__ProductAction__c;";
            return await context.Set<ProductAction>().FromSqlRaw(soql, new object[] { }).AsNoTracking().ToListAsync();
        }
    }
}
