using FeedReader.Share.Protocols;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace FeedReader.MessageServer
{
    public class EventSubscriber
    {
        public Guid Id { get; set; }
        public bool IsMessageServer { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public IAsyncStreamReader<Event> EventReader { get; set; }
        public IAsyncStreamWriter<Event> EventWriter { get; set; }
    }

    public class MessageService : IHostedService
    {
        const int PORT = 9538;

        Guid Id { get; } = Guid.NewGuid();
        IDbContextFactory<DbContext> DbFactory { get; set; }
        ConcurrentDictionary<Guid, EventSubscriber> EventSubscribers = new ConcurrentDictionary<Guid, EventSubscriber>();
        List<Task> WorkingTasks { get; set; } = new List<Task>();
        BlockingCollection<Event> Events { get; set; } = new BlockingCollection<Event>();
        CancellationTokenSource CancellationTokenSource { get; set; }
        ILogger Logger { get; set; }

        public MessageService(IDbContextFactory<DbContext> dbFactory, ILogger<MessageService> logger)
        {
            DbFactory = dbFactory;
            Logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource = new CancellationTokenSource();

            using (var db = DbFactory.CreateDbContext())
            {
                // Get self ip address, save to db.
                var ips = GetLocalIpAddress();
                var updatedTime = DateTime.UtcNow;
                db.MessageServers.Add(new MessageServer()
                {
                    Id = Id,
                    IpAddress = JsonConvert.SerializeObject(ips.Select(ip => ip.ToString()).ToArray()),
                    LastHeartbeatTime = updatedTime
                });
                await db.SaveChangesAsync();

                // Try to connect to other servers.
                foreach (var messageServer in await db.MessageServers.Where(s => s.Id != Id).OrderByDescending(s => s.LastHeartbeatTime).ToArrayAsync())
                {
                    if (!IsServerActive(messageServer))
                    {
                        continue;
                    }

                    foreach (var ip in JsonConvert.DeserializeObject<string[]>(messageServer.IpAddress))
                    {
                        if (ip == "127.0.0.1" || ips.IndexOf(ip) >= 0)
                        {
                            continue;
                        }

                        ips.Add(ip);
                        LogInfo($"Connecting to {messageServer.Id} at {ip}.");
                        try
                        {
                            var channel = new Channel(ip, PORT, ChannelCredentials.Insecure);
                            var client = new Share.Protocols.MessageServerApi.MessageServerApiClient(channel);
                            var streams = client.SubscribeEvents();
                            var subscriber = await Handshake(streams.ResponseStream, streams.RequestStream);
                            WorkingTasks.Add(SubscribeEvent(subscriber));
                            LogInfo($"Connect to {messageServer.Id} at {ip} successed.");
                            break;
                        }
                        catch (Exception ex)
                        {
                            LogDebug(ex, $"Connect to {ip} failed.");
                        }
                    }
                }
            }

            // Start grpc server so that it can accept connections.
            var server = new Server()
            {
                Services = { Share.Protocols.MessageServerApi.BindService(new MessageServerApi(this)) },
                Ports = { new ServerPort("0.0.0.0", PORT, ServerCredentials.Insecure) }
            };
            server.Start();

            // Start worker task.
            WorkingTasks.Add(ProcessOutgoingEvents());

            // Start work task to update heartbeat
            WorkingTasks.Add(SendHartbeat());

            // Start clean db task.
            WorkingTasks.Add(CleanUpDB());
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource.Cancel();
            foreach (var item in EventSubscribers)
            {
                item.Value.CancellationTokenSource.Cancel();
            }
            Task.WaitAll(WorkingTasks.ToArray());
            WorkingTasks.Clear();
            await Task.CompletedTask;
        }

        public async Task<EventSubscriber> Handshake(IAsyncStreamReader<Event> sr, IAsyncStreamWriter<Event> sw)
        {
            if (CancellationTokenSource.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            // Write handshake message.
            await sw.WriteAsync(new Event()
            {
                SenderId = Id.ToString(),
                IsFromMessageServer = true
            });

            // Wait for handshake message.
            if (!await sr.MoveNext(CancellationTokenSource.Token))
            {
                throw new InvalidOperationException("Handshake failed.");
            }

            // Create event subscriber.
            return new EventSubscriber()
            {
                Id = Guid.Parse(sr.Current.SenderId),
                EventReader = sr,
                EventWriter = sw,
                CancellationTokenSource = new CancellationTokenSource(),
                IsMessageServer = sr.Current.IsFromMessageServer
            };
        }
        
        public async Task SubscribeEvent(EventSubscriber subscriber)
        {
            if (CancellationTokenSource.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            LogInfo($"is subscribed by {subscriber.Id}.");

            // Register outgoing stream.
            if (!EventSubscribers.TryAdd(subscriber.Id, subscriber))
            {
                throw new Exception($"can't subscribe by {subscriber.Id}.");
            }

            // Process incoming stream.
            try
            {
                while (await subscriber.EventReader.MoveNext(subscriber.CancellationTokenSource.Token))
                {
    
                    LogTrace($"received one message from {subscriber.Id}.");
                    Events.Add(subscriber.EventReader.Current);
                }
            }
            catch
            {
                subscriber.CancellationTokenSource.Cancel();
            }

            // Unregister outgoing stream.
            EventSubscribers.Remove(subscriber.Id, out subscriber);
            LogInfo($"is unsubscribed by {subscriber.Id}.");
        }

        Task ProcessOutgoingEvents()
        {
            return Task.Run(async () =>
            {
                LogInfo($"is waiting for broadcasting message.");
                while (true)
                {
                    try
                    {
                        var e = Events.Take(CancellationTokenSource.Token);
                        var senderId = Guid.Parse(e.SenderId);
                        var isFromMessageSender = e.IsFromMessageServer;
                        e.IsFromMessageServer = true;
                        foreach (var subscriber in EventSubscribers)
                        {
                            // If this message comes from message server, don't broadcasting to other message servers to avoid loop.
                            if (isFromMessageSender && subscriber.Value.IsMessageServer)
                            {
                                continue;
                            }

                            // Don't send the message back to the sender.
                            if (senderId == subscriber.Value.Id)
                            {
                                continue;
                            }

                            LogTrace($"broadcasting message from {senderId} to {subscriber.Key}.");
                            try
                            {   
                                await subscriber.Value.EventWriter.WriteAsync(e);
                            }
                            catch (Exception ex)
                            {
                                LogError(ex, $"failed to forward message from {senderId} to {subscriber.Key}.");
                                subscriber.Value.CancellationTokenSource.Cancel();
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Deque event failed");
                    }
                }
                Logger.LogInformation($"Message server {Id} stopped forwarding message.");
            });
        }

        bool IsServerActive(MessageServer server)
        {
            return (DateTime.UtcNow - server.LastHeartbeatTime).TotalSeconds < 120;
        }

        async Task CleanUpDB()
        {
            while (true)
            {
                await Task.Delay(new Random().Next(5, 11) * 60 * 1000, CancellationTokenSource.Token);

                // Find out the first active server who will claen up the db.
                using (var db = DbFactory.CreateDbContext())
                {
                    Guid? firstActiveServerId = null;
                    foreach (var messageServer in await db.MessageServers.ToArrayAsync(CancellationTokenSource.Token))
                    {
                        if (IsServerActive(messageServer))
                        {
                            if (firstActiveServerId == null)
                            {
                                firstActiveServerId = messageServer.Id;
                            }
                        }
                        else
                        {
                            db.MessageServers.Remove(messageServer);
                        }
                    }
                    if (firstActiveServerId == Id)
                    {
                        await db.SaveChangesAsync(CancellationTokenSource.Token);
                    }
                }
            }
        }

        async Task SendHartbeat()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(new Random().Next(10, 31) * 1000, CancellationTokenSource.Token);
                    using (var db = DbFactory.CreateDbContext())
                    {
                        var item = await db.MessageServers.FindAsync(new object[] { Id }, CancellationTokenSource.Token);
                        item.LastHeartbeatTime = DateTime.UtcNow;
                        await db.SaveChangesAsync(CancellationTokenSource.Token);
                    }
                }
            }
            catch
            {
            }
        }

        List<string> GetLocalIpAddress()
        {
            var ips = new List<string>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            var ipAddr = ip.Address.ToString();
                            if (ipAddr == "127.0.0.1")
                            {
                                continue;
                            }
                            LogTrace($"Find local ip: {ipAddr}");
                            ips.Add(ipAddr);
                        }
                    }
                }
            }
            return ips;
        }

        void LogInfo(string msg)
        {
            Logger.LogInformation(LogLine(msg));
        }

        void LogDebug(Exception ex, string msg)
        {
            Logger.LogDebug(ex, LogLine(msg));
        }

        void LogTrace(string msg)
        {
            Logger.LogTrace(LogLine(msg));
        }

        string LogLine(string msg)
        {
            return $"[{DateTime.Now}] Message server {Id}: {msg}";
        }

        void LogError(Exception ex, string msg)
        {
            Logger.LogError(ex, LogLine(msg));
        }
    }
}
