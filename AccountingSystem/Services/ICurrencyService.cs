using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public interface ICurrencyService
    {
        decimal Convert(decimal amount, Currency fromCurrency, Currency toCurrency);
    }
}
