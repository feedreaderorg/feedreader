using FeedReader.Share.Protocols;
using Grpc.Core;
using System.Threading.Tasks;

namespace FeedReader.MessageServer
{
    public class MessageServerApi : Share.Protocols.MessageServerApi.MessageServerApiBase
    {
        MessageService MessageService { get; set; }

        public MessageServerApi(MessageService messageService)
        {
            MessageService = messageService;
        }

        public async override Task SubscribeEvents(IAsyncStreamReader<Event> requestStream, IServerStreamWriter<Event> responseStream, ServerCallContext context)
        {
            var subscriber = await MessageService.Handshake(requestStream, responseStream);
            await MessageService.SubscribeEvent(subscriber);
        }
    }
}
