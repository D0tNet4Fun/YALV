using System.Linq;

namespace YALV.Core.Domain
{
    public class ColumnVisibilitySettings
    {
        public bool ShowId { get; set; }

        public bool ShowTimestamp { get; set; }

        public bool ShowThread { get; set; }

        public bool ShowLevel { get; set; }

        public bool ShowLogger { get; set; }

        public bool ShowMessage { get; set; }

        public bool ShowApplication { get; set; }

        public bool ShowUserName { get; set; }

        public bool ShowMachineName { get; set; }

        public bool ShowHostName { get; set; }

        public bool ShowClass { get; set; }

        public bool ShowMethod { get; set; }

        public void ShowAll()
        {
            var properties = GetType().GetProperties().Where(p => p.PropertyType == typeof(bool) && p.Name.StartsWith("Show"));
            foreach (var propertyInfo in properties)
            {
                propertyInfo.SetValue(this, true, null);
            }
        }
    }
}