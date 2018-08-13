using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Job.RabbitMqToBlobConverter.Core.Domain;
using Lykke.Job.RabbitMqToBlobConverter.Core.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lykke.Job.RabbitMqToBlobConverter.Services
{
    public class MessageConverter : IMessageConverter
    {
        private readonly ITypeInfo _typeInfo;
        private readonly ILog _log;

        public MessageConverter(ITypeInfo typeInfo, ILogFactory logFactory)
        {
            _typeInfo = typeInfo;
            _log = logFactory.CreateLog(this);
        }

        public Dictionary<string, List<string>> Convert(object message)
        {
            var result = new Dictionary<string, List<string>>();
            ProcessTypeItem(
                message,
                null,
                null,
                result);
            return result;
        }

        private void ProcessTypeItem(
            object obj,
            Type parentType,
            string parentId,
            Dictionary<string, List<string>> data)
        {
            if (obj is IEnumerable items)
            {
                foreach (var item in items)
                {
                    AddValueLevel(
                        item,
                        parentType,
                        parentId,
                        data);
                }
            }
            else
            {
                AddValueLevel(
                    obj,
                    parentType,
                    parentId,
                    data);
            }
        }

        private void AddValueLevel(
            object obj,
            Type parentType,
            string parentId,
            Dictionary<string, List<string>> data)
        {
            if (obj == null)
                return;

            Type type = obj.GetType();
            string typeName = type.Name;

            var idPropertyName = _typeInfo.GetIdPropertyName(typeName);
            var typeData = _typeInfo.PropertiesMap[type];

            string id = null;
            if (typeData.ValueProperties.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                bool hasParentIdProperty = false;
                for (int i = 0; i < typeData.ValueProperties.Count; ++i)
                {
                    var valueProperty = typeData.ValueProperties[i];
                    object value = valueProperty.GetValue(obj);
                    if (valueProperty.Name == _typeInfo.IdPropertyName || valueProperty.Name == idPropertyName)
                    {
                        if (value == null)
                            _log.Warning($"Id property {valueProperty.Name} of {typeName} is null", context: obj);
                        id = value?.ToString() ?? string.Empty;
                    }
                    else
                    {
                        string strValue = string.Empty;
                        if (value != null)
                        {
                            if (valueProperty.PropertyType == typeof(DateTime))
                            {
                                strValue = DateTimeConverter.Convert((DateTime)value);
                            }
                            else if (valueProperty.PropertyType == typeof(DateTime?))
                            {
                                strValue = DateTimeConverter.Convert(((DateTime?)value).Value);
                            }
                            else if (StructureBuilder.GenericCollectionTypes.Any(t => t == valueProperty.PropertyType))
                            {
                                var strValues = new List<string>();
                                var enumerable = (IEnumerable)value;
                                foreach (var item in enumerable)
                                {
                                    strValues.Add(item.ToString() ?? string.Empty);
                                }
                                strValue = string.Join(';', strValues);
                            }
                            else
                            {
                                strValue = value.ToString();
                            }
                        }
                        if (typeData.ParentIdPropertyName != null && valueProperty.Name == typeData.ParentIdPropertyName)
                            hasParentIdProperty = true;
                        if (sb.Length > 0 || i > 0 && (i != 1 || id == null))
                            sb.Append(',');
                        sb.Append(strValue);
                    }
                }

                if (parentId != null && !hasParentIdProperty)
                {
                    sb.Insert(0, $"{parentId},");
                }
                else if (sb.Length > 0 && parentType != null && _typeInfo.PropertiesMap[parentType].ValueProperties.Count > 0)
                {
                    _log.Warning(
                        $"Message of type {parentType.Name} doesn't have any identificators that can be used to make relations to its children", context: obj);
                    sb.Insert(0, ",");
                }

                if (id != null)
                    sb.Insert(0, $"{id},");

                if (data.ContainsKey(typeName))
                    data[typeName].Add(sb.ToString());
                else
                    data.Add(typeName, new List<string> { sb.ToString() });
            }

            if (typeData.OneChildrenProperties.Count == 0 && typeData.ManyChildrenProperties.Count == 0)
                return;

            if (id == null)
                id = GetIdFromChildren(obj, typeData);
            if (id == null)
                id = parentId;
            if (id == null && typeData.RelationProperty != null)
                id = typeData.RelationProperty.GetValue(obj)?.ToString();

            foreach (var childProperty in typeData.OneChildrenProperties)
            {
                object value = childProperty.GetValue(obj);
                if (value == null)
                    continue;

                ProcessTypeItem(
                    value,
                    type,
                    childProperty == typeData.ChildWithIdProperty ? null : id,
                    data);
            }

            foreach (var childrenProperty in typeData.ManyChildrenProperties)
            {
                object value = childrenProperty.GetValue(obj);
                if (value == null)
                    continue;

                var items = value as IEnumerable;
                if (items == null)
                    throw new InvalidOperationException($"Couldn't cast value '{value}' of property {childrenProperty.Name} from {typeName} to IEnumerable");
                foreach (var item in items)
                {
                    ProcessTypeItem(
                        item,
                        type,
                        childrenProperty == typeData.ChildWithIdProperty ? null : id,
                        data);
                }
            }
        }

        private string GetIdFromChildren(object obj, TypeData typeData)
        {
            if (typeData.ChildWithIdProperty == null)
                return null;

            var child = typeData.ChildWithIdProperty.GetValue(obj);
            if (child == null)
                throw new InvalidOperationException($"Property {typeData.ChildWithIdProperty.Name} with Id can't be null in {obj.ToJson()}");

            return typeData.IdPropertyInChild.GetValue(child)?.ToString();
        }
    }
}
