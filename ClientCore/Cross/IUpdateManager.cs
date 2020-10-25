using System.Threading.Tasks;

namespace Streamster.ClientCore.Cross
{
    public interface IUpdateManager
    {
        Task Update(string appUpdatePath);
    }
}
