using Common.Log;
using Lykke.Common.Log;
using Lykke.Job.RabbitMqToBlobConverter.Core.Services;
using Lykke.Job.RabbitMqToBlobConverter.Services.Deserializers;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using System;
using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.RabbitSubscribers
{
    public class RabbitSubscriber : IRabbitMqSubscriber, IMessageDeserializer<object>
    {
        private const string _endpointName = "rabbitmqtoblobconverter";

        private readonly IMessageConverter _messageConverter;
        private readonly IBlobUploader _blobUploader;
        private readonly ILogFactory _logFactory;
        private readonly ILog _log;
        private readonly string _connectionString;
        private readonly string _exchangeName;
        private readonly string _routingKey;

        private Type _type;
        private RabbitMqSubscriber<object> _subscriber;
        private SerializationFormat? _deserializationFormat;

        public RabbitSubscriber(
            IMessageConverter messageConverter,
            IBlobUploader blobUploader,
            ILogFactory logFactory,
            string connectionString,
            string exchangeName,
            string routingKey)
        {
            _messageConverter = messageConverter;
            _blobUploader = blobUploader;
            _logFactory = logFactory;
            _log = logFactory.CreateLog(this);
            _connectionString = connectionString;
            _exchangeName = exchangeName;
            _routingKey = routingKey;
        }

        public void Start(Type type)
        {
            _type = type;

            var endpointName = string.IsNullOrWhiteSpace(_routingKey)
                ? _endpointName
                : $"{_endpointName}.{_routingKey}";

            var settings = RabbitMqSubscriptionSettings
                .CreateForSubscriber(_connectionString, _exchangeName, endpointName)
                .MakeDurable();

            if (!string.IsNullOrWhiteSpace(_routingKey))
                settings.UseRoutingKey(_routingKey);

            _subscriber = new RabbitMqSubscriber<object>(
                    _logFactory,
                    settings,
                    new ResilientErrorHandlingStrategy(
                        _logFactory,
                        settings,
                        TimeSpan.FromSeconds(10),
                        next: new DeadQueueErrorHandlingStrategy(_logFactory, settings)))
                .SetMessageDeserializer(this)
                .Subscribe(ProcessMessageAsync)
                .CreateDefaultBinding()
                .SetConsole(new LogToConsole())
                .Start();
        }

        private async Task ProcessMessageAsync(object arg)
        {
            try
            {
                var messageData = _messageConverter.Convert(arg);

                await _blobUploader.SaveToBlobAsync(messageData);
            }
            catch (Exception e)
            {
                _log.Error(e);
                throw;
            }
        }

        public void Dispose()
        {
            _subscriber?.Dispose();
        }

        public async Task StopAsync()
        {
            _subscriber?.Stop();

            await _blobUploader.StopAsync();
        }

        public object Deserialize(byte[] data)
        {
            object result;
            if (_deserializationFormat.HasValue)
            {
                switch (_deserializationFormat.Value)
                {
                    case SerializationFormat.Json:
                        if (JsonDeserializer.TryDeserialize(data, _type, out result))
                            return result;
                        break;
                    case SerializationFormat.MessagePack:
                        if (MessagePackDeserializer.TryDeserialize(data, _type, out result))
                            return result;
                        break;
                    case SerializationFormat.Protobuf:
                        if (ProtobufDeserializer.TryDeserialize(data, _type, out result))
                            return result;
                        break;
                    default:
                        throw new NotSupportedException($"Serialization format {_deserializationFormat.Value} is not supported");
                }
            }

            bool success = JsonDeserializer.TryDeserialize(data, _type, out result);
            if (success)
            {
                _deserializationFormat = SerializationFormat.Json;
                return result;
            }

            success = MessagePackDeserializer.TryDeserialize(data, _type, out result);
            if (success)
            {
                _deserializationFormat = SerializationFormat.MessagePack;
                return result;
            }

            success = ProtobufDeserializer.TryDeserialize(data, _type, out result);
            if (success)
            {
                _deserializationFormat = SerializationFormat.Protobuf;
                return result;
            }

            throw new InvalidOperationException("Couldn't deserialize message");
        }
    }
}
