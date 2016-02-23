using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using YALV.Core.Domain;

namespace YALV.Core.Filters
{
    internal abstract class FilterBase : IFilter
    {
        public const string DebuggerDisplayString = "{GetType().Name,nq}: " +
                                                    "If {Property.Name, nq} {Relation} {Value} {Value2 ?? string.Empty,nq} then " +
                                                    "{Mode} {(Mode==FilterMode.Include && !IsAdditive) ? \"(not additive)\" : string.Empty,nq}";

        private static readonly IDictionary<string, PropertyInfo> LogItemProperties = typeof(LogItem).GetProperties().ToDictionary(p => p.Name);
        private readonly PropertyInfo _property;

        protected FilterBase(string propertyName)
        {
            if (!LogItemProperties.TryGetValue(propertyName, out _property))
            {
                throw new ArgumentException(string.Format("Property {0} does not exist on type {1}", propertyName, typeof(LogItem)));
            }
            Culture = CultureInfo.InvariantCulture;
        }

        public FilterMode Mode { get; set; }
        public CultureInfo Culture { get; set; }

        protected bool IsInclusive
        {
            get { return Mode == FilterMode.Include; }
        }

        public PropertyInfo Property
        {
            get { return _property; }
        }

        public FilterResult Apply(LogItem logItem)
        {
            var match = Match(Property, logItem);
            switch (Mode)
            {
                case FilterMode.Exclude:
                    return match ? FilterResult.Exclude : FilterResult.Ignore;
                case FilterMode.Include:
                    return match ? FilterResult.Include : FilterResult.CanExclude;
                default:
                    throw new NotSupportedException();
            }
        }

        protected abstract bool Match(PropertyInfo property, LogItem logItem);
    }

    internal abstract class FilterBase<T> : FilterBase
    {
        protected FilterBase(string propertyName)
            : base(propertyName)
        {
        }

        public T Value { get; set; }
        public T Value2 { get; set; }

        protected override sealed bool Match(PropertyInfo property, LogItem logItem)
        {
            T currentValue;
            try
            {
                currentValue = (T)property.GetValue(logItem, null);
            }
            catch (InvalidCastException)
            {
                throw new InvalidOperationException(string.Format("Filter target type does not match property {0} type. Expected: {1}, actual: {2}",
                    property.Name, typeof(T), property.PropertyType));
            }
            return Match(currentValue);
        }

        protected abstract bool Match(T currentValue);
    }
}