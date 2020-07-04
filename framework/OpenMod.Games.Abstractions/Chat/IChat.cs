using System.Drawing;
using System.Threading.Tasks;
using OpenMod.API.Ioc;

namespace OpenMod.Games.Abstractions.Chat
{
    [Service]
    public interface IChat
    {
        Task BroadcastAsync(string message, Color color);
    }
}