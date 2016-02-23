using System.Globalization;
using YALV.Core.Domain;

namespace YALV.Core.Filters
{
    public interface IFilter
    {
        FilterMode Mode { get; set; }
        CultureInfo Culture { get; set; }

        FilterResult Apply(LogItem logItem);
    }
}