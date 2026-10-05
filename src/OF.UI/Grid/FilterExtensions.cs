namespace OF.UI.Grid
{
    using Infragistics.Web.Mvc;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class GridColumnExtensions
    {
        public static bool Nullable<T>(this GridColumn col)
        {
            var modelType = typeof(T).GetProperty(col.Key);
            if (modelType == null)
            {
                return false;
            }
            else
            {
                return System.Nullable.GetUnderlyingType(modelType.PropertyType) != null;
            }
        }
    }

    //
    // Summary:
    //     filtering extensions
    public static class FilterExtensions
    {
        //
        // Summary:
        //     converts a list of filtering expressions into a LINQ predicate
        //
        // Parameters:
        //   expressions:
        //
        //   isCaseSensitive:
        public static string BuildPredicate<T>(List<FilterExpression> expressions, bool? isCaseSensitive)
        {
            StringBuilder stringBuilder = new StringBuilder();
            int num = 0;
            DateTime dateTime = DateTime.Now;
            DateTime now = DateTime.Now;
            int year = now.Year;
            int month = now.Month;
            int day = now.Day;
            foreach (FilterExpression expression in expressions)
            {
                if (num != 0 && num <= expressions.Count - 1)
                {
                    stringBuilder.Append(expression.Logic.ToLower() == "AND".ToLower() ? " AND " : " OR ");
                }

                string stringToUnescape = isCaseSensitive == true ? expression.Expr : expression.Expr.ToLower();
                stringToUnescape = Uri.UnescapeDataString(stringToUnescape);
                if (stringToUnescape.Contains("\""))
                {
                    stringToUnescape = stringToUnescape.Replace("\"", "\"\"");
                }

                if ((expression.Column.DataType == "date" || expression.Column.DataType == "time") && expression.Condition.ToLower() != "empty" && expression.Condition.ToLower() != "notempty")
                {
                    if (stringToUnescape == "null")
                    {
                        if (expression.Column.Nullable<T>())
                        {
                            return stringBuilder.Append(expression.Key + " != null AND " + expression.Key + ".Value == null").ToString();
                        }

                        return stringBuilder.Append(expression.Key + " == null").ToString();
                    }

                    if (!string.IsNullOrEmpty(expression.Expr))
                    {
                        try
                        {
                            dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(double.Parse(expression.Expr)).ToUniversalTime();
                        }
                        catch
                        {
                            if (expression.Column.Nullable<T>())
                            {
                                return stringBuilder.Append(expression.Key + " != null AND " + expression.Key + ".Value == DateTime.MaxValue").ToString();
                            }

                            return stringBuilder.Append(expression.Key + " == DateTime.MaxValue").ToString();
                        }
                    }
                }

                string text = expression.Condition.ToLower();
                if (text == "StartsWith".ToLower())
                {
                    stringBuilder.Append(expression.Key + " != null AND " + expression.Key + (isCaseSensitive != true ? ".toLower()" : "") + ".StartsWith(\"" + stringToUnescape + "\")");
                }
                else if (text == "Contains".ToLower())
                {
                    stringBuilder.Append(expression.Key + " != null AND " + expression.Key + (isCaseSensitive != true ? ".toLower()" : "") + ".Contains(\"" + stringToUnescape + "\")");
                }
                else if (text == "EndsWith".ToLower())
                {
                    stringBuilder.Append(expression.Key + " != null AND " + expression.Key + (isCaseSensitive != true ? ".toLower()" : "") + ".EndsWith(\"" + stringToUnescape + "\")");
                }
                else if (text == "Equals".ToLower())
                {
                    if (expression.Column.DataType == "numeric" || expression.Column.DataType == "number" || expression.Column.DataType == "bool")
                    {
                        stringBuilder.Append(expression.Key + " == " + stringToUnescape);
                    }
                    else
                    {
                        stringBuilder.Append(expression.Key + " != null AND " + expression.Key + (isCaseSensitive != true ? ".toLower()" : "") + " == \"" + stringToUnescape + "\"");
                    }
                }
                else if (text == "DoesNotEqual".ToLower())
                {
                    if (expression.Column.DataType == "numeric" || expression.Column.DataType == "number" || expression.Column.DataType == "bool")
                    {
                        stringBuilder.Append("(" + expression.Key + " == null OR " + expression.Key + " != " + stringToUnescape + ")");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + " == null OR " + expression.Key + " == \"\" OR " + expression.Key + (isCaseSensitive != true ? ".toLower()" : "") + " != \"" + stringToUnescape + "\")");
                    }
                }
                else if (text == "DoesNotContain".ToLower())
                {
                    stringBuilder.Append("(" + expression.Key + " == null OR " + expression.Key + " == \"\" OR ! " + expression.Key + (isCaseSensitive != true ? ".toLower()" : "") + ".Contains(\"" + stringToUnescape + "\"))");
                }
                else if (text == "LessThan".ToLower())
                {
                    stringBuilder.Append(expression.Key + " < " + stringToUnescape);
                }
                else if (text == "GreaterThan".ToLower())
                {
                    stringBuilder.Append(expression.Key + " > " + stringToUnescape);
                }
                else if (text == "LessThanOrEqualTo".ToLower())
                {
                    stringBuilder.Append(expression.Key + " <= " + stringToUnescape);
                }
                else if (text == "GreaterThanOrEqualTo".ToLower())
                {
                    stringBuilder.Append(expression.Key + " >= " + stringToUnescape);
                }
                else if (text == "NotNull".ToLower())
                {
                    stringBuilder.Append(expression.Key + " != null");
                }
                else if (text == "Null".ToLower())
                {
                    stringBuilder.Append(expression.Key + " == null");
                }
                else if (text == "Empty".ToLower())
                {
                    if (expression.Column.DataType == "number" || expression.Column.DataType == "date" || expression.Column.DataType == "bool")
                    {
                        stringBuilder.Append(expression.Key + " == null");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + " == null OR " + expression.Key + " == \"\")");
                    }
                }
                else if (text == "NotEmpty".ToLower())
                {
                    if (expression.Column.DataType == "number" || expression.Column.DataType == "date" || expression.Column.DataType == "bool")
                    {
                        stringBuilder.Append(expression.Key + " != null");
                    }
                    else
                    {
                        stringBuilder.Append(expression.Key + " != \"\" AND " + expression.Key + " != null");
                    }
                }
                else if (text == "True".ToLower())
                {
                    stringBuilder.Append(expression.Key + " == true");
                }
                else if (text == "False".ToLower())
                {
                    stringBuilder.Append(expression.Key + " == false");
                }
                else if (text == "On".ToLower() && expression.Column.DataType == "date")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append("(" + expression.Key + " != null AND " + expression.Key + ".Value.Day == " + dateTime.Day + " AND " + expression.Key + ".Value.Year == " + dateTime.Year + " AND " + expression.Key + ".Value.Month == " + dateTime.Month + ")");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + " != null AND " + expression.Key + ".Day == " + dateTime.Day + " AND " + expression.Key + ".Year == " + dateTime.Year + " AND " + expression.Key + ".Month == " + dateTime.Month + ")");
                    }
                }
                else if (text == "NotOn".ToLower() && expression.Column.DataType == "date")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append("(" + expression.Key + " == null OR !(" + expression.Key + ".Value.Day == " + dateTime.Day + " AND " + expression.Key + ".Value.Year == " + dateTime.Year + " AND " + expression.Key + ".Value.Month == " + dateTime.Month + "))");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + " == null OR !(" + expression.Key + ".Day == " + dateTime.Day + " AND " + expression.Key + ".Year == " + dateTime.Year + " AND " + expression.Key + ".Month == " + dateTime.Month + "))");
                    }
                }
                else if (text == "After".ToLower() && expression.Column.DataType == "date")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND ((" + expression.Key + ".Value.Year > " + dateTime.Year + " OR (" + expression.Key + ".Value.Month > " + dateTime.Month + " AND " + expression.Key + ".Value.Year == " + dateTime.Year + ") OR (" + expression.Key + ".Value.Day > " + dateTime.Day + " AND " + expression.Key + ".Value.Year == " + dateTime.Year + " AND " + expression.Key + ".Value.Month == " + dateTime.Month + ")))");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Year > " + dateTime.Year + " OR (" + expression.Key + ".Month > " + dateTime.Month + " AND " + expression.Key + ".Year == " + dateTime.Year + ") OR (" + expression.Key + ".Day > " + dateTime.Day + " AND " + expression.Key + ".Year == " + dateTime.Year + " AND " + expression.Key + ".Month == " + dateTime.Month + "))");
                    }
                }
                else if (text == "Before".ToLower() && expression.Column.DataType == "date")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND ((" + expression.Key + ".Value.Year < " + dateTime.Year + " OR (" + expression.Key + ".Value.Month < " + dateTime.Month + " AND " + expression.Key + ".Value.Year == " + dateTime.Year + ") OR (" + expression.Key + ".Value.Day < " + dateTime.Day + " AND " + expression.Key + ".Value.Year == " + dateTime.Year + " AND " + expression.Key + ".Value.Month == " + dateTime.Month + ")))");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Year < " + dateTime.Year + " OR (" + expression.Key + ".Month < " + dateTime.Month + " AND " + expression.Key + ".Year == " + dateTime.Year + ") OR (" + expression.Key + ".Day < " + dateTime.Day + " AND " + expression.Key + ".Year == " + dateTime.Year + " AND " + expression.Key + ".Month == " + dateTime.Month + "))");
                    }
                }
                else if (text == "Today".ToLower() && expression.Column.DataType == "date")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND (" + expression.Key + ".Value.Day == " + day + " AND " + expression.Key + ".Value.Year == " + year + " AND " + expression.Key + ".Value.Month == " + month + ")");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Day == " + day + " AND " + expression.Key + ".Year == " + year + " AND " + expression.Key + ".Month == " + month + ")");
                    }
                }
                else if (text == "Yesterday".ToLower() && expression.Column.DataType == "date")
                {
                    DateTime dateTime2 = now.AddDays(-1.0);
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND (" + expression.Key + ".Value.Day == " + dateTime2.Day + " AND " + expression.Key + ".Value.Year == " + dateTime2.Year + " AND " + expression.Key + ".Value.Month == " + dateTime2.Month + ")");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Day == " + dateTime2.Day + " AND " + expression.Key + ".Year == " + dateTime2.Year + " AND " + expression.Key + ".Month == " + dateTime2.Month + ")");
                    }
                }
                else if (text == "ThisMonth".ToLower() && expression.Column.DataType == "date")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND (" + expression.Key + ".Value.Year == " + year + " AND " + expression.Key + ".Value.Month == " + month + ")");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Year == " + year + " AND " + expression.Key + ".Month == " + month + ")");
                    }
                }
                else if (text == "LastMonth".ToLower() && expression.Column.DataType == "date")
                {
                    if (month == 1)
                    {
                        if (expression.Column.Nullable<T>())
                        {
                            stringBuilder.Append(expression.Key + " != null AND (" + expression.Key + ".Value.Year == " + (year - 1) + " AND " + expression.Key + ".Value.Month ==  12)");
                        }
                        else
                        {
                            stringBuilder.Append("(" + expression.Key + ".Year == " + (year - 1) + " AND " + expression.Key + ".Month == 12)");
                        }
                    }
                    else if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND (" + expression.Key + ".Value.Year == " + year + " AND " + expression.Key + ".Value.Month == " + (month - 1) + ")");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Year == " + year + " AND " + expression.Key + ".Month == " + (month - 1) + ")");
                    }
                }
                else if (text == "NextMonth".ToLower() && expression.Column.DataType == "date")
                {
                    if (month == 12)
                    {
                        if (expression.Column.Nullable<T>())
                        {
                            stringBuilder.Append(expression.Key + " != null AND (" + expression.Key + ".Value.Year == " + (year + 1) + " AND " + expression.Key + ".Value.Month == 1)");
                        }
                        else
                        {
                            stringBuilder.Append("(" + expression.Key + ".Year == " + (year + 1) + " AND " + expression.Key + ".Month == 1)");
                        }
                    }
                    else if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND (" + expression.Key + ".Value.Year == " + year + " AND " + expression.Key + ".Value.Month == " + (month + 1) + ")");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Year == " + year + " AND " + expression.Key + ".Month == " + (month + 1) + ")");
                    }
                }
                else if (text == "ThisYear".ToLower() && expression.Column.DataType == "date")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND " + expression.Key + ".Value.Year == " + year);
                    }
                    else
                    {
                        stringBuilder.Append(expression.Key + ".Year == " + year);
                    }
                }
                else if (text == "LastYear".ToLower() && expression.Column.DataType == "date")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND " + expression.Key + ".Value.Year == " + (year - 1));
                    }
                    else
                    {
                        stringBuilder.Append(expression.Key + ".Year == " + (year - 1));
                    }
                }
                else if (text == "NextYear".ToLower() && expression.Column.DataType == "date")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND " + expression.Key + ".Value.Year == " + (year + 1));
                    }
                    else
                    {
                        stringBuilder.Append(expression.Key + ".Year == " + (year + 1));
                    }
                }
                else if (text == "At".ToLower() && expression.Column.DataType == "time")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append("(" + expression.Key + " != null AND " + expression.Key + ".Value.Hour == " + dateTime.Hour + " AND " + expression.Key + ".Value.Minute == " + dateTime.Minute + ")");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + " != null AND " + expression.Key + ".Hour == " + dateTime.Hour + " AND " + expression.Key + ".Minute == " + dateTime.Minute + ")");
                    }
                }
                else if (text == "NotAt".ToLower() && expression.Column.DataType == "time")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append("(" + expression.Key + " != null AND (" + expression.Key + ".Value.Hour != " + dateTime.Hour + " OR " + expression.Key + ".Value.Minute != " + dateTime.Minute + "))");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + " != null AND (" + expression.Key + ".Hour != " + dateTime.Hour + " OR " + expression.Key + ".Minute != " + dateTime.Minute + "))");
                    }
                }
                else if (text == "Before".ToLower() && expression.Column.DataType == "time")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND (" + expression.Key + ".Value.Hour < " + dateTime.Hour + " OR (" + expression.Key + ".Value.Minute < " + dateTime.Minute + " AND " + expression.Key + ".Value.Hour == " + dateTime.Hour + "))");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Hour < " + dateTime.Hour + " OR (" + expression.Key + ".Minute < " + dateTime.Minute + " AND " + expression.Key + ".Hour == " + dateTime.Hour + "))");
                    }
                }
                else if (text == "After".ToLower() && expression.Column.DataType == "time")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND (" + expression.Key + ".Value.Hour > " + dateTime.Hour + " OR (" + expression.Key + ".Value.Minute > " + dateTime.Minute + " AND " + expression.Key + ".Value.Hour == " + dateTime.Hour + "))");
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Hour > " + dateTime.Hour + " OR (" + expression.Key + ".Minute > " + dateTime.Minute + " AND " + expression.Key + ".Hour == " + dateTime.Hour + "))");
                    }
                }
                else if (text == "AtBefore".ToLower() && expression.Column.DataType == "time")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND " + expression.Key + ".Value.Minute <= " + dateTime.Minute + " AND " + expression.Key + ".Value.Hour <= " + dateTime.Hour);
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Minute <= " + dateTime.Minute + " AND " + expression.Key + ".Hour <= " + dateTime.Hour + ")");
                    }
                }
                else if (text == "AtAfter".ToLower() && expression.Column.DataType == "time")
                {
                    if (expression.Column.Nullable<T>())
                    {
                        stringBuilder.Append(expression.Key + " != null AND " + expression.Key + ".Value.Minute >= " + dateTime.Minute + " AND " + expression.Key + ".Value.Hour >= " + dateTime.Hour);
                    }
                    else
                    {
                        stringBuilder.Append("(" + expression.Key + ".Minute >= " + dateTime.Minute + " AND " + expression.Key + ".Hour >= " + dateTime.Hour + ")");
                    }
                }
                else
                {
                    if (expression.Column.DataType == "date")
                    {
                        throw new Exception("Invalid date filter.");
                    }

                    if (expression.Column.DataType == "time")
                    {
                        throw new Exception("Invalid time filter");
                    }
                }

                num++;
            }

            return stringBuilder.ToString();
        }
    }
}
