﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Simple.OData.Client.Extensions;

namespace Simple.OData.Client
{
    class CommandWriter
    {
        private readonly ISchema _schema;

        public CommandWriter(ISchema schema)
        {
            _schema = schema;
        }

        public HttpCommand CreateGetCommand(string commandText, bool scalarResult = false)
        {
            return HttpCommand.Get(commandText, scalarResult);
        }

        public HttpCommand CreateInsertCommand(string commandText, IDictionary<string, object> entryData, CommandContent entryContent)
        {
            return HttpCommand.Post(commandText, entryData, entryContent.ToString());
        }

        public HttpCommand CreateUpdateCommand(string commandText, IDictionary<string, object> entryData, CommandContent entryContent, bool merge = false)
        {
            return new HttpCommand(merge ? RestVerbs.MERGE : RestVerbs.PUT, commandText, entryData, entryContent.ToString());
        }

        public HttpCommand CreateDeleteCommand(string commandText)
        {
            return HttpCommand.Delete(commandText);
        }

        public HttpCommand CreateLinkCommand(string collection, string associationName, int contentId, int associationId)
        {
            return CreateLinkCommand(collection, associationName, FormatLinkPath(contentId), FormatLinkPath(associationId));
        }

        public HttpCommand CreateLinkCommand(string collection, string associationName, string entryPath, string linkPath)
        {
            var linkEntry = CreateLinkElement(linkPath);
            var linkMethod = _schema.FindAssociation(collection, associationName).IsMultiple ?
                RestVerbs.POST :
                RestVerbs.PUT;

            var commandText = FormatLinkPath(entryPath, associationName);
            return new HttpCommand(linkMethod, commandText, null, linkEntry.ToString(), true);
        }

        public HttpCommand CreateUnlinkCommand(string collection, string associationName, string entryPath)
        {
            var commandText = FormatLinkPath(entryPath, associationName);
            return HttpCommand.Delete(commandText);
        }

        public CommandContent CreateEntry(string entityTypeName, IDictionary<string, object> row)
        {
            var entry = CreateEmptyEntryWithNamespaces();

            var resourceName = GetQualifiedResourceName(_schema.TypesNamespace, entityTypeName);
            entry.Element(null, "category").SetAttributeValue("term", resourceName);
            var properties = entry.Element(null, "content").Element("m", "properties");

            foreach (var prop in row)
            {
                EdmTypeSerializer.Write(properties, prop);
            }

            return new CommandContent(entry);
        }

        public void AddLink(CommandContent content, string collection, KeyValuePair<string, object> associatedData)
        {
            if (associatedData.Value == null)
                return;

            var association = _schema.FindAssociation(collection, associatedData.Key);
            var associatedKeyValues = GetLinkedEntryKeyValues(association.ReferenceTableName, associatedData);
            if (associatedKeyValues != null)
            {
                AddDataLink(content.Entry, association.ActualName, association.ReferenceTableName, associatedKeyValues);
            }
        }

        private XElement CreateLinkElement(string link)
        {
            var entry = CreateEmptyMetadataWithNamespaces();

            entry.SetValue(link);

            return entry;
        }

        private string FormatLinkPath(int contentId)
        {
            return "$" + contentId;
        }

        private string FormatLinkPath(string entryPath, string linkName)
        {
            return string.Format("{0}/$links/{1}", entryPath, linkName);
        }

        private void AddDataLink(XElement container, string associationName, string linkedEntityName, IEnumerable<object> linkedEntityKeyValues)
        {
            var entry = XElement.Parse(Properties.Resources.DataServicesAtomEntryXml).Element(null, "link");
            var rel = entry.Attribute("rel");
            rel.SetValue(rel.Value + associationName);
            entry.SetAttributeValue("title", associationName);
            entry.SetAttributeValue("href", string.Format("{0}({1})",
                linkedEntityName,
                string.Join(",", linkedEntityKeyValues.Select(new ValueFormatter().FormatContentValue))));
            container.Add(entry);
        }

        private IEnumerable<object> GetLinkedEntryKeyValues(string collection, KeyValuePair<string, object> entryData)
        {
            var entryProperties = GetLinkedEntryProperties(entryData.Value);
            var associatedKeyNames = _schema.FindConcreteTable(collection).GetKeyNames();
            var associatedKeyValues = new object[associatedKeyNames.Count()];
            for (int index = 0; index < associatedKeyNames.Count(); index++)
            {
                bool ok = entryProperties.TryGetValue(associatedKeyNames[index], out associatedKeyValues[index]);
                if (!ok)
                    return null;
            }
            return associatedKeyValues;
        }

        private IDictionary<string, object> GetLinkedEntryProperties(object entryData)
        {
            if (entryData is ODataEntry)
                return (Dictionary<string, object>)(entryData as ODataEntry);

            var entryProperties = entryData as IDictionary<string, object>;
            if (entryProperties == null)
            {
                entryProperties = new Dictionary<string, object>();
                var entryType = entryData.GetType();
                foreach (var entryProperty in entryType.GetDeclaredProperties())
                {
                    entryProperties.Add(
                        entryProperty.Name,
                        entryType.GetDeclaredProperty(entryProperty.Name).GetValue(entryData, null));
                }
            }
            return entryProperties;
        }

        private XElement CreateEmptyEntryWithNamespaces()
        {
            var entry = XElement.Parse(Properties.Resources.DataServicesAtomEntryXml);
            entry.Element(null, "updated").SetValue(DateTime.UtcNow.ToIso8601String());
            entry.Element(null, "link").Remove();
            return entry;
        }

        private XElement CreateEmptyMetadataWithNamespaces()
        {
            var entry = XElement.Parse(Properties.Resources.DataServicesMetadataEntryXml);
            return entry;
        }

        private string GetQualifiedResourceName(string namespaceName, string collectionName)
        {
            return string.Join(".", namespaceName, collectionName);
        }
    }
}
