using System.Numerics;
using System.Threading.Tasks;
using OpenMod.API.Users;
using OpenMod.Games.Abstractions.Player;
using OpenMod.Unturned.Users;
using UnityVec = UnityEngine.Vector3;

namespace OpenMod.Unturned.Player
{
    public class UnturnedPlayer : IPlayer
    {
        /// <inheritdoc />
        public IUser User { get; }

        private UnturnedUser m_UntUsr => (UnturnedUser) User;
 
        /// <inheritdoc />
        public float Health
        {
            get => m_UntUsr.Player.life.health;
            set => m_UntUsr.Player.life.tellHealth(m_UntUsr.SteamId, (byte) value);
        }
        
        public async Task TeleportAsync(Vector3 location) =>
            m_UntUsr.Player.teleportToLocation(new UnityVec(location.X, location.Y, location.Z), m_UntUsr.Player.look.yaw);
    }
}