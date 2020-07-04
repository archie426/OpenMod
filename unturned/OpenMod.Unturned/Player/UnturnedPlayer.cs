using System.Numerics;
using System.Threading.Tasks;
using OpenMod.API.Users;
using OpenMod.Games.Abstractions.Player;
using OpenMod.Unturned.Users;
using Vector3 = UnityEngine.Vector3;

namespace OpenMod.Unturned.Player
{
    public class UnturnedPlayer : IPlayer
    {
        /// <inheritdoc />
        public IUser User { get; }

        private UnturnedUser _untUsr => (UnturnedUser) User;
 
        /// <inheritdoc />
        public float Health
        {
            get => _untUsr.Player.life.health;
            set => _untUsr.Player.life.tellHealth(_untUsr.SteamId, (byte) value);
        }
        
        public async Task TeleportAsync(Vector3 location) =>
            _untUsr.Player.teleportToLocation(new Vector3(location.x, location.y, location.z), _untUsr.Player.look.yaw);
    }
}