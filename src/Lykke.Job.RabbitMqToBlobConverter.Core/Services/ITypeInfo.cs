using Lykke.Job.RabbitMqToBlobConverter.Core.Domain;
using System;
using System.Collections.Generic;

namespace Lykke.Job.RabbitMqToBlobConverter.Core.Services
{
    public interface ITypeInfo
    {
        string IdPropertyName { get; }

        Dictionary<Type, TypeData> PropertiesMap { get; }

        string GetIdPropertyName(string typeName);
    }
}
