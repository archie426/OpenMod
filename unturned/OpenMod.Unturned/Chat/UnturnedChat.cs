using System.Threading.Tasks;
using OpenMod.API.Ioc;
using OpenMod.Games.Abstractions.Chat;
using SDG.Unturned;
using Color = System.Drawing.Color;
using UnityColor = UnityEngine.Color;

namespace OpenMod.Unturned.Chat
{
    [ServiceImplementation]
    public class UnturnedChat : IChat
    {
        /// <inheritdoc />
        public async Task BroadcastAsync(string message, Color color)
        {
            ChatManager.serverSendMessage(message, new UnityColor(color.R, color.G, color.B, color.A));
        }
    }
}