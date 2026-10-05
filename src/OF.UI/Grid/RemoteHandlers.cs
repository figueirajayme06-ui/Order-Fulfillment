using Infragistics.Web.Mvc;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace OF.UI.Grid
{
    public class RemoteHandlers : IRemoteHandlers
    {
        List<FilterExpression> GetSortExpressions(IQueryCollection queryString, IGridModel grid)
        {
            var filtering = new GridFiltering();

            List<FilterExpression> list = new List<FilterExpression>();

            foreach (string key in queryString.Keys)
            {
                if (string.IsNullOrEmpty(key) || !key.StartsWith(filtering.FilterExprUrlKey + "("))
                {
                    continue;
                }

                string text = key.Substring(key.IndexOf("(")).Replace("(", "").Replace(")", "");
                int num = text.IndexOf(":", StringComparison.Ordinal);
                string text2 = ((num > -1) ? text.Substring(0, num) : text);
                string logic = "AND";
                if (queryString[filtering.FilterLogicUrlKey].ToString() != null && (queryString[filtering.FilterLogicUrlKey].ToString().ToLower() == "and" || queryString[filtering.FilterLogicUrlKey].ToString().ToLower() == "or"))
                {
                    logic = (string?)queryString[filtering.FilterLogicUrlKey];
                }

                GridColumn gridColumn = null;
                foreach (GridColumn dataColumn in grid.GetDataColumns())
                {
                    if (dataColumn.Key == text2)
                    {
                        gridColumn = dataColumn;
                        break;
                    }
                }

                if (gridColumn == null)
                {
                    throw new Exception("There is no column named " + text2);
                }

                if (gridColumn.IsUnbound)
                {
                    continue;
                }

                MatchCollection matchCollection = new Regex("[a-z]+\\(.*?\\)", RegexOptions.IgnoreCase).Matches((string?)queryString[key]);
                string[] array = new string[matchCollection.Count];
                int num2 = 0;
                foreach (Match item in matchCollection)
                {
                    array[num2] = item.Value;
                    num2++;
                }

                for (num2 = 0; num2 < array.Length; num2++)
                {
                    FilterExpression filterExpression = new FilterExpression();
                    filterExpression.Logic = logic;
                    filterExpression.Key = text2;
                    filterExpression.Column = gridColumn;
                    filterExpression.Condition = array[num2].Substring(0, array[num2].IndexOf("("));
                    if ((!array[num2].StartsWith("contains()") && !array[num2].StartsWith("equals()") && !array[num2].StartsWith("startsWith()") && !array[num2].StartsWith("endsWith()") && !array[num2].StartsWith("doesNotContain()") && !array[num2].StartsWith("doesNotEqual()")) || !(filterExpression.Condition != "null") || !(filterExpression.Condition != "notNull") || !(filterExpression.Condition != "empty") || !(filterExpression.Condition != "notEmpty") || !(filterExpression.Condition != "today") || !(filterExpression.Condition != "yesterday") || !(filterExpression.Condition != "thisMonth") || !(filterExpression.Condition != "nextMonth") || !(filterExpression.Condition != "lastMonth") || !(filterExpression.Condition != "thisYear") || !(filterExpression.Condition != "nextYear") || !(filterExpression.Condition != "lastYear"))
                    {
                        string source = array[num2].Substring(array[num2].IndexOf("("));
                        filterExpression.Expr = StringExtensions.ReplaceFirst(source, "(", "");
                        filterExpression.Expr = StringExtensions.ReplaceLast(filterExpression.Expr, ")", "");
                        list.Add(filterExpression);
                    }
                }
            }

            return list;
        }

        List<FilterExpression> GetFilterExpressions(IQueryCollection queryString, IGridModel grid)
        {
            List<FilterExpression> list = new List<FilterExpression>();
            var filtering = new GridFiltering();

            foreach (string key in queryString.Keys)
            {
                if (string.IsNullOrEmpty(key) || !key.StartsWith(filtering.FilterExprUrlKey + "("))
                {
                    continue;
                }

                string text = key.Substring(key.IndexOf("(")).Replace("(", "").Replace(")", "");
                int num = text.IndexOf(":", StringComparison.Ordinal);
                string text2 = ((num > -1) ? text.Substring(0, num) : text);
                string logic = "AND";
                if (queryString[filtering.FilterLogicUrlKey].ToString() != null && (queryString[filtering.FilterLogicUrlKey].ToString().ToLower() == "and" || queryString[filtering.FilterLogicUrlKey].ToString().ToLower() == "or"))
                {
                    logic = (string?)queryString[filtering.FilterLogicUrlKey];
                }

                GridColumn gridColumn = null;
                foreach (GridColumn dataColumn in grid.GetDataColumns())
                {
                    if (dataColumn.Key == text2)
                    {
                        gridColumn = dataColumn;
                        break;
                    }
                }

                if (gridColumn == null)
                {
                    throw new Exception("No grid column named " + text2);
                }

                if (gridColumn.IsUnbound)
                {
                    continue;
                }

                MatchCollection matchCollection = new Regex("[a-z]+\\(.*?\\)", RegexOptions.IgnoreCase).Matches((string?)queryString[key]);
                string[] array = new string[matchCollection.Count];
                int num2 = 0;
                foreach (Match item in matchCollection)
                {
                    array[num2] = item.Value;
                    num2++;
                }

                for (num2 = 0; num2 < array.Length; num2++)
                {
                    FilterExpression filterExpression = new FilterExpression();
                    filterExpression.Logic = logic;
                    filterExpression.Key = text2;
                    filterExpression.Column = gridColumn;
                    filterExpression.Condition = array[num2].Substring(0, array[num2].IndexOf("("));
                    if ((!array[num2].StartsWith("contains()") && !array[num2].StartsWith("equals()") && !array[num2].StartsWith("startsWith()") && !array[num2].StartsWith("endsWith()") && !array[num2].StartsWith("doesNotContain()") && !array[num2].StartsWith("doesNotEqual()")) || !(filterExpression.Condition != "null") || !(filterExpression.Condition != "notNull") || !(filterExpression.Condition != "empty") || !(filterExpression.Condition != "notEmpty") || !(filterExpression.Condition != "today") || !(filterExpression.Condition != "yesterday") || !(filterExpression.Condition != "thisMonth") || !(filterExpression.Condition != "nextMonth") || !(filterExpression.Condition != "lastMonth") || !(filterExpression.Condition != "thisYear") || !(filterExpression.Condition != "nextYear") || !(filterExpression.Condition != "lastYear"))
                    {
                        string source = array[num2].Substring(array[num2].IndexOf("("));
                        filterExpression.Expr = StringExtensions.ReplaceFirst(source, "(", "");
                        filterExpression.Expr = StringExtensions.ReplaceLast(filterExpression.Expr, ")", "");
                        list.Add(filterExpression);
                    }
                }
            }

            return list;
        }

        string[] CustomExtensions = new string[] { "StartsWithOneOf", "EndsWithOneOf", "ContainsOneOf", "EqualsOneOf", "NotContainsOneOf", "NotEqualsOneOf" };


        public IQueryable ApplyFiltering<T>(IQueryCollection queryString, IQueryable data, IGridModel grid)
        {
            var expressions = GetFilterExpressions(queryString, grid);
            var predicate = FilterExtensions.BuildPredicate<T>(expressions.Where(x => !CustomExtensions.Contains(x.Condition)).ToList(), false);
            var startingQueryEmpty = String.IsNullOrEmpty(predicate);

            var extensions = expressions.Where(x => CustomExtensions.Contains(x.Condition));

            if (extensions.Count() > 0)
            {
                var sb = new StringBuilder(predicate);

                foreach (var extension in extensions) 
                {
                    var val = WebUtility.UrlDecode(extension.Expr);
                    var parts = val.Split(',');

                    if (!startingQueryEmpty) sb.Append(" " + extension.Logic + " ");
                    startingQueryEmpty = false;
                    sb.Append("(");
                    for (var i=0; i<parts.Length; i++)
                    {
                        var item = parts[i];
                        if (extension.Condition == "StartsWithOneOf")
                        {
                            sb.Append(extension.Key + " != null AND " + extension.Key + ".toLower().StartsWith(\"" + item.ToLower() + "\")");
                            if (i < parts.Length - 1) sb.Append(" OR ");
                        }

                        if (extension.Condition == "EndsWithOneOf")
                        {
                            sb.Append(extension.Key + " != null AND " + extension.Key + ".toLower().EndsWith(\"" + item.ToLower() + "\")");
                            if (i < parts.Length - 1) sb.Append(" OR ");
                        }

                        if (extension.Condition == "ContainsOneOf")
                        {
                            sb.Append(extension.Key + " != null AND " + extension.Key + ".toLower().Contains(\"" + item.ToLower() + "\")");
                            if (i < parts.Length - 1) sb.Append(" OR ");
                        }

                        if (extension.Condition == "EqualsOneOf")
                        {
                            sb.Append(extension.Key + " != null AND " + extension.Key + ".toLower().Equals(\"" + item.ToLower() + "\")");
                            if (i < parts.Length - 1) sb.Append(" OR ");
                        }

                        if (extension.Condition == "NotContainsOneOf")
                        {
                            sb.Append(extension.Key + " != null AND !" + extension.Key + ".toLower().Contains(\"" + item.ToLower() + "\")");
                            if (i < parts.Length - 1) sb.Append(" AND ");
                        }

                        if (extension.Condition == "NotEqualsOneOf")
                        {
                            sb.Append(extension.Key + " != null AND !" + extension.Key + ".toLower().Equals(\"" + item.ToLower() + "\")");
                            if (i < parts.Length - 1) sb.Append(" AND ");
                        }
                    }
                    sb.Append(")");
                }

                predicate = sb.ToString();
            }

            if (string.IsNullOrEmpty(predicate))
            {
                return data;
            }

            return  data.Where(predicate);
        }

        IQueryable ApplyOrder(IQueryable source, string property, string methodName)
        {
            string[] props = property.Split('.');
            Type type = source.ElementType;
            ParameterExpression arg = Expression.Parameter(type, "x");
            Expression expr = arg;
            foreach (string prop in props)
            {
                var propName = prop.Split(':')[0];
                PropertyInfo pi = type.GetProperty(propName);
                expr = Expression.Property(expr, pi);
                type = pi.PropertyType;
            }
            Type delegateType = typeof(Func<,>).MakeGenericType(source.ElementType, type);
            LambdaExpression lambda = Expression.Lambda(delegateType, expr, arg);

            object result = typeof(Queryable).GetMethods().Single(
                    method => method.Name == methodName
                            && method.IsGenericMethodDefinition
                            && method.GetGenericArguments().Length == 2
                            && method.GetParameters().Length == 2)
                    .MakeGenericMethod(source.ElementType, type)
                    .Invoke(null, new object[] { source, lambda });
            return (IQueryable)result;
        }

        public IQueryable ApplySorting(IQueryCollection queryString, IQueryable data, IGridModel grid)
        {
            List<SortExpression> sortExpressions = new List<SortExpression>();
            SortingExtensions.BuildExpressions(sortExpressions, queryString, "sort", grid.Columns);

            string orderBy = "OrderBy";
            string orderByDescending = "OrderByDescending";
            foreach (SortExpression expr in sortExpressions)
            {

                data = ApplyOrder(data, expr.Key, expr.Mode == SortMode.Ascending ? orderBy : orderByDescending);
                orderBy = "ThenBy";
                orderByDescending = "ThenByDescending";
            }
            return data;
        }
    }
}
