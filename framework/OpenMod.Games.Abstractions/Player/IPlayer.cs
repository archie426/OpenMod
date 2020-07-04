using System.Numerics;
using System.Threading.Tasks;
using OpenMod.API.Users;

namespace OpenMod.Games.Abstractions.Player
{
    public interface IPlayerUser : IUser
    {
        IPlayer Player { get; }
    }

    public interface IPlayerUser<TPlayer> : IPlayerUser where TPlayer: IPlayer
    {
        new TPlayer Player { get; }
    }
    
    
    public interface IPlayer<TUser, TSelf> : IPlayer
        where TUser : IPlayerUser<TSelf>
        where TSelf : IPlayer
    {
         TUser User { get; }
    }
    
    public interface IPlayer
    {
        public IUser User { get; }
        
        public float Health { get; set; }

        Task TeleportAsync(Vector3 location);
    }
}