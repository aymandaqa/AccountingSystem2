using AccountingSystem.Models;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public interface IAccountService
    {
        Task<(int Id, string Code)> CreateAccountAsync(string name, int parentAccountId);
    }
}
