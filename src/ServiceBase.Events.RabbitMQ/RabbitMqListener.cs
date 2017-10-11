﻿namespace ServiceBase.Events.RabbitMQ
{
    using System;
    using System.Collections.Generic;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using MessagePack;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Handles queue connections, message parsing and errors.
    /// <see href="https://www.rabbitmq.com/dotnet-api-guide.html"/>
    /// </summary>
    public class RabbitMqListener : IDisposable
    {
        private readonly RabbitMqOptions _options;
        private readonly IBinarySerializer _serializer;
        private readonly ILogger<RabbitMqListener> _logger;

        private IConnection _connection;
        private List<IModel> _models;


        /// <summary>
        /// Default constructor for <see cref="RabbitMqListener"/>
        /// </summary>
        /// <param name="options">Instance of 
        /// <see cref="RabbitMqOptions"/></param>
        /// <param name="logger">Instance of 
        /// <see cref="ILogger{RabbitMqListener}"/></param>
        public RabbitMqListener(
            RabbitMqOptions options,
            IBinarySerializer serializer,
            ILogger<RabbitMqListener> logger)
        {
            this._options = options;
            this._logger = logger;
            this._serializer = serializer;
            this._models = new List<IModel>();

            ConnectionFactory factory = new ConnectionFactory()
            {
                Uri = this._options.Uri
            };

            this._connection = factory.CreateConnection();
        }

        /// <summary>
        /// Creates an event based consumer and connects to exchange provided
        /// via options. Will call <paramref name="action"/> as soon a message 
        /// arrives 
        /// </summary>
        /// <typeparam name="TMessage">Type of the message that will be passed 
        /// to <paramref name="action"/> action</typeparam>
        /// <param name="queueName">Name of the queue that will be created and
        /// attached to exchange provided via </param>
        /// <param name="action">Action will be called if message can be 
        /// successfully parsed to <typeparamref name="TMessage"/> type</param>
        public void ReceiveEventBased<TMessage>(
            string queueName,
            Action<TMessage> action)
        {
            if (String.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentNullException(nameof(queueName));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action)); 
            }

            // TODO: Handle DLX config
            // http://www.rabbitmq.com/dlx.html

            IModel model = this._connection.CreateModel();

            EventingBasicConsumer consumer =
                new EventingBasicConsumer(model);

            model.ExchangeDeclare(
                this._options.ExchangeName, ExchangeType.Fanout);

            model.QueueDeclare(queueName, false, false, false, null);

            model.QueueBind(queueName,
                this._options.ExchangeName, String.Empty, null);

            consumer.Received += (ch, ea) =>
            {
                try
                {
                    var obj = this._serializer.Deserialize<TMessage>(ea.Body);
                    action.Invoke(obj);
                }
                catch (Exception ex)
                {
                    model.BasicNack(ea.DeliveryTag, false, true);
                    this._logger.LogError(ex, ex.Message);
                }
                finally
                {
                    model.BasicAck(ea.DeliveryTag, false);
                }
            };

            model.BasicConsume(queueName, false, consumer);
            this._models.Add(model);
        }

        /// <summary>
        /// Creates an interval based consumer and connects to exchange 
        /// provided via options. Will call <paramref name="action"/> every N 
        /// seconds provided via <paramref name="timeSpan"/>
        /// </summary>
        /// <typeparam name="TMessage">Type of the message that will be passed 
        /// to <typeparamref name="action"/> action</typeparam>
        /// <param name="queueName">Name of the queue that will be created and
        /// attached to exchange provided via </param>
        /// <param name="timeSpan">Checking for messages interval</param>
        /// <param name="action">Action will be called if message can be 
        /// successfully parsed to <typeparamref name="TMessage"/> type</param>
        public void ReceiveIntervalBased<TMessage>(
            string queueName,
            TimeSpan timeSpan,
            Action<IEnumerable<TMessage>> action)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Disposes all open channels and connection
        /// </summary>
        public void Dispose()
        {
            foreach (var item in this._models)
            {
                item.Close(200, "Goodbye");
            }

            this._connection.Close();
        }
    }
}