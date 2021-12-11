using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;

namespace AudioChat.Hubs
{
    public class ChatHub : Hub
    {
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Clients.Others.SendAsync("Disconnect", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
        public async Task SendMessage(ChannelReader<byte[]> stream)
        {
            while (await stream.WaitToReadAsync())
            {
                while (stream.TryRead(out var item))
                {
                    // do something with the stream item
                    await Clients.Others.SendAsync("ReceiveMessage", Context.ConnectionId,item);
                }
            }
            
        }
    }
}
