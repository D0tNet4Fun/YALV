using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using YALV.Core.Domain;
using YALV.Core.Filters;
using YALV.Core.Providers;

namespace YALV.Core
{
    public static class DataService
    {
        private static readonly Dictionary<string, PropertyInfo> LogItemProperties = typeof(LogItem).GetProperties().ToDictionary(p => p.Name);

        public static void SaveFolderFile(IList<PathItem> folders, string path)
        {
            FileStream fileStream = null;
            StreamWriter streamWriter = null;
            try
            {
                if (folders != null)
                {
                    fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    streamWriter = new StreamWriter(fileStream);
                    foreach (PathItem item in folders)
                    {
                        string line = String.Format("<folder name=\"{0}\" path=\"{1}\" />", item.Name, item.Path);
                        streamWriter.WriteLine(line);
                    }
                    streamWriter.Close();
                    streamWriter = null;
                    fileStream.Close();
                    fileStream = null;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error saving Favorites list [{0}]:\r\n{1}\r\n{2}", path, ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                if (streamWriter != null)
                    streamWriter.Close();
                if (fileStream != null)
                    fileStream.Close();
            }

        }

        public static IList<PathItem> ParseFolderFile(string path)
        {
            FileStream fileStream = null;
            StreamReader streamReader = null;
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                    return null;

                fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                streamReader = new StreamReader(fileStream, true);
                string sBuffer = String.Format("<root>{0}</root>", streamReader.ReadToEnd());
                streamReader.Close();
                streamReader = null;
                fileStream.Close();
                fileStream = null;

                var stringReader = new StringReader(sBuffer);
                var xmlTextReader = new XmlTextReader(stringReader) { Namespaces = false };

                IList<PathItem> result = new List<PathItem>();
                while (xmlTextReader.Read())
                {
                    if ((xmlTextReader.NodeType != XmlNodeType.Element) || (xmlTextReader.Name != "folder"))
                        continue;

                    PathItem item = new PathItem(xmlTextReader.GetAttribute("name"), xmlTextReader.GetAttribute("path"));
                    result.Add(item);
                }
                return result;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error parsing Favorites list [{0}]:\r\n{1}\r\n{2}", path, ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                if (streamReader != null)
                    streamReader.Close();
                if (fileStream != null)
                    fileStream.Close();
            }
        }

        public static IEnumerable<LogItem> ParseLogFile(string path)
        {
            try
            {
                AbstractEntriesProvider provider = EntriesProviderFactory.GetProvider();
                return provider.GetEntries(path);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error parsing log file [{0}]:\r\n{1}\r\n{2}", path, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public static ColumnSettings[] ParseSettings(string path)
        {
            if (!File.Exists(path))
            {
                return new ColumnSettings[0];
            }

            var xml = XDocument.Load(path);
            var columnsElement = xml.Descendants("columns").SingleOrDefault();
            if (columnsElement == null)
            {
                Trace.TraceWarning("Column settings not found in file {0}", path);
                return new ColumnSettings[0];
            }

            var columnElements = columnsElement.Descendants("column");
            try
            {
                var array = columnElements.Select(x =>
                    new ColumnSettings
                    {
                        Id = x.Attribute("id").Value,
                        DisplayIndex = int.Parse(x.Attribute("displayIndex").Value),
                        Width = double.Parse(x.Attribute("width").Value),
                        Visible = bool.Parse(x.Attribute("visible").Value),
                    }).ToArray();
                return array;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error parsing column settings from file {0}:\r\n{1}", path, ex.ToString());
                throw;
            }
        }

        public static void SaveSettings(ColumnSettings[] columnSettingsCollection, string path)
        {
            var xml = File.Exists(path) ? XDocument.Load(path) : new XDocument();

            var columnsElement = xml.Descendants("columns").SingleOrDefault();
            if (columnsElement == null)
            {
                if (xml.Root == null)
                {
                    var root = new XElement("settings");
                    xml.Add(root);
                }
                columnsElement = new XElement("columns");
                xml.Root.Add(columnsElement);
            }

            try
            {
                var columnElements = from columnSettings in columnSettingsCollection
                                     select new XElement("column",
                                         new XAttribute("id", columnSettings.Id),
                                         new XAttribute("displayIndex", columnSettings.DisplayIndex),
                                         new XAttribute("width", columnSettings.Width),
                                         new XAttribute("visible", columnSettings.Visible));
                columnsElement.ReplaceAll(columnElements);
                xml.Save(path);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error saving column settings in file {0}:\r\n{1}", path, ex.ToString());
                throw;
            }
        }

        public static IEnumerable<IFilter> ParseFilters(string path)
        {
            if (!File.Exists(path))
            {
                yield break;
            }

            using (var fileStream = File.OpenRead(path))
            using (var reader = new XmlTextReader(fileStream))
            {
                while (reader.ReadToFollowing("filter"))
                {
                    IFilter filter;
                    try
                    {
                        filter = ParseFilter(reader);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(string.Format("Error parsing filter: {0}", ex));
                        continue;
                    }
                    yield return filter;
                }
            }
        }

        private static IFilter ParseFilter(XmlReader reader)
        {
            var propertyName = reader.GetAttribute("property");
            PropertyInfo property;
            if (!LogItemProperties.TryGetValue(propertyName, out property))
            {
                throw new MissingMemberException(typeof(LogItem).FullName, propertyName);
            }

            var relationString = reader.GetAttribute("relation");

            IFilter filter;
            if (property.PropertyType == typeof(string))
            {
                var stringFilter = new StringFilter(propertyName);
                StringRelation relation;
                if (Enum.TryParse(relationString, true, out relation))
                {
                    stringFilter.Relation = relation;
                }
                if (reader.MoveToAttribute("value"))
                {
                    stringFilter.Value = reader.Value;
                }
                filter = stringFilter;
            }
            else if (property.PropertyType == typeof(DateTime))
            {
                var dateTimeFilter = new DateTimeFilter(propertyName);
                DateTimeRelation relation;
                if (Enum.TryParse(relationString, true, out relation))
                {
                    dateTimeFilter.Relation = relation;
                }
                if (reader.MoveToAttribute("value"))
                {
                    dateTimeFilter.Value = DateTime.Parse(reader.Value);
                }
                if (reader.MoveToAttribute("value2"))
                {
                    dateTimeFilter.Value2 = DateTime.Parse(reader.Value);
                }
                filter = dateTimeFilter;
            }
            else
            {
                throw new NotSupportedException(string.Format("Cannot filter property {0} of type {1}", propertyName, property.PropertyType.FullName));
            }

            FilterMode mode;
            if (Enum.TryParse(reader.GetAttribute("mode"), true, out mode))
            {
                filter.Mode = mode;
            }
            return filter;
        }
    }
}