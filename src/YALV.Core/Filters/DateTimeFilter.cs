using System;
using System.Diagnostics;

namespace YALV.Core.Filters
{
    [DebuggerDisplay(DebuggerDisplayString)]
    class DateTimeFilter : FilterBase<DateTime>
    {
        public DateTimeFilter(string propertyName)
            : base(propertyName)
        {
        }

        public DateTimeRelation Relation { get; set; }

        protected override bool Match(DateTime currentValue)
        {
            switch (Relation)
            {
                case DateTimeRelation.Before:
                    return IsInclusive ? currentValue <= Value : currentValue < Value;
                case DateTimeRelation.After:
                    return IsInclusive ? currentValue >= Value : currentValue > Value;
                case DateTimeRelation.Between:
                    return IsInclusive ? Value <= currentValue && currentValue <= Value2 : Value < currentValue && currentValue < Value2;
            }
            throw new NotSupportedException();
        }
    }

    public enum DateTimeRelation
    {
        Before,
        After,
        Between
    }
}