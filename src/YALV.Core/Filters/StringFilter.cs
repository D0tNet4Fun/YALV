using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace YALV.Core.Filters
{
    [DebuggerDisplay(DebuggerDisplayString)]
    class StringFilter : FilterBase<string>
    {
        public StringFilter(string propertyName)
            : base(propertyName)
        {
        }

        public StringRelation Relation { get; set; }

        protected override bool Match(string currentValue)
        {
            switch (Relation)
            {
                case StringRelation.Is:
                    return string.Compare(currentValue, Value, true, Culture) == 0;
                case StringRelation.IsNot:
                    return string.Compare(currentValue, Value, true, Culture) != 0;
                case StringRelation.BeginsWith:
                    return currentValue.StartsWith(Value, true, Culture);
                case StringRelation.EndsWith:
                    return currentValue.EndsWith(Value, true, Culture);
                case StringRelation.Contains:
                    return Culture.CompareInfo.IndexOf(currentValue, Value, CompareOptions.IgnoreCase) >= 0;
                case StringRelation.Excludes:
                    return Culture.CompareInfo.IndexOf(currentValue, Value, CompareOptions.IgnoreCase) < 0;
                case StringRelation.MatchesRegex:
                    return new Regex(Value, RegexOptions.IgnoreCase).IsMatch(currentValue);
            }
            throw new NotSupportedException();
        }
    }

    public enum StringRelation
    {
        Is,
        IsNot,
        BeginsWith,
        EndsWith,
        Contains,
        Excludes,
        MatchesRegex
    }
}